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

        ' Consumimos la palabra clave CLEAR
        NextToken()

        Dim expr As String = ""

        ' Si no estamos al final de la sentencia, hay argumento
        If Tid() <> TokenID.TCO_EOL AndAlso Tid() <> TokenID.TSP_DOSPUNTOS Then
            ' Parsear expresión a texto
            If Not ParseExprTexto(writer, False, expr) Then
                Return
            End If
        End If

        ' Generamos la expresión para el CLEAR, según sea solo variables o ramtopAhoe
        If expr <> "" Then
            GuardarIRP_Token(writer, TokenID.TK_CLEAR_RAM)
        Else
            GuardarIRP_Token(writer, TokenID.TK_CLEAR)
        End If
    End Sub

    Private Sub ParseDim(writer As StreamWriter)

        ' Consumir DIM
        NextToken()

        ' Esperar identificador (nombre del array)
        If Tid() <> TokenID.TES_IDENT Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba nombre de array en DIM")
            Exit Sub
        End If

        Dim arrayName As String = TokenValor()
        NextToken()

        ' Debe seguir '('
        If Tid() <> TokenID.TSP_PAR_ABIERTO Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba '(' en DIM")
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
            If Tid() = TokenID.TSP_COMA Then
                NextToken()
                Continue Do
            End If

            ' Caso 2: cierre de paréntesis → fin de DIM
            If Tid() = TokenID.TSP_PAR_CERRADO Then
                Exit Do
            End If

            ' Caso 3: error sintáctico
            ErrorSintactico(writer, TokenColumna, "Se esperaba ',' o ')' en DIM")
            Exit Sub
        Loop

        ' Consumir ')'
        If Tid() <> TokenID.TSP_PAR_ABIERTO Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba ')' en DIM")
            Exit Sub
        End If

        NextToken()

        ' Emitir IRP
        GuardarIRP_Token_Valor(writer, TokenID.TK_DIM, String.Join(",", dims))

    End Sub

    Private Sub ParseLet(writer As StreamWriter)

        ' Consumir LET solo si es explícito
        If tokensLinea(idx).ID = TokenID.TK_LET Then
            NextToken()
        End If


        ' Parte izquierda de la asignación
        Dim lvalue As String = Nothing
        If Not ParseLValue(writer, lvalue) Then
            Exit Sub
        End If

        ' Esperar '='
        If Tid() <> TokenID.TOP_EQ Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba '=' en LET")
            Exit Sub
        End If

        ' Consumir '='
        NextToken()

        ' Parte Derecha de la asignación
        Dim rvalue As String = Nothing
        If Not ParseExprTexto(writer, False, rvalue) Then
            Return
        End If

        ' Emitir IRP
        GuardarIRP_Token_Valor(writer, TokenID.TK_LET, $"{lvalue} = {rvalue}")

    End Sub

    Private Function ParseLValue(writer As StreamWriter, ByRef lvalue As String) As Boolean

        ' Debe empezar por identificador
        If Tid() <> TokenID.TES_IDENT OrElse
       Not Char.IsLetter(TokenValor()(0)) Then
            ErrorSintactico(writer, TokenColumna, "Nombre de variable inválido")
            Return False
        End If

        Dim sb As New StringBuilder()
        sb.Append(TokenValor())
        NextToken()

        ' ✅ NUEVO: acceso a array mediante TES_GREXPR
        If Tid() = TokenID.TES_GREXPR Then
            '+++sb.Append("("c)
            sb.Append(TokenValor())
            '+++sb.Append(")"c)
            NextToken()
        End If

        lvalue = sb.ToString()
        Return True
    End Function


    Private Sub ParseRandomize(writer As StreamWriter)

        NextToken() ' consumir RANDOMIZE

        ' RANDOMIZE solo
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_Token(writer, TokenID.TK_RANDOMIZE)
            Exit Sub
        End If

        ' RANDOMIZE USR <expr>
        If Tid() = TokenID.TK_RANDOMIZE AndAlso TokenValor() = "USR" Then
            NextToken() ' consumir USR

            Dim expr As String = Nothing
            If Not ParseExprTexto(writer, False, expr) Then Exit Sub

            GuardarIRP_Token_Valor(writer, TokenID.TK_RANDOMIZE_USR, $"{expr}")
            Exit Sub
        End If

        ' RANDOMIZE <expr>
        Dim seed As String = Nothing
        If Not ParseExprTexto(writer, False, seed) Then Exit Sub

        GuardarIRP_Token_Valor(writer, TokenID.TK_RANDOMIZE, $"{seed}")

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

        Select Case id

        ' ===== Literales =====
            Case TokenID.TES_STRING
                ' El valor viene sin comillas, se añaden aquí
                Return valor

            Case TokenID.TES_NUMBER
                Return valor

        ' ===== Expresión agrupada =====
            Case TokenID.TES_GREXPR
                ' IMPORTANTE:
                ' El valor ya contiene TODO, incluidas comas internas.
                ' Se añaden los paréntesis aquí.
                Return valor

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
        Dim PrintItem As New PrintItem(TokenID.TCO_UNKNOWN)

        NextToken() ' consumir AT

        Dim exprX As String = Nothing
        Dim exprY As String = Nothing

        ' Primera expresión (obligatoria, hasta coma)
        If Not ParseExprTexto(writer, False, exprX, ",") Then
            Return PrintItem
        End If

        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba ',' en AT")
            Return PrintItem
        End If

        NextToken() ' consumir coma

        ' Segunda expresión (hasta ; , : o EOL)
        If Not ParseExprTexto(writer, False, exprY, ",;") Then
            Return PrintItem
        End If

        ' Construir el valor FINAL del AT
        Dim valorAT As String = exprX & "," & exprY

        ' Crear PrintItem (separator se decide FUERA)
        PrintItem.ID = TokenID.TK_AT
        PrintItem.Value = valorAT
        PrintItem.Separator = PrintSeparator.N

        Return PrintItem

    End Function









    'Private Sub ParsePrint1(writer As StreamWriter)

    '    NextToken() ' consumir PRINT
    '    Dim sbActual As New StringBuilder()

    '    While idx < tokensLinea.Count AndAlso
    '          Tid() <> TokenID.TCO_EOL AndAlso
    '          Tid() <> TokenID.TSP_DOSPUNTOS

    '        Dim tok = tokensLinea(idx)

    '        ' ===============================
    '        ' MODIFICADOR AT (CASO ESPECIAL)
    '        ' ===============================
    '        If tok.ID = TokenID.TK_AT Then
    '            parseAT(writer, tok, sbActual)
    '            Continue While
    '        End If

    '        ' ===============================
    '        ' MODIFICADOR TAB (CASO ESPECIAL)
    '        ' ===============================
    '        If tok.ID = TokenID.TK_TAB Then
    '            parseTAB(writer, tok, sbActual)
    '            Continue While
    '        End If

    '        ' ==============================================
    '        ' MODIFICADORES PRINT (TAB, INK, PAPER, etc.)
    '        ' ==============================================
    '        If tok.EsModificadorPrint() Then
    '            ' Cerrar PRINT previo si existe
    '            SavePrint(writer, sbActual, False)

    '            ' Nombre del modificador
    '            sbActual.Append(tok.Mnemonic)
    '            NextToken()

    '            ' Argumento (una expresión simple o GREXPR)
    '            If Tid() <> TokenID.TCO_EOL AndAlso Tid() <> TokenID.TSP_DOSPUNTOS Then
    '                sbActual.Append(" ")

    '                Select Case Tid()
    '                    Case TokenID.TES_GREXPR
    '                        sbActual.Append("("c)
    '                        sbActual.Append(TokenValor())
    '                        sbActual.Append(")"c)
    '                        NextToken()

    '                    Case Else
    '                        sbActual.Append(TokenValor())
    '                        NextToken()
    '                End Select
    '            End If

    '            ' ✅ CONSUMIR separador final si existe
    '            If Tid() = TokenID.TSP_PUNTOYCOMA Then
    '                sbActual.Append(";")
    '                NextToken()
    '            ElseIf Tid() = TokenID.TSP_COMA Then
    '                sbActual.Append(",")
    '                NextToken()
    '            End If

    '            SavePrint(writer, sbActual, False)
    '            Continue While
    '        End If

    '        ' --- Separadores PRINT ---
    '        If tok.ID = TokenID.TSP_PUNTOYCOMA OrElse tok.ID = TokenID.TSP_COMA Then
    '            ' ✅ CONSUMIR separador final si existe
    '            If Tid() = TokenID.TSP_PUNTOYCOMA Then
    '                sbActual.Append(";")
    '                NextToken()
    '            ElseIf Tid() = TokenID.TSP_COMA Then
    '                sbActual.Append(",")
    '                NextToken()
    '            End If

    '            SavePrint(writer, sbActual, False)
    '            NextToken()
    '            Continue While
    '        End If

    '        ' --- Token imprimible normal ---
    '        ConsumirHastaSeparador(writer, sbActual)

    '        ' aquí el elemento YA está completo
    '        ' ahora miramos si hay separador
    '        If Tid() = TokenID.TSP_PUNTOYCOMA Then
    '            sbActual.Append(";")
    '            NextToken()
    '        ElseIf Tid() = TokenID.TSP_COMA Then
    '            sbActual.Append(",")
    '            NextToken()
    '        End If

    '        SavePrint(writer, sbActual, False)

    '    End While

    '    ' Cerrar PRINT previo si existe
    '    SavePrint(writer, sbActual, False)


    'End Sub

    '' ================================================
    '' CONSUMIR AT y TAB (CASOS ESPECIALES DEL PRINT)
    '' ================================================
    'Private Sub parseAT(writer As StreamWriter, tok As Token, ByVal sbactual As StringBuilder)
    '    ' Cerrar PRINT previo si existe
    '    SavePrint(writer, sbactual, False)

    '    NextToken() ' consumir AT

    '    Dim exprX As String = Nothing
    '    Dim exprY As String = Nothing


    '    ' Primera expresión (obligatoria, termina en coma)
    '    If Not ParseExprTexto(writer, False, exprX, ",") Then Exit Sub

    '    If Tid() <> TokenID.TSP_COMA Then
    '        ErrorSintactico(writer, TokenColumna, "Se esperaba ',' en AT")
    '        Exit Sub
    '    End If

    '    NextToken() ' consumir coma


    '    ' Segunda expresión (termina en ; , : o EOL)
    '    If Not ParseExprTexto(writer, False, exprY, ",;") Then Exit Sub


    '    Dim sbAT As New StringBuilder()
    '    sbAT.Append("AT ")
    '    sbAT.Append(exprX)
    '    sbAT.Append(",")
    '    sbAT.Append(exprY)
    '    GuardarIRP(writer, TokenID.TK_AT, sbAT.ToString())

    '    ' Separador final (si existe)
    '    If Tid() = TokenID.TSP_PUNTOYCOMA Then
    '        ' ; no tiene efecto en SuperBASIC → se ignora
    '        NextToken()

    '    ElseIf Tid() = TokenID.TSP_COMA Then
    '        ' Caso raro: coma tras AT

    '        ' , debe afectar al siguiente PRINT, pero seguramente es un error
    '        WarningSintactico(writer, tok.Col, $"'Posible error: coma tras un AT")
    '        GuardarIRP(writer, TokenID.TK_PRINT, ",")
    '        NextToken()
    '    End If


    'End Sub


    'Private Sub parseTAB(writer As StreamWriter, tok As Token, ByVal sbactual As StringBuilder)
    '    ' Cerrar PRINT previo si existe
    '    SavePrint(writer, sbactual, False)

    '    NextToken() ' consumir TAB
    '    sbactual.Append("TO ")

    '    ' ahora TAB consume un token más con el nro de columna TES_NUMBER
    '    sbactual.Append(TokenValor())
    '    NextToken()

    '    '  SavePrint(writer, sbactual, False)

    '    Dim sepTrasAT As Char = ChrW(0)

    '    If Tid() = TokenID.TSP_PUNTOYCOMA Then
    '        sbactual.Append(";")
    '        SavePrint(writer, sbactual, False)
    '        NextToken()

    '    ElseIf Tid() = TokenID.TSP_COMA Then
    '        ' , debe afectar al siguiente PRINT, pero seguramente es un error
    '        WarningSintactico(writer, tok.Col, $"'Posible error: coma tras un TAB")

    '        sbactual.Append(",")
    '        SavePrint(writer, sbactual, False)
    '        NextToken()
    '    End If

    'End Sub


    'Private Sub ConsumirHastaSeparador(writer As StreamWriter, sb As StringBuilder)
    '    While idx < tokensLinea.Count AndAlso
    '      Tid() <> TokenID.TCO_EOL AndAlso
    '      Tid() <> TokenID.TSP_DOSPUNTOS AndAlso
    '      Tid() <> TokenID.TSP_PUNTOYCOMA AndAlso
    '      Tid() <> TokenID.TSP_COMA

    '        Dim tok = tokensLinea(idx)

    '        Select Case tok.ID
    '            Case TokenID.TES_STRING, TokenID.TES_GREXPR
    '                sb.Append(tok.GetValor)
    '                '    sb.Append($"{Constantes.C_COMILLAS}{tok.Value}{Constantes.C_COMILLAS}")

    '                'Case TokenID.TES_GREXPR
    '                '    sb.Append($"{Constantes.C_PAR_APE}{tok.Value}{Constantes.C_PAR_CIE}")

    '            Case Else
    '                If Not tok.CanAppearInPrint() Then
    '                    ErrorSintactico(writer, tok.Col, $"'{tok.Value}' no es válido dentro de PRINT")
    '                    Exit Sub
    '                End If
    '                sb.Append(tok.GetValor)
    '        End Select

    '        sb.Append(" ")
    '        NextToken()
    '    End While
    'End Sub


    'Private Sub SavePrint(writer As StreamWriter, ByRef sb As StringBuilder, esAT As Boolean)
    '    Dim aux As String = sb.ToString.Trim

    '    If aux.Length > 0 Then
    '        GuardarIRP(writer, If(esAT, TokenID.TK_AT, TokenID.TK_PRINT), aux)
    '    End If
    '    sb.Clear()
    'End Sub

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
            ErrorSintactico(writer, TokenColumna, "Se esperaba THEN en IF")
            Exit Sub
        End If

        ' Consumir THEN
        NextToken()

        ' ✅ Emitir SOLO el IF, como sentencia independiente
        GuardarIRP_Token_Valor(writer, TokenID.TK_IF, $"{condicion}")

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
        Dim aux As String = Nothing

        ' Consumir FOR
        NextToken()

        ' Variable de control
        If Tid() <> TokenID.TES_IDENT Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba variable en FOR")
            Exit Sub
        End If

        Dim varName As String = TokenValor()
        NextToken()

        ' =
        If Not IsEqual(Tid()) Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba '=' en FOR")
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
            ErrorSintactico(writer, TokenColumna, "Se esperaba TO en FOR")
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

        GuardarIRP_Token_Valor(writer, TokenID.TK_FOR, sb.ToString())

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

        Dim sb As New StringBuilder()
        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TCO_EOL

            Select Case Tid()

                Case TokenID.TES_NUMBER, TokenID.TES_STRING, TokenID.TES_GREXPR
                    sb.Append(TokenValor())

                'Case TokenID.TES_STRING
                '    sb.Append($"{Constantes.C_COMILLAS}{TokenValor()}{Constantes.C_COMILLAS}")

                'Case TokenID.TES_GREXPR
                '    sb.Append($"{Constantes.C_PAR_APE}{TokenValor()}{Constantes.C_PAR_CIE}")

                Case TokenID.TSP_COMA
                    sb.Append(" , ")

                Case Else
                    ErrorSintactico(writer, TokenColumna, "Sintaxis inválida en DATA")
                    Exit Sub

            End Select

            NextToken()
        End While

        GuardarIRP_Token_Valor(writer, TokenID.TK_DATA, sb.ToString())

    End Sub

    Private Sub ParseBeep(writer As StreamWriter)

        NextToken() ' consumir BEEP

        Dim sb As New StringBuilder()
        Dim expr As String = Nothing

        ' 1er parámetro: permitir coma EXTERIOR
        If Not ParseExprTexto(writer, True, expr, ",") Then Return
        sb.Append(expr)

        ' Coma obligatoria entre parámetros
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(writer, TokenColumna, "Se esperaba ',' en BEEP")
            Return
        End If

        sb.Append(" , ")
        NextToken()

        ' 2º parámetro
        If Not ParseExprTexto(writer, False, expr) Then Return
        sb.Append(expr)

        GuardarIRP_Token_Valor(writer, TokenID.TK_BEEP, sb.ToString())

    End Sub

    Private Sub ParseRun(writer As StreamWriter)
        NextToken()
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_Token(writer, TokenID.TK_RUN)
        Else
            Dim expr As String = Nothing
            If Not ParseExprTexto(writer, True, expr) Then Return
            GuardarIRP_Token_Valor(writer, TokenID.TK_RUN, $"{expr}")
        End If
    End Sub

    Private Sub ParseList(writer As StreamWriter)
        NextToken()
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_Token(writer, TokenID.TK_LIST)
        Else
            Dim expr As String = Nothing
            If Not ParseExprTexto(writer, True, expr) Then Return
            GuardarIRP_Token_Valor(writer, TokenID.TK_LIST, $"{expr}")
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
        GuardarIRP_Token_Valor(writer, id, $"{expr}")
    End Sub

    Private Sub ParseSimpleStmt(writer As StreamWriter, id As TokenID)
        NextToken()
        GuardarIRP_Token(writer, id)
    End Sub

    Private Sub ParseUnaryStmt(writer As StreamWriter, id As TokenID)

        NextToken()

        Dim expr As String = Nothing
        If Not ParseExprTexto(writer, True, expr) Then Return

        GuardarIRP_Token_Valor(writer, id, $"{expr}")

    End Sub

    Private Sub ParseBinaryStmt(writer As StreamWriter, id As TokenID)

        NextToken() ' consumir POKE u OUT

        Dim sb As New StringBuilder()
        Dim expr As String = Nothing

        ' Primer argumento
        If Not ParseExprTexto(writer, False, expr) Then Return
        sb.Append(expr)

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(writer, TokenColumna, $"Se esperaba ',' en {id}")
            Return
        End If
        sb.Append(" , ")
        NextToken()

        ' Segundo argumento
        If Not ParseExprTexto(writer, False, expr) Then Return
        sb.Append(expr)

        GuardarIRP_Token_Valor(writer, id, sb.ToString())

    End Sub

    ' ============================================================
    ' PARSE DE EXPRESIONES en RPN
    ' ============================================================
    Private Function ParseExprTexto(writer As StreamWriter,
                                    permiteComaExterior As Boolean,
                                    ByRef resultado As String,
                                    Optional stopTokens As String = "") As Boolean

        Dim sb As New StringBuilder()
        Dim nivelParentesis As Integer = 0

        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TCO_EOL AndAlso
          Tid() <> TokenID.TSP_DOSPUNTOS AndAlso
          Not IsControlKeyword(Tid())

            ' 🔹 NUEVO: parada por tokens externos configurables

            If nivelParentesis = 0 AndAlso TokenEsStopChar(stopTokens) Then
                Exit While   ' NO consumir el token
            End If

            ' ❌ Punto y coma solo es error si NO es stopToken
            If Tid() = TokenID.TSP_PUNTOYCOMA AndAlso
               (stopTokens = "" OrElse Not TokenEsStopChar(stopTokens)) Then
                ErrorSintactico(writer, TokenColumna, "Expresión no válida")
                Return False
            End If

            ' ❌ Coma no permitida a nivel superior si NO es stopToken
            If Tid() = TokenID.TSP_COMA AndAlso
               nivelParentesis = 0 AndAlso
               Not permiteComaExterior AndAlso
               (stopTokens = "" OrElse Not TokenEsStopChar(stopTokens)) Then
                Exit While
            End If

            ' Control de paréntesis
            If Tid() = TokenID.TSP_PAR_ABIERTO Then
                nivelParentesis += 1
            ElseIf Tid() = TokenID.TSP_PAR_CERRADO Then
                nivelParentesis -= 1
            End If

            ' Construcción textual

            Select Case Tid()

                ' LITERALES
                Case TokenID.TES_STRING, TokenID.TES_GREXPR, TokenID.TES_NUMBER, TokenID.TES_IDENT
                    '    sb.Append($"{Constantes.C_COMILLAS}{TokenValor()}{Constantes.C_COMILLAS}")

                    'Case TokenID.TES_GREXPR
                    '    sb.Append($"{Constantes.C_PAR_APE}{TokenValor()}{Constantes.C_PAR_CIE}")

                    'Case TokenID.TES_NUMBER
                    '    sb.Append(TokenValor())

                    'Case TokenID.TES_IDENT
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
                Case TokenID.TSP_PAR_ABIERTO : sb.Append("(")
                Case TokenID.TSP_PAR_CERRADO : sb.Append(")")
                Case TokenID.TSP_COMA : sb.Append(",")

                Case Else
                    ' No debería llegar nada importante aquí
            End Select


            sb.Append(" ") 'Revisar si es No necesario
            NextToken()
        End While

        If nivelParentesis <> 0 Then
            ErrorSintactico(writer, TokenColumna, "Paréntesis desequilibrados en expresión")
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

End Module