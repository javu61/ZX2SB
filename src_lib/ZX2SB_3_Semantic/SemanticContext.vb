' ============================================================
' SEMANTIC CONTEXT
' Estado global del análisis semántico
' ============================================================
Public Structure SemanticContext

	' -----------------------------
	' Uso general del lenguaje
	' -----------------------------
	Public UsaPrint As Boolean
	Public UsaData As Boolean
	Public UsaRead As Boolean

	' -----------------------------
	' PRINT: control de cursor
	' -----------------------------
	Public UsaAT As Boolean
	Public UsaTAB As Boolean
	Public UsaComaEnPrint As Boolean

	' -----------------------------
	' PRINT: atributos ZX
	' -----------------------------
	Public UsaINK As Boolean
	Public UsaPAPER As Boolean
	Public UsaBRIGHT As Boolean
	Public UsaFLASH As Boolean
	Public UsaOVER As Boolean
	Public UsaINVERSE As Boolean

	' -----------------------------
	' Inicialización del runtime
	' (abstracto: lo interpreta el Generator)
	' -----------------------------
	Public RequiereInicializacionRuntime As Boolean

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