Imports System
Imports System.IO
Imports System.Runtime.ConstrainedExecution
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Text
Imports System.Xml

Public Module Parser

    ' ============================================================
    ' ESTADO GLOBAL DEL PARSER (solo una línea ZX cada vez)
    ' ============================================================
    Private tokensLinea As List(Of Token)
    Private idx As Integer

    Private opts As CmdOptions
    Private NroErrores As Integer = 0
    Private NroWarnings As Integer = 0
    Private LineaParaMostrar As String = ""
    Private NroLineaFichero As Integer = 0
    Private PrimeraLinea As Boolean = True
    Private bufferLinea As New List(Of Token)
    Private encontradoEOF As Boolean = False
    Private UltimaFueIF As Boolean = False

    ' ============================================================
    ' PARSE PRINCIPAL
    ' ============================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        Dim ant_tok As Token
        Dim ant_exist As Boolean = False
        NroLineaFichero = 0
        PrimeraLinea = True
        encontradoEOF = False
        bufferLinea.Clear()

        opts = _opts

        NroLineaFichero = 0
        NroErrores = 0
        NroWarnings = 0

        Using writer As New StreamWriter(opts.FSalidaPar, False, New UTF8Encoding(False))
            GuardarIRP_Texto(writer, Constantes.PAR_NOMBRE & " " & Constantes.PAR_VERSION)

            For Each LineaLeida As String In File.ReadLines(ObtenerFicheroEntrada(opts))
                ' Eliminar BOM UTF‑8 si existe
                LineaLeida = LineaLeida.TrimStart(ChrW(&HFEFF))

                ' ----------------------------------------------------------
                ' Primera línea, Debe contener tipo y versión del fichero
                ' ----------------------------------------------------------
                If PrimeraLinea Then
                    If Not LineaLeida.StartsWith(Constantes.LEX_NOMBRE) Then
                        ErrorSintactico(writer, 0, "[ERROR] No es un fichero " & Constantes.LEX_NOMBRE & ": " & LineaLeida)
                        Return (1)
                    End If

                    If Not LineaLeida.StartsWith(Constantes.LEX_NOMBRE & " " & Constantes.LEX_VERSION) Then
                        ErrorSintactico(writer, 0, "[ERROR] Versión incorrecta del fichero " & Constantes.LEX_NOMBRE & ": " & LineaLeida)
                        Return (1)
                    End If
                    PrimeraLinea = False
                    Continue For
                End If

                ' --------------------------------------------
                ' Línea original (contexto de error)
                ' --------------------------------------------
                Dim NroLineaPrograma As Integer
                If LineaLeida.StartsWith(MarcaSRC) Then
                    LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, LineaLeida)

                    GuardarIRP_Texto(writer, $"{Constantes.MarcaSRC} {LineaParaMostrar}")
                    Continue For
                End If

                ' --------------------------------------------
                ' Token normal
                ' --------------------------------------------
                Dim tok As New Token(LineaLeida)


                If ant_exist AndAlso
                   ant_tok.ID = TokenID.TES_IDENT AndAlso
                   tok.ID = TokenID.TES_IDENT Then

                    ' Concatenar identificadores
                    Dim idx As Integer = bufferLinea.Count - 1
                    Dim tkaux As Token = bufferLinea(idx)
                    tkaux.Value &= "_" & tok.Value
                    bufferLinea(idx) = tkaux

                    ' El último token válido es el acumulado
                    ant_tok = tkaux

                Else
                    bufferLinea.Add(tok)
                    ant_tok = tok
                    ant_exist = True
                End If


                ' --------------------------------------------
                ' EOF explícito del fichero TOK
                ' --------------------------------------------
                If tok.ID = TokenID.TCO_EOF Then
                    encontradoEOF = True
                    Exit For
                End If

                ' --------------------------------------------
                ' Fin de línea lógica ZX
                ' --------------------------------------------
                If tok.ID = TokenID.TCO_EOL Then
                    ParsearLineaTokens(bufferLinea, NroLineaFichero, writer)
                    bufferLinea.Clear()
                    ant_exist = False
                End If
            Next

            If Not encontradoEOF Then
                MostrarMensaje(opts, "[ERROR PARSER] Fichero TOK incompleto: falta EOF, posible fichero truncado")
                Return 1
            End If


            GuardarIRP_Token(writer, TokenID.TCO_EOF)
            writer.Close()
        End Using

        Return NroErrores
    End Function


    ' ============================================================
    ' PARSE DE UNA LÍNEA ZX (lista de strings hasta EOL)
    ' ============================================================
    Private Sub ParsearLineaTokens(lineaTokens As List(Of Token),
                                   IndiceLineaAST As Integer,
                                   writer As StreamWriter)

        tokensLinea = lineaTokens
        idx = 0

        ' --------------------------------------------
        ' Requerir número de línea
        ' --------------------------------------------
        If Tid() <> TokenID.TCO_LINE Then
            ErrorSintactico(writer, 0, "Línea sin número")
            Exit Sub
        End If

        Dim numLinea As Integer = Integer.Parse(TokenValor())
        NextToken()

        GuardarIRP_Token_Valor(writer, TokenID.TCO_LINE, $"{numLinea}")

        ' --------------------------------------------
        ' Parsear sentencias hasta EOL
        ' --------------------------------------------
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL
            ParseStatement(writer)

            If Tid() = TokenID.TSP_DOSPUNTOS Then
                NextToken()
                UltimaFueIF = False

            ElseIf Tid() = TokenID.TCO_EOL Then
                Exit While

            ElseIf UltimaFueIF Then
                ' ✅ El THEN ya actuó como separador
                ' NO consumir token, continuar al siguiente ParseStatement
                UltimaFueIF = False

            Else
                ErrorSintactico(writer, TokenColumna, "Falta ':' entre sentencias")
                Exit While
            End If


        End While

        GuardarIRP_Token(writer, TokenID.TCO_EOL)
    End Sub


    ' ============================================================
    ' FUNCIONES AUXILIARES DE TOKEN (STRING-BASED)
    ' ============================================================

    Private Function Ttk() As Token
        Return tokensLinea(idx)
    End Function

    Private Function Tid() As TokenID
        Return tokensLinea(idx).ID
    End Function

    Private Function TokenLinea() As Integer
        Return tokensLinea(idx).Line
    End Function

    Private Function TokenColumna() As Integer
        Return tokensLinea(idx).Col
    End Function

    Private Function TokenValor() As String
        Return tokensLinea(idx).GetValor
    End Function

    Private Sub NextToken()
        idx += 1
    End Sub

    Private Function PeekTid() As TokenID
        If idx + 1 < tokensLinea.Count Then
            Return tokensLinea(idx + 1).ID
        End If
        Return TokenID.TCO_NONE

    End Function



    ' ============================================================
    ' PARSE DE UNA SENTENCIA
    ' ============================================================
    Private Sub ParseStatement(writer As StreamWriter)
        Dim tok = tokensLinea(idx)
        Dim tipo As TokenID = Tid()
        Dim valor As String = TokenValor().ToUpperInvariant()

        If tipo = TokenID.TCO_EOL Then
            NextToken()
            Exit Sub
        End If

        If tok.IsStatementStart() Then
            Select Case tok.ID
                Case TokenID.TK_LET : ParseLet(writer) : Exit Sub
                Case TokenID.TK_PRINT : ParsePRINT(writer) : Exit Sub
                Case TokenID.TK_IF : ParseIf(writer) : Exit Sub
                Case TokenID.TK_GO : ParseGo(writer) : Exit Sub   'GO TO o GO SUB
                Case TokenID.TK_GOTO : ParseGoto(writer) : Exit Sub
                Case TokenID.TK_GOSUB : ParseGosub(writer) : Exit Sub
                Case TokenID.TK_RETURN : ParseReturn(writer) : Exit Sub
                Case TokenID.TK_RESTORE : ParseRestore(writer) : Exit Sub
                Case TokenID.TK_READ : ParseRead(writer) : Exit Sub
                Case TokenID.TK_DATA : ParseData(writer) : Exit Sub
                Case TokenID.TK_STOP : ParseStop(writer) : Exit Sub
                Case TokenID.TK_FOR : ParseFor(writer) : Exit Sub
                Case TokenID.TK_NEXT : ParseNext(writer) : Exit Sub
                Case TokenID.TK_REM : ParseREM(writer) : Exit Sub
                Case TokenID.TK_CLEAR : ParseClear(writer) : Exit Sub
                Case TokenID.TK_DIM : ParseDim(writer) : Exit Sub
                Case TokenID.TK_RANDOMIZE : ParseRandomize(writer) : Exit Sub


                Case TokenID.TK_CLS : ParseSimpleStmt(writer, TokenID.TK_CLS) : Exit Sub
                Case TokenID.TK_BORDER : ParseUnaryStmt(writer, TokenID.TK_BORDER) : Exit Sub
                Case TokenID.TK_PAUSE : ParseUnaryStmt(writer, TokenID.TK_PAUSE) : Exit Sub
                Case TokenID.TK_BEEP : ParseBeep(writer) : Exit Sub
                Case TokenID.TK_INK : ParseUnaryStmt(writer, TokenID.TK_INK) : Exit Sub
                Case TokenID.TK_PAPER : ParseUnaryStmt(writer, TokenID.TK_PAPER) : Exit Sub
                Case TokenID.TK_BRIGHT : ParseUnaryStmt(writer, TokenID.TK_BRIGHT) : Exit Sub
                Case TokenID.TK_FLASH : ParseUnaryStmt(writer, TokenID.TK_FLASH) : Exit Sub
                Case TokenID.TK_INVERSE : ParseUnaryStmt(writer, TokenID.TK_INVERSE) : Exit Sub

                Case TokenID.TK_POKE : ParseBinaryStmt(writer, TokenID.TK_POKE) : Exit Sub
                Case TokenID.TK_OUT : ParseBinaryStmt(writer, TokenID.TK_OUT) : Exit Sub

                Case TokenID.TK_RUN : ParseRun(writer) : Exit Sub
                Case TokenID.TK_LIST : ParseList(writer) : Exit Sub
                Case TokenID.TK_LOAD : ParseLoad(writer) : Exit Sub
                Case TokenID.TK_SAVE : ParseSave(writer) : Exit Sub
                Case TokenID.TK_MERGE : ParseMerge(writer) : Exit Sub

            End Select

            ErrorSintactico(writer, TokenColumna, "Comando no reconocido: " & valor)
            Exit Sub
        End If

        ' LET implícito si es opcional
        If tipo = TokenID.TES_IDENT AndAlso PeekTid() = TokenID.TOP_EQ Then
            ErrorSintactico(writer, 0, "Sentencia no válida ¿Falta el LET?")
            Exit Sub
        End If

        ErrorSintactico(writer, 0, "Sentencia no válida")
    End Sub


    ' ============================================================
    ' SENTENCIAS
    ' ============================================================
    Private Sub ParseREM(Writer As StreamWriter)
        NextToken() ' consumir REM

        Dim comentario As String = ""

        If Tid() = TokenID.TES_STRING Then
            comentario = TokenValor()
            NextToken()
        End If

        GuardarIRP_Token_Valor(Writer, TokenID.TK_REM, comentario)

        ' consumir hasta EOL por seguridad
        While Tid() <> TokenID.TCO_EOL AndAlso idx < tokensLinea.Count
            NextToken()
        End While
    End Sub

    Private Sub ParseReturn(writer As StreamWriter)
        GuardarIRP_Token(writer, TokenID.TK_RETURN)
        NextToken()
    End Sub

    Private Sub ParseStop(writer As StreamWriter)
        GuardarIRP_Token(writer, TokenID.TK_STOP)
        NextToken()
    End Sub

    Private Sub ParseClear(writer As StreamWriter)
        'CLEAR        ; borra variables
        'CLEAR n      ; borra variables y fija RAMTOP = n



        ' Consumir CLEAR
        NextToken()

        ' Argumento opcional
        Dim expr As List(Of RPN.RPN_Node) = Nothing

        If Tid() <> TokenID.TCO_EOL AndAlso Tid() <> TokenID.TSP_DOSPUNTOS Then
            If Not ParseExprTexto(writer, False, expr) Then
                Exit Sub
            End If
        End If

        ' Emitir IR estructural
        GuardarIRP_CLEAR(writer, expr)
    End Sub


    Private Sub ParseDim(writer As StreamWriter)

        ' Consumir DIM
        NextToken()

        ' Nombre del array
        If Tid() <> TokenID.TES_IDENT Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba nombre de array en DIM")
            Exit Sub
        End If

        Dim arrayName As String = TokenValor()
        NextToken()

        ' Debe venir '('
        If Tid() <> TokenID.TSP_PAR_ABIERTO Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba '(' en DIM")
            Exit Sub
        End If
        NextToken()

        ' Lista de dimensiones (cada una es RPN)
        Dim dims As New List(Of List(Of RPN.RPN_Node))

        Do
            Dim exprDim As List(Of RPN.RPN_Node) = Nothing

            If Not ParseExprTexto(writer, False, exprDim, ",)") Then
                Exit Sub
            End If

            dims.Add(exprDim)

            ' ¿Otra dimensión?
            If Tid() = TokenID.TSP_COMA Then
                NextToken()
                Continue Do
            End If

            Exit Do
        Loop

        ' Debe cerrar ')'
        If Tid() <> TokenID.TSP_PAR_CERRADO Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba ')' en DIM")
            Exit Sub
        End If
        NextToken()

        ' Emitir IR DIM estructural
        GuardarIRP_DIM(writer, arrayName, dims)
    End Sub

    Private Sub ParseLet(writer As StreamWriter)

        ' Consumir LET solo si es explícito
        If Tid() = TokenID.TK_LET Then
            NextToken()
        End If

        ' Lado izquierdo (variable o array)
        Dim name As String = Nothing
        Dim indices As List(Of List(Of RPN.RPN_Node)) = Nothing

        If Not ParseLValue(writer, name, indices) Then Exit Sub

        ' Debe venir '='
        If Tid() <> TokenID.TOP_EQ Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba '=' en LET")
            Exit Sub
        End If
        NextToken() ' consumir '='

        ' Lado derecho: expresión RPN tipada
        Dim rpn As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, rpn) Then Exit Sub

        ' Emitir IR estructural
        GuardarIRP_LET(writer, name, indices, rpn)
    End Sub

    Private Function ParseLValue(writer As StreamWriter,
                                 ByRef name As String,
                                 ByRef indices As List(Of List(Of RPN.RPN_Node))
                                ) As Boolean

        indices = New List(Of List(Of RPN.RPN_Node))

        ' Debe empezar por identificador
        If Tid() <> TokenID.TES_IDENT OrElse Not Char.IsLetter(TokenValor()(0)) Then
            ErrorSintactico(writer, TokenColumna, "Nombre de variable inválido")
            Return False
        End If

        name = TokenValor()
        NextToken()

        ' ¿Acceso a array?
        If Tid() = TokenID.TSP_PAR_ABIERTO Then
            NextToken() ' consumir '('

            Do
                Dim exprIdx As List(Of RPN.RPN_Node) = Nothing

                ' Cada índice es una expresión RPN
                If Not ParseExprTexto(writer, False, exprIdx, ",)") Then
                    Return False
                End If

                indices.Add(exprIdx)

                ' ¿Más dimensiones?
                If Tid() = TokenID.TSP_COMA Then
                    NextToken()
                    Continue Do
                End If

                Exit Do
            Loop

            ' Debe cerrar ')'
            If Tid() <> TokenID.TSP_PAR_CERRADO Then
                ErrorSintactico(writer, TokenColumna, "Se esperaba ')'")
                Return False
            End If
            NextToken()
        End If

        Return True
    End Function

    Private Sub ParseRandomize(writer As StreamWriter)

        ' Consumir RANDOMIZE
        NextToken()

        Dim modoUSR As Boolean = False
        Dim expr As List(Of RPN.RPN_Node) = Nothing

        ' ¿RANDOMIZE solo?
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_RANDOMIZE(writer, False, Nothing)
            Exit Sub
        End If

        ' ¿RANDOMIZE USR ... ?
        If Tid() = TokenID.TK_USR Then
            modoUSR = True
            NextToken()
        End If

        ' Argumento obligatorio si no era RANDOMIZE solo
        If Not ParseExprTexto(writer, False, expr) Then Exit Sub

        GuardarIRP_RANDOMIZE(writer, modoUSR, expr)
    End Sub



    Private Function ParsePRINT(writer As StreamWriter) As Boolean
        Dim items As New List(Of PrintItem)


        Dim actual As New PrintItem(TokenID.TCO_UNKNOWN)
        Dim tieneValor As Boolean = False

        ' TK_PRINT ya consumido

        While idx < tokensLinea.Count AndAlso
              Tid() <> TokenID.TCO_EOL AndAlso
              Tid() <> TokenID.TSP_DOSPUNTOS

            Select Case Tid()
                ' ===============================
                ' MODIFICADOR AT (CASO ESPECIAL)
                ' ===============================
                Case TokenID.TK_AT
                    Dim item = ParseAT(writer)
                    If item.ID = TokenID.TCO_UNKNOWN Then Return False

                    ' AHORA miramos el separador real de PRINT
                    If Tid() = TokenID.TSP_PUNTOYCOMA Then
                        item.Separator = PrintSeparator.P
                        NextToken()
                    ElseIf Tid() = TokenID.TSP_COMA Then
                        ' Caso raro: coma tras AT
                        ' debe afectar al siguiente PRINT, pero seguramente es un error
                        WarningSintactico(writer, TokenColumna(), $"'Posible error: coma tras un AT")
                        item.Separator = PrintSeparator.C
                        NextToken()
                    Else
                        item.Separator = PrintSeparator.N
                    End If

                    items.Add(item)
                    Continue While


                Case TokenID.TSP_PUNTOYCOMA
                    ' Separador ;
                    actual.Separator = PrintSeparator.P
                    items.Add(actual)

                    actual = New PrintItem(TokenID.TCO_UNKNOWN)
                    tieneValor = False
                    NextToken()

                Case TokenID.TSP_COMA
                    ' Separador ,
                    actual.Separator = PrintSeparator.C
                    items.Add(actual)

                    actual = New PrintItem(TokenID.TCO_UNKNOWN)
                    tieneValor = False
                    NextToken()

                Case Else
                    ' Token normal
                    If actual.ID = TokenID.TCO_UNKNOWN AndAlso Tid() <> TokenID.TK_PRINT Then
                        actual.ID = Tid()
                    End If

                    actual.Value &= ReconstruirToken(Tid(), TokenValor())
                    tieneValor = True
                    NextToken()

            End Select
        End While

        ' Cierre final si hay valor pendiente
        If tieneValor Then
            actual.Separator = PrintSeparator.N
            items.Add(actual)
        End If

        'Guardar la lista del PRINT
        For Each pItem In items
            GuardarIRP_PRINT(writer, pItem)
        Next

        Return True
    End Function

    Private Function ReconstruirToken(id As TokenID, valor As String) As String

        'Si es un operador lo procesamos directo, no tiene valor
        valor = GetTextoOperador(id)
        If (valor <> "") Then
            Return (valor)
        End If

        Select Case id

        ' ===== Literales =====
            Case TokenID.TES_STRING
                ' El valor viene sin comillas, se añaden aquí
                Return valor

            Case TokenID.TES_NUMBER
                Return valor

        ' ===== Identificadores y keywords =====
            Case TokenID.TES_IDENT
                Return valor

            Case TokenID.TK_AT, TokenID.TK_TAB,
             TokenID.TK_INK, TokenID.TK_PAPER,
             TokenID.TK_BRIGHT, TokenID.TK_FLASH,
             TokenID.TK_INVERSE, TokenID.TK_OVER

                ' Keywords de PRINT y similares
                Return valor

                ' ===== Fallback =====
            Case Else
                ' Para cualquier otro token textual
                Return valor

        End Select

    End Function

    Private Function ParseAT(writer As StreamWriter) As PrintItem

        Dim pi As New PrintItem(TokenID.TK_AT)

        ' Consumir AT
        NextToken()

        ' Primera expresión (Y)
        Dim exprY As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, exprY, ",") Then
            Return pi
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba ',' en AT")
            Return pi
        End If
        NextToken()

        ' Segunda expresión (X)
        Dim exprX As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, exprX, ",;") Then
            Return pi
        End If

        ' Guardar estructura en el PrintItem
        pi.Expr1 = exprY
        pi.Expr2 = exprX
        pi.Separator = PrintSeparator.N

        Return pi
    End Function


    Private Sub ParseIf(writer As StreamWriter)
        ' Consumir IF
        NextToken()

        ' Parsear condición como RPN tipado
        Dim condicion As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, condicion) Then
            Exit Sub
        End If

        ' Debe venir THEN
        If Tid() <> TokenID.TK_THEN Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba THEN en IF")
            Exit Sub
        End If

        ' Consumir THEN
        NextToken()

        ' Emitir IF como sentencia independiente, con condición RPN
        GuardarIRP_IF(writer, condicion)

        ' Importante: el cuerpo del IF NO se consume aquí
        UltimaFueIF = True
    End Sub

    Private Sub ParseGo(writer As StreamWriter)

        ' Consumir GO
        NextToken()

        ' Debe venir TO o SUB
        If Tid() = TokenID.TK_TO Then
            ParseGoto(writer)
            Exit Sub
        End If

        If Tid() = TokenID.TK_SUB Then
            ParseGosub(writer)
            Exit Sub
        End If

        ErrorSintactico(writer, TokenColumna, "Se esperaba TO o SUB tras GO")

    End Sub

    Private Sub ParseGoto(writer As StreamWriter)
        NextToken()
        Dim ln As String = TokenValor()
        NextToken()
        GuardarIRP_Token_Valor(writer, TokenID.TK_GOTO, $"{ln}")
    End Sub


    Private Sub ParseGosub(writer As StreamWriter)
        NextToken()
        Dim ln As String = TokenValor()
        NextToken()
        GuardarIRP_Token_Valor(writer, TokenID.TK_GOSUB, $"{ln}")
    End Sub

    ' ------------------------------------------------------------
    ' FOR I = expr TO expr [STEP expr]
    ' ------------------------------------------------------------
    Private Sub ParseFor(writer As StreamWriter)

        ' Consumir FOR
        NextToken()

        ' Variable de control
        If Tid() <> TokenID.TES_IDENT Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba variable de control en FOR")
            Exit Sub
        End If

        Dim varName As String = TokenValor()
        NextToken()

        ' Debe venir '='
        If Tid() <> TokenID.TOP_EQ Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba '=' en FOR")
            Exit Sub
        End If
        NextToken() ' consumir '='

        ' Expresión inicial
        Dim exprInit As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, True, exprInit) Then Exit Sub

        ' Debe venir TO
        If Tid() <> TokenID.TK_TO Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba TO en FOR")
            Exit Sub
        End If
        NextToken()

        ' Expresión límite
        Dim exprLimit As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, True, exprLimit) Then Exit Sub

        ' STEP opcional
        Dim exprStep As List(Of RPN.RPN_Node) = Nothing
        If Tid() = TokenID.TK_STEP Then
            NextToken()
            If Not ParseExprTexto(writer, True, exprStep) Then Exit Sub
        End If

        ' Emitir IR FOR (estructural, no textual)
        GuardarIRP_FOR(writer, varName, exprInit, exprLimit, exprStep)
    End Sub

    ' ------------------------------------------------------------
    ' NEXT [I]
    ' ------------------------------------------------------------
    Private Sub ParseNext(writer As StreamWriter)

        ' Consumir NEXT
        NextToken()

        Dim sb As String = ""
        ' Variable opcional
        If Tid() = TokenID.TES_IDENT Then
            sb = TokenValor()
            NextToken()
        End If
        GuardarIRP_Token_Valor(writer, TokenID.TK_NEXT, sb.ToString())

    End Sub

    Private Sub ParseRestore(writer As StreamWriter)

        ' Consumir RESTORE
        NextToken()

        ' ¿Hay número de línea?
        If Tid() = TokenID.TES_NUMBER Then
            Dim ln As String = TokenValor()
            NextToken()
            GuardarIRP_Token_Valor(writer, TokenID.TK_RESTORE, $"{ln}")
        Else
            GuardarIRP_Token(writer, TokenID.TK_RESTORE)
        End If

    End Sub

    Private Sub ParseRead(writer As StreamWriter)

        ' Consumir READ
        NextToken()

        Dim sb As New StringBuilder()
        While idx < tokensLinea.Count AndAlso
                 Tid() <> TokenID.TCO_EOL AndAlso
                 Tid() <> TokenID.TSP_DOSPUNTOS

            Select Case Tid()

                Case TokenID.TES_IDENT
                    sb.Append(TokenValor())

                Case TokenID.TSP_COMA
                    sb.Append(" , ")

                Case Else
                    ErrorSintactico(writer, TokenColumna, "Sintaxis inválida en READ")
                    Exit Sub

            End Select

            NextToken()
        End While

        GuardarIRP_Token_Valor(writer, TokenID.TK_READ, sb.ToString())

    End Sub

    Private Sub ParseData(writer As StreamWriter)

        ' Consumir DATA
        NextToken()

        Dim items As New List(Of List(Of RPN.RPN_Node))

        ' DATA puede ir hasta EOL (no ":" como separador de sentencias)
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL

            Dim expr As List(Of RPN.RPN_Node) = Nothing

            ' Cada elemento es una expresión
            If Not ParseExprTexto(writer, False, expr, ",") Then
                Exit Sub
            End If

            items.Add(expr)

            ' ¿Más elementos?
            If Tid() = TokenID.TSP_COMA Then
                NextToken()
                Continue While
            End If

            Exit While
        End While

        ' Emitir IR DATA estructural
        GuardarIRP_DATA(writer, items)
    End Sub

    Private Sub ParseBeep(writer As StreamWriter)

        ' Consumir BEEP
        NextToken()

        ' Primer argumento: duración
        Dim exprDuration As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, exprDuration, ",") Then
            Exit Sub
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba ',' en BEEP")
            Exit Sub
        End If
        NextToken()

        ' Segundo argumento: tono
        Dim exprPitch As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, exprPitch) Then
            Exit Sub
        End If

        ' Emitir IR BEEP estructural
        GuardarIRP_BEEP(writer, exprDuration, exprPitch)
    End Sub

    Private Sub ParseRun(writer As StreamWriter)

        ' Consumir RUN
        NextToken()

        ' ¿RUN sin argumento?
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_RUN(writer, Nothing)
            Exit Sub
        End If

        ' RUN con expresión (línea de inicio)
        Dim expr As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, True, expr) Then
            Exit Sub
        End If

        GuardarIRP_RUN(writer, expr)
    End Sub

    Private Sub ParseList(writer As StreamWriter)

        ' Consumir LIST
        NextToken()

        ' LIST sin argumentos
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_LIST(writer, Nothing, Nothing)
            Exit Sub
        End If

        ' Primer argumento (línea inicial)
        Dim exprStart As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, True, exprStart, ",") Then
            Exit Sub
        End If

        ' ¿Rango?
        If Tid() = TokenID.TSP_COMA Then
            NextToken()

            Dim exprEnd As List(Of RPN.RPN_Node) = Nothing
            If Not ParseExprTexto(writer, True, exprEnd) Then
                Exit Sub
            End If

            GuardarIRP_LIST(writer, exprStart, exprEnd)
            Exit Sub
        End If

        ' Solo una expresión
        GuardarIRP_LIST(writer, exprStart, Nothing)
    End Sub

    Private Sub ParseLoad(writer As StreamWriter)
        ParseFileStmt(writer, TokenID.TK_LOAD)
    End Sub
    Private Sub ParseSave(writer As StreamWriter)
        ParseFileStmt(writer, TokenID.TK_SAVE)
    End Sub
    Private Sub ParseMerge(writer As StreamWriter)
        ParseFileStmt(writer, TokenID.TK_MERGE)
    End Sub

    Private Sub ParseFileStmt(writer As StreamWriter, id As TokenID)

        ' Consumir LOAD / SAVE / MERGE
        NextToken()

        ' Argumento obligatorio: expresión (nombre de fichero)
        Dim expr As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, True, expr) Then
            Exit Sub
        End If

        ' Emitir IR estructural
        GuardarIRP_FILE(writer, id, expr)
    End Sub

    Private Sub ParseSimpleStmt(writer As StreamWriter, id As TokenID)
        NextToken()
        GuardarIRP_Token(writer, id)
    End Sub

    Private Sub ParseUnaryStmt(writer As StreamWriter, id As TokenID)

        ' Consumir la palabra clave (INK, PAPER, BRIGHT, etc.)
        NextToken()

        ' Argumento obligatorio: una expresión
        Dim expr As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, True, expr) Then
            Exit Sub
        End If

        ' Emitir IR estructural
        GuardarIRP_UNARY(writer, id, expr)
    End Sub

    Private Sub ParseBinaryStmt(writer As StreamWriter, id As TokenID)

        ' Consumir la palabra clave (POKE, OUT, etc.)
        NextToken()

        ' Primer argumento
        Dim exprLeft As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, exprLeft, ",") Then
            Exit Sub
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(writer, TokenColumna, $"Se esperaba ',' en {id}")
            Exit Sub
        End If
        NextToken()

        ' Segundo argumento
        Dim exprRight As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(writer, False, exprRight) Then
            Exit Sub
        End If

        ' Emitir IR estructural
        GuardarIRP_BINARY(writer, id, exprLeft, exprRight)
    End Sub


    ' ============================================================
    ' PARSE DE EXPRESIONES en RPN
    ' ============================================================
    Private Function ParseExprTexto(
    writer As StreamWriter,
    permiteComaExterior As Boolean,
    ByRef resultado As List(Of RPN.RPN_Node),
    Optional stopTokens As String = ""
) As Boolean

        resultado = New List(Of RPN.RPN_Node)()

        Dim operators As New Stack(Of RPN.RPN_Node)
        Dim nivelParentesis As Integer = 0
        Dim ultimoFueOperando As Boolean = False

        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TCO_EOL AndAlso
          Tid() <> TokenID.TSP_DOSPUNTOS AndAlso
          Not IsControlKeyword(Tid())

            ' Parada por token externo
            If nivelParentesis = 0 AndAlso TokenEsStopChar(stopTokens) Then Exit While

            ' Coma exterior no permitida
            If Tid() = TokenID.TSP_COMA AndAlso
           nivelParentesis = 0 AndAlso
           Not permiteComaExterior AndAlso
           (stopTokens = "" OrElse Not TokenEsStopChar(stopTokens)) Then
                Exit While
            End If

            Select Case Tid()

            ' =====================================================
            ' IDENTIFICADOR (variable o llamada / array)
            ' =====================================================
                Case TokenID.TES_IDENT

                    Dim nombre As String = TokenValor()
                    NextToken()

                    ' ¿Llamada / indexación?
                    If Tid() = TokenID.TSP_PAR_ABIERTO Then
                        NextToken() ' consumir '('

                        Dim args As New List(Of RPN.RPN_Node)
                        Dim exprArg As List(Of RPN.RPN_Node)

                        Do
                            If Not ParseExprTexto(writer, False, exprArg, ",)") Then
                                Return False
                            End If
                            args.AddRange(exprArg)

                            If Tid() = TokenID.TSP_COMA Then
                                NextToken()
                                Continue Do
                            End If
                            Exit Do
                        Loop

                        If Tid() <> TokenID.TSP_PAR_CERRADO Then
                            ErrorSintactico(writer, TokenColumna, "Se esperaba ')'")
                            Return False
                        End If
                        NextToken()

                        ' Insertar argumentos RPN
                        resultado.AddRange(args)

                        ' Insertar nodo CALLFUN
                        resultado.Add(New RPN.RPN_Node With {
                        .Kind = RPN.RPNKind.CALLFUN,
                        .TokenID = TokenID.TES_IDENT,
                        .Value = nombre,
                        .Arity = args.Count
                    })

                        ultimoFueOperando = True

                    Else
                        ' Variable simple
                        resultado.Add(New RPN.RPN_Node With {
                        .Kind = RPN.RPNKind.VAR,
                        .TokenID = TokenID.TES_IDENT,
                        .Value = nombre,
                        .Arity = 0
                    })
                        ultimoFueOperando = True
                    End If

            ' =====================================================
            ' CONSTANTES
            ' =====================================================
                Case TokenID.TES_NUMBER, TokenID.TES_STRING
                    resultado.Add(New RPN.RPN_Node With {
                    .Kind = RPN.RPNKind.CTE,
                    .TokenID = Tid(),
                    .Value = TokenValor(),
                    .Arity = 0
                })
                    ultimoFueOperando = True
                    NextToken()

            ' =====================================================
            ' PARÉNTESIS
            ' =====================================================
                Case TokenID.TSP_PAR_ABIERTO
                    operators.Push(New RPN.RPN_Node With {.Value = "("})
                    nivelParentesis += 1
                    ultimoFueOperando = False
                    NextToken()

                Case TokenID.TSP_PAR_CERRADO
                    nivelParentesis -= 1
                    While operators.Count > 0 AndAlso operators.Peek().Value <> "("
                        resultado.Add(operators.Pop())
                    End While
                    If operators.Count = 0 Then
                        ErrorSintactico(writer, TokenColumna, "Paréntesis desequilibrados")
                        Return False
                    End If
                    operators.Pop() ' quitar "("
                    ultimoFueOperando = True
                    NextToken()

                    ' =====================================================
                    ' OPERADORES
                    ' =====================================================
                Case Else
                    If Ttk().IsOperator() Then
                        Dim opToken As TokenID = Tid()
                        Dim opText As String = GetTextoOperador(opToken)
                        Dim kind As RPN.RPNKind
                        Dim arity As Integer

                        If opToken = TokenID.TOP_MINUS AndAlso Not ultimoFueOperando Then
                            kind = RPN.RPNKind.OPE_UNARY
                            arity = 1
                            opText = "UNARY_MINUS"
                        ElseIf opToken = TokenID.TK_NOT Then
                            kind = RPN.RPNKind.OPE_UNARY
                            arity = 1
                        Else
                            kind = RPN.RPNKind.OPE_BINARY
                            arity = 2
                        End If

                        Dim node As New RPN.RPN_Node With {
                        .Kind = kind,
                        .TokenID = opToken,
                        .Value = opText,
                        .Arity = arity
                    }

                        While operators.Count > 0 AndAlso
                          operators.Peek().Value <> "(" AndAlso
                          Precedencia(operators.Peek().Value) >= Precedencia(opText)
                            resultado.Add(operators.Pop())
                        End While

                        operators.Push(node)
                        ultimoFueOperando = False
                        NextToken()
                    Else
                        Exit While
                    End If
            End Select
        End While

        If nivelParentesis <> 0 Then
            ErrorSintactico(writer, TokenColumna, "Paréntesis desequilibrados en expresión")
            Return False
        End If

        While operators.Count > 0
            If operators.Peek().Value = "(" Then
                ErrorSintactico(writer, TokenColumna, "Paréntesis desequilibrados en expresión")
                Return False
            End If
            resultado.Add(operators.Pop())
        End While

        Return True
    End Function


    Private Function GetTextoOperador(tid As TokenID) As String
        Select Case tid
        ' ===== Operadores aritméticos =====
            Case TokenID.TOP_PLUS : Return "+"
            Case TokenID.TOP_MINUS : Return "-"
            Case TokenID.TOP_MUL : Return "*"
            Case TokenID.TOP_DIV : Return "/"
            Case TokenID.TOP_POW : Return "^"

        ' ===== Operadores relacionales =====
            Case TokenID.TOP_EQ : Return "="
            Case TokenID.TOP_NE : Return "<>"
            Case TokenID.TOP_LT : Return "<"
            Case TokenID.TOP_GT : Return ">"
            Case TokenID.TOP_LE : Return "<="
            Case TokenID.TOP_GE : Return ">="

        ' ===== Operadores lógicos =====
            Case TokenID.TK_AND : Return "AND"
            Case TokenID.TK_OR : Return "OR"
        End Select
        Return ""
    End Function


    Private Function Precedencia(op As String) As Integer
        Select Case op

            Case "^" : Return 7
            Case "UNARY_MINUS" : Return 6
            Case "*", "/" : Return 5
            Case "+", "-" : Return 4
            Case "=", "<>", "<", ">", "<=", ">=" : Return 3
            Case "NOT" : Return 2
            Case "AND" : Return 1
            Case "OR" : Return 0

        End Select
        Return -1
    End Function



    ' ----------------------------------------------------------
    ' Helpers
    ' ----------------------------------------------------------
    Private Function TokenEsStopChar(stopChars As String) As Boolean
        If stopChars = "" Then Return False

        Select Case Tid()
            Case TokenID.TSP_COMA : Return stopChars.Contains(","c)
            Case TokenID.TSP_PUNTOYCOMA : Return stopChars.Contains(";"c)
            Case TokenID.TSP_PAR_CERRADO : Return stopChars.Contains(")"c)
            Case TokenID.TSP_PAR_ABIERTO : Return stopChars.Contains("("c)
            Case TokenID.TSP_DOSPUNTOS : Return stopChars.Contains(":"c)
        End Select

        Return False
    End Function


    Public Function IsControlKeyword(id As TokenID) As Boolean
        Select Case id
            Case TokenID.TK_THEN, TokenID.TK_TO, TokenID.TK_STEP, TokenID.TK_USR
                Return True
        End Select
        Return False
    End Function


    Public Function IsEqual(id As TokenID) As Boolean
        Return id = TokenID.TOP_EQ
    End Function

    ' ============================================================
    ' ERROR SINTÁCTICO
    ' ============================================================
    Private Sub ErrorSintactico(writer As StreamWriter, columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If

        MostrarError(opts, writer, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)

        ' EVITAR BUCLES INFINITOS, IR AL FIN DE LA LINEA
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL
            NextToken()
        End While
    End Sub

    Private Sub WarningSintactico(writer As StreamWriter, columna As Integer, descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        If (columna <> 0) Then
            columna = columna - 1
        End If
        MensajeError(opts, writer, True, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)

    End Sub

    Private Sub GuardarIRP_Token(writer As StreamWriter, ID As TokenID)
        GuardarIRP(writer, New Token(ID, ""), New PrintItem(TokenID.TCO_UNKNOWN))
    End Sub

    Private Sub GuardarIRP_Token_Valor(writer As StreamWriter, ID As TokenID, valor As String)
        GuardarIRP(writer, New Token(ID, valor), New PrintItem(TokenID.TCO_UNKNOWN))
    End Sub

    Private Sub GuardarIRP_PRINT(writer As StreamWriter, pi As PrintItem)
        GuardarIRP(writer, New Token(TokenID.TK_PRINT), pi)
    End Sub

    Private Sub GuardarIRP(writer As StreamWriter, tok As Token, pi As PrintItem)
        Dim idNum As Integer = CInt(tok.ID)
        Dim value As String = If(tok.Value IsNot Nothing, tok.Value, "")
        Dim Linea As String = ""
        Dim Comentario As String = ""

        If pi.ID = TokenID.TCO_UNKNOWN Then
            Linea = $"{idNum} {value}"
            Comentario = $"{tok.ID.ToString()}"
        Else
            Linea = $"{idNum} {pi.ToText} {value}"
            Comentario = $"{tok.ID.ToString()} {pi.ID.ToString()}"
        End If

        If Len(Linea) < 49 Then
                Linea &= Space(50 - Len(Linea)) & $"{Constantes.MarcaComentario} {Comentario}"
                GuardarIRP_Texto(writer, Linea)
            Else
                GuardarIRP_Texto(writer, Linea)
                Linea = Space(50) & $"{Constantes.MarcaComentario} {Comentario}"
                GuardarIRP_Texto(writer, Linea)
            End If
    End Sub

    Private Sub GuardarIRP_Texto(writer As StreamWriter, linea As String)
        writer.WriteLine(linea)
        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If

    End Sub

    '*************************************************************
    '* GUARDAR LOS IRP DE CADA TIPO
    '*************************************************************

    Private Function RPN_ToText(rpn As List(Of RPN.RPN_Node)) As String
        Dim sb As New StringBuilder()

        For Each n In rpn
            Select Case n.Kind
                Case RPNKind.VAR
                    sb.Append($"VAR({n.Value}) ")

                Case RPNKind.CTE
                    sb.Append($"CTE({n.Value}) ")

                Case RPNKind.OPE_UNARY
                    sb.Append($"OP({n.Value}) ")

                Case RPNKind.OPE_BINARY
                    sb.Append($"OP({n.Value}) ")

                Case RPNKind.CALLFUN
                    sb.Append($"CALL({n.Value},{n.Arity}) ")
            End Select
        Next

        Return sb.ToString().Trim()
    End Function

    Private Sub GuardarIRP_LET(writer As StreamWriter,
                               name As String,
                               indices As List(Of List(Of RPN.RPN_Node)),
                               expr As List(Of RPN.RPN_Node)
                               )

        Dim sb As New StringBuilder()

        ' --- LValue ---
        sb.Append($"VAR({name})")

        If indices IsNot Nothing AndAlso indices.Count > 0 Then
            sb.Append(" IDX(")
            For i = 0 To indices.Count - 1
                If i > 0 Then sb.Append(",")
                sb.Append(RPN_ToText(indices(i)))
            Next
            sb.Append(")")
        End If

        ' --- Asignación ---
        sb.Append(" := ")

        ' --- RValue ---
        sb.Append(RPN_ToText(expr))

        ' Emitir IR textual tipado
        GuardarIRP_Token_Valor(writer, TokenID.TK_LET, sb.ToString())

    End Sub

    Private Sub GuardarIRP_IF(writer As StreamWriter, condicion As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_FOR(writer As StreamWriter, varName As String, exprInit As List(Of RPN.RPN_Node),
                               exprLimit As List(Of RPN.RPN_Node), exprStep As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_CLEAR(writer As StreamWriter, expr As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_DIM(writer As StreamWriter, arrayName As String, dims As List(Of List(Of RPN.RPN_Node)))

    End Sub

    Private Sub GuardarIRP_RANDOMIZE(writer As StreamWriter, modoUSR As Boolean, expr As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_DATA(writer As StreamWriter, items As List(Of List(Of RPN.RPN_Node)))

    End Sub

    Private Sub GuardarIRP_BEEP(writer As StreamWriter, exprDuration As List(Of RPN.RPN_Node), exprPitch As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_RUN(writer As StreamWriter, expr As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_LIST(writer As StreamWriter, exprStart As List(Of RPN.RPN_Node), exprEnd As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_FILE(writer As StreamWriter, id As TokenID, expr As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_UNARY(writer As StreamWriter, id As TokenID, expr As List(Of RPN.RPN_Node))

    End Sub

    Private Sub GuardarIRP_BINARY(writer As StreamWriter, id As TokenID, exprLeft As List(Of RPN.RPN_Node),
                                  exprRight As List(Of RPN.RPN_Node))

    End Sub


End Module