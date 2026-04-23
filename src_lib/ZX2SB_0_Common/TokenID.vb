' =====================================================
' TokenID - Identificadores canónicos ZX2SB 
' =====================================================
' Código de 4 dígitos en formato [F][T][NN]

' F = Familia
'  1 = GENERAL
'  2 = BLOQUES
'  3 = GENERAFN
'  4 = NOSOPORTADO
'  9 = ESPECIALES

' T = Tipo
'  1 = Sentencia (KW)
'  2 = Función    (FN)
'  3 = Procedimiento (PROC)
'  4 = Operador / símbolo (OP / SYM)

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
    TT_ESPECIALES = 900
End Enum


Public Enum TokenID As Integer

    ' =====================================================
    ' CONTROL
    ' =====================================================
    TE_EOF = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 0
    TE_EOL = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 1
    TE_LINE = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 2
    TE_UNKNOWN = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 3
    TE_NONE = TokenFamily.TF_ESPECIALES + TokenTipo.TT_ESPECIALES + 23

    ' =====================================================
    ' IDENTIFICADORES Y LITERALES
    ' =====================================================
    TE_IDENT = TokenFamily.TF_GENERAL + 4
    TE_NUMBER = TokenFamily.TF_GENERAL + 5
    TE_STRING = TokenFamily.TF_GENERAL + 6

    ' =====================================================
    ' OPERADORES Y SIMBOLOS
    ' =====================================================
    TOP_PLUS = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 7     ' +
    TOP_MINUS = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 8    ' -
    TOP_MUL = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 9      ' *
    TOP_DIV = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 10     ' /
    TOP_POW = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 11     ' ^

    TOP_EQ = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 12      ' =
    TOP_NE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 13      ' <>
    TOP_LT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 14      ' <
    TOP_GT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 15      ' >
    TOP_LE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 16      ' <=
    TOP_GE = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 17      ' >=

    TK_AND = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 25     'Logico AND
    TK_NOT = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 48     'Logico OR
    TK_OR = TokenFamily.TF_GENERAL + TokenTipo.TT_OPERADOR + 24      'Logico NOT

    TS_PAR_ABIERTO = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 18 ' (
    TS_PAR_CERRADO = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 19 ' )
    TS_COMMA = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 20       ' ,
    TS_PUNTOYCOMA = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 21  ' ;
    TS_DOSPUNTOS = TokenFamily.TF_GENERAL + TokenTipo.TT_SIMBOLO + 22   ' :

    ' =====================================================
    ' SENTENCIAS ZX BASIC
    ' =====================================================

    TK_CLEAR = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 26
    TK_CLEAR_RAM = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 27
    TK_CLS = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 28
    TK_COPY = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 29
    TK_CONTINUE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 30
    TK_DATA = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 31
    TK_DIM = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 32
    TK_ELSE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 33
    TK_FAST = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 34
    TK_FN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 35
    TK_FOR = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 36
    TK_GO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 37
    TK_GOSUB = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 38
    TK_GOTO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 39
    TK_IF = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 40
    TK_INPUT = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 41
    TK_LET = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 42
    TK_LIST = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 43
    TK_LOAD = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 44
    TK_MERGE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 45
    TK_NEXT = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 46
    TK_NEW = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 47
    TK_PAUSE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 49
    TK_PRINT = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 50
    TK_READ = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 51
    TK_REM = TokenFamily.TF_BLOQUES + TokenTipo.TT_SENTENCIA + 52
    TK_RESTORE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 53
    TK_RETURN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 54
    TK_RUN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 55
    TK_SAVE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 56
    TK_SCROLL = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 57
    TK_SLOW = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_SENTENCIA + 58
    TK_STEP = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 59
    TK_STOP = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 60
    TK_THEN = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 61
    TK_TO = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 62
    TK_VERIFY = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 63
    TK_END = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 64
    TK_SUB = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 65
    TK_RANDOMIZE = TokenFamily.TF_GENERAL + TokenTipo.TT_SENTENCIA + 66
    TK_RANDOMIZE_USR = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 67 ' ESPECIALES PARA SEPARAR EN DOS ALGUN TIPO

    ' =====================================================
    ' ATRIBUTOS PRINT
    ' =====================================================
    TK_AT = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 68
    TK_TAB = TokenFamily.TF_GENERAL + TokenTipo.TT_DIRECTIVA + 69

    ' =====================================================
    ' FUNCIONES ZX BASIC
    ' =====================================================
    TK_ABS = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 70
    TK_ATTR = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 71
    TK_BIN = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 72
    TK_BRIGHT = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 73
    TK_CHR_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 74
    TK_CODE = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 75
    TK_FLASH = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 76
    TK_IN = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + 77
    TK_INK = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 78
    TK_INKEY_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 79
    TK_INVERSE = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 80
    TK_LEN = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 81
    TK_OVER = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 82
    TK_PAPER = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 83
    TK_PEEK = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + 84
    TK_PI = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 85
    TK_POINT = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 86
    TK_RND = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 87
    TK_SCREEN_S = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 88
    TK_STR_S = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 89
    TK_USR = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_FUNCION + 90
    TK_VAL = TokenFamily.TF_GENERAL + TokenTipo.TT_FUNCION + 91
    TK_VAL_S = TokenFamily.TF_GENERAFN + TokenTipo.TT_FUNCION + 92

    ' =====================================================
    ' PROCEDIMIENTOS ZX BASIC
    ' =====================================================
    TK_BORDER = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 93
    TK_BEEP = TokenFamily.TF_GENERAFN + TokenTipo.TT_PROCEDIMIENTO + 94
    TK_CIRCLE = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 95
    TK_DRAW = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 96
    TK_OUT = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_PROCEDIMIENTO + 97
    TK_PLOT = TokenFamily.TF_GENERAL + TokenTipo.TT_PROCEDIMIENTO + 98
    TK_POKE = TokenFamily.TF_NOSOPORTADO + TokenTipo.TT_PROCEDIMIENTO + 99

End Enum