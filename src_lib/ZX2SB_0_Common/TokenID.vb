' =====================================================
' TokenID - Identificadores canónicos ZX2SB 
' =====================================================
' Código de 4 dígitos en formato [F][T][NN]

' F = Familia. Indica como se va a generar el código
'  GENERALES: Sentencia simple
'  BLOQUES: Necesitan un tratamiento especial
'  GENERAFN: Siempre generan una función FN de tipo normal
'  NOSOPORTADO: Siempre generan una función FN como no soportada
'  ESPECIALES: No generan código

' T = Tipo. Tipo de token
'  Sentencias: Se generan con ese nombre
'  Funciones: Se generan con ese nombre, pueden usar parámetros, retornan un valor
'  Procedimiento: Se generan con ese nombre, pueden usar parámetros, no retornan un valor
'  Operador o símbolo: Símbolos 
'  Directiva: Para uso en PRINT
'  Agrupaciones: Agrupan contenido para su consumo (variables, cadenas, etc.)
'  Especiales: Marcadores que no generan código

' NN = índice correlativo dentro del grupo
' =====================================================

Public Enum TokenFamily As Integer
    TF_GENERAL = 1000
    TF_BLOQUES = 2000
    TF_GENERAFN = 3000
    TF_NOSOPORTADO = 4000
    TF_ESPECIALES = 9000
End Enum

Public Enum TokenTipo As Integer
    TT_SENTENCIA = 100
    TT_FUNCION = 200
    TT_PROCEDIMIENTO = 300
    TT_OPERADOR = 400
    TT_SIMBOLO = 500
    TT_DIRECTIVA = 600      ' Directivas de formato
    TT_AGRUPACIONES = 700   ' No son palabras reservadas, pero agrupan partes de la sentencia
    TT_ESPECIALES = 900
End Enum


Public Enum TokenID As Integer

    ' =====================================================
    ' CONTROL
    ' =====================================================
    TCO_EOF = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 0
    TCO_EOL = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 1
    TCO_LINE = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 2
    TCO_UNKNOWN = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 3
    TCO_NONE = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 4
    TCO_INIT = TokenFamily.TF_ESPECIALES + TokenTipo.TT_PROCEDIMIENTO + 5     'Función especial de inicio

    ' =====================================================
    ' IDENTIFICADORES Y LITERALES
    ' =====================================================
    TES_IDENT = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + 0   'Un identificador
    TES_NUMBER = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + 1  'Un número
    TES_STRING = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + 2  'Una cadena
    'TES_GREXPR = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + 3  'Un grupo entre paréntesis

    ' =====================================================
    ' OPERADORES Y SIMBOLOS
    ' =====================================================
    TOP_PLUS = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 0        ' +
    TOP_MINUS = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 1       ' -
    TOP_MUL = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 2         ' *
    TOP_DIV = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 3         ' /
    TOP_POW = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 4         ' ^

    TOP_EQ = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 5          ' =
    TOP_NE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 6          ' <>
    TOP_LT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 7          ' <
    TOP_GT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 8          ' >
    TOP_LE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 9          ' <=
    TOP_GE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 10         ' >=

    TK_AND = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 11         'Logico AND
    TK_NOT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 12         'Logico OR
    TK_OR = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 13          'Logico NOT

    TSP_PAR_ABIERTO = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 14 ' (
    TSP_PAR_CERRADO = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 15 ' )
    TSP_COMA = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 16        ' ,
    TSP_PUNTOYCOMA = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 17  ' ;
    TSP_DOSPUNTOS = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 18   ' :

    ' =====================================================
    ' SENTENCIAS ZX BASIC
    ' =====================================================

    TK_CLEAR = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 26
    TK_CLS = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 28
    TK_CONTINUE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 30
    TK_DATA = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 31
    TK_DIM = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 32
    TK_ELSE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 33
    TK_FN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 35
    TK_FOR = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 36
    TK_GOSUB = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 38
    TK_GOTO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 39
    TK_IF = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 40
    TK_INPUT = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 41
    TK_LET = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 42
    TK_NEXT = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 46
    TK_PAUSE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 49
    TK_PRINT = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 50
    TK_READ = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 51
    TK_REM = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 52
    TK_RESTORE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 53
    TK_RETURN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 54
    TK_RUN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 55
    TK_STEP = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 59
    TK_STOP = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 60
    TK_THEN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 61
    TK_TO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 62
    TK_VERIFY = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 63
    TK_END = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 64
    TK_RANDOMIZE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 66

    TK_COPY = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 29
    TK_FAST = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 34
    TK_LIST = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 43
    TK_LOAD = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 44
    TK_MERGE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 45
    TK_NEW = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 47
    TK_SAVE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 56
    TK_SCROLL = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 57
    TK_SLOW = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 58

    ' =====================================================
    ' ATRIBUTOS PRINT
    ' =====================================================
    TK_TAB = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 0    'Es la única que ahora puede ir dentro del print

    TK_AT = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 1
    TK_BRIGHT = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 2
    TK_FLASH = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 3
    TK_INK = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 4
    TK_INVERSE = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 5
    TK_OVER = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 6
    TK_PAPER = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 7

    ' =====================================================
    ' FUNCIONES ZX BASIC
    ' =====================================================
    TK_ABS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 0
    TK_ATTR = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 1
    TK_CHR_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 2
    TK_CODE = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 3
    TK_INKEY_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 4
    TK_INT = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 5
    TK_LEN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 6
    TK_PI = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 7
    TK_POINT = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 8
    TK_RND = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 9
    TK_SCREEN_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 10
    TK_SGN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 11
    TK_VAL = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 12
    TK_COS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 13
    TK_SIN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 14
    TK_TAN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 15
    TK_ASN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 16
    TK_ACS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 17
    TK_ATN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 18
    TK_LN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 19
    TK_EXP = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 20
    TK_SQR = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 21

    TK_BIN = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 22
    TK_STR_S = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 23
    TK_VAL_S = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 24
    TK_RANDOMIZE_USR = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 25 ' ESPECIALES PARA SEPARAR EN DOS ALGUN TIPO
    TK_CLEAR_RAM = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 26

    TK_IN = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + 27
    TK_PEEK = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + 28
    TK_USR = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + 29

    ' =====================================================
    ' PROCEDIMIENTOS ZX BASIC
    ' =====================================================
    TK_BORDER = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 0
    TK_CIRCLE = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 1
    TK_DRAW = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 2
    TK_PLOT = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 3

    TK_BEEP = TokenFamily.TF_GENERAFN + TokenTipo.TT_PROCEDIMIENTO + 4

    TK_OUT = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_PROCEDIMIENTO + 5
    TK_POKE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_PROCEDIMIENTO + 6

End Enum