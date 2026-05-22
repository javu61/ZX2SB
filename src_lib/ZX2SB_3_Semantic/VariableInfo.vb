' ============================================================
' INFO DE VARIABLE
' ============================================================
Public Structure VariableInfo

    Public Name As String          ' Nombre de la variable (A, B$, C, etc.)
    Public IsString As Boolean     ' True si es string (acabada en $)
    Public NrDim As Integer        ' Indica si la variable es un arreglo cuantas dimensiones tiene (0=variable simple)
    Public WasAssigned As Boolean  ' Indica que se ha asignado un valor a la variable
    Public WasUsed As Boolean      ' Indica que se ha usado la variable

End Structure