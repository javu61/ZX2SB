' ============================================================
' AST - Expresiones (Common)
' Definición estructural, sin semántica
' ============================================================

' ------------------------------------------------------------
' Tipos de expresión
' ------------------------------------------------------------
Public Enum AstExprKind
    LiteralExpr
    VariableExpr
    UnaryExpr
    BinaryExpr
    FunctionCallExpr
End Enum


' ------------------------------------------------------------
' Expresión genérica
' ------------------------------------------------------------
Public Structure AstExpr

    ' Tipo de expresión
    Public Kind As AstExprKind

    ' Datos comunes
    Public Text As String          ' Literal, nombre de variable, nombre de función, operador
    Public IsString As Boolean     ' Solo para literales

    ' Subexpresiones
    Public Left As Integer         ' Índice a otra expresión (o -1)
    Public Right As Integer        ' Índice a otra expresión (o -1)

    ' Argumentos de función
    Public Args() As Integer       ' Índices a expresiones

End Structure