Imports System.Threading

Public Module ReservedWords

    Public Function GetTokenID(lexema As String, ByRef id As TokenID) As Boolean

        Select Case lexema.ToUpperInvariant()
                ' ===============================
                ' SENTENCIAS ZX BASIC
                ' ===============================
            Case "CLEAR" : id = TokenID.TK_CLEAR
            Case "CLS" : id = TokenID.TK_CLS
            Case "CONTINUE" : id = TokenID.TK_CONTINUE
            Case "DATA" : id = TokenID.TK_DATA
            Case "DIM" : id = TokenID.TK_DIM
            Case "ELSE" : id = TokenID.TK_ELSE
            Case "FN" : id = TokenID.TK_FN
            Case "FOR" : id = TokenID.TK_FOR
            Case "GOSUB" : id = TokenID.TK_GOSUB
            Case "GOTO" : id = TokenID.TK_GOTO
            Case "IF" : id = TokenID.TK_IF
            Case "INPUT" : id = TokenID.TK_INPUT
            Case "LET" : id = TokenID.TK_LET
            Case "NEXT" : id = TokenID.TK_NEXT
            Case "PRINT" : id = TokenID.TK_PRINT
            Case "READ" : id = TokenID.TK_READ
            Case "REM" : id = TokenID.TK_REM
            Case "RESTORE" : id = TokenID.TK_RESTORE
            Case "RETURN" : id = TokenID.TK_RETURN
            Case "RUN" : id = TokenID.TK_RUN
            Case "SAVE" : id = TokenID.TK_SAVE
            Case "STEP" : id = TokenID.TK_STEP
            Case "STOP" : id = TokenID.TK_STOP
            Case "THEN" : id = TokenID.TK_THEN
            Case "TO" : id = TokenID.TK_TO
            Case "VERIFY" : id = TokenID.TK_VERIFY
            Case "END" : id = TokenID.TK_END
            Case "RANDOMIZE" : id = TokenID.TK_RANDOMIZE

                ' ===============================
                ' ATRIBUTOS DE PRINT (ZX BASIC)
                ' ===============================
            Case "AT" : id = TokenID.TK_AT
            Case "TAB" : id = TokenID.TK_TAB

                ' ===============================
                ' FUNCIONES ZX BASIC
                ' ===============================
            Case "ABS" : id = TokenID.TK_ABS
            Case "ATTR" : id = TokenID.TK_ATTR
            Case "BIN" : id = TokenID.TK_BIN
            Case "BRIGHT" : id = TokenID.TK_BRIGHT
            Case "CHR$" : id = TokenID.TK_CHR_S
            Case "CODE" : id = TokenID.TK_CODE
            Case "FLASH" : id = TokenID.TK_FLASH
            Case "INK" : id = TokenID.TK_INK
            Case "INKEY$" : id = TokenID.TK_INKEY_S
            Case "INVERSE" : id = TokenID.TK_INVERSE
            Case "LEN" : id = TokenID.TK_LEN
            Case "OVER" : id = TokenID.TK_OVER
            Case "PAPER" : id = TokenID.TK_PAPER
            Case "PI" : id = TokenID.TK_PI
            Case "POINT" : id = TokenID.TK_POINT
            Case "RND" : id = TokenID.TK_RND
            Case "SCREEN$" : id = TokenID.TK_SCREEN_S
            Case "STR$" : id = TokenID.TK_STR_S
            Case "VAL" : id = TokenID.TK_VAL
            Case "VAL$" : id = TokenID.TK_VAL_S

                ' ===============================
                ' PROCEDIMIENTOS ZX BASIC
                ' ===============================
            Case "BORDER" : id = TokenID.TK_BORDER
            Case "BEEP" : id = TokenID.TK_BEEP
            Case "CIRCLE" : id = TokenID.TK_CIRCLE
            Case "DRAW" : id = TokenID.TK_DRAW
            Case "PLOT" : id = TokenID.TK_PLOT

                ' ===============================
                ' OPERADORES LOGICOS
                ' ===============================
            Case "AND" : id = TokenID.TK_AND
            Case "OR" : id = TokenID.TK_OR
            Case "NOT" : id = TokenID.TK_NOT

                ' ===============================
                ' NO ES PALABRA RESERVADA
                ' ===============================
            Case Else
                Return False
        End Select

        Return True
    End Function

End Module
