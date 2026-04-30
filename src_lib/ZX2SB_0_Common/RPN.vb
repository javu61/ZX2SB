Public Module RPN

    Public Enum RPNKind
        VAR          ' Variable o símbolo (A, B$, ARR)
        CTE          ' Constante literal (5, "HELLO")
        OPE_UNARY    ' Operador unario (UNARY_MINUS, NOT)
        OPE_BINARY   ' Operador binario (+, -, *, AND, =, etc.)
        CALLFUN      ' Llamada a función o acceso a array
    End Enum

    Public Structure RPN_Node
        Public Kind As RPNKind
        Public TokenID As TokenID

        ' Texto base:
        '  - variable: "A", "B$", "ARR"
        '  - constante: "5", """HELLO"""
        '  - operador: "+", "AND", "UNARY_MINUS"
        Public Value As String

        ' Número de operandos que consume:
        '  - 0 para VAR / CTE
        '  - 1 para OPE_UNARY
        '  - 2 para OPE_BINARY
        '  - N para CALL
        Public Arity As Integer
    End Structure


    Public Structure IR_Let
        Public Name As String
        Public Indices As List(Of List(Of RPN.RPN_Node))
        Public Expr As List(Of RPN.RPN_Node)
    End Structure

End Module
