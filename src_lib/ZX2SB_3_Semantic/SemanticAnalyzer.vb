Imports System
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml

Public Module SemanticAnalyzer
    Dim opts As CmdOptions
    Dim NroErrores As Integer = 0
    Dim NroWarnings As Integer = 0
    Dim NroLineaFichero As Integer = 0
    Dim NroLineaPrograma As Integer = 0
    Dim LineaParaMostrar As String = ""
    Dim UltimaLineaZX As Integer = -1

    ' Contexto semántico global
    Dim ctx As SemanticContext

    'Control de estructura del programa
    Dim ForStack As New Stack(Of String)
    Dim GosubStack As New Stack(Of Integer)

    'Writer del fichero de salida
    Dim stReader As StreamReader
    Dim stWriter As StreamWriter

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        opts = _opts
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        'stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        NroLineaFichero = 0
        NroErrores = 0

        ' --- Inicializar contexto ---
        InicializarContexto()

        ' ============================================================
        ' PRIMERA PASADA (Recolección estructural)
        ' ============================================================
        opts.Pasada = 1
        ProcesarIR()
        If NroErrores <> 0 Then
            Return 1
        End If

        ' ============================================================
        ' SEGUNDA PASADA (Análisis semántico + generación IRS)
        ' ============================================================
        GuardarIRS_Texto(Constantes.SEM_NOMBRE & " " & Constantes.SEM_VERSION)
        opts.Pasada = 2
        ProcesarIR()
        If NroErrores <> 0 Then
            Return 1
        End If

        GuardarIRP_Token(New Token(TokenID.TCO_EOF))
        stWriter.Flush()
        GuardarVarAndData()

        ' Los avisos de variables son warnings
        EmitirWarningsVariables()


        Return 0


    End Function

    ' ============================================================
    ' INICIALIZACIÓN DEL CONTEXTO
    ' ============================================================
    Private Sub InicializarContexto()
        ctx.Variables = New Dictionary(Of String, VariableInfo)
        ctx.FuncionesAuxiliares = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        ctx.DataNodes = New List(Of DataNode)

    End Sub

    ' ============================================================
    ' BUCLE PRINCIPAL DE LECTURA DEL IR
    ' ============================================================

    Private Sub ProcesarIR()
        Dim primeralinea As Boolean = True
        Dim EOFRecibido As Boolean = False

        NroLineaFichero = 0
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()

            If String.IsNullOrWhiteSpace(LineaLeida) Then Continue While

            ' ---------------------------------------------
            ' Cabecera IRP
            ' ---------------------------------------------
            If primeralinea Then
                MostrarMensaje(opts, LineaLeida)
                If Not LineaLeida.StartsWith(Constantes.PAR_NOMBRE) Then
                    ErrorSemantico("[ERROR] No es un fichero " & Constantes.PAR_NOMBRE & ": " & LineaLeida)
                    Exit Sub
                End If

                If Not LineaLeida.StartsWith(Constantes.PAR_NOMBRE & " " & Constantes.PAR_VERSION) Then
                    ErrorSemantico("[ERROR] Versión incorrecta del fichero " & Constantes.PAR_NOMBRE & ": " & LineaLeida)
                    Exit Sub
                End If
                primeralinea = False
                Continue While
            End If

            ' ---------------------------------------------
            ' Línea fuente original (contexto de error)
            ' ---------------------------------------------
            If LineaLeida.StartsWith(MarcaSRC) Then
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, LineaLeida)
                If opts.Pasada = 2 Then
                    GuardarIRS_Texto($"{Constantes.MarcaSRC} {LineaParaMostrar}")
                End If
                Continue While
            End If

            ' ----------------------------------------------------------------------------
            ' Línea del IR, montar Token auxliar para descomponer la línea correctamente
            ' ----------------------------------------------------------------------------
            Dim auxTok As New Token(LineaLeida)

            Select Case auxTok.ID
                Case TokenID.TCO_NONE
                    'Línea en blanco, no haemos nada
                Case TokenID.TCO_EOF
                    ' fin del fichero
                    EOFRecibido = True
                    Exit While
                Case TokenID.TCO_LINE
                    ' LINE n
                    Select Case opts.Pasada
                        Case 1
                            Dim n As Integer
                            If Integer.TryParse(auxTok.Value, n) Then
                                If opts.Pasada = 1 Then
                                    ' Validación de numeración correlativa en ZX BASIC
                                    If n <= UltimaLineaZX Then
                                        ErrorSemantico($"Numeración de líneas no válida: la línea {n} aparece después de {UltimaLineaZX}")
                                    End If
                                    UltimaLineaZX = n
                                End If
                            Else
                                ErrorSemantico($"LINE inválido en IRP: {LineaLeida}")
                            End If
                        Case 2
                            P2_AnalizarYEmitirStmt(auxTok, LineaParaMostrar)
                    End Select
                Case Else
                    Select Case opts.Pasada
                        Case 1
                            P1_RecolectarDesdeStmt(auxTok)
                        Case 2
                            P2_AnalizarYEmitirStmt(auxTok, LineaParaMostrar)
                        Case Else
                            Throw New InvalidOperationException("Pasada semántica desconocida")
                    End Select
            End Select
        End While

        'Console.WriteLine($"EOF....{EOFRecibido}")

        If Not EOFRecibido Then
            ErrorSemantico($"El contenido no termina por un EOF, posible fichero inválido")
        End If
    End Sub

    ' ============================================================
    ' PRIMERA PASADA (Recolección estructural)
    ' ============================================================
    ' - Leer IRP
    ' - Construir LineMap
    ' - Registrar variables
    ' - Registrar DATA
    ' - Marcar flags de uso (PRINT, READ, etc.)
    Private Sub P1_RecolectarDesdeStmt(tk As Token)
        Select Case tk.ID
            Case TokenID.TK_LET
                RecolectarLET(tk)

            Case TokenID.TK_FOR
                RecolectarFOR(tk)

            Case TokenID.TK_IF
                RecolectarIF(tk)

            Case TokenID.TK_DIM
                RecolectarDIM(tk)

            Case TokenID.TK_PRINT
                RecolectarPRINT(tk)

            Case TokenID.TK_DATA
                RecolectarDATA(tk)

            Case TokenID.TK_READ
                RecolectarREAD(tk)
        End Select

        ' El resto de sentencias no relevantes para la pasada 1 se ignoran

    End Sub

    Private Sub RecolectarLET(tk As Token)

        Dim lhs As String = ""
        Dim rhs As String = ""

        ' Separación estructural LET (:=)
        SepararLet(tk, lhs, rhs)

        ' ---------------------------------
        ' LHS: variable base
        ' ---------------------------------
        If Not lhs.StartsWith("V(") Then
            Exit Sub  ' LET inválido, ya se detectará en pasada 2
        End If

        Dim endVar As Integer = lhs.IndexOf(")"c)
        Dim baseName As String = lhs.Substring(2, endVar - 2)

        Dim info As VariableInfo = Nothing
        If Not ctx.Variables.TryGetValue(baseName, info) Then
            info = New VariableInfo With {
            .Name = baseName,
            .IsString = baseName.EndsWith("$"c)
        }
            ctx.Variables.Add(baseName, info)
        End If

        info.WasAssigned = True
        ctx.Variables(baseName) = info

        ' ---------------------------------
        ' LHS: índices (si existen)
        ' ---------------------------------
        Dim idxPos As Integer = lhs.IndexOf("IDX(")
        If idxPos >= 0 Then

            ' lhs contiene "... IDX( ... )"
            Dim start As Integer = idxPos + 4      ' justo después de "IDX("
            Dim endPos As Integer = lhs.LastIndexOf(")"c)

            If endPos < start Then
                Throw New FormatException($"IDX mal formado en LET: {lhs}")
            End If

            Dim idxText As String = lhs.Substring(start, endPos - start)


            Dim indexParts = SplitTopLevel(idxText, ","c)
            For Each part In indexParts
                Dim rpnIdx = ParseRPN(part.Trim())
                MarcarUsoVariablesRPN(rpnIdx)
            Next
        End If

        ' ---------------------------------
        ' RHS: expresión
        ' ---------------------------------
        Dim rpnRhs = ParseRPN(rhs)
        MarcarUsoVariablesRPN(rpnRhs)

    End Sub

    Private Sub RecolectarIF(tk As Token)

        ' En pasada 1 solo nos interesa marcar
        ' las variables usadas en la condición

        If tk.RPN Is Nothing OrElse tk.RPN.Count = 0 Then
            Exit Sub
        End If

        MarcarUsoVariablesRPN(tk.RPN)

    End Sub


    Private Sub RecolectarFOR(tk As Token)

        Dim text As String = tk.Value

        ' -----------------------------
        ' Separar variable de control
        ' -----------------------------
        Dim posAssign = text.IndexOf(":=", StringComparison.Ordinal)
        If posAssign < 0 Then Exit Sub

        Dim lhs = text.Substring(0, posAssign).Trim()
        Dim rest = text.Substring(posAssign + 2).Trim()

        ' lhs debe ser V(x)
        If Not lhs.StartsWith("V(") Then Exit Sub

        Dim endVar = lhs.IndexOf(")"c)
        If endVar < 0 Then Exit Sub

        Dim varName = lhs.Substring(2, endVar - 2)

        ' Registrar variable de control como asignada
        Dim info As VariableInfo = Nothing
        If Not ctx.Variables.TryGetValue(varName, info) Then
            info = New VariableInfo With {
            .Name = varName,
            .IsString = varName.EndsWith("$"c)
        }
            ctx.Variables.Add(varName, info)
        End If

        info.WasAssigned = True
        ctx.Variables(varName) = info

        ' -----------------------------
        ' Separar TO
        ' -----------------------------
        Dim posTo = rest.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase)
        If posTo < 0 Then Exit Sub

        Dim initExpr = rest.Substring(0, posTo).Trim()
        Dim tail = rest.Substring(posTo + 4).Trim()

        ' -----------------------------
        ' Separar STEP (opcional)
        ' -----------------------------
        Dim limitExpr As String
        Dim stepExpr As String = Nothing

        Dim posStep = tail.IndexOf(" STEP ", StringComparison.OrdinalIgnoreCase)
        If posStep >= 0 Then
            limitExpr = tail.Substring(0, posStep).Trim()
            stepExpr = tail.Substring(posStep + 6).Trim()
        Else
            limitExpr = tail
        End If

        ' -----------------------------
        ' Marcar uso de variables
        ' -----------------------------
        MarcarUsoVariablesRPN(ParseRPN(initExpr))
        MarcarUsoVariablesRPN(ParseRPN(limitExpr))

        If stepExpr IsNot Nothing Then
            MarcarUsoVariablesRPN(ParseRPN(stepExpr))
        End If

    End Sub

    Private Sub RecolectarPRINT(tk As Token)

        ' PRINT siempre lleva una expresión RPN pura
        If tk.RPN Is Nothing OrElse tk.RPN.Count = 0 Then
            Exit Sub
        End If

        MarcarUsoVariablesRPN(tk.RPN)

    End Sub

    Private Sub RecolectarDIM(tk As Token)
        ' Ejemplos de stmt: DIM A(10), DIM B$(40,2)

        Dim resto As String = tk.Value.Trim()

        ' Extraer el nombre base antes de '('
        Dim posParen As Integer = resto.IndexOf("("c)
        If posParen < 0 Then Exit Sub

        Dim varName As String =
        resto.Substring(0, posParen).Trim()

        ' Registrar variable si no existe
        If Not ctx.Variables.ContainsKey(varName) Then
            ctx.Variables.Add(
            varName,
            New VariableInfo With {
                .Name = varName,
                .IsString = varName.EndsWith("$"c),
                .WasAssigned = False,
                .WasUsed = False
            }
        )
        End If

    End Sub

    Private Sub RecolectarDATA(tk As Token)
        ' Formato: DATA v1 , v2 , "v3" ...
        Dim datos = tk.Value.Split(","c)

        For Each raw In datos
            Dim valorTexto = raw.Trim()

            Dim node As New DataNode
            node.Line = UltimaLineaZX
            node.Value = ParseDataValue(valorTexto)

            ctx.DataNodes.Add(node)
        Next

    End Sub

    Private Function ParseDataValue(text As String) As Object

        text = text.Trim()

        ' Literal IR: C(...)
        If text.StartsWith("C(") AndAlso text.EndsWith(")") Then
            Dim inner As String = text.Substring(2, text.Length - 3).Trim()

            ' String
            If inner.StartsWith("""") AndAlso inner.EndsWith("""") Then
                Return inner.Substring(1, inner.Length - 2)
            End If

            ' Numérico
            Dim n As Double
            If Double.TryParse(inner,
                               Globalization.NumberStyles.Any,
                               Globalization.CultureInfo.InvariantCulture,
                               n) Then
                Return n
            End If
        End If

        WarningSemantico($"Literal DATA no reconocido: {text}")
        Return text
    End Function

    Private Sub RecolectarREAD(tk As Token)

        ' Formato: READ A , B$ , C
        Dim vars = tk.Value.Split(","c)

        For Each v In vars
            Dim name = v.Trim()
            If Not ctx.Variables.ContainsKey(name) Then
                Dim info As New VariableInfo With {
                    .Name = name,
                    .IsString = name.EndsWith("$"c)
                }
                ctx.Variables.Add(name, info)
            End If
        Next

    End Sub

    ' -----------------------------------------------------------------------------------------------------
    ' --- SEGUNDA PASADA
    ' -----------------------------------------------------------------------------------------------------

    Private Sub P2_AnalizarYEmitirStmt(tk As Token, lineaSRC As String)
        If tk.ID = TokenID.TCO_LINE Then
            GuardarIRP_Token(tk)
            Return
        End If
        If tk.ID = TokenID.TCO_EOL Then
            GuardarIRP_Token(tk)
            Return
        End If

        If tk.ID = TokenID.TCO_UNKNOWN Then
            ErrorSemantico($"Sentencia desconocida en '{lineaSRC}': {tk}")
            Return
        End If

        ' Avisos antes de generar
        Select Case Token.GetFamily(tk.ID)
            Case TokenFamily.TF_NOSOPORTADO   ' No soportadas
                WarningSemantico($"Sentencia no soportada: {tk}")
            Case TokenFamily.TF_ESPECIALES   ' La especiales no se tratan
                ErrorSemantico($"Token especial no soportado: {tk}")
        End Select

        Try
            ' Las que necesitan semántica especial
            If tk.ID = TokenID.TK_LET Then
                AnalizarLET(tk, lineaSRC)
                Return
            End If

            If tk.ID = TokenID.TK_PRINT Then
                AnalizarPRINT(tk)
                Return
            End If

            If tk.ID = TokenID.TK_IF Then
                AnalizarIF(tk)
                Return
            End If

            If tk.ID = TokenID.TK_FOR Then
                AnalizarFOR(tk)
                Return
            End If

            If tk.ID = TokenID.TK_NEXT Then
                AnalizarNEXT(tk)
                Return
            End If

            If tk.ID = TokenID.TK_GOSUB Then
                AnalizarGOSUB(tk)
                Return
            End If

            If tk.ID = TokenID.TK_RETURN Then
                AnalizarRETURN(tk, lineaSRC)
                Return
            End If

            If tk.ID = TokenID.TK_GOTO OrElse tk.ID = TokenID.TK_STOP Then
                GuardarIRP_Token(tk)
                Return
            End If

            If tk.ID = TokenID.TK_READ Then
                AnalizarREAD(tk)
                Return
            End If

            ' CLEAR

            If tk.ID = TokenID.TK_CLEAR_RAM Then
                WarningSemantico($"CLEAR con parámetro no soportado directamente en SuperBASIC")
                GuardarIRP_Token(tk)
                Return
            End If

            If tk.ID = TokenID.TK_CLEAR Then
                GuardarIRP_Token(tk)
                Return
            End If

            'RANDOMIZE

            If tk.ID = TokenID.TK_RANDOMIZE Then
                GuardarIRP_Token(tk)
                Return
            End If

            ' RANDOMIZE USR (ZX-only)
            If tk.ID = TokenID.TK_RANDOMIZE_USR Then
                WarningSemantico($"RANDOMIZE USR no soportado directamente en QL")
                GuardarIRP_Token(tk)
                Return
            End If


            If tk.ID = TokenID.TK_DATA Then
                ' DATA se procesa solo en la pasada 1
                ' No se emite como código ejecutable, se añadirá al final
                Return
            End If


            ' el resto pasan directos
            GuardarIRP_Token(tk)
            Return

        Catch ex As Exception
            ErrorSemantico($"Error semántico en '{lineaSRC}': {ex.Message}")
        End Try

    End Sub

    ' ============================================================
    ' En la SEGUNDA PASADA
    ' - Emitir sección VARS
    ' - Emitir sección DATA
    ' ============================================================
    Private Function GuardarVarAndData() As Boolean
        Dim IDToken As Integer = 0
        Dim atributos As String = ""

        ' --------------------------------------------------------
        ' Sección de variables
        ' --------------------------------------------------------
        If ctx.Variables.Count <> 0 Then
            opts.Fase = SubFases.Variables
            stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))

            GuardarIRS_Texto(Constantes.VAR_NOMBRE & " " & Constantes.VAR_VERSION)
            GuardarIRS_Texto("")

            For Each kv In ctx.Variables
                Dim v = kv.Value

                Dim flags As String = ""
                flags &= " ["
                flags &= If(v.WasAssigned, "A", " ")
                flags &= If(v.WasUsed, "U", " ")
                flags &= "]"

                If v.IsString Then
                    GuardarIRS_VAR("STR " & v.Name & flags)
                Else
                    GuardarIRS_VAR("NUM " & v.Name & flags)
                End If

            Next
            GuardarIRS_VAR("-")
            GuardarIRS_VAR("ENDVARS")
            stWriter.Flush()
            stWriter.Close()
        End If


        ' --------------------------------------------------------
        ' Sección DATA
        ' --------------------------------------------------------

        If ctx.DataNodes.Count <> 0 Then
            opts.Fase = SubFases.Data
            stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))

            GuardarIRS_Texto(Constantes.DATA_NOMBRE & " " & Constantes.DATA_VERSION)
            GuardarIRS_Texto("")

            For Each d In ctx.DataNodes
                If TypeOf d.Value Is String Then
                    GuardarIRS_DATA($"NODE {d.Line} {Constantes.C_COMILLAS}{d.Value}{Constantes.C_COMILLAS}")
                Else
                    GuardarIRS_DATA($"NODE {d.Line} {d.Value}")
                End If
            Next
            GuardarIRS_DATA("-")
            GuardarIRS_DATA("ENDDATA")
            stWriter.Flush()
            stWriter.Close()
        End If

        Return (NroErrores = 0)

    End Function

    Private Sub AnalizarLET(tk As Token, lineaSRC As String)

        ' tk.RPN YA está reconstruida automáticamente
        Dim rpn As List(Of RPN.RPN_Node) = tk.RPN

        ' --- Marcar uso de variables ---
        MarcarUsoVariablesRPN(rpn)

        ' --- Marcar asignación de la variable base ---
        Dim baseVarName As String = ExtraerVariableBaseDesdeLET(tk.Value) ' solo LValue
        If ctx.Variables.ContainsKey(baseVarName) Then
            Dim v = ctx.Variables(baseVarName)
            v.WasAssigned = True
            ctx.Variables(baseVarName) = v
        End If

        ' --- Comprobación de tipos (usando RPN) ---
        Dim varType As VarType = If(baseVarName.EndsWith("$"c), VarType.StringType, VarType.Numeric)
        Dim exprType As VarType = GetExprType(rpn, ctx)

        If varType <> exprType Then
            WarningSemantico($"Posible asignación incompatible: {lineaSRC}")
        End If

        ' --- Emitir IR tal cual ---
        GuardarIRP_Token(tk)
    End Sub

    Private Function ExtraerVariableBaseDesdeLET(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return ""
        End If

        Dim i As Integer = 0

        ' Avanzar mientras sean caracteres válidos de nombre
        While i < value.Length AndAlso
          (Char.IsLetterOrDigit(value(i)) OrElse value(i) = "$"c)
            i += 1
        End While

        Return value.Substring(0, i)
    End Function



    Private Sub AnalizarPRINT(tk As Token)
        ' Cada TK_PRINT / TK_AT / TK_INK / etc.
        ' se trata como una acción independiente

        ' 1. Marcar uso de variables (si hay expresión)
        MarcarUsoVariablesRPN(tk.RPN)

        ' 2. Emitir directamente el token
        GuardarIRP_Token(tk)

    End Sub


    Private Sub AnalizarIF(tk As Token)

        ' La condición YA viene como RPN reconstruida desde el IR
        Dim rpn As List(Of RPN.RPN_Node) = tk.RPN

        ' Marcar uso de variables en la condición
        MarcarUsoVariablesRPN(rpn)

        ' Emitir IF estructural tal cual
        GuardarIRP_Token(tk)

    End Sub

    Private Sub AnalizarFOR(tk As Token)

        Dim text As String = tk.Value

        ' V(j) := C(1) TO C(10) [STEP ...]
        Dim posAssign = text.IndexOf(":=", StringComparison.Ordinal)
        If posAssign < 0 Then
            ErrorSemantico("FOR inválido: falta :=")
            Exit Sub
        End If

        Dim lhs = text.Substring(0, posAssign).Trim()     ' V(j)
        Dim rest = text.Substring(posAssign + 2).Trim()  ' C(1) TO C(10)

        ' ---- variable de control ----
        If Not lhs.StartsWith("V(") Then
            ErrorSemantico("FOR inválido: variable de control")
            Exit Sub
        End If
        Dim varName = lhs.Substring(2, lhs.IndexOf(")") - 2)

        ' ---- separar TO ----
        Dim posTo = rest.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase)
        If posTo < 0 Then
            ErrorSemantico("FOR inválido: falta TO")
            Exit Sub
        End If

        Dim initExpr = rest.Substring(0, posTo).Trim()
        Dim tail = rest.Substring(posTo + 4).Trim() ' después de TO

        ' ---- separar STEP (opcional) ----
        Dim limitExpr As String
        Dim stepExpr As String = Nothing

        Dim posStep = tail.IndexOf(" STEP ", StringComparison.OrdinalIgnoreCase)
        If posStep >= 0 Then
            limitExpr = tail.Substring(0, posStep).Trim()
            stepExpr = tail.Substring(posStep + 6).Trim()
        Else
            limitExpr = tail
        End If

        ' ---- RPN individual de cada parte ----
        Dim rpnInit = ParseRPN(initExpr)
        Dim rpnLimit = ParseRPN(limitExpr)
        Dim rpnStep = If(stepExpr IsNot Nothing, ParseRPN(stepExpr), Nothing)

        ' ---- semántica ----
        MarcarUsoVariablesRPN(rpnInit)
        MarcarUsoVariablesRPN(rpnLimit)
        If rpnStep IsNot Nothing Then MarcarUsoVariablesRPN(rpnStep)

        ' ---- pila FOR/NEXT ----
        ForStack.Push(varName)

        GuardarIRP_Token(tk)
    End Sub

    Private Function ExtraerVariableControlFOR(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return ""
        End If

        Dim i As Integer = 0

        ' Avanzar mientras sean caracteres válidos de identificador
        While i < value.Length AndAlso
          (Char.IsLetterOrDigit(value(i)) OrElse value(i) = "$"c)
            i += 1
        End While

        Return value.Substring(0, i)
    End Function

    Private Sub AnalizarNEXT(tk As Token)

        ' tk.Value contiene:
        '   ""   → NEXT
        '   "i"  → NEXT i
        Dim varName As String = tk.Value.Trim()

        ' NEXT sin FOR previo
        If ForStack.Count = 0 Then
            ErrorSemantico($"NEXT{If(varName <> "", " " & varName, "")} sin FOR previo")
            Exit Sub
        End If

        ' Variable esperada según el FOR
        Dim esperado As String = ForStack.Pop()

        ' NEXT con variable explícita
        If varName <> "" Then
            If varName <> esperado Then
                WarningSemantico($"NEXT {varName} no coincide con FOR {esperado}")
            End If
        End If

        ' Emitir tal cual
        GuardarIRP_Token(tk)

    End Sub

    Private Sub AnalizarGOSUB(tk As Token)

        ' Solo marcamos que hay una llamada GOSUB pendiente de RETURN
        GosubStack.Push(1)

        GuardarIRP_Token(New Token(TokenID.TK_GOSUB, tk.Value))

    End Sub

    Private Sub AnalizarRETURN(tk As Token, lineaSRC As String)

        If GosubStack.Count = 0 Then
            WarningSemantico($"RETURN sin GOSUB previo: {lineaSRC}")
        Else
            GosubStack.Pop()
        End If

        GuardarIRP_Token(New Token(TokenID.TK_RETURN, tk.Value))

    End Sub

    Private Sub AnalizarREAD(tk As Token)
        ' Formato: READ A , B$ , C
        Dim stmt As String = tk.Value
        Dim vars = stmt.Split(","c)

        For Each n In vars
            Dim name As String = n.Trim()

            If name = "" Then Continue For

            If ctx.Variables.ContainsKey(name) Then
                Dim v = ctx.Variables(name)
                v.WasAssigned = True
                ctx.Variables(name) = v
            Else
                ' ✅ READ asigna la variable implícitamente
                ctx.Variables.Add(
                name,
                New VariableInfo With {
                    .Name = name,
                    .IsString = name.EndsWith("$"c),
                    .WasAssigned = True
                }
            )
            End If
        Next

        GuardarIRP_Token(New Token(TokenID.TK_READ, stmt))
    End Sub

    Private Sub MarcarUsoVariablesRPN(rpn As List(Of RPN.RPN_Node))
        If rpn Is Nothing Then Exit Sub

        For Each n In rpn
            Select Case n.Kind

                Case RPNKind.VAR
                    Dim name As String = n.Value

                    If Not ctx.Variables.ContainsKey(name) Then
                        ctx.Variables.Add(
                        name,
                        New VariableInfo With {
                            .Name = name,
                            .IsString = name.EndsWith("$"c),
                            .WasUsed = True
                        }
                    )
                    Else
                        Dim v = ctx.Variables(name)
                        v.WasUsed = True
                        ctx.Variables(name) = v
                    End If

                Case RPNKind.FUN_CALL
                    ' Marcar uso de función auxiliar
                    ctx.FuncionesAuxiliares.Add(n.Value)

                    ' Los argumentos YA están como nodos VAR en la RPN,
                    ' así que no hay que hacer nada más aquí.

            End Select
        Next
    End Sub


    Private Function AjustarPalabras(expr As String,
                                     palabra As String,
                                     ByRef i As Integer,
                                     sb As StringBuilder) As Boolean

        ' ¿Cabe la palabra?
        If i + palabra.Length > expr.Length Then
            Return False
        End If

        ' Extraer posible coincidencia
        Dim aux As String = expr.Substring(i, palabra.Length)

        ' Comparar exactamente
        If String.Compare(aux, palabra, StringComparison.OrdinalIgnoreCase) <> 0 Then
            Return False
        End If

        ' Comprobar límites léxicos
        If Not EsLimiteIzquierdo(expr, i) OrElse
           Not EsLimiteDerecho(expr, i + palabra.Length - 1) Then
            Return False
        End If

        ' Espacio canónico antes (si no es inicio y no hay ya espacio)
        If sb.Length > 0 AndAlso sb(sb.Length - 1) <> " "c Then
            sb.Append(" ")
        End If

        ' Añadir palabra
        sb.Append(palabra)

        ' Espacio canónico después
        sb.Append(" ")

        ' Avanzar índice
        i += palabra.Length
        Return True
    End Function



    Private Function EsLimiteIzquierdo(expr As String, i As Integer) As Boolean
        If i = 0 Then Return True
        Return Not Char.IsLetterOrDigit(expr(i - 1)) AndAlso expr(i - 1) <> "_"c
    End Function

    Private Function EsLimiteDerecho(expr As String, i As Integer) As Boolean
        If i >= expr.Length - 1 Then Return True
        Return Not Char.IsLetterOrDigit(expr(i + 1)) AndAlso expr(i + 1) <> "_"c
    End Function

    ' ============================================================
    ' Helpers de expresiones
    ' ============================================================
    Private Function SepararLet(tk As Token, ByRef lvalue As String, ByRef rvalue As String) As Boolean

        If tk.ID <> TokenID.TK_LET Then
            ErrorSemantico($"'La sentencia no es un LET {tk.ID.ToString}")
            Return False
        End If

        Dim pos As Integer = tk.Value.IndexOf(":=", StringComparison.Ordinal)
        If pos < 0 Then
            ErrorSemantico($"'La sentencia LET no contiene la igualdad (:=)")
            Return False
        End If

        lvalue = tk.Value.Substring(0, pos).Trim()
        rvalue = tk.Value.Substring(pos + 2).Trim()


        If lvalue = "" Then
            ErrorSemantico($"'La sentencia LET no contiene parte izquierda")
            Return False
        End If

        If rvalue = "" Then
            ErrorSemantico($"'La sentencia LET no contiene parte derecha")
            Return False
        End If

        Return True
    End Function

    'retona el nombde de la variable de la parte L
    Private Function ExtraerBaseNameDeLHS(lhs As String) As String
        If Not lhs.StartsWith("V(") Then
            Throw New FormatException($"LHS LET inválido: {lhs}")
        End If

        Dim endPos = lhs.IndexOf(")"c)
        If endPos < 0 Then
            Throw New FormatException($"LHS LET inválido: {lhs}")
        End If

        Return lhs.Substring(2, endPos - 2)
    End Function

    'retona los índices de la variable de la parte L si existen
    Private Function SplitTopLevel(text As String, separator As Char) As List(Of String)
        Dim result As New List(Of String)
        Dim level As Integer = 0
        Dim start As Integer = 0

        For i As Integer = 0 To text.Length - 1
            Dim ch As Char = text(i)

            Select Case ch
                Case "("c
                    level += 1

                Case ")"c
                    level -= 1

                Case separator
                    If level = 0 Then
                        result.Add(text.Substring(start, i - start).Trim())
                        start = i + 1
                    End If
            End Select
        Next

        ' último segmento
        If start < text.Length Then
            result.Add(text.Substring(start).Trim())
        End If

        Return result
    End Function

    Public Function GetExprType(rpn As List(Of RPN.RPN_Node), ctx As SemanticContext) As VarType

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            Return VarType.Unknown
        End If

        Dim stack As New Stack(Of VarType)

        For Each n In rpn
            Select Case n.Kind

                Case RPNKind.VAR
                    ' El tipo depende del nombre ($)
                    If n.Value.EndsWith("$"c) Then
                        stack.Push(VarType.StringType)
                    Else
                        stack.Push(VarType.Numeric)
                    End If

                Case RPNKind.CTE
                    ' Constante string o numérica
                    If n.Value.StartsWith("""") Then
                        stack.Push(VarType.StringType)
                    Else
                        stack.Push(VarType.Numeric)
                    End If

                Case RPNKind.FUN_CALL
                    ' Por ahora asumimos funciones numéricas
                    ' (puedes refinar esto luego)
                    stack.Push(VarType.Numeric)

                Case RPNKind.UNARY_OP
                    Dim t As VarType = stack.Pop()
                    stack.Push(t)

                Case RPNKind.BINARY_OP
                    Dim t2 As VarType = stack.Pop()
                    Dim t1 As VarType = stack.Pop()

                    ' Si alguno es string → string
                    If t1 = VarType.StringType OrElse t2 = VarType.StringType Then
                        stack.Push(VarType.StringType)
                    Else
                        stack.Push(VarType.Numeric)
                    End If

            End Select
        Next

        If stack.Count > 0 Then
            Return stack.Peek()
        End If

        Return VarType.Unknown
    End Function

    ' ============================================================
    ' GESTIÓN DE ERRORES / AVISOS
    ' ============================================================
    Private Sub EmitirWarningsVariables()

        For Each kv In ctx.Variables

            Dim v = kv.Value

            ' Usada pero nunca asignada
            If v.WasUsed AndAlso Not v.WasAssigned Then
                WarningVariables($"Variable '{v.Name}' usada pero nunca asignada.")

                ' 🔍 Posible confusión entre variable numérica y string ($)
                Dim base = NombreBase(v.Name)

                For Each kv2 In ctx.Variables
                    Dim other = kv2.Value

                    If other.WasAssigned AndAlso
                       NombreBase(other.Name) = base AndAlso
                       other.IsString <> v.IsString Then
                        WarningVariables($"¿Quiso decir '{other.Name}' en lugar de '{v.Name}'?")
                        Exit For
                    End If
                Next
            End If

            ' Asignada pero nunca usada
            If v.WasAssigned AndAlso Not v.WasUsed Then
                WarningVariables($"Variable '{v.Name}' asignada pero nunca usada.")
            End If

        Next

    End Sub

    Private Function NombreBase(varName As String) As String
        If varName.EndsWith("$"c) Then
            Return varName.Substring(0, varName.Length - 1)
        End If
        Return varName
    End Function

    Private Sub ErrorSemantico(descripcion As String)
        NroErrores += 1
        MensajeError(opts, stReader, stWriter, False, NroLineaFichero, 0, LineaParaMostrar, descripcion, False)
    End Sub

    Private Sub WarningSemantico(descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        MensajeError(opts, stReader, stWriter, True, NroLineaFichero, 0, LineaParaMostrar, descripcion, False)

    End Sub

    Private Sub WarningVariables(descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        MensajeError(opts, Nothing, Nothing, True, 0, 0, "", "[Variables] " & descripcion, False)
    End Sub


    ' ============================================================
    ' GRABAR EN DESTINO
    ' ============================================================

    Private Sub GuardarIRP_Token(tk As Token)
        Dim pi As New PrintItem
        If tk.ID = TokenID.TK_PRINT Then
            pi = PrintItem.FromToken(tk)
        Else
            pi.ID = TokenID.TCO_UNKNOWN
        End If

        GuardarIRS(tk, pi)
    End Sub

    Private Sub GuardarIRS(tk As Token, pi As PrintItem)
        Dim idNum As Integer = CInt(tk.ID)
        Dim value As String = If(tk.Value IsNot Nothing, tk.Value, "")
        Dim Linea As String = ""
        Dim Comentario As String = ""

        If pi.ID = TokenID.TCO_UNKNOWN Then
            Linea = $"{idNum} {value}"
            Comentario = $"{tk.ID.ToString()}"
        Else
            Linea = $"{idNum} {value}" ' Linea = $"{idNum} {pi.ToText} {value}"
            Comentario = $"{tk.ID.ToString()} {pi.ID.ToString()}"
        End If

        If Len(Linea) < 49 Then
            Linea &= Space(50 - Len(Linea)) & $"{Constantes.MarcaComentario} {Comentario}"
            GuardarIRS_Texto(Linea)
        Else
            GuardarIRS_Texto(Linea)
            Linea = Space(50) & $"{Constantes.MarcaComentario} {Comentario}"
            GuardarIRS_Texto(Linea)
        End If
    End Sub

    Private Sub GuardarIRS_VAR(linea As String)
        GuardarIRS_Aux("VAR", linea)
    End Sub

    Private Sub GuardarIRS_DATA(linea As String)
        GuardarIRS_Aux("DATA", linea)
    End Sub

    Private Sub GuardarIRS_Aux(tipo As String, linea As String)
        If (linea <> "-") Then
            GuardarIRS_Texto(tipo & "  " & linea)
        Else
            GuardarIRS_Texto("")
        End If
    End Sub

    Private Sub GuardarIRS_Texto(linea As String)

        If stWriter Is Nothing Then
            Throw New InvalidOperationException("OutWriter no inicializado")
        End If

        stWriter.WriteLine(linea)

        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If
    End Sub

End Module