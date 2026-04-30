' ============================================================
' SEMANTIC CONTEXT
' Estado global del análisis semántico
' ============================================================
Public Structure SemanticContext

	' -----------------------------
	' Variables detectadas
	' -----------------------------
	Public Variables As Dictionary(Of String, VariableInfo)

	' -----------------------------
	' Funciones auxiliares usadas
	' -----------------------------
	Public FuncionesAuxiliares As HashSet(Of String)

	' -----------------------------
	' DATA / READ
	' -----------------------------
	Public DataNodes As List(Of DataNode)

End Structure