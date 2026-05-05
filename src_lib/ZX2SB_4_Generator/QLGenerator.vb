Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml
Imports ZX2SB

Public Module QLGenerator

    Dim opts As CmdOptions
    Dim pos As Integer
    Dim NroErrores As Integer = 0
    Dim NroWarnings As Integer = 0
    Dim LineaParaMostrar As String = ""
    Dim PrimeraLinea As Boolean = True
    Dim NroLineaFichero As Integer = 0
    Dim LineaZXActual As Integer = -1
    Dim ContadorLineaInterna As Integer = 0
    Dim EncontradoEOF As Boolean = False
    Dim stReader As StreamReader
    Dim stWriter As StreamWriter

    ' --- Estado del IF ZX en curso ---
    Dim IFContador As Integer = 0       ' Cuantos IF tenemos abiertos

    ' Funciones FN usadas durante la traducción
    Private ReadOnly FuncionesFNUsadas As New List(Of Token)

    ' Pila de seguimiento de bucles FOR
    Private ForStack As New Stack(Of String)

    ' Generador auxiliar de FN
    Private AuxFN As QLFnLibrary

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_Opts As CmdOptions) As Integer
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        opts = _Opts
        NroErrores = 0

        FuncionesFNUsadas.Clear()
        AuxFN = New QLFnLibrary()

        stWriter.NewLine = ChrW(10)   ' Fín de línea para el QL es solo LF (ASCII 10)

        'Cabecera del programa
        Dim startLine As Integer = 1
        GrabarFuncion(stWriter, startLine, AuxFN.GenerateProgramInit())


        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()
            If String.IsNullOrWhiteSpace(LineaLeida) Then Continue While

            ' ----------------------------------------------------------
            ' Primera línea, Debe contener tipo y versión del fichero
            ' ----------------------------------------------------------
            If PrimeraLinea Then
                If Not LineaLeida.StartsWith(Constantes.SEM_NOMBRE) Then
                    EmitirError(stWriter, 0, "[ERROR] No es un fichero " & Constantes.SEM_NOMBRE & ": " & LineaLeida)
                    Return (1)
                End If

                If Not LineaLeida.StartsWith(Constantes.SEM_NOMBRE & " " & Constantes.SEM_VERSION) Then
                    EmitirError(stWriter, 0, "[ERROR] Versión incorrecta del fichero " & Constantes.LEX_NOMBRE & ": " & LineaLeida)
                    Return (1)
                End If
                PrimeraLinea = False
                Continue While
            End If

            ' --------------------------------------------
            ' Línea fuente original (contexto de error)
            ' --------------------------------------------
            If LineaLeida.StartsWith(MarcaSRC) Then
                CerrarIF(stWriter)

                ContadorLineaInterna = 0
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, LineaZXActual, LineaLeida)

                ' ⚠️ TODA sentencia debe llevar número de línea, pero estas las dejamos sin nro para
                '    verlas en el fichero generado pero no en el QL, solo si estamos en modo debug
                If (opts.ModoDebug) Then
                    GrabarSalida(stWriter, "REM --> " & LineaParaMostrar, False)
                End If
                Continue While
            End If

            ' ----------------------------------------------------------------------------
            ' Línea del IR, montar Token auxliar para descomponer la línea correctamente
            ' ----------------------------------------------------------------------------
            Dim auxTok As New Token(LineaLeida)

            Select Case auxTok.ID
                Case TokenID.TCO_EOL
                    CerrarIF(stWriter)
                Case TokenID.TCO_EOF
                    EncontradoEOF = True
                    CerrarIF(stWriter)
                Case TokenID.TCO_LINE
                    ' Nro de línea
                    If Not Integer.TryParse(auxTok.Value, LineaZXActual) Then
                        EmitirError(stWriter, 0, $"Número de línea ZX inválido: '{auxTok.Value}'")
                        LineaZXActual = -1
                    End If
                Case Else
                    ' Sentencia ejecutable
                    GenerarSentenciaQL(stWriter, LineaZXActual, auxTok)
            End Select

        End While

        ' -----------------------------------------
        ' Funciones auxiliares FN
        ' -----------------------------------------
        EmitirFuncionesAuxiliares(stWriter)

        ' -----------------------------------------
        ' Cierre final
        ' -----------------------------------------
        If Not EncontradoEOF Then
            EmitirError(stWriter, 0, "[ERROR GENERADOR] Fichero IRS incompleto: falta EOF, posible fichero truncado")
            Return 1
        End If

        stReader.Close()
        stWriter.Close()
        Return 0

    End Function

    ' =========================================================
    ' Traducción de una sentencia ZX → SuperBASIC QL
    ' =========================================================
    Function GenerarLineaNumerada(codigo As String) As String
        Dim nroInterno As Integer = LineaZXActual * Constantes.GQL_FACTOR + ContadorLineaInterna

        ContadorLineaInterna += 1

        Return $"{nroInterno} {codigo}"
    End Function

    Private Sub GenerarSentenciaQL(writer As StreamWriter, numeroLineaActual As Integer, tk As Token)
        Dim cmd As String = tk.Mnemonic
        Dim stmt As String = tk.Value

        Select Case Token.GetFamily(tk.ID)
            Case TokenFamily.TF_BLOQUES
                GenerarConBloques(writer, tk, numeroLineaActual)

            Case TokenFamily.TF_GENERAFN,
                 TokenFamily.TF_NOSOPORTADO,
                 TokenFamily.TF_GENERAL
                EmitirLinea(writer, SentenciaSimpleQL(writer, tk), False)


            Case TokenFamily.TF_ESPECIALES
                ' Esto son EOL, EOF, LINE, no generan nada aquí

            Case Else
                'No puede llegar nada, pero por si acaso me falta alguna en las listas
                EmitirError(writer, 0, $"REM [SIN TRADUCCION ==>] {cmd} {stmt}")
        End Select
    End Sub

    Private Function SentenciaSimpleQL(writer As StreamWriter, tk As Token) As String
        Dim cmd As String = tk.Mnemonic
        Dim stmt As String = tk.Value

        Select Case Token.GetFamily(tk.ID)
            Case TokenFamily.TF_GENERAFN,
                 TokenFamily.TF_NOSOPORTADO

                ' REGISTRAR SIEMPRE EL USO DE LA FN
                FuncionesFNUsadas.Add(tk)

                If tk.IsFunction Then
                    ' FUNCIÓN
                    If stmt <> "" Then
                        Return $"{tk.FNMnemonic}({stmt})"
                    Else
                        Return $"{tk.FNMnemonic}"
                    End If
                Else
                    ' PROCEDIMIENTO o SENTENCIA
                    If stmt <> "" Then
                        Return $"{tk.FNMnemonic} {stmt}"
                    Else
                        Return $"{tk.FNMnemonic}"
                    End If
                End If

            Case TokenFamily.TF_GENERAL
                If (tk.ID = TokenID.TK_GOTO) OrElse (tk.ID = TokenID.TK_GOSUB) Then
                    If IsNumeric(stmt) Then
                        stmt = stmt & "00"
                    End If
                End If
                If stmt = "" Then
                    Return cmd
                Else
                    Return $"{cmd} {stmt}"
                End If

            Case Else
                'No puede llegar nada, pero por si acaso
                EmitirError(writer, 0, "Sentencias por bloques no soportadas")
        End Select
        Return ""
    End Function

    Private Sub GenerarConBloques(writer As StreamWriter, tk As Token, numeroLineaActual As Integer)
        Select Case tk.ID
            Case TokenID.TK_REM
                GenerarRem(writer, tk)

            Case TokenID.TK_LET
                GenerarLet(writer, tk)

            Case TokenID.TK_PRINT
                GenerarPRINT(writer, numeroLineaActual, tk)

            Case TokenID.TK_IF
                GenerarIF(writer, tk)

            Case TokenID.TK_FOR
                GenerarFor(writer, tk)

            Case TokenID.TK_NEXT
                GenerarNext(writer, tk)
        End Select
    End Sub

    Private Function LeerNumeroLiteral(texto As String) As Integer
        Dim n As Integer = 0
        Dim i As Integer = 0

        While i < texto.Length AndAlso Char.IsWhiteSpace(texto(i))
            i += 1
        End While

        While i < texto.Length AndAlso Char.IsDigit(texto(i))
            n = n * 10 + (Asc(texto(i)) - Asc("0"c))
            i += 1
        End While

        Return n
    End Function

    ' =========================================================
    ' Generar IF (ZX BASIC → SuperBASIC QL)
    '
    ' Entrada (IRS):
    '   STMT IF <condición>
    '
    ' Semántica ZX BASIC:
    '   - El IF NO tiene END IF
    '   - Condiciona TODO lo que sigue en la misma línea ZX
    '   - Los IF se cierran SOLO al llegar a SRC o EOF
    '
    ' El cuerpo NO se procesa aquí.
    ' Las sentencias siguientes pertenecerán a este IF
    ' hasta que el generador cierre los IF abiertos.
    '
    ' stmt viene como:
    ' IF <condición>
    ' <cuerpo>
    ' <cuerpo>
    '
    ' Ejemplo IRS:
    ' stmt If A = 1
    ' stmt If B = 2
    ' stmt If C = 4
    ' stmt Print A
    ' stmt If C = 3
    ' stmt Print G
    ' =========================================================

    Private Sub GenerarIF(writer As StreamWriter, tkIF As Token)
        'Dim cmd As String = tkIF.Mnemonic
        'Dim stmt As String = tkIF.Value
        Dim tkThen As New Token(TokenID.TK_THEN)

        ' stmt llega como: "A = 1"

        Dim lineaIF As String = $"{tkIF.Mnemonic} {tkIF.Value} {tkThen.Mnemonic}"

        EmitirLinea(writer, lineaIF, True)

        IFContador += 1
    End Sub




    Private Sub CerrarIF(writer As StreamWriter)

        While IFContador > 0
            EmitirLinea(writer, "End If", True)  'No es un token, solo es del QL
            IFContador -= 1
        End While

    End Sub


    ' ---------------------------------------------------------
    ' Procesa una sentencia QL si estamos dentro de un IF ZX
    '
    ' Devuelve:
    '   True  -> la sentencia ha sido emitida dentro del IF
    '   False -> no hay IF activo, la sentencia es normal
    ' ---------------------------------------------------------
    Private Function ProcesadoPorIF(writer As StreamWriter, sentenciaQL As String) As Boolean

        ' Si no hay IF activo, no hacemos nada
        If IFContador = 0 Then
            Return False
        End If

        ' Estamos dentro de un IF: emitir la sentencia dentro del bloque
        EmitirLinea(writer, sentenciaQL, True)

        Return True

    End Function



    ' =========================================================
    ' Emisión de FOR/NEXT
    ' =========================================================
    Private Sub GenerarFor(writer As StreamWriter, tkFOR As Token)
        ' Seguimiento del bucle
        Dim varName As String = tkFOR.Value.Split("="c)(0).Trim().ToUpperInvariant()
        ForStack.Push(varName)

        ' Emitir FOR QL provisional
        EmitirLinea(writer, $"{tkFOR.Mnemonic} {tkFOR.Value}", False)
    End Sub

    Private Sub GenerarNext(writer As StreamWriter, tk As Token)
        Dim varName As String = tk.Value

        ' Comprobación estructural (NO bloqueante)
        If ForStack.Count = 0 Then
            EmitirWarning(writer, $"Next {varName} sin For previo")
        ElseIf ForStack.Peek() <> varName Then
            EmitirWarning(writer, $"For/Next no estructurado: se cierra {varName}, " & $"pero el último For abierto era {ForStack.Peek()}")
            ' Buscar y eliminar el FOR correspondiente si quieres
            EliminarForDePila(varName)
        Else
            ForStack.Pop()
        End If

        ' 👉 Aquí está la clave:
        ' siempre respetar la variable del ZX
        EmitirLinea(writer, $"End For {varName}", False) 'No es un token, es comando propio del QL

    End Sub

    Private Sub EliminarForDePila(varName As String)

        ' La pila de FOR se gestiona de arriba abajo.
        ' Si encontramos la variable, eliminamos ESE FOR
        ' y preservamos el orden de los otros.

        If ForStack.Count = 0 Then Exit Sub

        Dim temp As New Stack(Of String)
        Dim encontrado As Boolean = False

        While ForStack.Count > 0
            Dim v As String = ForStack.Pop()

            If Not encontrado AndAlso v = varName Then
                ' Eliminamos el FOR correspondiente
                encontrado = True
                Exit While
            Else
                temp.Push(v)
            End If
        End While

        ' Restaurar los FOR que estaban por encima
        While temp.Count > 0
            ForStack.Push(temp.Pop())
        End While

    End Sub

    ' =========================================================
    ' Emisión de LET (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub GenerarRem(writer As StreamWriter, tk As Token)
        If opts.SinComentarios Then
            Exit Sub
        End If

        Dim comentario As String = tk.Value
        If Left(comentario, 1) = Constantes.C_COMILLAS And Right(comentario, 1) = Constantes.C_COMILLAS Then
            comentario = Mid(comentario, 2, Len(comentario) - 2)
        End If
        EmitirLinea(writer, $"{tk.Mnemonic} {comentario}", False)

    End Sub

    Private Sub GenerarLet(writer As StreamWriter, tk As Token)
        Dim cmd As String = tk.Mnemonic
        Dim stmt As String = tk.Value

        ' Separar por el primer igual que esté fuera de los paréntesis
        Dim p1 As Integer = BuscarIgualDelLet(stmt)
        If p1 < 0 Then
            ' Caso degenerado: LET A
            EmitirLinea(writer, stmt, False)
        End If

        Dim lhs As String = stmt.Substring(0, p1).Trim()
        Dim rhs As String = stmt.Substring(p1 + 1).Trim()
        'rhs = ReescribirExpresion(rhs, True)
        ' 🔑 El '=' SE INSERTA AQUÍ SIEMPRE
        EmitirLinea(writer, $"{lhs}={rhs}", False) 'En el QL no hace falta el LET
    End Sub

    Private Function BuscarIgualDelLet(texto As String) As Integer
        Dim nivel As Integer = 0

        'Inicialmente no pasábamos el igual, se separaba por un espacio, pero como pueden haber espacios
        'dentro de los paréntesis se vigila, pero se mantiene por si acaso se vuelve a quitar el igual
        For i As Integer = 0 To texto.Length - 1
            Dim ch As Char = texto(i)

            Select Case ch
                Case "("c
                    nivel += 1

                Case ")"c
                    If nivel > 0 Then nivel -= 1

                Case "="c
                    If nivel = 0 Then
                        Return i   ' ✅ igual separador LHS / RHS
                    End If
            End Select
        Next

        Return -1   ' ❌ no encontrado
    End Function


    ' =========================================================
    ' Emisión de PRINT (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub GenerarPRINT(writer As StreamWriter, numeroLineaActual As Integer, tk As Token)
        Dim item As New PrintItem(tk)
        Dim tkaux As New Token(item.ID, item.Value)
        Dim Comando As String = "PRINT "

        'Las directivas ya no van dentro del print, salto TAB
        If tk.IsPrintDirective And tk.ID <> TokenID.TK_TAB Then
            EmitirLinea(writer, $"{tkaux.Mnemonic} {tkaux.Value}", False)

            'Solo necesitas añadir un separador si es coma, e irá en linea aparte
            If item.Separator = PrintSeparator.C Then
                EmitirLinea(writer, Comando & ",", False)
            End If

        Else
            Dim sb As New StringBuilder
            sb.Append(Comando)

            'TAB debe cambiarse en el QL
            If item.ID = TokenID.TK_TAB Then
                sb.Append(EmitirTAB(writer, item.Value))
            Else
                'El resto van directas
                sb.Append(SentenciaSimpleQL(writer, tkaux))
            End If

            ' Aplicar separador
            Select Case item.Separator
                Case PrintSeparator.P
                    sb.Append(";")
                Case PrintSeparator.C
                    sb.Append(",")
                Case PrintSeparator.N
                    ' nada
            End Select

            EmitirLinea(writer, sb.ToString(), False)

        End If

    End Sub


    Private Function EmitirTAB(writer As StreamWriter, value As String) As String
        Dim v = value.Trim()

        ' Quitar paréntesis si existen
        If v.StartsWith("("c) AndAlso v.EndsWith(")"c) Then
            v = v.Substring(1, v.Length - 2)
        End If

        Return ("TO " & v)
    End Function


    Private Sub EmitirFuncionesAuxiliares(writer As StreamWriter)

        Dim startLine As Integer = 9000
        GrabarSeparador(writer, startLine)
        GrabarSalida(writer, $"{startLine} REM =========================================", True)
        startLine += 10
        GrabarSalida(writer, $"{startLine} REM ===== FUNCIONES AUXILIARES ZX BASIC =====", True)
        startLine += 10
        GrabarSalida(writer, $"{startLine} REM =========================================", True)
        startLine += 10
        GrabarSeparador(writer, startLine)

        'Añadimos la rutina de inicialización siempre
        Dim fInit As New Token(TokenID.TCO_INIT)
        GrabarFuncion(writer, startLine, AuxFN.GenerateFnProcedure(startLine, fInit, opts.Funciones))

        If FuncionesFNUsadas.Count <> 0 Then
            For Each fn In FuncionesFNUsadas
                GrabarFuncion(writer, startLine, AuxFN.GenerateFnProcedure(startLine, fn, opts.Funciones))
            Next
        End If
    End Sub

    Private Sub GrabarFuncion(writer As StreamWriter, ByRef LineaFinal As Integer, ListaLineas As List(Of String))
        For Each l In ListaLineas
            GrabarSalida(writer, l, True)
        Next

        LineaFinal += ListaLineas.Count * 10 + 10
        GrabarSeparador(writer, LineaFinal)
    End Sub

    Private Sub EmitirLinea(writer As StreamWriter, sentenciaQL As String, DesdeIF As Boolean)
        If (DesdeIF) OrElse (Not ProcesadoPorIF(writer, sentenciaQL)) Then
            GrabarSalida(writer, GenerarLineaNumerada(sentenciaQL), True)
        End If
    End Sub

    ' ============================================================
    ' ERRORES
    ' ============================================================
    Private Sub EmitirError(writer As StreamWriter, columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If

        MostrarError(opts, stReader, stWriter, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)
    End Sub

    Private Sub EmitirWarning(writer As StreamWriter, mensaje As String)
        NroWarnings += 1

        ' 1) Aviso por consola
        If Not opts.Silencioso Then
            MostrarWarning(opts, stReader, stwriter, NroLineaFichero, 0, $"[WARNING {NroWarnings}] {mensaje}", "")
        End If

        ' 2) Aviso dentro del código SuperBASIC
        '    (REM obligatorio en QL)
        GrabarSalida(writer, $"REM WARNING: {mensaje}", False)

    End Sub

    Private Sub GrabarSalida(writer As StreamWriter, linea As String, generada As Boolean)
        If (linea = "-") Then
            linea = Constantes.GQL_SEPARADOR
        End If

        writer.WriteLine(linea)

        If (generada) Then
            linea = "   " & linea
        End If
        If Not opts.Silencioso Then
            MostrarMensaje(opts, Constantes.MarcaGen & " " & linea)
        End If

    End Sub
    Private Sub GrabarSeparador(writer As StreamWriter, ByRef nroLinea As Integer)
        GrabarSalida(writer, $"{nroLinea} " & Constantes.GQL_SEPARADOR, True)
        nroLinea += 10

    End Sub

End Module