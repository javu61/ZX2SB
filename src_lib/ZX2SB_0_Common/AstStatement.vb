' ============================================================
' ASTSTATEMENT - Definición estructural (Common)
' ============================================================

Public Structure AstStatement

    Public Kind As AstStmtKind

    ' Nombre del comando (LET, PRINT, BEEP, GRAPHIC…)
    Public Name As String

    ' Argumentos de la sentencia
    Public Args() As AstExpr

    ' Campo auxiliar entero (línea, contador, flags)
    Public ExtraInt As Integer

    ' Campo auxiliar texto  (REM, DATA, etc.)
    Public ExtraStr As String

End Structure