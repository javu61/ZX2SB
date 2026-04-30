Public Module RPN

    Public Enum RPNKind
        VAR   ' Referencia a variable (A_B, X$, etc.)
        CTE   ' Constante (5, "HELLO")
        OPE   ' Operador (+, -, *, AND, etc.)
    End Enum

    Public Structure RPN_Node
        Public Kind As RPNKind
        Public Value As String  ' "A_B", "5", "+", "AND", etc.
    End Structure

End Module
