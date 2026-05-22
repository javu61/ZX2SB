' ============================================================
' NODO DATA (lógico, no Z80 aún)
' ============================================================

Public Enum DataKind
    dtNumber
    dtString
    dtVariable
    dtRPN
End Enum

Public Structure DataNode
    Public dnLine As Integer              ' Línea ZX original donde aparece el DATA
    Public dnKind As DataKind             ' Tipo de entrada
    Public dnValue As Object              ' Valor del DATA 
End Structure