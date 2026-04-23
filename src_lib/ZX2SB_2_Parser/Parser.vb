Imports System
Imports System.IO
Imports System.Runtime.ConstrainedExecution
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
        NroLineaFichero = 0
        PrimeraLinea = True
        encontradoEOF = False
        bufferLinea.Clear()

        opts = _opts

        NroLineaFichero = 0
        NroErrores = 0

        Using writer As New StreamWriter(opts.FSalidaPar, False, New UTF8Encoding(False))
            GuardarTextoIRP(writer, Constantes.PAR_NOMBRE & " " & Constantes.PAR_VERSION)

            For Each LineaLeida As String In File.ReadLines(ObtenerFicheroEntrada(opts))
                ' Eliminar BOM UTF‑8 si existe
                LineaLeida = LineaLeida.TrimStart(ChrW(&HFEFF))

                ' ----------------------------------------------------------
                ' Primera línea, Debe contener tipo y versión del fichero
                ' ----------------------------------------------------------
                If PrimeraLinea Then
                    If Not LineaLeida.StartsWith(Constantes.LEX_NOMBRE) Then
                        ErrorSintaxis(writer, 0, "[ERROR] No es un fichero " & Constantes.LEX_NOMBRE & ": " & LineaLeida)
                        Return (1)
                    End If

                    If Not LineaLeida.StartsWith(Constantes.LEX_NOMBRE & " " & Constantes.LEX_VERSION) Then
                        ErrorSintaxis(writer, 0, "[ERROR] Versión incorrecta del fichero " & Constantes.LEX_NOMBRE & ": " & LineaLeida)
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

                    GuardarTextoIRP(writer, $"{Constantes.MarcaSRC} {LineaParaMostrar}")
                    Continue For
                End If

                ' --------------------------------------------
                ' Token normal
                ' --------------------------------------------
                Dim tok As New Token(LineaLeida)
                bufferLinea.Add(tok)

                ' --------------------------------------------
                ' EOF explícito del fichero TOK
                ' --------------------------------------------
                If tok.ID = TokenID.TE_EOF Then
                    encontradoEOF = True
                    Exit For
                End If

                ' --------------------------------------------
                ' Fin de línea lógica ZX
                ' --------------------------------------------
                If tok.ID = TokenID.TE_EOL Then
                    ParsearLineaTokens(bufferLinea, NroLineaFichero, writer)
                    bufferLinea.Clear()
                End If
            Next

            If Not encontradoEOF Then
                MostrarMensaje(opts, "[ERROR PARSER] Fichero TOK incompleto: falta EOF, posible fichero truncado")
                Return 1
            End If


            GuardarIRP(writer, TokenID.TE_EOF)
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
        If Tid() <> TokenID.TE_LINE Then
            ErrorSintaxis(writer, 0, "Línea sin número")
            Exit Sub
        End If

        Dim numLinea As Integer = Integer.Parse(TokenValor())
        NextToken()

        GuardarIRP(writer, TokenID.TE_LINE, $"{numLinea}")

        ' --------------------------------------------
        ' Parsear sentencias hasta EOL
        ' --------------------------------------------
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TE_EOL
            ParseStatement(writer)

            If Tid() = TokenID.TS_DOSPUNTOS Then
                NextToken()
                UltimaFueIF = False

            ElseIf Tid() = TokenID.TE_EOL Then
                Exit While

            ElseIf UltimaFueIF Then
                ' ✅ El THEN ya actuó como separador
                ' NO consumir token, continuar al siguiente ParseStatement
                UltimaFueIF = False

            Else
                ErrorSintaxis(writer, TokenColumna, "Falta ':' entre sentencias")
                Exit While
            End If


        End While

        GuardarIRP(writer, TokenID.TE_EOL)
    End Sub


    ' ============================================================
    ' FUNCIONES AUXILIARES DE TOKEN (STRING-BASED)
    ' ============================================================

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
        Return tokensLinea(idx).Value
    End Function

    Private Sub NextToken()
        idx += 1
    End Sub

    Private Function PeekTid() As TokenID
        If idx + 1 < tokensLinea.Count Then
            Return tokensLinea(idx + 1).ID
        End If
        Return TokenID.TE_NONE

    End Function



    ' ============================================================
    ' PARSE DE UNA SENTENCIA
    ' ============================================================
    Private Sub ParseStatement(writer As StreamWriter)
        Dim tok = tokensLinea(idx)
        Dim tipo As TokenID = Tid()
        Dim valor As String = TokenValor().ToUpperInvariant()

        If tipo = TokenID.TE_EOL Then
            NextToken()
            Exit Sub
        End If

        If tok.IsStatementStart() Then
            Select Case tok.ID
                Case TokenID.TK_LET : ParseLet(writer) : Exit Sub
                Case TokenID.TK_PRINT : ParsePrint(writer) : Exit Sub
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

            ErrorSintaxis(writer, TokenColumna, "Comando no reconocido: " & valor)
            Exit Sub
        End If

        ' LET implícito si es opcional
        If tipo = TokenID.TE_IDENT AndAlso PeekTid() = TokenID.TOP_EQ Then
            ErrorSintaxis(writer, 0, "Sentencia no válida ¿Falta el LET?")
            Exit Sub
        End If

        ErrorSintaxis(writer, 0, "Sentencia no válida")
    End Sub


    ' ============================================================
    ' SENTENCIAS
    ' ============================================================
    Private Sub ParseREM(Writer As StreamWriter)
        NextToken() ' consumir REM

        Dim comentario As String = ""

        If Tid() = TokenID.TE_STRING Then
            comentario = TokenValor()
            NextToken()
        End If

        GuardarIRP(Writer, TokenID.TK_REM, comentario)

        ' consumir hasta EOL por seguridad
        While Tid() <> TokenID.TE_EOL AndAlso idx < tokensLinea.Count
            NextToken()
        End While
    End Sub

    Private Sub ParseReturn(writer As StreamWriter)
        GuardarIRP(writer, TokenID.TK_RETURN)
        NextToken()
    End Sub

    Private Sub ParseStop(writer As StreamWriter)
        GuardarIRP(writer, TokenID.TK_STOP)
        NextToken()
    End Sub

    Private Sub ParseClear(writer As StreamWriter)
        'CLEAR        ; borra variables
        'CLEAR n      ; borra variables y fija RAMTOP = n

        ' Consumimos la palabra clave CLEAR
        NextToken()

        Dim expr As String = ""

        ' Si no estamos al final de la sentencia, hay argumento
        If Tid() <> TokenID.TE_EOL AndAlso Tid() <> TokenID.TS_DOSPUNTOS Then
            ' Parsear expresión a texto
            If Not ParseExprTexto(writer, False, expr) Then
                Return
            End If
        End If

        ' Generamos la expresión para el CLEAR, según sea solo variables o ramtopAhoe
        If expr <> "" Then
            GuardarIRP(writer, TokenID.TK_CLEAR_RAM)
        Else
            GuardarIRP(writer, TokenID.TK_CLEAR)
        End If
    End Sub

    Private Sub ParseDim(writer As StreamWriter)

        ' Consumir DIM
        NextToken()

        ' Esperar identificador (nombre del array)
        If Tid() <> TokenID.TE_IDENT Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba nombre de array en DIM")
            Exit Sub
        End If

        Dim arrayName As String = TokenValor()
        NextToken()

        ' Debe seguir '('
        If Tid() <> TokenID.TS_PAR_ABIERTO Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba '(' en DIM")
            Exit Sub
        End If

        NextToken() ' consumir '('

        ' Parsear lista de dimensiones como texto
        Dim dims As New List(Of String)

        Do
            Dim expr As String = Nothing

            If Not ParseExprTexto(writer, False, expr, ",)") Then
                Exit Sub
            End If

            dims.Add(expr)

            ' Caso 1: coma → siguiente dimensión
            If Tid() = TokenID.TS_COMMA Then
                NextToken()
                Continue Do
            End If

            ' Caso 2: cierre de paréntesis → fin de DIM
            If Tid() = TokenID.TS_PAR_CERRADO Then
                Exit Do
            End If

            ' Caso 3: error sintáctico
            ErrorSintaxis(writer, TokenColumna, "Se esperaba ',' o ')' en DIM")
            Exit Sub
        Loop

        ' Consumir ')'
        If Tid() <> TokenID.TS_PAR_ABIERTO Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba ')' en DIM")
            Exit Sub
        End If

        NextToken()

        ' Emitir IRP
        GuardarIRP(writer, TokenID.TK_DIM, String.Join(",", dims))

    End Sub

    Private Sub ParseLet(writer As StreamWriter)

        ' Consumir LET solo si es explícito
        If tokensLinea(idx).ID = TokenID.TK_LET Then
            NextToken()
        End If


        ' Parsear l‑value (variable o elemento de array)
        Dim lvalue As String = Nothing
        If Not ParseLValue(writer, lvalue) Then
            Exit Sub
        End If

        ' Esperar '='
        If Tid() <> TokenID.TOP_EQ Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba '='")
            Exit Sub
        End If

        ' Consumir '='
        NextToken()

        ' Parsear expresión a texto
        Dim expr As String = Nothing
        If Not ParseExprTexto(writer, False, expr) Then
            Return
        End If

        ' Emitir IRP
        GuardarIRP(writer, TokenID.TK_LET, $"{lvalue} = {expr}")

    End Sub

    Private Function ParseLValue(writer As StreamWriter, ByRef lvalue As String) As Boolean

        ' Debe empezar por identificador
        If Tid() <> TokenID.TE_IDENT OrElse
       Not Char.IsLetter(TokenValor()(0)) Then
            ErrorSintaxis(writer, TokenColumna, "Nombre de variable inválido")
            Return False
        End If

        Dim sb As New StringBuilder()
        sb.Append(TokenValor())
        NextToken()

        ' ¿Acceso a array?
        If Tid() = TokenID.TS_PAR_ABIERTO Then
            sb.Append("("c)
            NextToken()

            Do
                Dim expr As String = Nothing

                ' Expresión de índice: se detiene en , o )
                If Not ParseExprTexto(writer, False, expr, ",)") Then
                    Return False
                End If

                sb.Append(expr)

                If Tid() = TokenID.TS_COMMA Then
                    sb.Append(",")
                    NextToken()
                    Continue Do
                End If

                Exit Do
            Loop

            If Tid() <> TokenID.TS_PAR_CERRADO Then
                ErrorSintaxis(writer, TokenColumna, "Se esperaba ')'")
                Return False
            End If

            sb.Append(")"c)
            NextToken()
        End If

        lvalue = sb.ToString()
        Return True

    End Function

    Private Sub ParseRandomize(writer As StreamWriter)

        NextToken() ' consumir RANDOMIZE

        ' RANDOMIZE solo
        If Tid() = TokenID.TE_EOL OrElse Tid() = TokenID.TS_DOSPUNTOS Then
            GuardarIRP(writer, TokenID.TK_RANDOMIZE)
            Exit Sub
        End If

        ' RANDOMIZE USR <expr>
        If Tid() = TokenID.TK_RANDOMIZE AndAlso TokenValor() = "USR" Then
            NextToken() ' consumir USR

            Dim expr As String = Nothing
            If Not ParseExprTexto(writer, False, expr) Then Exit Sub

            GuardarIRP(writer, TokenID.TK_RANDOMIZE_USR, $"{expr}")
            Exit Sub
        End If

        ' RANDOMIZE <expr>
        Dim seed As String = Nothing
        If Not ParseExprTexto(writer, False, seed) Then Exit Sub

        GuardarIRP(writer, TokenID.TK_RANDOMIZE, $"{seed}")

    End Sub

    Private Sub ParsePrint(writer As StreamWriter)

        ' Consumir PRINT
        NextToken()


        Dim sb As New StringBuilder()

        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TE_EOL AndAlso
          Tid() <> TokenID.TS_DOSPUNTOS

            Dim tok = tokensLinea(idx)

            Select Case tok.ID

                Case TokenID.TE_STRING
                    sb.Append(Constantes.C_COMILLAS)
                    sb.Append(tok.Value)
                    sb.Append(Constantes.C_COMILLAS)

                Case TokenID.TS_PUNTOYCOMA
                    sb.Append(";")

                Case TokenID.TS_COMMA
                    sb.Append(",")

                Case Else
                    ' Todo lo demás permitido en expresiones PRINT:
                    ' - procedimientos (INK, PAPER, etc.)
                    ' - funciones (TAB, AT, CHR$, etc.)
                    ' - identificadores, números, operadores

                    If Not tok.CanAppearInPrint() Then
                        ErrorSintaxis(writer, tok.Col, $"'{tok.Value}' no es válido dentro de PRINT")
                        Exit Sub
                    End If
                    sb.Append(tok.Value)


            End Select

            sb.Append(" ")
            NextToken()
        End While

        GuardarIRP(writer, TokenID.TK_PRINT, sb.ToString().Trim())

    End Sub

    Private Sub ParseIf(writer As StreamWriter)

        ' Consumir IF
        NextToken()

        ' Parsear condición
        Dim condicion As String = Nothing
        If Not ParseExprTexto(writer, False, condicion) Then
            Return
        End If

        ' Debe venir THEN
        If Tid() <> TokenID.TK_THEN Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba THEN en IF")
            Exit Sub
        End If

        ' Consumir THEN
        NextToken()

        ' ✅ Emitir SOLO el IF, como sentencia independiente
        GuardarIRP(writer, TokenID.TK_IF, $"{condicion}")

        ' ❗ MUY IMPORTANTE:
        ' NO consumir aquí el cuerpo
        ' El cuerpo lo parseará el bucle general de sentencias,
        ' exactamente igual que si no estuviéramos en un IF.
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

        ErrorSintaxis(writer, TokenColumna, "Se esperaba TO o SUB tras GO")

    End Sub

    Private Sub ParseGoto(writer As StreamWriter)
        NextToken()
        Dim ln As String = TokenValor()
        NextToken()
        GuardarIRP(writer, TokenID.TK_GOTO, $"{ln}")
    End Sub


    Private Sub ParseGosub(writer As StreamWriter)
        NextToken()
        Dim ln As String = TokenValor()
        NextToken()
        GuardarIRP(writer, TokenID.TK_GOSUB, $"{ln}")
    End Sub

    ' ------------------------------------------------------------
    ' FOR I = expr TO expr [STEP expr]
    ' ------------------------------------------------------------
    Private Sub ParseFor(writer As StreamWriter)
        Dim aux As String = Nothing

        ' Consumir FOR
        NextToken()

        ' Variable de control
        If Tid() <> TokenID.TE_IDENT Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba variable en FOR")
            Exit Sub
        End If

        Dim varName As String = TokenValor()
        NextToken()

        ' =
        If Not IsEqual(Tid()) Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba '=' en FOR")
            Exit Sub
        End If
        NextToken()

        Dim sb As New StringBuilder()
        sb.Append(varName)
        sb.Append(" = ")

        ' Expr inicial
        If Not ParseExprTexto(writer, True, aux) Then
            Return
        End If
        sb.Append(aux)

        ' TO
        If Tid() <> TokenID.TK_TO Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba TO en FOR")
            Exit Sub
        End If
        sb.Append(" TO ")
        NextToken()

        ' Expr final
        If Not ParseExprTexto(writer, True, aux) Then
            Return
        End If
        sb.Append(aux)

        ' STEP opcional
        If Tid() = TokenID.TK_STEP Then
            sb.Append(" STEP ")
            NextToken()
            If Not ParseExprTexto(writer, True, aux) Then
                Return
            End If
            sb.Append(aux)
        End If

        GuardarIRP(writer, TokenID.TK_FOR, sb.ToString())

    End Sub

    ' ------------------------------------------------------------
    ' NEXT [I]
    ' ------------------------------------------------------------
    Private Sub ParseNext(writer As StreamWriter)

        ' Consumir NEXT
        NextToken()

        Dim sb As String = ""
        ' Variable opcional
        If Tid() = TokenID.TE_IDENT Then
            sb = TokenValor()
            NextToken()
        End If
        GuardarIRP(writer, TokenID.TK_NEXT, sb.ToString())

    End Sub

    Private Sub ParseRestore(writer As StreamWriter)

        ' Consumir RESTORE
        NextToken()

        ' ¿Hay número de línea?
        If Tid() = TokenID.TE_NUMBER Then
            Dim ln As String = TokenValor()
            NextToken()
            GuardarIRP(writer, TokenID.TK_RESTORE, $"{ln}")
        Else
            GuardarIRP(writer, TokenID.TK_RESTORE)
        End If

    End Sub

    Private Sub ParseRead(writer As StreamWriter)

        ' Consumir READ
        NextToken()

        Dim sb As New StringBuilder()
        While idx < tokensLinea.Count AndAlso
                 Tid() <> TokenID.TE_EOL AndAlso
                 Tid() <> TokenID.TS_DOSPUNTOS

            Select Case Tid()

                Case TokenID.TE_IDENT
                    sb.Append(TokenValor())

                Case TokenID.TS_COMMA
                    sb.Append(" , ")

                Case Else
                    ErrorSintaxis(writer, TokenColumna, "Sintaxis inválida en READ")
                    Exit Sub

            End Select

            NextToken()
        End While

        GuardarIRP(writer, TokenID.TK_READ, sb.ToString())

    End Sub

    Private Sub ParseData(writer As StreamWriter)

        ' Consumir DATA
        NextToken()

        Dim sb As New StringBuilder()
        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TE_EOL

            Select Case Tid()

                Case TokenID.TE_NUMBER
                    sb.Append(TokenValor())

                Case TokenID.TE_STRING
                    sb.Append(Constantes.C_COMILLAS)
                    sb.Append(TokenValor())
                    sb.Append(Constantes.C_COMILLAS)

                Case TokenID.TS_COMMA
                    sb.Append(" , ")

                Case Else
                    ErrorSintaxis(writer, TokenColumna, "Sintaxis inválida en DATA")
                    Exit Sub

            End Select

            NextToken()
        End While

        GuardarIRP(writer, TokenID.TK_DATA, sb.ToString())

    End Sub

    Private Sub ParseBeep(writer As StreamWriter)

        NextToken() ' consumir BEEP

        Dim sb As New StringBuilder()
        Dim expr As String = Nothing

        ' 1er parámetro: permitir coma EXTERIOR
        If Not ParseExprTexto(writer, True, expr, ",") Then Return
        sb.Append(expr)

        ' Coma obligatoria entre parámetros
        If Tid() <> TokenID.TS_COMMA Then
            ErrorSintaxis(writer, TokenColumna, "Se esperaba ',' en BEEP")
            Return
        End If

        sb.Append(" , ")
        NextToken()

        ' 2º parámetro
        If Not ParseExprTexto(writer, False, expr) Then Return
        sb.Append(expr)

        GuardarIRP(writer, TokenID.TK_BEEP, sb.ToString())

    End Sub

    Private Sub ParseRun(writer As StreamWriter)
        NextToken()
        If Tid() = TokenID.TE_EOL OrElse Tid() = TokenID.TS_DOSPUNTOS Then
            GuardarIRP(writer, TokenID.TK_RUN)
        Else
            Dim expr As String = Nothing
            If Not ParseExprTexto(writer, True, expr) Then Return
            GuardarIRP(writer, TokenID.TK_RUN, $"{expr}")
        End If
    End Sub

    Private Sub ParseList(writer As StreamWriter)
        NextToken()
        If Tid() = TokenID.TE_EOL OrElse Tid() = TokenID.TS_DOSPUNTOS Then
            GuardarIRP(writer, TokenID.TK_LIST)
        Else
            Dim expr As String = Nothing
            If Not ParseExprTexto(writer, True, expr) Then Return
            GuardarIRP(writer, TokenID.TK_LIST, $"{expr}")
        End If
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
        NextToken()
        Dim expr As String = Nothing
        If Not ParseExprTexto(writer, True, expr) Then Return
        GuardarIRP(writer, id, $"{expr}")
    End Sub

    Private Sub ParseSimpleStmt(writer As StreamWriter, id As TokenID)
        NextToken()
        GuardarIRP(writer, id)
    End Sub

    Private Sub ParseUnaryStmt(writer As StreamWriter, id As TokenID)

        NextToken()

        Dim expr As String = Nothing
        If Not ParseExprTexto(writer, True, expr) Then Return

        GuardarIRP(writer, id, $"{expr}")

    End Sub

    Private Sub ParseBinaryStmt(writer As StreamWriter, id As TokenID)

        NextToken() ' consumir POKE u OUT

        Dim sb As New StringBuilder()
        Dim expr As String = Nothing

        ' Primer argumento
        If Not ParseExprTexto(writer, False, expr) Then Return
        sb.Append(expr)

        ' Coma obligatoria
        If Tid() <> TokenID.TS_COMMA Then
            ErrorSintaxis(writer, TokenColumna, $"Se esperaba ',' en {id}")
            Return
        End If
        sb.Append(" , ")
        NextToken()

        ' Segundo argumento
        If Not ParseExprTexto(writer, False, expr) Then Return
        sb.Append(expr)

        GuardarIRP(writer, id, sb.ToString())

    End Sub

    ' ============================================================
    ' PARSE DE EXPRESIONES (FORMA TEXTO, SE RESOLVERÁ EN SEMANTIC)
    ' ============================================================


    Private Function ParseExprTexto(writer As StreamWriter,
                                    permiteComaExterior As Boolean,
                                    ByRef resultado As String,
                                    Optional stopTokens As String = "") As Boolean

        Dim sb As New StringBuilder()
        Dim nivelParentesis As Integer = 0

        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TE_EOL AndAlso
          Tid() <> TokenID.TS_DOSPUNTOS AndAlso
          Not IsControlKeyword(Tid())

            ' 🔹 NUEVO: parada por tokens externos configurables

            If nivelParentesis = 0 AndAlso TokenEsStopChar(stopTokens) Then
                Exit While   ' NO consumir el token
            End If


            ' ❌ Punto y coma NO permitido en expresiones
            If Tid() = TokenID.TS_PUNTOYCOMA Then
                ErrorSintaxis(writer, TokenColumna, "Expresión no válida")
                Return False
            End If

            ' ❌ Coma no permitida a nivel superior
            If Tid() = TokenID.TS_COMMA AndAlso
           Not permiteComaExterior AndAlso
           nivelParentesis = 0 Then
                Exit While   ' FIN de la expresión, NO error
            End If

            ' Control de paréntesis
            If Tid() = TokenID.TS_PAR_ABIERTO Then
                nivelParentesis += 1
            ElseIf Tid() = TokenID.TS_PAR_CERRADO Then
                nivelParentesis -= 1
            End If

            ' Construcción textual

            Select Case Tid()

                ' LITERALES
                Case TokenID.TE_STRING
                    sb.Append(Constantes.C_COMILLAS)
                    sb.Append(TokenValor())
                    sb.Append(Constantes.C_COMILLAS)

                Case TokenID.TE_NUMBER
                    sb.Append(TokenValor())

                Case TokenID.TE_IDENT
                    sb.Append(TokenValor())

                ' OPERADORES RELACIONALES / ASIGNACIÓN
                Case TokenID.TOP_EQ : sb.Append("=")
                Case TokenID.TOP_NE : sb.Append("<>")
                Case TokenID.TOP_LT : sb.Append("<")
                Case TokenID.TOP_GT : sb.Append(">")
                Case TokenID.TOP_LE : sb.Append("<=")
                Case TokenID.TOP_GE : sb.Append(">=")

                ' OPERADORES ARITMÉTICOS
                Case TokenID.TOP_PLUS : sb.Append("+")
                Case TokenID.TOP_MINUS : sb.Append("-")
                Case TokenID.TOP_MUL : sb.Append("*")
                Case TokenID.TOP_DIV : sb.Append("/")
                Case TokenID.TOP_POW : sb.Append("^")

                ' OPERADORES LOGICOS
                Case TokenID.TK_AND : sb.Append("AND")
                Case TokenID.TK_NOT : sb.Append("NOT")
                Case TokenID.TK_OR : sb.Append("OR")

                ' ESTRUCTURA
                Case TokenID.TS_PAR_ABIERTO : sb.Append("(")
                Case TokenID.TS_PAR_CERRADO : sb.Append(")")
                Case TokenID.TS_COMMA : sb.Append(",")

                Case Else
                    ' No debería llegar nada importante aquí
            End Select


            sb.Append(" ") 'Revisar si es No necesario
            NextToken()
        End While

        If nivelParentesis <> 0 Then
            ErrorSintaxis(writer, TokenColumna, "Paréntesis desequilibrados en expresión")
            Return False
        End If

        resultado = sb.ToString().Trim()
        Return True

    End Function

    ' ----------------------------------------------------------
    ' Helpers
    ' ----------------------------------------------------------
    Private Function TokenEsStopChar(stopChars As String) As Boolean
        If stopChars = "" Then Return False

        Select Case Tid()
            Case TokenID.TS_COMMA : Return stopChars.Contains(","c)
            Case TokenID.TS_PAR_CERRADO : Return stopChars.Contains(")"c)
            Case TokenID.TS_PAR_ABIERTO : Return stopChars.Contains("("c)
            Case TokenID.TS_DOSPUNTOS : Return stopChars.Contains(":"c)
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
    Private Sub ErrorSintaxis(writer As StreamWriter, columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If

        MostrarError(opts, writer, NroLineaFichero, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)

        ' EVITAR BUCLES INFINITOS, IR AL FIN DE LA LINEA
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TE_EOL
            NextToken()
        End While
    End Sub

    Private Sub GuardarIRP(writer As StreamWriter, ID As TokenID)
        Dim token As New Token(ID, "")
        GuardarIRP(writer, token)
    End Sub

    Private Sub GuardarIRP(writer As StreamWriter, ID As TokenID, valor As String)
        Dim token As New Token(ID, valor)
        GuardarIRP(writer, token)
    End Sub

    Private Sub GuardarIRP(writer As StreamWriter, tok As Token)
        Dim idNum As Integer = CInt(tok.ID)
        Dim idName As String = tok.ID.ToString()
        Dim value As String = If(tok.Value IsNot Nothing, tok.Value, "")
        Dim Linea As String = $"{idNum} {value}"
        If opts.Verbose Then
            If Len(Linea) < 49 Then
                Linea &= Space(50 - Len(Linea)) & $" ; {idName}"
                GuardarTextoIRP(writer, Linea)
            Else
                GuardarTextoIRP(writer, Linea)
                Linea = Space(50) & $" ; {idName}"
                GuardarTextoIRP(writer, Linea)
            End If
        End If
    End Sub

    Private Sub GuardarTextoIRP(writer As StreamWriter, linea As String)
        writer.WriteLine(linea)
        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If

    End Sub

End Module