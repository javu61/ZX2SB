' ============================================================
' NODO DATA (lógico, no Z80 aún)
' ============================================================
Public Structure DataNode

    ' Línea ZX original donde aparece el DATA
    Public Line As Integer

    ' Valor del DATA (Double o String)
    Public Value As Object

End Structure