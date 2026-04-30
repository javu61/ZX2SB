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

    ReadOnly RegexIdentificador As New Text.RegularExpressions.Regex("^[A-Z][A-Z0-9]*\$?$", Text.RegularExpressions.RegexOptions.IgnoreCase)

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        opts = _opts
        NroLineaFichero = 0
        NroErrores = 0

        ' --- Inicializar contexto ---
        InicializarContexto()

        ' ============================================================
        ' PRIMERA PASADA (Recolección estructural)
        ' ============================================================
        opts.Pasada = 1
        ProcesarIR(Nothing)
        If NroErrores <> 0 Then
            Return 1
        End If

        ' ============================================================
        ' SEGUNDA PASADA (Análisis semántico + generación IRS)
        ' ============================================================
        opts.Pasada = 2
        Using writer As New StreamWriter(opts.FSalidaSem, False, New UTF8Encoding(False))
            GuardarIRS_Texto(writer, Constantes.SEM_NOMBRE & " " & Constantes.SEM_VERSION)
            ProcesarIR(writer)
            If NroErrores <> 0 Then
                Return 1
            End If
            GuardarIRP_Token(writer, New Token(TokenID.TCO_EOF))
            GuardarVarAndData()
        End Using

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

    Private Sub ProcesarIR(writer As StreamWriter)
        Dim primeralinea As Boolean = True
        Dim EOFRecibido As Boolean = False
        NroLineaFichero = 0

        For Each LineaLeida As String In File.ReadLines(opts.FSalidaPar)

            If String.IsNullOrWhiteSpace(LineaLeida) Then Continue For

            ' ---------------------------------------------
            ' Cabecera IRP
            ' ---------------------------------------------
            If primeralinea Then
                MostrarMensaje(opts, LineaLeida)
                If Not LineaLeida.StartsWith(Constantes.PAR_NOMBRE) Then
                    ErrorSemantico(writer, 0, "[ERROR] No es un fichero " & Constantes.PAR_NOMBRE & ": " & LineaLeida)
                    Exit Sub
                End If

                If Not LineaLeida.StartsWith(Constantes.PAR_NOMBRE & " " & Constantes.PAR_VERSION) Then
                    ErrorSemantico(writer, 0, "[ERROR] Versión incorrecta del fichero " & Constantes.PAR_NOMBRE & ": " & LineaLeida)
                    Exit Sub
                End If
                primeralinea = False
                Continue For
            End If

            ' ---------------------------------------------
            ' Línea fuente original (contexto de error)
            ' ---------------------------------------------
            If LineaLeida.StartsWith(MarcaSRC) Then
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, LineaLeida)
                If opts.Pasada = 2 Then
                    GuardarIRS_Texto(writer, $"{Constantes.MarcaSRC} {LineaParaMostrar}")
                End If
                Continue For
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
                Case TokenID.TCO_LINE
                    ' LINE n
                    Select Case opts.Pasada
                        Case 1
                            Dim n As Integer
                            If Integer.TryParse(auxTok.Value, n) Then
                                If opts.Pasada = 1 Then
                                    ' Validación de numeración correlativa en ZX BASIC
                                    If n <= UltimaLineaZX Then
                                        ErrorSemantico(Nothing, 0, $"Numeración de líneas no válida: la línea {n} aparece después de {UltimaLineaZX}")
                                    End If
                                    UltimaLineaZX = n
                                End If
                            Else
                                ErrorSemantico(Nothing, 0, $"LINE inválido en IRP: {LineaLeida}")
                            End If
                        Case 2
                            P2_AnalizarYEmitirStmt(auxTok, LineaParaMostrar, writer)
                    End Select
                Case Else
                    Select Case opts.Pasada
                        Case 1
                            P1_RecolectarDesdeStmt(auxTok)
                        Case 2
                            P2_AnalizarYEmitirStmt(auxTok, LineaParaMostrar, writer)
                        Case Else
                            Throw New InvalidOperationException("Pasada semántica desconocida")
                    End Select
            End Select
        Next

        If Not EOFRecibido Then
            ErrorSemantico(Nothing, 0, $"El contenido no termina por un EOF, posible fichero inválido")
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

        If tk.ID = TokenID.TK_LET Then
            RecolectarLET(tk)
            Return
        End If

        If tk.ID = TokenID.TK_DIM Then
            RecolectarDIM(tk)
            Return
        End If

        If tk.ID = TokenID.TK_PRINT Then
            RecolectarPrint(tk)
            Return
        End If

        If tk.ID = TokenID.TK_DATA Then
            RecolectarDATA(tk)
            Return
        End If

        If tk.ID = TokenID.TK_READ Then
            RecolectarREAD(tk)
            Return
        End If

        ' El resto de sentencias no relevantes para la pasada 1 se ignoran

    End Sub

    Private Sub RecolectarLET(tk As Token)
        ' Formato: LET <var> <expr>
        Dim pL As String = ""
        Dim pR As String = ""
        SepararLetIR(tk.Value, pL, pR)
        Dim varName As String = ExprTypeEvaluator.GetBaseVariableName(pL)

        If Not ctx.Variables.ContainsKey(varName) Then
            Dim info As New VariableInfo With {
                .Name = varName,
                .IsString = varName.EndsWith("$"c)
                }
            ctx.Variables.Add(varName, info)
        End If

    End Sub

    Private Sub RecolectarPrint(tk)

    End Sub

    Private Sub RecolectarDIM(tk As Token)
        ' Ejemplos de stmt: DIM A(10), DIM B$(40,2)

        Dim resto As String = tk.Value.Trim()

        ' Extraer el nombre base antes de '('
        Dim posParen As Integer = resto.IndexOf("("c)
        If posParen < 0 Then Exit Sub

        Dim varName As String =
        resto.Substring(0, posParen).Trim().ToUpperInvariant()

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
            node.Value = ParseLiteral(valorTexto)

            ctx.DataNodes.Add(node)
        Next

    End Sub

    Private Function ParseLiteral(text As String) As Object

        text = text.Trim()

        ' String literal
        If text.StartsWith(Constantes.C_COMILLAS) AndAlso text.EndsWith(Constantes.C_COMILLAS) Then
            Return text.Substring(1, text.Length - 2)
        End If

        ' Numérico
        Dim n As Double
        If Double.TryParse(text,
                           Globalization.NumberStyles.Any,
                           Globalization.CultureInfo.InvariantCulture,
                           n) Then
            Return n
        End If

        ' Si no se reconoce, dejarlo como string
        WarningSemantico(Nothing, 0, $"Literal DATA no reconocido: {text}")
        Return text

    End Function

    Private Sub RecolectarREAD(tk As Token)

        ' Formato: READ A , B$ , C
        Dim vars = tk.Value.Split(","c)

        For Each v In vars
            Dim name = v.Trim().ToUpperInvariant()
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

    Private Sub P2_AnalizarYEmitirStmt(tk As Token, lineaSRC As String, writer As StreamWriter)
        If tk.ID = TokenID.TCO_LINE Then
            GuardarIRP_Token(writer, tk)
            Return
        End If
        If tk.ID = TokenID.TCO_EOL Then
            GuardarIRP_Token(writer, tk)
            Return
        End If

        If tk.ID = TokenID.TCO_UNKNOWN Then
            ErrorSemantico(Nothing, 0, $"Sentencia desconocida en '{lineaSRC}': {tk}")
            Return
        End If

        ' Avisos antes de generar
        Select Case Token.GetFamily(tk.ID)
            Case TokenFamily.TF_NOSOPORTADO   ' No soportadas
                WarningSemantico(writer, 0, $"Sentencia no soportada: {tk}")
            Case TokenFamily.TF_ESPECIALES   ' La especiales no se tratan
                ErrorSemantico(writer, 0, $"Token especial no soportado: {tk}")
        End Select

        Try
            ' Las que necesitan semántica especial
            If tk.ID = TokenID.TK_LET Then
                AnalizarLET(tk, lineaSRC, writer)
                Return
            End If

            If tk.ID = TokenID.TK_PRINT Then
                AnalizarPRINT(tk, writer)
                Return
            End If

            If tk.ID = TokenID.TK_IF Then
                AnalizarIF(tk, writer)
                Return
            End If

            If tk.ID = TokenID.TK_FOR Then
                AnalizarFOR(tk, writer)
                Return
            End If

            If tk.ID = TokenID.TK_NEXT Then
                AnalizarNEXT(tk, writer)
                Return
            End If

            If tk.ID = TokenID.TK_GOSUB Then
                AnalizarGOSUB(tk, writer)
                Return
            End If

            If tk.ID = TokenID.TK_RETURN Then
                AnalizarRETURN(tk, lineaSRC, writer)
                Return
            End If

            If tk.ID = TokenID.TK_GOTO OrElse tk.ID = TokenID.TK_STOP Then
                GuardarIRP_Token(writer, tk)
                Return
            End If

            If tk.ID = TokenID.TK_READ Then
                AnalizarREAD(tk, writer)
                Return
            End If

            ' CLEAR

            If tk.ID = TokenID.TK_CLEAR_RAM Then
                WarningSemantico(Nothing, 0, $"CLEAR con parámetro no soportado directamente en SuperBASIC")
                GuardarIRP_Token(writer, tk)
                Return
            End If

            If tk.ID = TokenID.TK_CLEAR Then
                GuardarIRP_Token(writer, tk)
                Return
            End If

            'RANDOMIZE

            If tk.ID = TokenID.TK_RANDOMIZE Then
                GuardarIRP_Token(writer, tk)
                Return
            End If

            ' RANDOMIZE USR (ZX-only)
            If tk.ID = TokenID.TK_RANDOMIZE_USR Then
                WarningSemantico(writer, 0, $"RANDOMIZE USR no soportado directamente en QL")
                GuardarIRP_Token(writer, tk)
                Return
            End If


            ' el resto pasan directos
            GuardarIRP_Token(writer, tk)
            Return

        Catch ex As Exception
            ErrorSemantico(Nothing, 0, $"Error semántico en '{lineaSRC}': {ex.Message}")
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
        Using writer As New StreamWriter(opts.FVar, False, New UTF8Encoding(False))
            If ctx.Variables.Count <> 0 Then
                GuardarIRS_Texto(writer, Constantes.VAR_NOMBRE & " " & Constantes.VAR_VERSION)
                GuardarIRS_Texto(writer, "")

                For Each kv In ctx.Variables
                    Dim v = kv.Value

                    Dim flags As String = ""
                    flags &= " ["
                    flags &= If(v.WasAssigned, "A", " ")
                    flags &= If(v.WasUsed, "U", " ")
                    flags &= "]"

                    If v.IsString Then
                        GuardarIRS_VAR(writer, "STR " & v.Name & flags)
                    Else
                        GuardarIRS_VAR(writer, "NUM " & v.Name & flags)
                    End If

                Next
                GuardarIRS_VAR(writer, "")
                GuardarIRS_VAR(writer, "ENDVARS")
            End If
        End Using

        ' --------------------------------------------------------
        ' Sección DATA
        ' --------------------------------------------------------
        Using writer As New StreamWriter(opts.FData, False, New UTF8Encoding(False))
            If ctx.DataNodes.Count <> 0 Then
                GuardarIRS_Texto(writer, Constantes.DATA_NOMBRE & " " & Constantes.DATA_VERSION)
                GuardarIRS_Texto(writer, "")

                For Each d In ctx.DataNodes
                    If TypeOf d.Value Is String Then
                        GuardarIRS_DATA(writer, $"NODE {d.Line} {Constantes.C_COMILLAS}{d.Value}{Constantes.C_COMILLAS}")
                    Else
                        GuardarIRS_DATA(writer, $"NODE {d.Line} {d.Value}")
                    End If
                Next
                GuardarIRS_DATA(writer, "")
                GuardarIRS_DATA(writer, "ENDDATA")
            End If
        End Using

        Return (NroErrores = 0)

    End Function

    Private Sub AnalizarLET(tk As Token, lineaSRC As String, writer As StreamWriter)

        Dim resto As String = ""
        Dim lvalue As String = ""
        Dim rvalue As String = ""

        SepararLetIR(tk.Value, lvalue, rvalue)

        ' --- EXTRAER NOMBRE BASE DEL LVALUE ---
        ' Ejemplos:
        '   A        -> A
        '   A(3)     -> A
        '   B$       -> B$
        '   B$(I,J)  -> B$

        Dim baseVarName As String = ExprTypeEvaluator.GetBaseVariableName(lvalue)
        'If Not ctx.Variables.ContainsKey(baseVarName) Then
        '    WarningSintactico(Nothing, 0, $"Variable no declarada implícitamente: {baseVarName}")
        '    Exit Sub
        'End If

        ' Marcar uso en expresión (lado derecho)
        MarcarUsoVariables(rvalue)

        ' Marcar asignación en la variable BASE
        If ctx.Variables.ContainsKey(baseVarName) Then
            Dim v = ctx.Variables(baseVarName)
            v.WasAssigned = True
            ctx.Variables(baseVarName) = v
        End If

        ' --- COMPROBACIÓN DE TIPOS ---
        Dim varType As VarType = If(baseVarName.EndsWith("$"c), VarType.StringType, VarType.Numeric)
        Dim exprType As VarType = ExprTypeEvaluator.GetExprType(rvalue, ctx)

        If varType = VarType.Numeric AndAlso exprType = VarType.StringType Then
            WarningSemantico(Nothing, 0, $"Posible asignación inválida num <- str: {lineaSRC}")
            Exit Sub
        End If

        If varType = VarType.StringType AndAlso exprType = VarType.Numeric Then
            WarningSemantico(Nothing, 0, $"Posible asignación inválida str <- num: {lineaSRC}")
            Exit Sub
        End If

        lvalue = NormalizarEspacios(lvalue)
        rvalue = NormalizarEspacios(rvalue)
        GuardarIRP_Token(writer, New Token(TokenID.TK_LET, lvalue & "=" & rvalue))

    End Sub

    Private Sub SepararLetIR(stmt As String, ByRef lvalue As String, ByRef rvalue As String)

        Dim nivel As Integer = 0

        For i As Integer = 0 To stmt.Length - 1
            Dim ch As Char = stmt(i)

            Select Case ch
                Case "("c
                    nivel += 1
                Case ")"c
                    nivel -= 1
                Case "="c
                    If nivel = 0 Then
                        lvalue = stmt.Substring(0, i).Trim()
                        rvalue = stmt.Substring(i + 1).Trim()
                        Return
                    End If
            End Select
        Next

        ' Si no se encontró separador válido
        lvalue = ""
        rvalue = ""
    End Sub

    Private Sub AnalizarPRINT(tk As Token, writer As StreamWriter)

        ' stmt llega como:
        ' PRINT AT 3,3;INK 7;"Hola mundo",A

        'Marcar uso de variables SOLO fuera de strings
        MarcarUsoVariables(tk.Value)

        '  Guardar el PRINT COMPLETO, SIN TOCARLO
        GuardarIRP_Token(writer, New Token(TokenID.TK_PRINT, tk.Value))

    End Sub

    Private Sub AnalizarIF(tk As Token, writer As StreamWriter)
        Dim condicion As String = tk.Value.Trim()

        condicion = NormalizarEspacios(condicion)
        ' Emitir IF como nodo estructural
        GuardarIRP_Token(writer, New Token(TokenID.TK_IF, condicion))

    End Sub



    Private Sub AnalizarFOR(tk As Token, writer As StreamWriter)
        Dim varName As String = ""
        Dim vm As VariableMatch = Nothing
        Dim stmt As String = tk.Value

        If Not TryMatchVariable(stmt, VarCheckContext.ForControl, vm) Then
            ErrorSemantico(writer, 0, "Variable de control FOR inválida")
            Exit Sub
        End If


        ForStack.Push(varName)

        ' Uso en expresión
        MarcarUsoVariables(stmt.Substring(stmt.IndexOf("="c) + 1))

        ' Marcar asignación
        If ctx.Variables.ContainsKey(varName) Then
            Dim v = ctx.Variables(varName)
            v.WasAssigned = True
            ctx.Variables(varName) = v
        End If

        stmt = NormalizarEspacios(stmt)
        GuardarIRP_Token(writer, New Token(TokenID.TK_FOR, stmt))

    End Sub

    Private Sub AnalizarNEXT(tk As Token, writer As StreamWriter)
        Dim stmt As String = tk.Value
        Dim varName = stmt.Trim()
        Dim vm As VariableMatch = Nothing

        ' NEXT sin variable
        If varName = "" Then
            ErrorSemantico(writer, 0, "NEXT debe indicar la variable de control del FOR")
            Exit Sub
        End If

        ' Variable debe tener nombre correcto
        If Not TryMatchVariable(varName, VarCheckContext.ForControl, vm) Then
            ErrorSemantico(writer, 0, "Variable de control NEXT inválida")
            Exit Sub
        End If

        'Next debe estar tras un for
        If ForStack.Count = 0 Then
            ErrorSemantico(Nothing, 0, $"NEXT {stmt} sin FOR previo")
            Exit Sub
        End If

        Dim esperado = ForStack.Pop()

        If varName <> "" AndAlso varName <> esperado Then
            WarningSemantico(Nothing, 0, $"NEXT {varName} no coincide con FOR {esperado}")
        End If

        GuardarIRP_Token(writer, New Token(TokenID.TK_NEXT, stmt))

    End Sub

    Private Sub AnalizarGOSUB(tk As Token, writer As StreamWriter)

        ' Solo marcamos que hay una llamada GOSUB pendiente de RETURN
        GosubStack.Push(1)

        GuardarIRP_Token(writer, New Token(TokenID.TK_GOSUB, tk.Value))

    End Sub

    Private Sub AnalizarRETURN(tk As Token, lineaSRC As String, writer As StreamWriter)

        If GosubStack.Count = 0 Then
            WarningSemantico(Nothing, 0, $"RETURN sin GOSUB previo: {lineaSRC}")
        Else
            GosubStack.Pop()
        End If

        GuardarIRP_Token(writer, New Token(TokenID.TK_RETURN, tk.Value))

    End Sub

    Private Sub AnalizarREAD(tk As Token, writer As StreamWriter)
        ' Formato: READ A , B$ , C
        Dim stmt As String = tk.Value
        Dim vars = stmt.Split(","c)

        For Each n In vars
            Dim name As String = n.Trim()

            ' ✅ Normalizar completamente el identificador
            name = name.ToUpperInvariant()

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

        GuardarIRP_Token(writer, New Token(TokenID.TK_READ, stmt))
    End Sub

    Private Sub MarcarUsoVariables(expr As String)

        If String.IsNullOrEmpty(expr) Then Exit Sub

        Dim i As Integer = 0
        Dim len As Integer = expr.Length

        While i < len

            Dim ch As Char = expr(i)

            ' 1. Omitir literales string
            If ch = Constantes.C_COMILLAS Then
                i += 1
                While i < len AndAlso expr(i) <> Constantes.C_COMILLAS
                    i += 1
                End While
                If i < len Then i += 1
                Continue While
            End If

            ' 2. Posible inicio de identificador
            If Char.IsLetter(ch) Then
                Dim start As Integer = i
                i += 1
                Dim hasDollar As Boolean = False

                ' Letras y dígitos
                While i < len AndAlso Char.IsLetterOrDigit(expr(i))
                    i += 1
                End While

                ' $ opcional (solo uno y al final)
                If i < len AndAlso expr(i) = "$"c Then
                    hasDollar = True
                    i += 1
                End If

                Dim name As String = expr.Substring(start, i - start).ToUpperInvariant()

                ' ✅ Validación ZX BASIC final
                ' (letra inicial garantizada)
                If name.Length > 0 Then

                    ' ❌ Palabras reservadas (cuando toque)
                    ' If IsKeyword(name) Then GoTo NextChar

                    ' ✅ Crear / marcar variable
                    If Not ctx.Variables.ContainsKey(name) Then
                        ctx.Variables.Add(name,
                        New VariableInfo With {
                                                .Name = name,
                                                .IsString = hasDollar,
                                                .WasUsed = True
                                                })
                    Else
                        Dim v = ctx.Variables(name)
                        v.WasUsed = True
                        ctx.Variables(name) = v
                    End If
                End If

                Continue While
            End If

            i += 1
        End While

    End Sub

    ' ------------------------------------------------------------
    ' Normaliza espacios SOLO fuera de cadenas y comentarios REM
    ' ------------------------------------------------------------
    Private Function NormalizarEspacios(expr As String) As String

        If expr Is Nothing Then
            Return expr

        End If
        Dim sb As New StringBuilder()
        Dim inString As Boolean = False
        Dim i As Integer = 0

        While i < expr.Length

            Dim ch As Char = expr(i)

            ' ---- Strings ----
            If ch = Constantes.C_COMILLAS Then
                inString = Not inString
                sb.Append(ch)
                i += 1
                Continue While
            End If

            If inString Then
                sb.Append(ch)
                i += 1
                Continue While
            End If

            ' ---- Fuera de strings ----

            ' Ignorar espacios (por defecto)
            If ch = Constantes.C_ESPACIO Then
                i += 1
                Continue While
            End If

            Dim lista As New List(Of String)
            lista.Add("AND")
            lista.Add("OR")
            lista.Add("NOT")
            lista.Add("TO")

            For Each palabra In lista
                If AjustarPalabras(expr, palabra, i, sb) Then
                    Continue While
                End If
            Next

            'Ahora son TOKENS, no hay que tratar estas
            'lista.Clear()
            'lista.Add("AT")
            'lista.Add("INK")
            'lista.Add("PAPER")
            'lista.Add("BRIGHT")
            'lista.Add("FLASH")
            'lista.Add("OVER")
            'lista.Add("INVERSE")

            'For Each palabra In lista
            '    If AjustarPalabras(expr, palabra, i, sb) Then
            '        Continue While
            '    End If
            'Next

            ' ---- Carácter normal ----
            sb.Append(ch)
            i += 1
        End While

        Return sb.ToString().Trim()
    End Function

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

    Private Sub ErrorSemantico(writer As StreamWriter, columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If
        MensajeError(opts, writer, False, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)
    End Sub
    Private Sub WarningSemantico(writer As StreamWriter, columna As Integer, descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        If (columna <> 0) Then
            columna = columna - 1
        End If
        MensajeError(opts, writer, True, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)

    End Sub

    Private Sub WarningVariables(descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        MensajeError(opts, Nothing, True, 0, 0, "", "[Variables] " & descripcion)
    End Sub


    ' ============================================================
    ' GRABAR EN DESTINO
    ' ============================================================

    Private Sub GuardarIRP_Token(writer As StreamWriter, tk As Token)
        Dim pi As New PrintItem
        If tk.ID = TokenID.TK_PRINT Then
            pi = PrintItem.FromText(tk.Value)
        Else
            pi.ID = TokenID.TCO_UNKNOWN
        End If

        GuardarIRS(writer, tk, pi)
    End Sub



    Private Sub GuardarIRS(writer As StreamWriter, tk As Token, pi As PrintItem)
        Dim idNum As Integer = CInt(tk.ID)
        Dim value As String = If(tk.Value IsNot Nothing, tk.Value, "")
        Dim Linea As String = ""
        Dim Comentario As String = ""

        If tk.ID <> TokenID.TK_REM Then tk.Value = NormalizarEspacios(tk.Value)
        If pi.ID <> TokenID.TK_REM Then pi.Value = NormalizarEspacios(pi.Value)

        If pi.ID = TokenID.TCO_UNKNOWN Then
            Linea = $"{idNum} {value}"
            Comentario = $"{tk.ID.ToString()}"
        Else
            Linea = $"{idNum} {value}" ' Linea = $"{idNum} {pi.ToText} {value}"
            Comentario = $"{tk.ID.ToString()} {pi.ID.ToString()}"
        End If

        If Len(Linea) < 49 Then
            Linea &= Space(50 - Len(Linea)) & $"{Constantes.MarcaComentario} {Comentario}"
            GuardarIRS_Texto(writer, Linea)
        Else
            GuardarIRS_Texto(writer, Linea)
            Linea = Space(50) & $"{Constantes.MarcaComentario} {Comentario}"
            GuardarIRS_Texto(writer, Linea)
        End If
    End Sub

    Private Sub GuardarIRS_VAR(writer As StreamWriter, linea As String)
        GuardarIRS_Texto(writer, "VAR  " & linea)
    End Sub

    Private Sub GuardarIRS_DATA(writer As StreamWriter, linea As String)
        GuardarIRS_Texto(writer, "DATA " & linea)
    End Sub

    Private Sub GuardarIRS_Texto(writer As StreamWriter, linea As String)
        writer.WriteLine(linea)

        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If
    End Sub

End Module