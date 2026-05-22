' ============================================================
' NODO DATA (lógico, no Z80 aún)
' ============================================================
Public Enum TipoForNext
    tpFor
    tpNext
End Enum

Public Structure ForNextInfo

    Public Linea As Integer
    Public VarName As String
    Public Tipo As TipoForNext
    Public Contador As Integer

End Structure
