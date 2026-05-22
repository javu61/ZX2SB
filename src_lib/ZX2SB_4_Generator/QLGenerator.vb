Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Text.Json
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
    Private FuncionesFNUsadas As New HashSet(Of TokenID)

    ' Pila de seguimiento de bucles FOR
    Private ForStack As New Stack(Of String)

    Dim lData As New List(Of QLFnLibrary.stData)

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_Opts As CmdOptions) As Integer
        opts = _Opts

        'Leer las lineas de DATA desde el fichero auxiliar
        LeerData()

        'Bloque pincipal
        opts.Fase = SubFases.Base
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        stWriter.NewLine = ChrW(10)   ' Fín de línea para el QL es solo LF (ASCII 10)
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        NroErrores = 0
        PrimeraLinea = True

        FuncionesFNUsadas.Clear()

        'Cabecera del programa
        Dim startLine As Integer = 1
        GrabarFuncion(startLine, QLFnLibrary.Generate_ProgramInit())

        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()
            If String.IsNullOrWhiteSpace(LineaLeida) Then Continue While

            ' ----------------------------------------------------------
            ' Primera línea, Debe contener tipo y versión del fichero
            ' ----------------------------------------------------------
            If PrimeraLinea Then
                Dim resultado As String = ""
                If Not GetVersion(opts, LineaLeida, resultado) Then
                    ErrorGenerador(0, resultado)
                Else
                    ' No se guarda la versión aquí
                End If
                PrimeraLinea = False
                Continue While
            End If

            ' --------------------------------------------
            ' Línea fuente original (contexto de error)
            ' --------------------------------------------
            If LineaLeida.StartsWith(Marca_SRC) Then
                CerrarIF(stWriter)

                ContadorLineaInterna = 0
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, LineaZXActual, LineaLeida)

                ' ⚠️ TODA sentencia debe llevar número de línea, pero estas las dejamos sin nro para
                '    verlas en el fichero generado pero no en el QL, solo si estamos en modo debug
                If (opts.ModoDebug) Then
                    GrabarSalida("REM --> " & LineaParaMostrar, False)
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
                        ErrorGenerador(0, $"Número de línea ZX inválido: '{auxTok.Value}'")
                        LineaZXActual = -1
                    End If
                Case Else
                    ' Sentencia ejecutable
                    GenerarSentenciaQL(auxTok)
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
            ErrorGenerador(0, "[ERROR GENERADOR] Fichero IRS incompleto: falta EOF, posible fichero truncado")
            Return 1
        End If

        stWriter.Flush()
        stReader.Close()
        stWriter.Close()
        Return 0

    End Function

    Private Sub LeerData()
        ' -----------------------------------------
        ' DATA
        ' -----------------------------------------
        opts.Fase = SubFases.Data
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))

        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()
            If String.IsNullOrWhiteSpace(LineaLeida) Then Continue While

            ' ----------------------------------------------------------
            ' Primera línea, Debe contener tipo y versión del fichero
            ' ----------------------------------------------------------
            If PrimeraLinea Then
                Dim resultado As String = ""
                If Not GetVersion(opts, LineaLeida, resultado) Then
                    ErrorGenerador(0, resultado)
                Else
                    ' No se guarda la versión aquí
                End If
                PrimeraLinea = False
                Continue While
            End If

            ' ----------------------------------------------------------------------------
            ' Línea de los DATA
            ' ----------------------------------------------------------------------------
            Dim partes = LineaLeida.Split(New Char() {Constantes.C_ESPACIO}, 4, StringSplitOptions.RemoveEmptyEntries)
            If partes(0) = "DATA" AndAlso partes(1) = "NODE" AndAlso partes(2) <> "" AndAlso partes(3) <> "" Then
                Dim d As stData

                If Integer.TryParse(partes(2), d.Numero) Then
                    d.Cadena = (partes(3)(0) = Constantes.C_COMILLAS)
                    d.Valor = partes(3)
                    lData.Add(d)
                Else
                    ' opcional: registrar error
                    ErrorGenerador(0, $"Número de línea DATA inválido: {partes(2)}")
                End If
            End If

        End While

        stReader.Close()
    End Sub


    ' =========================================================
    ' Traducción de una sentencia ZX → SuperBASIC QL
    ' =========================================================
    Function GenerarLineaNumerada(codigo As String) As String
        Dim nroInterno As Integer = LineaZXActual * Constantes.GQL_FACTOR + ContadorLineaInterna

        ContadorLineaInterna += 1

        Return $"{nroInterno} {codigo}"
    End Function

    Private Sub GenerarSentenciaQL(tk As Token)
        Dim cmd As String = tk.Mnemonic
        Dim stmt As String = tk.Value


        Select Case tk.GetFamily()
            Case TokenFamily.TF_BLOQUES
                GenerarConBloques(tk)

            Case TokenFamily.TF_GENERAFN, TokenFamily.TF_NOSOPORTADO
                EmitirLinea(SentenciaSimpleQL(tk), False)

            Case TokenFamily.TF_GENERAL
                EmitirLinea(SentenciaSimpleQL(tk), False)

            Case TokenFamily.TF_ESPECIALES
                ' Esto son EOL, EOF, LINE, no generan nada aquí

            Case Else
                'No puede llegar nada, pero por si acaso me falta alguna en las listas
                ErrorGenerador(0, $"REM [SIN TRADUCCION ==>] {cmd} {stmt}")
        End Select
    End Sub

    Private Function SentenciaSimpleQL(tk As Token) As String
        Dim cmd As String = tk.Mnemonic
        Dim stmt As String = tk.Value

        Select Case tk.GetFamily()
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

            Case TokenFamily.TF_GENERAFN,
                 TokenFamily.TF_NOSOPORTADO

                ' REGISTRAR SIEMPRE EL USO DE LA FN
                RegistrarFN(tk)

                If tk.IsFunction Then
                    ' FUNCIÓN
                    If stmt <> "" Then
                        Return $"{tk.Mnemonic}({stmt})"
                    Else
                        Return $"{tk.Mnemonic}"
                    End If
                Else
                    ' PROCEDIMIENTO o SENTENCIA
                    If stmt <> "" Then
                        Return $"{tk.Mnemonic} {stmt}"
                    Else
                        Return $"{tk.Mnemonic}"
                    End If
                End If

            Case Else
                'No puede llegar nada, pero por si acaso
                ErrorGenerador(0, "Sentencias por bloques no soportadas")
        End Select
        Return ""
    End Function

    Private Sub GenerarConBloques(tk As Token)
        Select Case tk.ID
            Case TokenID.TK_REM
                Generar_REM(tk)

            Case TokenID.TK_LET
                Generar_LET(tk)

            Case TokenID.TK_PRINT
                Generar_PRINT(tk)

            Case TokenID.TK_IF
                Generar_IF(tk)

            Case TokenID.TK_FOR
                Generar_FOR(tk)

            Case TokenID.TK_NEXT
                Generar_NEXT(tk)

            Case TokenID.TK_DATA
                Generar_DATA(tk)

            Case TokenID.TK_DIM
                Generar_DIM(tk)

        End Select
    End Sub



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

    Private Sub Generar_IF(tkIF As Token)
        Dim expr As String

        If tkIF.RPN IsNot Nothing AndAlso tkIF.RPN.Count > 0 Then
            expr = RPN.RPNToInfix(tkIF.RPN)
        Else
            expr = tkIF.Value ' fallback
        End If

        Dim lineaIF As String = $"IF {expr} THEN"


        EmitirLinea(lineaIF, True)

        IFContador += 1
    End Sub

    Private Sub CerrarIF(writer As StreamWriter)

        While IFContador > 0
            EmitirLinea("End If", True)  'No es un token, solo es del QL
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
    Private Function ProcesadoPorIF(sentenciaQL As String) As Boolean

        ' Si no hay IF activo, no hacemos nada
        If IFContador = 0 Then
            Return False
        End If

        ' Estamos dentro de un IF: emitir la sentencia dentro del bloque
        EmitirLinea(sentenciaQL, True)

        Return True

    End Function

    ' =========================================================
    ' Emisión de FOR/NEXT
    ' =========================================================
    Private Sub Generar_FOR(tkFOR As Token)

        Dim varName As String = ""
        Dim initExprL As New List(Of RPN_Node)
        Dim limitExprL As New List(Of RPN_Node)
        Dim stepExprL As New List(Of RPN_Node)

        If Not Descomponer.dFOR(tkFOR.RPN, varName, initExprL, limitExprL, stepExprL) Then
            ErrorGenerador(0, "FOR inválido")
            Exit Sub
        End If

        ' ---------------------------------
        ' Construir expresiones
        ' ---------------------------------
        Dim initExpr As String = RPN.RPNToInfix(initExprL)
        Dim limitExpr As String = RPN.RPNToInfix(limitExprL)

        Dim linea As String

        If stepExprL IsNot Nothing AndAlso stepExprL.Count > 0 Then
            Dim stepExpr As String = RPN.RPNToInfix(stepExprL)
            linea = $"FOR {varName.ToUpperInvariant()}={initExpr} TO {limitExpr} STEP {stepExpr}"
        Else
            linea = $"FOR {varName.ToUpperInvariant()}={initExpr} TO {limitExpr}"
        End If

        EmitirLinea(linea, False)

        ' SOLO para el generador (no semántico)
        ForStack.Push(varName)

    End Sub

    Private Sub Generar_NEXT(tk As Token)

        Dim varName As String = tk.Value.ToUpperInvariant()

        If ForStack.Count = 0 Then
            EmitirWarning($"NEXT {varName} sin FOR previo")

        ElseIf ForStack.Peek() <> varName Then
            EmitirWarning($"FOR/NEXT no estructurado: se cierra {varName}, pero esperaba {ForStack.Peek()}")
            EliminarForDePila(varName)

        Else
            ForStack.Pop()
        End If

        EmitirLinea($"END FOR {varName}", False)

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
    ' Emisión de DATA
    ' =========================================================
    Private Sub Generar_DATA(tk As Token)
        ' tk.Value contiene solo contantes:
        ' C(80) , C(120) , C("OTRA CADENA") , C(0)

        Dim sb As New StringBuilder()
        sb.Append("DATA ")

        Dim actual As New List(Of RPN_Node)
        Dim first As Boolean = True

        For Each node In tk.RPN

            If node.Kind = RPNKind.DATA_SEP Then

                If actual.Count > 0 Then

                    If Not first Then sb.Append(" ,")
                    sb.Append(GenerarElementoDATA(actual))

                    actual.Clear()
                    first = False

                End If

            Else
                actual.Add(node)
            End If

        Next

        If actual.Count > 0 Then
            If Not first Then sb.Append(" ,")
            sb.Append(GenerarElementoDATA(actual))
        End If

        EmitirLinea(sb.ToString(), False)

    End Sub
    Private Function GenerarElementoDATA(expr As List(Of RPN_Node)) As String

        ' ---------------------------------
        ' Caso simple: constante o variable
        ' ---------------------------------
        If expr.Count = 1 Then

            Dim n = expr(0)

            Select Case n.Kind

                Case RPNKind.CTE

                    If n.TokenID = TokenID.TES_STRING Then
                        Return $"""{n.Value}"""
                    Else
                        Return n.Value
                    End If

                Case RPNKind.VAR
                    Return n.Value

            End Select

        End If

        ' ---------------------------------
        ' Caso complejo: expresión
        ' ---------------------------------
        Return RPN.RPNToInfix(expr)

    End Function

    ' =========================================================
    ' Emisión de REM
    ' =========================================================
    Private Sub Generar_REM(tk As Token)
        If opts.SinComentarios Then
            Exit Sub
        End If

        Dim comentario As String = tk.Value
        If Left(comentario, 1) = Constantes.C_COMILLAS And Right(comentario, 1) = Constantes.C_COMILLAS Then
            comentario = Mid(comentario, 2, Len(comentario) - 2)
        End If
        EmitirLinea($"{tk.Mnemonic} {comentario}", False)

    End Sub


    ' =========================================================
    ' Emisión de LET (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub Generar_LET(tk As Token)

        Dim auxRpn = tk.RPN

        If auxRpn Is Nothing OrElse auxRpn.Count = 0 Then
            ErrorGenerador(0, "LET sin RPN")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Buscar asignación A(=)
        ' ------------------------------------------------------
        Dim idxAssign = auxRpn.FindIndex(Function(n) n.Kind = RPNKind.ASSIGN)

        If idxAssign < 0 Then
            ErrorGenerador(0, "LET inválido: falta asignación")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Separar LHS / RHS
        ' ------------------------------------------------------
        Dim lhs = auxRpn.GetRange(0, idxAssign)
        Dim rhs = auxRpn.GetRange(idxAssign + 1, auxRpn.Count - idxAssign - 1)

        If lhs.Count = 0 Then
            ErrorGenerador(0, "LET inválido: LHS vacío")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Construir AMBOS LADOS
        ' ------------------------------------------------------

        Dim lhsExpr As String = RPN.RPNToInfix(lhs)
        Dim rhsExpr As String = RPN.RPNToInfix(rhs)

        ' ------------------------------------------------------
        ' Emitir LET (sin palabra LET en QL)
        ' ------------------------------------------------------
        EmitirLinea($"{lhsExpr}={rhsExpr}", False)

    End Sub

    ' =========================================================
    ' Emisión de PRINT (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub Generar_PRINT(tk As Token)

        Dim item As New PrintItem(tk)
        item.FromToken(tk)
        Dim comando As String = "PRINT "

        ' -----------------------------------------
        ' Directivas fuera del PRINT
        ' -----------------------------------------
        If item.IsPrintDirective Then
            'Si ha sido tratada no hay mas que hacer
            If EmitirDirectivaPRINT(item) Then
                Return
            End If
        End If

        ' -----------------------------------------
        ' PRINT normal
        ' -----------------------------------------
        Dim sb As New StringBuilder
        sb.Append(comando)

        If item.prID = TokenID.TK_TAB Then
            If (item.prExpr1 Is Nothing) Then
                ErrorGenerador(0, $"Comando TAB mal formado, no tiene valor")
            End If
            Dim expr = RPN.RPNToInfix(item.prExpr1)
            sb.Append("TO " & expr)

        Else
            If item.prExpr1 IsNot Nothing Then
                ' REGISTRAR EL USO DE LAS FN en los PRINT
                For Each node In item.prExpr1
                    Select Case Token.GetFamilyFromID(node.TokenID)
                        Case TokenFamily.TF_GENERAFN, TokenFamily.TF_NOSOPORTADO
                            Dim tkaux As New Token(node.TokenID)
                            RegistrarFN(tkaux)
                    End Select
                Next

                Dim expr = RPN.RPNToInfix(item.prExpr1)
                sb.Append(expr)
            End If

        End If

        ' -----------------------------------------
        ' Separador
        ' -----------------------------------------
        Select Case item.prSeparator
            Case PrintSeparator.P
                sb.Append(";")
            Case PrintSeparator.C
                sb.Append(Constantes.C_COMA)
            Case PrintSeparator.N
                ' nada
        End Select

        EmitirLinea(sb.ToString(), False)

    End Sub

    Private Function EmitirDirectivaPRINT(item As PrintItem) As Boolean
        Select Case item.prID
            Case TokenID.TK_TAB
                Return (False)

            Case TokenID.TK_AT
                If (item.prExpr1 Is Nothing) Or (item.prExpr2 Is Nothing) Then
                    ErrorGenerador(0, "AT al formado, no tiene las dos coordenadas")
                    Return True
                End If

                Dim x = RPN.RPNToInfix(item.prExpr1)
                Dim y = RPN.RPNToInfix(item.prExpr2)

                EmitirLinea($"AT {x},{y}", False)

            Case Else
                Dim tk As New Token(item.prID)
                Dim cmd As String = tk.Mnemonic
                If (item.prExpr1 Is Nothing) Then
                    ErrorGenerador(0, $"Comando {cmd} mal formado, no tiene valor")
                    Return True
                End If
                Dim value = RPN.RPNToInfix(item.prExpr1)

                EmitirLinea($"{cmd} {value}", False)
        End Select

        ' Si hay coma tras el comando, forzar un nuevo PRINT con ella
        If item.prSeparator = PrintSeparator.C Then
            EmitirLinea("PRINT " & Constantes.C_COMA, False)
        End If
        Return (True)
    End Function


    ' =========================================================
    ' Emisión de DIM
    ' =========================================================
    Private Sub Generar_DIM(tk As Token)

        Dim rpn = tk.RPN

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            ErrorGenerador(0, "DIM sin RPN")
            Exit Sub
        End If

        Dim nombre As String = ""
        Dim dimensiones As New List(Of String)

        ' ---------------------------------
        ' Analizar RPN
        ' ---------------------------------
        For i As Integer = 0 To rpn.Count - 1

            Dim n = rpn(i)

            Select Case n.Kind

                Case RPNKind.VAR
                    ' base array
                    Dim pos = n.Value.IndexOf(","c)
                    nombre = n.Value.Substring(0, pos)

                Case RPNKind.CTE
                    ' dimensión literal
                    dimensiones.Add(n.Value)

                Case RPNKind.IDX
                    ' indica nº dimensiones → ya lo tenemos en lista
                    ' opcional validar:
                    If dimensiones.Count <> n.Arity Then
                        ErrorGenerador(0, $"DIM inconsistente en {nombre}")
                    End If

            End Select

        Next

        ' ---------------------------------
        ' Construir salida
        ' ---------------------------------
        Dim sb As New StringBuilder
        sb.Append("DIM ")
        sb.Append(nombre)

        If dimensiones.Count > 0 Then
            sb.Append("(")
            For i = 0 To dimensiones.Count - 1
                sb.Append(dimensiones(i))
                If i < dimensiones.Count - 1 Then sb.Append(",")
            Next
            sb.Append(")")
        End If

        EmitirLinea(sb.ToString(), False)

    End Sub


    Private Sub EmitirFuncionesAuxiliares(writer As StreamWriter)

        Dim startLine As Integer = 9000
        GrabarSeparador(startLine)
        GrabarSalida($"{startLine} REM =========================================", True)
        startLine += 10
        GrabarSalida($"{startLine} REM ===== FUNCIONES AUXILIARES ZX BASIC =====", True)
        startLine += 10
        GrabarSalida($"{startLine} REM =========================================", True)
        startLine += 10
        GrabarSeparador(startLine)

        'Añadimos la rutina de inicialización siempre
        Dim fInit As New Token(TokenID.TCO_INIT)
        GrabarFuncion(startLine, QLFnLibrary.GenerateFnProcedure(startLine, fInit, opts.Funciones))

        If FuncionesFNUsadas.Count <> 0 Then
            For Each tkid In FuncionesFNUsadas
                Dim tk As New Token(tkid)
                GrabarFuncion(startLine, QLFnLibrary.GenerateFnProcedure(startLine, tk, opts.Funciones))
            Next
        End If
    End Sub

    Private Sub GrabarFuncion(ByRef LineaFinal As Integer, ListaLineas As List(Of String))
        For Each l In ListaLineas
            GrabarSalida(l, True)
        Next

        LineaFinal += ListaLineas.Count * 10 + 10
        GrabarSeparador(LineaFinal)
    End Sub

    Private Sub EmitirLinea(sentenciaQL As String, DesdeIF As Boolean)
        If (DesdeIF) OrElse (Not ProcesadoPorIF(sentenciaQL)) Then
            GrabarSalida(GenerarLineaNumerada(sentenciaQL), True)
        End If
    End Sub

    ' =============================================
    ' REGISTRAR EL USO DE LA FN SI NO EXISTE
    ' =============================================
    Private Sub RegistrarFN(tk As Token)
        If Not FuncionesFNUsadas.Contains(tk.ID) Then
            FuncionesFNUsadas.Add(tk.ID)
        End If
    End Sub


    ' ============================================================
    ' ERRORES
    ' ============================================================
    Private Sub ErrorGenerador(columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If

        MostrarError(opts, stReader, stWriter, NroLineaFichero, columna, LineaParaMostrar,
                     New String(Constantes.C_ESPACIO, columna) & Constantes.Marca_Error & descripcion)
    End Sub

    Private Sub EmitirWarning(mensaje As String)
        NroWarnings += 1

        ' 1) Aviso por consola
        MostrarWarning(opts, stReader, stWriter, NroLineaFichero, 0, $"[WARNING {NroWarnings}] {mensaje}", "")

        ' 2) Aviso dentro del código SuperBASIC
        '    (REM obligatorio en QL)
        GrabarSalida($"REM WARNING: {mensaje}", False)

    End Sub

    Private Sub GrabarSalida(linea As String, generada As Boolean)
        If (linea = "-") Then
            linea = Constantes.GQL_SEPARADOR
        End If

        stWriter.WriteLine(linea)

        If (generada) Then
            linea = "   " & linea
        End If
        If Not opts.Silencioso Then
            MostrarMensaje(opts, Constantes.Marca_Gen & " " & linea)
        End If

    End Sub
    Private Sub GrabarSeparador(ByRef nroLinea As Integer)
        GrabarSalida($"{nroLinea} " & Constantes.GQL_SEPARADOR, True)
        nroLinea += 10

    End Sub



End Module