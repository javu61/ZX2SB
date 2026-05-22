' =====================================================
' TokenID - Identificadores canónicos ZX2SB 
' =====================================================
' Código de 4 dígitos en formato [F][T][A][NN]

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

' A = Aridad. Nro de argumentos que necesita, se usará en funciones y en pseudo funciones de uso interno como TCO_INIT 

' NN = índice correlativo dentro del grupo
' =====================================================

Public Enum TokenFamily As Integer
    TF_GENERAL = 1 * 10000
    TF_BLOQUES = 2 * 10000
    TF_GENERAFN = 3 * 10000
    TF_NOSOPORTADO = 4 * 10000
    TF_ESPECIALES = 9 * 10000
End Enum

Public Enum TokenTipo As Integer
    TT_SENTENCIA = 1 * 1000
    TT_FUNCION = 2 * 1000
    TT_PROCEDIMIENTO = 3 * 1000
    TT_OPERADOR = 4 * 1000
    TT_SIMBOLO = 5 * 1000
    TT_DIRECTIVA = 6 * 1000      ' Directivas de formato
    TT_AGRUPACIONES = 7 * 1000   ' No son palabras reservadas, pero agrupan partes de la sentencia
    TT_ESPECIALES = 9 * 1000
End Enum

Public Enum TokenAridad As Integer
    TA_NOA = 9 * 100    'Sin aridad
    TA_NR0 = 0 * 100    '0 Función numérica
    TA_NR1 = 1 * 100    '1 Función numérica
    TA_NR2 = 2 * 100    '2 Función numérica
    TA_ST0 = 3 * 100    '0 Función de Cadena
    TA_ST1 = 4 * 100    '1 Función de Cadena
    TA_ST2 = 5 * 100    '2 Función de Cadena
End Enum


Public Enum TokenID As Integer

    ' =====================================================
    ' CONTROL
    ' =====================================================
    TCO_EOF = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + TokenAridad.TA_NOA + 0
    TCO_EOL = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + TokenAridad.TA_NOA + 1
    TCO_LINE = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + TokenAridad.TA_NOA + 2
    TCO_UNKNOWN = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + TokenAridad.TA_NOA + 3
    TCO_NONE = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + TokenAridad.TA_NOA + 4
    TCO_INIT = TokenFamily.TF_ESPECIALES + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NR0 + 5     'Función especial de inicio

    ' =====================================================
    ' IDENTIFICADORES Y LITERALES
    ' =====================================================
    TES_IDENT = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + TokenAridad.TA_NOA + 0   'Un identificador
    TES_NUMBER = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + TokenAridad.TA_NOA + 1  'Un número
    TES_STRING = TokenFamily.TF_GENERAL + TokenTipo.TT_AGRUPACIONES + TokenAridad.TA_NOA + 2  'Una cadena

    ' =====================================================
    ' OPERADORES Y SIMBOLOS
    ' =====================================================
    TOP_PLUS = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 0        ' +
    TOP_MINUS = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 1       ' -
    TOP_MUL = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 2         ' *
    TOP_DIV = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 3         ' /
    TOP_POW = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 4         ' ^

    TOP_EQ = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 5          ' =
    TOP_NE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 6          ' <>
    TOP_LT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 7          ' <
    TOP_GT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 8          ' >
    TOP_LE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 9          ' <=
    TOP_GE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 10         ' >=

    TK_AND = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 11         ' Logico AND
    TK_NOT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 12         ' Logico OR
    TK_OR = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + TokenAridad.TA_NOA + 13          ' Logico NOT

    TSP_PAR_ABIERTO = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 14 ' (
    TSP_PAR_CERRADO = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 15 ' )
    TSP_COMA = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 16        ' ,
    TSP_PUNTOYCOMA = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 17  ' ;
    TSP_DOSPUNTOS = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 18   ' :
    TK_CANAL = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 19        ' #

    TES_INI_PAR = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 20     ' Paréntesis ficticio para el normalizador [
    TES_FIN_PAR = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + TokenAridad.TA_NOA + 21     ' Paréntesis ficticio para el normalizador ]

    ' =====================================================
    ' SENTENCIAS ZX BASIC
    ' =====================================================
    TK_CLEAR = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 20
    TK_CLS = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 21
    TK_CONTINUE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 22
    TK_DIM = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 23
    TK_ELSE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 24
    TK_FN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 25
    TK_FOR = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 26
    TK_GOSUB = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 28
    TK_GOTO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 29
    TK_IF = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 30
    TK_INPUT = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 31
    TK_LET = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 32
    TK_NEXT = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 33
    TK_PAUSE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 34
    TK_PRINT = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 35
    TK_REM = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 36
    TK_RETURN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 37
    TK_STEP = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 39
    TK_STOP = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 40
    TK_THEN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 41
    TK_TO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 42
    TK_VERIFY = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 43
    TK_END = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 44
    TK_RANDOMIZE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 45

    TK_READ = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 50
    TK_RESTORE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 51
    TK_DATA = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 52

    ' Insrucciones no soportadas
    TK_SCROLL = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 10
    TK_NEW = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 11
    TK_RUN = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 38
    TK_COPY = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 12
    TK_LIST = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 13
    TK_LOAD = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 14
    TK_MERGE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 15
    TK_SAVE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 16
    TK_OPEN = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 17
    TK_CLOSE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 18
    TK_MOVE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 19
    TK_ERASE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 20
    TK_CAT = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 21
    TK_FORMAT = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 22

    TK_FAST = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 23
    TK_SLOW = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + TokenAridad.TA_NOA + 24

    ' =====================================================
    ' ATRIBUTOS PRINT
    ' =====================================================
    TK_TAB = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 0
    TK_AT = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 1
    TK_BRIGHT = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 2
    TK_FLASH = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 3
    TK_INK = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 4
    TK_INVERSE = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 5
    TK_OVER = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 6
    TK_PAPER = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + TokenAridad.TA_NOA + 7

    ' =====================================================
    ' FUNCIONES ZX BASIC
    ' =====================================================
    '0 argumentos
    TK_PI = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR0 + 0
    TK_RND = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR0 + 1

    '2 argumentos
    TK_ATTR = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR2 + 20
    TK_SCREEN_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR2 + 21

    '1 argumento
    TK_CHR_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_ST1 + 31
    TK_INKEY_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_ST1 + 33
    TK_STR_S = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + TokenAridad.TA_ST1 + 51
    TK_VAL_S = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + TokenAridad.TA_ST1 + 52

    TK_ABS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 30
    TK_CODE = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 32
    TK_INT = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 34
    TK_LEN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 35
    TK_SGN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 36
    TK_VAL = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 37
    TK_COS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 38
    TK_SIN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 39
    TK_TAN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 40
    TK_ASN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 41
    TK_ACS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 42
    TK_ATN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 43
    TK_LN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 44
    TK_EXP = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 45
    TK_SQR = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 46

    TK_BIN = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 50

    TK_IN = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 62
    TK_PEEK = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 27
    TK_USR = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 64

    ' ESPECIALES PARA SEPARAR EN DOS MODOS UN TIPO - 1 argumento
    TK_RANDOMIZE_USR = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 25
    TK_CLEAR_RAM = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + TokenAridad.TA_NR1 + 26

    ' =====================================================
    ' PROCEDIMIENTOS DE MANEJO DE PERIFERICOS
    ' =====================================================
    TK_BORDER = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 0
    TK_CIRCLE = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 1
    TK_DRAW = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 2
    TK_PLOT = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 3
    TK_POINT = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + TokenAridad.TA_NR2 + 8

    TK_BEEP = TokenFamily.TF_GENERAFN + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 4

    TK_OUT = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 5
    TK_POKE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_PROCEDIMIENTO + TokenAridad.TA_NOA + 6

End Enum