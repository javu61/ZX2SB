Public Module Descomponer
    ' --------------------------------------------------------------
    ' Separar el LET en izquierda y derecha LET var = Expresion
    ' --------------------------------------------------------------
    Public Function dLET(rpn As List(Of RPN_Node),
                     ByRef lhs As List(Of RPN_Node),
                     ByRef rhs As List(Of RPN_Node)) As Boolean

        lhs = Nothing
        rhs = Nothing

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            Return False
        End If

        Dim idxAssign = rpn.FindIndex(Function(n) n.Kind = RPNKind.ASSIGN)

        If idxAssign < 0 Then
            Return False
        End If

        lhs = rpn.GetRange(0, idxAssign)
        rhs = rpn.GetRange(idxAssign + 1, rpn.Count - idxAssign - 1)

        Return True

    End Function

    ' --------------------------------------------------------------
    ' Separa FOR variable = expresion TO expresion STEP expresion 
    ' --------------------------------------------------------------
    Public Function dFOR(rpn As List(Of RPN_Node),
                         ByRef varName As String,
                         ByRef initExpr As List(Of RPN_Node),
                         ByRef limitExpr As List(Of RPN_Node),
                         ByRef stepExpr As List(Of RPN_Node)) As Boolean

        ' Reset salida
        varName = ""
        initExpr = Nothing
        limitExpr = Nothing
        stepExpr = Nothing

        ' -----------------------------
        ' Validación básica
        ' -----------------------------
        If rpn Is Nothing OrElse rpn.Count = 0 Then
            Return False
        End If

        ' -----------------------------
        ' 1. Variable de control
        ' -----------------------------
        If rpn(0).Kind <> RPNKind.VAR Then
            Return False
        End If

        varName = rpn(0).Value

        ' -----------------------------
        ' 2. Buscar asignación A(=)
        ' -----------------------------
        Dim idxAssign As Integer = rpn.FindIndex(Function(n) n.Kind = RPNKind.ASSIGN)

        If idxAssign <= 0 Then
            Return False
        End If

        ' -----------------------------
        ' 3. INIT → hasta T()
        ' -----------------------------
        initExpr = New List(Of RPN_Node)

        Dim i As Integer = idxAssign + 1

        While i < rpn.Count AndAlso rpn(i).Kind <> RPNKind.FOR_TO
            initExpr.Add(rpn(i))
            i += 1
        End While

        ' -----------------------------
        ' 4. TO
        ' -----------------------------
        If i < rpn.Count AndAlso rpn(i).Kind = RPNKind.FOR_TO Then
            i += 1 ' saltar T()

            Dim startLimit As Integer = i

            While i < rpn.Count AndAlso rpn(i).Kind <> RPNKind.FOR_STEP
                i += 1
            End While

            limitExpr = rpn.GetRange(startLimit, i - startLimit)
        End If

        ' -----------------------------
        ' 5. STEP (opcional)
        ' -----------------------------
        If i < rpn.Count AndAlso rpn(i).Kind = RPNKind.FOR_STEP Then
            i += 1 ' saltar S()

            Dim startStep As Integer = i
            stepExpr = rpn.GetRange(startStep, rpn.Count - startStep)
        End If

        ' -----------------------------
        ' Resultado
        ' -----------------------------
        Return True

    End Function

    ' --------------------------------------------------------------
    ' Separa condiciones en una sentencia IF condicion 
    ' --------------------------------------------------------------
    Public Function dIF(rpn As List(Of RPN_Node)) As Boolean

        ' IF solo tiene expresión
        Return True

    End Function
End Module
