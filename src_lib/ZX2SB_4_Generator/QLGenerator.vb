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

    ' --- Estado del IF ZX en curso ---
    Dim IFContador As Integer = 0       ' Cuantos IF tenemos abiertos
    Dim IFMultiple As Boolean = False   ' El IF condiciona más de una sentencia
    Dim IFCondicion As String = ""      ' "IF <condición> THEN"
    Dim IFSentencia As String = ""      ' Primera sentencia condicionada (candidato a IF simple)


    ' Funciones FN usadas durante la traducción
    Private ReadOnly FuncionesFNUsadas As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    ' Pila de seguimiento de bucles FOR
    Private ForStack As New Stack(Of String)

    ' Generador auxiliar de FN
    Private AuxFN As QLFnLibrary

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_Opts As CmdOptions) As Integer

        opts = _Opts
        NroErrores = 0

        FuncionesFNUsadas.Clear()
        AuxFN = New QLFnLibrary()

        Using writer As New StreamWriter(opts.FSalidaGSB, False, New UTF8Encoding(False)) '✅ UTF‑8 sin BOM
            writer.NewLine = ChrW(10)   ' Fín de línea para el QL es solo LF (ASCII 10)

            'Cabecera del programa
            Dim startLine As Integer = 1
            GrabarFuncion(writer, startLine, AuxFN.GenerateProgramInit())


            For Each linea As String In File.ReadLines(opts.FSalidaSem)
                If String.IsNullOrWhiteSpace(linea) Then Continue For

                'Primera línea, contiene la versión del fichero
                If PrimeraLinea Then
                    If Not linea.StartsWith(Constantes.SEM_NOMBRE) Then
                        EmitirError(writer, 0, "[ERROR] No es un fichero " & Constantes.SEM_NOMBRE & ": " & linea)
                        Return (1)
                    End If

                    If Not linea.StartsWith(Constantes.SEM_NOMBRE & " " & Constantes.SEM_VERSION) Then
                        EmitirError(writer, 0, "[ERROR] Versión incorrecta del fichero " & Constantes.LEX_NOMBRE & ": " & linea)
                        Return (1)
                    End If
                    PrimeraLinea = False
                    Continue For
                End If

                ' -----------------------------------------
                ' Cabecera / EOF
                ' -----------------------------------------
                If linea.StartsWith("IRS ") Then
                    Continue For
                End If

                If linea = "EOF" Then
                    CerrarIF(writer)
                    Exit For
                End If

                ' --------------------------------------------
                ' Línea fuente original (contexto de error)
                ' --------------------------------------------
                If linea.StartsWith(MarcaSRC) Then
                    CerrarIF(writer)

                    ContadorLineaInterna = 0
                    LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, LineaZXActual, linea)

                    ' ⚠️ TODA sentencia debe llevar número de línea, pero estas las dejamos si nro para verlas en el
                    '    fichero generado pero no en el QL, solo si estamos en modo debug
                    If (opts.ModoDebug) Then
                        GrabarSalida(writer, "REM --> " & LineaParaMostrar, False)
                    End If
                    Continue For
                End If

                ' -----------------------------------------
                ' Sentencia ejecutable (IRS plano)
                ' -----------------------------------------
                If linea.StartsWith("STMT") Then
                    linea = linea.Substring(5).Trim()
                    GenerarSentenciaQL(writer, LineaZXActual, linea)
                End If
            Next

            ' -----------------------------------------
            ' Funciones auxiliares FN_
            ' -----------------------------------------
            EmitirFuncionesAuxiliares(writer)

        End Using

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

    Private Sub GenerarSentenciaQL(writer As StreamWriter, numeroLineaActual As Integer, stmt As String)

        Dim cmd As String
        Dim resto As String
        Dim id As TokenID
        Dim esp As Integer = stmt.IndexOf(" "c)

        If esp >= 0 Then
            cmd = stmt.Substring(0, esp).ToUpperInvariant()
            resto = stmt.Substring(esp + 1).Trim()
        Else
            cmd = stmt.ToUpperInvariant()
            resto = ""
        End If

        '+++If GeneranBloques.Contains(cmd) Then
        '+++    GenerarConBloques(writer, numeroLineaActual, cmd, resto)
        '+++ElseIf GenerarFNPropia.Contains(cmd) Then
        '+++    EmitirLinea(writer, EmitirFN(cmd, resto), False)
        '+++ElseIf UnsupportedStatements.Contains(cmd) Then
        '+++    EmitirLinea(writer, EmitirFN(cmd, resto), False)
        '+++ElseIf ReservedWords.GetTokenID(cmd, id) Then
        '+++    EmitirLinea(writer, $"{cmd} {resto}", False)
        '+++Else
        '+++    'No puede llegar nada, pero por si acaso me falta alguna en las listas
        '+++    EmitirLinea(writer, $"REM [SIN TRADUCCION ==>] {cmd} {resto}", False)
        '+++End If

    End Sub

    Private Sub GenerarConBloques(writer As StreamWriter, numeroLineaActual As Integer, cmd As String, resto As String)

        Select Case cmd
            Case "REM"
                GenerarRem(writer, resto)

            Case "LET"
                GenerarLet(writer, resto)

            Case "PRINT"
                GenerarPRINT(writer, numeroLineaActual, resto)

            Case "IF"
                GenerarIF(writer, resto)

            Case "FOR"
                GenerarFor(writer, resto)

            Case "NEXT"
                GenerarNext(writer, resto)
        End Select
    End Sub

    Private Function NoTratadas(stmt As String, f As String, ByRef res As String) As Boolean

        ' No reprocesar llamadas ya convertidas
        If stmt.StartsWith("FN_", StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        If stmt = f Then
            res = EmitirFN(f, "")
            Return True
        End If

        If stmt.StartsWith(f & " ", StringComparison.OrdinalIgnoreCase) Then
            res = EmitirFN(f, stmt)
            Return True
        End If

        Return False
    End Function

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

    Private Sub GenerarIF(writer As StreamWriter, stmt As String)
        ' stmt llega como: "A = 1"

        Dim lineaIF As String = "IF " & stmt & " THEN"

        EmitirLinea(writer, lineaIF, True)

        IFContador += 1
    End Sub




    Private Sub CerrarIF(writer As StreamWriter)

        While IFContador > 0
            EmitirLinea(writer, "END IF", True)
            IFContador -= 1
        End While

        IFCondicion = ""
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
    Private Sub GenerarFor(writer As StreamWriter, stmt As String)
        Dim varName As String = stmt.Split("="c)(0).Trim().ToUpperInvariant()

        ' Seguimiento del bucle
        ForStack.Push(varName)

        ' Emitir FOR QL provisional
        EmitirLinea(writer, $"FOR {stmt}", False)
    End Sub

    Private Sub GenerarNext(writer As StreamWriter, stmt As String)
        Dim varName As String = stmt

        ' Comprobación estructural (NO bloqueante)
        If ForStack.Count = 0 Then
            EmitirWarning(writer, $"NEXT {varName} sin FOR previo")
        ElseIf ForStack.Peek() <> varName Then
            EmitirWarning(writer, $"FOR/NEXT no estructurado: se cierra {varName}, " & $"pero el último For abierto era {ForStack.Peek()}")
            ' Buscar y eliminar el FOR correspondiente si quieres
            EliminarForDePila(varName)
        Else
            ForStack.Pop()
        End If

        ' 👉 Aquí está la clave:
        ' siempre respetar la variable del ZX
        EmitirLinea(writer, $"End For {varName}", False)

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
    Private Sub GenerarRem(writer As StreamWriter, stmt As String)
        If opts.SinComentarios Then
            Exit Sub
        End If

        If Left(stmt, 1) = Constantes.C_COMILLAS And Right(stmt, 1) = Constantes.C_COMILLAS Then
            stmt = Mid(stmt, 2, Len(stmt) - 2)
        End If
        EmitirLinea(writer, "REM " & stmt, False)

    End Sub

    Private Sub GenerarLet(writer As StreamWriter, stmt As String)
        ' Separar por el primer espacio que esté fuera de los paréntesis
        Dim p1 As Integer = BuscarEspacioTrasParentesis(stmt)
        If p1 < 0 Then
            ' Caso degenerado: LET A
            EmitirLinea(writer, stmt, False)
        End If

        Dim lhs As String = stmt.Substring(0, p1).Trim()
        Dim rhs As String = stmt.Substring(p1 + 1).Trim()
        rhs = ReescribirExpresion(rhs, True)
        ' 🔑 El '=' SE INSERTA AQUÍ SIEMPRE
        EmitirLinea(writer, $"{lhs}={rhs}", False)
    End Sub

    Private Function BuscarEspacioTrasParentesis(texto As String) As Integer
        Dim nivel As Integer = 0

        For i As Integer = 0 To texto.Length - 1
            Dim ch As Char = texto(i)

            Select Case ch
                Case "("c
                    nivel += 1

                Case ")"c
                    If nivel > 0 Then nivel -= 1

                Case " "c
                    If nivel = 0 Then
                        Return i   ' ✅ espacio separador LHS / RHS
                    End If
            End Select
        Next

        Return -1   ' ❌ no encontrado
    End Function


    ' =========================================================
    ' Emisión de PRINT (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub GenerarPRINT(writer As StreamWriter, numeroLineaActual As Integer, stmt As String)

        Dim contenido As String = stmt

        Dim buffer As New List(Of String)
        Dim prefijoSep As String = ""
        Dim sufijoSep As String = ""

        Dim partes = SplitConSeparadores(contenido)

        For Each p In partes

            Dim item As String = p.Text.Trim()
            Dim sepPosterior As String = p.SepPosterior
            Dim sepAnterior As String = p.SepAnterior

            If item = "" Then Continue For

            Dim u As String = item.ToUpperInvariant()

            ' --- ATRIBUTOS QUE CORTAN PRINT ---
            If u.StartsWith("AT ") Then
                EmitirPrintPendiente(writer, numeroLineaActual, buffer, prefijoSep, sufijoSep)
                prefijoSep = "" : sufijoSep = ""
                EmitirLinea(writer, NormalizarAT(item), False)
                Continue For
            End If

            If u.StartsWith("INK ") OrElse
               u.StartsWith("PAPER ") OrElse
               u.StartsWith("BRIGHT ") OrElse
               u.StartsWith("FLASH ") OrElse
               u.StartsWith("INVERSE ") OrElse
               u.StartsWith("OVER ") Then

                EmitirPrintPendiente(writer, numeroLineaActual, buffer, prefijoSep, sufijoSep)
                prefijoSep = "" : sufijoSep = ""
                EmitirLinea(writer, item, False)
                Continue For
            End If

            ' --- EXPRESIÓN IMPRIMIBLE ---

            If u.StartsWith("TAB ") OrElse u.StartsWith("TAB(") Then

                ' prefijo ,
                If buffer.Count = 0 AndAlso sepAnterior = "," Then
                    prefijoSep = ","
                End If

                buffer.Add(ConvertirTABaTO(item))

                ' sufijo
                If sepPosterior = "," OrElse sepPosterior = ";" Then
                    sufijoSep = sepPosterior
                Else
                    sufijoSep = ""
                End If

                Continue For
            End If

            If buffer.Count = 0 AndAlso sepAnterior = "," Then
                prefijoSep = ","
            End If

            item = ReescribirExpresion(item, False)
            buffer.Add(item)

            If sepPosterior = "," OrElse sepPosterior = ";" Then
                sufijoSep = sepPosterior
            Else
                sufijoSep = ""
            End If

        Next

        EmitirPrintPendiente(writer, numeroLineaActual, buffer, prefijoSep, sufijoSep)
    End Sub


    ' --- Divide la sentencia PRINT por los separadores de impresión ; y ,
    Private Function SplitConSeparadores(texto As String) _
                     As List(Of (Text As String, SepAnterior As String, SepPosterior As String))

        Dim res As New List(Of (String, String, String))

        Dim actual As String = ""
        Dim inString As Boolean = False
        Dim lastSep As String = ""
        Dim i As Integer = 0

        While i < texto.Length

            Dim c As Char = texto(i)

            ' --- Manejo de strings ---
            If c = Constantes.C_COMILLAS Then
                inString = Not inString
                actual &= c
                i += 1
                Continue While
            End If

            ' --- AT es atómico: consumir expr,expr completo ---
            If Not inString AndAlso
               i + 2 < texto.Length AndAlso
               texto.Substring(i).TrimStart().ToUpperInvariant().StartsWith("AT ") Then

                ' copiar "AT "
                actual &= "AT "
                i += texto.Substring(i).IndexOf("AT ") + 3

                ' copiar hasta la coma
                While i < texto.Length AndAlso texto(i) <> ","c
                    actual &= texto(i)
                    i += 1
                End While

                ' copiar coma
                If i < texto.Length AndAlso texto(i) = ","c Then
                    actual &= ","
                    i += 1
                End If

                ' copiar segunda expresión
                While i < texto.Length AndAlso texto(i) <> ";"c AndAlso texto(i) <> ","c
                    actual &= texto(i)
                    i += 1
                End While

                Continue While
            End If

            ' --- Separadores normales ---
            If Not inString AndAlso (c = ";"c OrElse c = ","c) Then
                res.Add((actual, lastSep, c.ToString()))
                lastSep = c.ToString()
                actual = ""
                i += 1
                Continue While
            End If

            actual &= c
            i += 1

        End While

        res.Add((actual, lastSep, ""))

        Return res
    End Function




    Private Sub EmitirPrintPendiente(writer As StreamWriter, numeroLineaActual As Integer, buffer As List(Of String), prefijo As String, sufijo As String)
        ' Eliminar PRINT neutro
        If buffer.Count = 0 AndAlso prefijo = "" AndAlso sufijo = ";" Then Exit Sub
        If buffer.Count = 0 AndAlso prefijo = "" AndAlso sufijo = "" Then Exit Sub

        Dim linea As String = "PRINT "

        ' Prefijo (, o ;)
        If prefijo = "," Then linea &= ","
        ' Prefijo ";" se ignora

        ' Contenido
        If buffer.Count > 0 Then
            linea &= String.Join(",", buffer)
        End If

        ' Sufijo
        If sufijo = "," OrElse sufijo = ";" Then
            linea &= sufijo
        End If

        EmitirLinea(writer, linea, False)
        buffer.Clear()
    End Sub

    Private Function NormalizarAT(texto As String) As String
        Dim t As String = texto.Trim()
        t = t.Replace("AT", "", StringComparison.OrdinalIgnoreCase).Trim()

        Dim partes() = t.Split(","c)
        If partes.Length >= 2 Then
            Return $"AT {partes(0).Trim()},{partes(1).Trim()}"
        End If

        ' fallback seguro
        Return "AT " & t
    End Function

    Private Function ConvertirTABaTO(texto As String) As String
        ' TAB(5) / TAB 5  → TO 5
        Dim t As String = texto.Trim().ToUpperInvariant()
        t = t.Replace("TAB", "").Trim()
        t = t.TrimStart("("c).TrimEnd(")"c)
        Return $"TO {t}"
    End Function

    ' --- Reescribir la parde derecha de la expresión
    Private Function ReescribirExpresion(expr As String, isLet As Boolean) As String

        ' Seguridad: aquí no debe haber asignaciones
        If (Not isLet) AndAlso ContieneIgualFueraDeCadena(expr) Then
            Throw New InvalidOperationException("ReescribirExpresion recibió una expresión con '=' fuera de cadena: " & expr)
        End If

        expr = expr.Trim()
        If expr = "" Then Return expr

        ' Separar en tokens por espacios
        Dim partes() As String = expr.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)

        ' Caso: BIN 1110011, PEEK 45, IN 7, USR 1234, etc.
        Dim nombre As String = partes(0).ToUpperInvariant()

        '+++If ReservedFunctions.Contains(nombre) Then

        '+++ ' Reconstruir parámetros (todo lo que sigue)
        '+++  Dim parametros As String = String.Join(",", partes.Skip(1))

        '+++   Return EmitirFN(nombre, parametros)

        '+++   End If

        ' No es función reservada → dejar tal cual
        Return expr
    End Function

    Private Function ContieneIgualFueraDeCadena(expr As String) As Boolean
        Dim enCadena As Boolean = False

        For i As Integer = 0 To expr.Length - 1
            If expr(i) = Constantes.C_COMILLAS Then
                enCadena = Not enCadena
            ElseIf expr(i) = "="c AndAlso Not enCadena Then
                Return True
            End If
        Next

        Return False
    End Function

    ' =========================================================
    ' Emisión de funciones auxiliares ZX → QL
    ' =========================================================
    Private Function EmitirFN(nombre As String, parametros As String) As String

        nombre = nombre.ToUpperInvariant()
        parametros = parametros.Replace(" ", "")
        Dim Linea As String = ""
        Dim tipofuncion As Boolean

        Dim id As TokenID = Nothing


        '+++  If ReservedFunctions.Contains(nombre) Then
        '+++   tipofuncion = True
        '+++   ElseIf ReservedProcedures.Contains(nombre) Then
        '+++   tipofuncion = False
        '+++    ElseIf ReservedStatements.Contains(nombre) Then
        '+++   tipofuncion = False
        '+++ ElseIf nombre = "CLEAR_VAR" Or nombre = "RANDOMIZE_USR" Then
        '+++   tipofuncion = False
        '+++   Else
        '+++   Throw New ApplicationException("ERROR: FN_{nombre} no se reconoce")
        '+++   End If


        ' ✅ REGISTRAR SIEMPRE EL USO DE LA FN
        FuncionesFNUsadas.Add(nombre)

        If tipofuncion Then
            ' FUNCIÓN
            If parametros <> "" Then
                Linea = $"FN_{nombre}({parametros})"
            Else
                Linea = $"FN_{nombre}"
            End If
        Else
            ' PROCEDIMIENTO o SENTENCIA
            If parametros <> "" Then
                Linea = $"FN_{nombre} {parametros}"
            Else
                Linea = $"FN_{nombre}"
            End If
        End If

        Return Linea
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
        GrabarFuncion(writer, startLine, AuxFN.GenerateFnProcedure(startLine, "INIT", opts.Funciones))

        If FuncionesFNUsadas.Count <> 0 Then
            For Each fn In FuncionesFNUsadas.OrderBy(Function(s) s)
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

        MostrarError(opts, writer, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)
    End Sub

    Private Sub EmitirWarning(writer As StreamWriter, mensaje As String)
        NroWarnings += 1

        ' 1) Aviso por consola
        If Not opts.Silencioso Then
            MostrarWarning(opts, writer, NroLineaFichero, 0, $"[WARNING {NroWarnings}] {mensaje}", "")
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