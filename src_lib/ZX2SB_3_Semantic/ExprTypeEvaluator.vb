Public Module ExprTypeEvaluator

    Public Enum VarCheckContext
        GeneralExpression      ' RHS, PRINT, IF, etc.
        AssignmentLValue       ' LET / READ
        ForControl             ' FOR I = ...
        DimDeclaration         ' DIM A(...)
    End Enum

    Public Structure VariableMatch
        Public Name As String
        Public IsString As Boolean
        Public IsArray As Boolean
    End Structure

    ' ----------------------------------------------
    ' Obtiene un nonbre de variable
    ' ----------------------------------------------
    Public Function TryMatchVariable(text As String,
                                     context As VarCheckContext,
                                     ByRef result As VariableMatch) As Boolean

        result = Nothing
        If String.IsNullOrWhiteSpace(text) Then Return False

        Dim expr = text.Trim()

        ' Empieza por letra
        If Not Char.IsLetter(expr(0)) Then Return False

        Dim i As Integer = 1
        While i < expr.Length AndAlso Char.IsLetterOrDigit(expr(i))
            i += 1
        End While

        Dim hasDollar As Boolean = False
        If i < expr.Length AndAlso expr(i) = "$"c Then
            hasDollar = True
            i += 1
        End If

        Dim name = expr.Substring(0, i).ToUpperInvariant()

        ' Palabras reservadas
        If Token.IsKeyword(name) Then Return False

        ' ¿Array?
        Dim j = i
        While j < expr.Length AndAlso Char.IsWhiteSpace(expr(j))
            j += 1
        End While
        Dim isArray As Boolean = (j < expr.Length AndAlso expr(j) = "("c)

        ' ---------- Reglas globales ----------
        If hasDollar AndAlso name.Length <> 2 Then Return False
        If isArray AndAlso name.Length <> If(hasDollar, 2, 1) Then Return False

        ' ---------- Contexto ----------
        Select Case context
            Case VarCheckContext.ForControl
                ' Solo una letra numérica, no array
                If name.Length <> 1 Then Return False
                If hasDollar OrElse isArray Then Return False

            Case VarCheckContext.DimDeclaration
                ' DIM solo permite arrays de una letra
                If Not isArray Then Return False
                If name.Length <> If(hasDollar, 2, 1) Then Return False

            Case VarCheckContext.AssignmentLValue
                ' Nada extra: las reglas globales ya filtran strings y arrays

            Case VarCheckContext.GeneralExpression
                ' Nada extra
        End Select


        result = New VariableMatch With {.Name = name,
                                         .IsString = hasDollar,
                                         .IsArray = isArray
                                        }

        Return True
    End Function

    ' ====================================================
    ' Obtiene el tipo semántico de una expresión textual
    ' ====================================================
    Public Function GetExprType(exprText As String, ByRef ctx As SemanticContext) As VarType

        If String.IsNullOrWhiteSpace(exprText) Then
            Return VarType.Unknown
        End If

        exprText = exprText.Trim()

        If ProducesString(exprText) Then
            Return VarType.StringType
        End If

        ' -------------------------------
        ' Literal string
        ' -------------------------------
        If IsStringLiteral(exprText) Then
            Return VarType.StringType
        End If

        ' -------------------------------
        ' Valor numérico literal
        ' -------------------------------
        If IsNumericLiteral(exprText) Then
            Return VarType.Numeric
        End If

        ' ✅ NUEVO: acceso a array
        If IsArrayAccess(exprText) Then
            Return GetArrayAccessType(exprText, ctx)
        End If

        ' -------------------------------
        ' Variable simple (A, B$, etc.)
        ' -------------------------------
        If IsVariableName(exprText) Then
            Return GetVariableType(exprText, ctx)
        End If

        ' -------------------------------
        ' Llamada a función (CHR$, LEN, etc.)
        ' -------------------------------
        If IsFunctionCall(exprText) Then
            Return GetFunctionCallType(exprText)
        End If

        ' -------------------------------
        ' Expresión compuesta
        ' -------------------------------
        Return GetCompositeExprType(exprText, ctx)

    End Function


    Private Function IsZXConditionalStringExpr(expr As String) As Boolean
        ' Heurística ZX:
        ' Si contiene strings y concatenaciones, es STRING
        If expr.Contains(Constantes.C_COMILLAS) Then Return True
        If expr.IndexOf("STR$", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Return False
    End Function

    Private Function ProducesString(expr As String) As Boolean
        expr = expr.Trim()

        ' Empieza por literal string
        If expr.StartsWith(Constantes.C_COMILLAS) Then Return True

        ' Función string explícita
        If expr.StartsWith("STR$", StringComparison.OrdinalIgnoreCase) Then Return True
        If expr.StartsWith("CHR$", StringComparison.OrdinalIgnoreCase) Then Return True

        ' Concatenación explícita de strings
        If expr.Contains(Constantes.C_COMILLAS & " +") OrElse expr.Contains("+ " & Constantes.C_COMILLAS) Then Return True

        Return False
    End Function

    ' ====================================================
    ' Helpers
    ' ====================================================

    Public Function AnalyzeVariableAccess(expr As String,
                                           ByRef baseName As String,
                                           ByRef isArray As Boolean,
                                           ByRef varType As VarType) As Boolean

        baseName = ""
        isArray = False
        varType = VarType.Unknown
        Dim vm As VariableMatch = Nothing

        If Not TryMatchVariable(expr, VarCheckContext.GeneralExpression, vm) Then Return False
        baseName = vm.Name
        isArray = vm.IsArray
        varType = If(vm.IsString, VarType.StringType, VarType.Numeric)

        Return True
    End Function


    Public Function GetBaseVariableName(lvalue As String) As String
        Dim name As String = ""
        Dim dummyArray As Boolean
        Dim dummyType As VarType
        If AnalyzeVariableAccess(lvalue, name, dummyArray, dummyType) Then
            Return name
        End If
        Return ""
    End Function

    Private Function IsArrayAccess(expr As String) As Boolean
        Dim name As String = ""
        Dim isArray As Boolean
        Dim t As VarType
        Return AnalyzeVariableAccess(expr, name, isArray, t) AndAlso isArray
    End Function

    Private Function GetArrayAccessType(expr As String, ByRef ctx As SemanticContext) As VarType
        ' Extraer nombre base del array
        Dim name As String = ""
        Dim isArray As Boolean
        Dim t As VarType

        If Not AnalyzeVariableAccess(expr, name, isArray, t) Then
            Return VarType.Unknown
        End If
        Return t

    End Function

    Private Function IsVariableName(text As String) As Boolean
        Dim baseName As String = ""
        Dim isArray As Boolean
        Dim t As VarType

        Return AnalyzeVariableAccess(text, baseName, isArray, t) AndAlso Not isArray
    End Function

    Private Function IsStringLiteral(text As String) As Boolean
        Return text.Length >= 2 AndAlso
               text.StartsWith(Constantes.C_COMILLAS) AndAlso
               text.EndsWith(Constantes.C_COMILLAS)
    End Function

    Private Function IsNumericLiteral(text As String) As Boolean
        Dim n As Double
        Return Double.TryParse(text,
                               Globalization.NumberStyles.Any,
                               Globalization.CultureInfo.InvariantCulture,
                               n)
    End Function

    Private Function GetVariableType(varName As String, ByRef ctx As SemanticContext) As VarType

        ' ✅ REGLA BASIC: el sufijo $ manda siempre
        If varName.EndsWith("$"c) Then
            Return VarType.StringType
        End If

        ' Variable conocida en el contexto
        ctx.Variables.TryGetValue(varName, Nothing)

        If ctx.Variables.ContainsKey(varName) Then
            Dim info = ctx.Variables(varName)
            Return If(info.IsString, VarType.StringType, VarType.Numeric)
        End If

        ' Inferencia implícita ZX BASIC
        If varName.EndsWith("$"c) Then
            Return VarType.StringType
        End If

        Return VarType.Numeric

    End Function

    Private Function IsFunctionCall(exprText As String) As Boolean
        Return exprText.Contains("("c) AndAlso exprText.EndsWith(")")
    End Function

    Private Function GetFunctionCallType(exprText As String) As VarType

        Dim fname = exprText.Substring(0, exprText.IndexOf("("c)).ToUpperInvariant()

        Select Case fname

            ' Funciones string
            Case "CHR$", "STR$", "INKEY$", "SCREEN$"
                Return VarType.StringType

            ' Funciones numéricas
            Case "LEN", "RND", "PI", "VAL", "CODE", "BIN", "ATTR", "POINT"
                Return VarType.Numeric

            Case Else
                Return VarType.Numeric   ' ZX BASIC: por defecto numérico

        End Select

    End Function

    Private Function GetCompositeExprType(exprText As String,
                                          ByRef ctx As SemanticContext) As VarType

        ' Regla ZX BASIC:
        ' Si en una expresión aparece ALGO string con el operador '+'
        ' → string
        ' si no → numérico

        If exprText.Contains("+") Then
            Dim parts = exprText.Split("+"c)

            For Each p In parts
                Dim t = GetExprType(p.Trim(), ctx)
                If t = VarType.StringType Then
                    Return VarType.StringType
                End If
            Next
        End If

        ' Comparaciones → numérico (boolean ZX)
        If ContainsComparison(exprText) Then
            Return VarType.Numeric
        End If

        Return VarType.Numeric

    End Function

    Private Function ContainsComparison(expr As String) As Boolean
        Return expr.Contains(">") OrElse
               expr.Contains("<") OrElse
               expr.Contains("=")
    End Function

End Module
