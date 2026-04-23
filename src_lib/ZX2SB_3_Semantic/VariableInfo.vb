' ============================================================
' INFO DE VARIABLE
' ============================================================
Public Structure VariableInfo

    ' Nombre de la variable (A, B$, C, etc.)
    Public Name As String

    ' True si es string (acabada en $)
    Public IsString As Boolean

    ' Nivel 1: seguimiento básico
    Public WasAssigned As Boolean
    Public WasUsed As Boolean

End Structure