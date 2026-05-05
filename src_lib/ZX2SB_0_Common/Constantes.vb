
' ===========================================
'  Constantes globales del proyecto ZB2SB
' ===========================================

Public Module Constantes
    Public Const MDir As String = "ZX2SB"
    Public Const MLex As String = "JLexer"
    Public Const MNor As String = "JNormalizar"
    Public Const MPar As String = "JParser"
    Public Const MSem As String = "JSemantic"
    Public Const MGSB As String = "JGeneratorSB"
    Public Const MRen As String = "JRenum"

    Public Const C_COMILLAS As Char = ChrW(34)                        ' "
    Public Const C_ESPACIO As Char = " "c                             ' Espacio en blanco
    Public Const C_PUNTO As Char = "."c                               ' Punto
    Public Const C_COMA As Char = ","c                                ' Coma
    Public Const C_PUNTOYCOMA As Char = ";"c                          ' Punto y Coma
    Public Const C_DOSPUNTOS As Char = ":"c                           ' Dos Puntos
    Public Const C_PAR_APE As Char = "("c                             ' (
    Public Const C_PAR_CIE As Char = ")"c                             ' )

    Public Const S_VACIA As String = C_COMILLAS & C_COMILLAS          ' Entre comillas vacía
    Public Const S_ESPACIO As String = C_COMILLAS & " " & C_COMILLAS  ' Entre comillas un espacio
    Public Const S_CERO As String = C_COMILLAS & "0" & C_COMILLAS     ' Entre Comillas un cero
    Public Const S_MENOS As String = C_COMILLAS & "-" & C_COMILLAS    ' Entre Comillas un menos
    Public Const S_MAS As String = C_COMILLAS & "+" & C_COMILLAS      ' Entre Comillas un mas

    Public Const GEN_TOKENS As String = ".csv"                        ' Para exportación
    Public Const CADENAVACIA As String = ""                           ' Cadena vacía

    Public Const VER_PROG As String = "0.0" & ChrW(&H3B1)            ' Versión del programa Alfa
    'Public Const VER_PROG As String ="0.0" & ChrW(&H3B2)            ' Versión del programa Beta
    Public Const MarcaAST As String = ChrW(&H2192)                   ' Marca para imprimir el AST  
    Public Const MarcaWarning As String = ChrW(&H21D2)               ' Marca para imprimir los Warnings 
    Public Const MarcaSRC = "-- SRC: "                               ' MArca de la línea original del fuente
    Public Const Sep_Comentario = ";"                                ' Separador inicial del comentario
    Public Const MarcaComentario = " " & Sep_Comentario & " -- "     ' Marcas para los comentarios
    Public Const MarcaGen = ChrW(&H21D2)                             ' Marca para el programa generado ⇒

    Public Const LOG_EXTENSION As String = ".log"

    Public Const LEX_EXTENSION As String = ".tok"
    Public Const NOR_EXTENSION As String = ".tkz"
    Public Const LEX_NOMBRE As String = "TOK"
    Public Const LEX_VERSION As String = "1.0"

    Public Const TKZ_NOMBRE As String = "TKZ"
    Public Const TKZ_VERSION As String = "1.0"

    Public Const PAR_EXTENSION As String = ".irp"
    Public Const PAR_NOMBRE As String = "IRP"
    Public Const PAR_VERSION As String = "1.0"

    Public Const SEM_EXTENSION As String = ".irs"
    Public Const SEM_NOMBRE As String = "IRS"
    Public Const SEM_VERSION As String = "1.0"

    Public Const VAR_EXTENSION As String = ".var"
    Public Const VAR_NOMBRE As String = "VAR"
    Public Const VAR_VERSION As String = "1.0"

    Public Const DTA_EXTENSION As String = ".dat"
    Public Const DATA_NOMBRE As String = "DATA"
    Public Const DATA_VERSION As String = "1.0"

    Public Const GQL_EXTENSION As String = ".sbg"
    Public Const GQL_SEPARADOR As String = "::"
    Public Const GQL_FACTOR As Integer = 100
    Public Const GQL_INIT As String = "ZX2SB_INIT"

    Public Const REN_EXTENSION As String = "_sb"
    Public Const LIN_EXTENSION As String = ".lin"

    ' Opciones que se pueden manejar en la entrada de los módulos
    Public Const opDirector = "-dzxb"
    Public Const opSilencioso = "-s"
    Public Const opVerbose = "-v"
    Public Const opBath = "-b"
    Public Const opZX = "-zx"
    Public Const opNoWarnings = "-nw"
    Public Const opContinuarSError = "-ce"
    Public Const opSinComentarios = "-nc"
    Public Const opDebug = "-d"
    Public Const opFuncion = "-f"
    Public Const opFuncion_Err = 0
    Public Const opFuncion_Msg = 1
    Public Const opFuncion_Ign = 2
    Public Const opBase = "-rb"
    Public Const opPaso = "-rp"
    Public Const opIND = "-i"

End Module
