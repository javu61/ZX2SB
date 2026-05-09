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
        opts = _Opts
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
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
                Dim resultado As String = ""
                If Not GetVersion(opts, LineaLeida, resultado) Then
                    EmitirError(stWriter, 0, resultado)
                Else
                    GrabarSalida(stWriter, "REM --> " & resultado, False)
                End If
                PrimeraLinea = False
                Continue While
            End If

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
            If LineaLeida.StartsWith(Marca_SRC) Then
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

        stWriter.Flush()
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
        Dim expr As String

        If tkIF.RPN IsNot Nothing AndAlso tkIF.RPN.Count > 0 Then
            expr = RPNToInfix(tkIF.RPN)
        Else
            expr = tkIF.Value ' fallback
        End If

        Dim lineaIF As String = $"IF {expr} THEN"


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

        Dim RPNAux = tkFOR.RPN
        Dim texto = tkFOR.Value

        If RPNAux Is Nothing OrElse RPNAux.Count = 0 Then
            EmitirError(writer, 0, "FOR sin RPN")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Separar por TO (texto)
        ' ------------------------------------------------------
        Dim partes = texto.Split(New String() {"TO"}, StringSplitOptions.None)

        If partes.Length <> 2 Then
            EmitirError(writer, 0, "FOR inválido: falta TO")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Parte izquierda: V(j) := C(1)
        ' ------------------------------------------------------
        Dim lhsRPN = RPNAux.TakeWhile(Function(n) n.Kind <> RPNKind.ASSIGN).ToList()
        Dim idxAssign = RPNAux.FindIndex(Function(n) n.Kind = RPNKind.ASSIGN)

        Dim initRPN = RPNAux.GetRange(idxAssign + 1, 1) ' C(1)

        Dim varName As String

        If lhsRPN.Count > 0 AndAlso lhsRPN(0).Kind = RPNKind.VAR Then
            varName = lhsRPN(0).Value.ToUpperInvariant()
        Else
            EmitirError(writer, 0, "FOR inválido: variable incorrecta")
            Exit Sub
        End If

        Dim initExpr = RPN.RPNToInfix(initRPN)

        ' ------------------------------------------------------
        ' Parte derecha: límite
        ' ------------------------------------------------------
        Dim limitText = partes(1).Trim()

        Dim limitRPN = ParseRPN(limitText)
        Dim limitExpr = RPN.RPNToInfix(limitRPN)

        ' ------------------------------------------------------
        ' Construcción final
        ' ------------------------------------------------------
        Dim linea As String = $"FOR {varName}={initExpr} TO {limitExpr}"

        EmitirLinea(writer, linea, False)

        ' registrar FOR
        ForStack.Push(varName)

    End Sub

    Private Sub GenerarNext(writer As StreamWriter, tk As Token)

        Dim varName As String = tk.Value.ToUpperInvariant()

        If ForStack.Count = 0 Then
            EmitirWarning(writer, $"NEXT {varName} sin FOR previo")

        ElseIf ForStack.Peek() <> varName Then
            EmitirWarning(writer, $"FOR/NEXT no estructurado: se cierra {varName}, pero esperaba {ForStack.Peek()}")
            EliminarForDePila(varName)

        Else
            ForStack.Pop()
        End If

        EmitirLinea(writer, $"END FOR {varName}", False)

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
    ' Emisión de REM
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


    ' =========================================================
    ' Emisión de LET (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub GenerarLet(writer As StreamWriter, tk As Token)

        Dim rpn = tk.RPN

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            EmitirError(writer, 0, "LET sin RPN")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Buscar asignación A(=)
        ' ------------------------------------------------------
        Dim idxAssign = rpn.FindIndex(Function(n) n.Kind = RPNKind.ASSIGN)

        If idxAssign < 0 Then
            EmitirError(writer, 0, "LET inválido: falta asignación")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Separar LHS / RHS
        ' ------------------------------------------------------
        Dim lhs = rpn.GetRange(0, idxAssign)
        Dim rhs = rpn.GetRange(idxAssign + 1, rpn.Count - idxAssign - 1)

        If lhs.Count = 0 Then
            EmitirError(writer, 0, "LET inválido: LHS vacío")
            Exit Sub
        End If

        ' ------------------------------------------------------
        ' Construir LHS (variable + índices)
        ' ------------------------------------------------------
        Dim lhsExpr As String

        ' variable base
        If lhs(0).Kind <> RPNKind.VAR Then
            EmitirError(writer, 0, "LET inválido: variable incorrecta")
            Exit Sub
        End If

        Dim varName = lhs(0).Value

        ' comprobar si hay índices (I -> IDX)
        Dim idxNode = lhs.FirstOrDefault(Function(n) n.Kind = RPNKind.FUN_CALL AndAlso n.Value = "IDX")

        Dim tieneIDX = lhs.Any(Function(n) n.Kind = RPNKind.FUN_CALL AndAlso n.Value = "IDX")

        If tieneIDX Then
            ' Extraer argumentos desde RPN usando stack
            Dim args = ExtraerArgsDesdeRPN(lhs, idxNode.Arity)

            lhsExpr = $"{varName}({String.Join(",", args)})"

        Else
            lhsExpr = varName
        End If

        ' ------------------------------------------------------
        ' Construir RHS
        ' ------------------------------------------------------
        Dim rhsExpr As String = RPNToInfix(rhs)

        ' ------------------------------------------------------
        ' Emitir LET (sin palabra LET en QL)
        ' ------------------------------------------------------
        EmitirLinea(writer, $"{lhsExpr}={rhsExpr}", False)

    End Sub

    Private Function ExtraerArgsDesdeRPN(rpn As List(Of RPN_Node), arity As Integer) As List(Of String)

        Dim stack As New Stack(Of String)

        For Each n In rpn

            Select Case n.Kind

                Case RPNKind.VAR
                    stack.Push(n.Value)

                Case RPNKind.CTE
                    stack.Push(n.Value)

                Case RPNKind.BINARY_OP
                    Dim b = stack.Pop()
                    Dim a = stack.Pop()
                    stack.Push($"{a}{n.Value}{b}")

                Case RPNKind.UNARY_OP
                    Dim a = stack.Pop()
                    stack.Push($"{n.Value}{a}")

                Case RPNKind.FUN_CALL

                    If n.Value = "IDX" Then
                        ' Extraer argumentos reales
                        Dim args As New List(Of String)

                        For i = 1 To n.Arity
                            args.Insert(0, stack.Pop())
                        Next

                        Return args
                    Else
                        ' Función normal
                        Dim args As New List(Of String)

                        For i = 1 To n.Arity
                            args.Insert(0, stack.Pop())
                        Next

                        stack.Push($"{n.Value}({String.Join(",", args)})")
                    End If

            End Select

        Next

        Return New List(Of String)

    End Function


    ' =========================================================
    ' Emisión de PRINT (ZX → SuperBASIC QL)
    ' =========================================================
    Private Sub GenerarPRINT(writer As StreamWriter, numeroLineaActual As Integer, tk As Token)

        Dim item As New PrintItem(tk)
        Dim comando As String = "PRINT "

        ' -----------------------------------------
        ' Directivas fuera del PRINT
        ' -----------------------------------------
        If tk.IsPrintDirective AndAlso item.ID <> TokenID.TK_TAB Then

            Select Case item.ID

                Case TokenID.TK_AT
                    Dim x = RPN.RPNToInfix(item.Expr1)
                    Dim y = RPN.RPNToInfix(item.Expr2)

                    EmitirLinea(writer, $"AT {x},{y}", False)

                Case TokenID.TK_INK, TokenID.TK_PAPER
                    Dim expr = RPN.RPNToInfix(item.Expr1)

                    EmitirLinea(writer, $"{item.ID.ToString.Replace("TK_", "")} {expr}", False)

            End Select

            ' Si hay coma, forzar PRINT
            If item.Separator = PrintSeparator.C Then
                EmitirLinea(writer, comando & Constantes.C_COMA, False)
            End If

            Return
        End If

        ' -----------------------------------------
        ' PRINT normal
        ' -----------------------------------------
        Dim sb As New StringBuilder
        sb.Append(comando)

        If item.ID = TokenID.TK_TAB Then

            Dim expr = RPN.RPNToInfix(item.Expr1)
            sb.Append("TO " & expr)

        Else

            If item.Expr1 IsNot Nothing Then
                Dim expr = RPN.RPNToInfix(item.Expr1)
                sb.Append(expr)
            End If

        End If

        ' -----------------------------------------
        ' Separador
        ' -----------------------------------------
        Select Case item.Separator
            Case PrintSeparator.P
                sb.Append(";")
            Case PrintSeparator.C
                sb.Append(Constantes.C_COMA)
            Case PrintSeparator.N
                ' nada
        End Select

        EmitirLinea(writer, sb.ToString(), False)

    End Sub

    Private Function EmitirTAB(item As PrintItem) As String

        If item.Expr1 Is Nothing Then
            Return "TO 0"
        End If

        Dim expr = RPN.RPNToInfix(item.Expr1)

        Return "TO " & expr

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
                     New String(Constantes.C_ESPACIO, columna) & Constantes.Marca_Error & descripcion)
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
            MostrarMensaje(opts, Constantes.Marca_Gen & " " & linea)
        End If

    End Sub
    Private Sub GrabarSeparador(writer As StreamWriter, ByRef nroLinea As Integer)
        GrabarSalida(writer, $"{nroLinea} " & Constantes.GQL_SEPARADOR, True)
        nroLinea += 10

    End Sub

End Module