Imports System
Imports System.Drawing
Imports System.Formats
Imports System.Formats.Asn1
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
    Private NCol As Integer = 0
    Private LineaParaMostrar As String = ""
    Private NroLineaFichero As Integer = 0
    Private NroLineaPrograma As Integer
    Private PrimeraLinea As Boolean = True
    Private bufferLinea As New List(Of Token)
    Private encontradoEOF As Boolean = False
    Private stReader As StreamReader
    Private stWriter As StreamWriter

    ' ============================================================
    ' PARSE PRINCIPAL
    ' ============================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        opts = _opts
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        NroLineaFichero = 0
        Dim PrimeraLinea As Boolean = True
        encontradoEOF = False
        bufferLinea.Clear()



        NroLineaFichero = 0
        NroErrores = 0
        NroWarnings = 0

        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()
            ' Eliminar BOM UTF‑8 si existe
            LineaLeida = LineaLeida.TrimStart(ChrW(&HFEFF))

            ' ----------------------------------------------------------
            ' Primera línea, Debe contener tipo y versión del fichero
            ' ----------------------------------------------------------
            If PrimeraLinea Then
                Dim resultado As String = ""
                If Not GetVersion(opts, LineaLeida, resultado) Then
                    ErrorSintactico(1, resultado)
                Else
                    GuardarIRP_Texto(resultado)
                End If
                PrimeraLinea = False
                Continue While
            End If


            ' --------------------------------------------
            ' Línea original (contexto para el  error)
            ' --------------------------------------------            
            If LineaLeida.StartsWith(Marca_SRC) Then
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, LineaLeida)

                GuardarIRP_Texto($"{Constantes.Marca_SRC} {LineaParaMostrar}")
                Continue While
            End If

            ' --------------------------------------------
            ' Token normal
            ' --------------------------------------------
            Dim tok As New Token(LineaLeida)
            If tok.ID <> TokenID.TCO_NONE Then bufferLinea.Add(tok)

            ' --------------------------------------------
            ' EOF explícito del fichero TOK
            ' --------------------------------------------
            If tok.ID = TokenID.TCO_EOF Then
                encontradoEOF = True
                Exit While
            End If

            ' --------------------------------------------
            ' Fin de línea lógica ZX
            ' --------------------------------------------
            If tok.ID = TokenID.TCO_EOL Then
                ParsearLineaTokens(bufferLinea, NroLineaFichero, stWriter)
                bufferLinea.Clear()
            End If
        End While


        If NroErrores = 0 AndAlso Not encontradoEOF Then
            MostrarMensaje(opts, "[ERROR PARSER] Fichero TOK incompleto: falta EOF, posible fichero truncado")
            Return 1
        End If



        GuardarIRP_Token(TokenID.TCO_EOF)
        stWriter.Flush()
        stReader.Close()
        stWriter.Close()

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
            ErrorSintactico(TokenColumna(), "Línea sin número")
            Exit Sub
        End If

        Dim numLinea As Integer = Integer.Parse(TokenValor())
        NextToken()

        GuardarIRP_Token_Valor(TokenID.TCO_LINE, $"{numLinea}")

        ' --------------------------------------------
        ' Parsear sentencias hasta EOL
        ' --------------------------------------------
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL
            ParseStatement()

            If Tid() = TokenID.TSP_DOSPUNTOS Then
                NextToken()

            ElseIf Tid() = TokenID.TCO_EOL Then
                Exit While

            Else
                ErrorSintactico(TokenColumna, "Falta ':' entre sentencias")
                Exit While
            End If


        End While

        GuardarIRP_Token(TokenID.TCO_EOL)
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
        Return tokensLinea(idx).Lin
    End Function

    Private Function TokenColumna() As Integer
        Return tokensLinea(idx).Col
    End Function

    Private Function TokenValor() As String
        Return tokensLinea(idx).GetValor
    End Function

    Private Sub NextToken()
        NCol = TokenColumna()
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
    Private Sub ParseStatement()
        Dim ncol As Integer = TokenColumna()
        Dim tok = tokensLinea(idx)
        Dim tipo As TokenID = Tid()

        If tipo = TokenID.TCO_EOL Then
            NextToken()
            Exit Sub
        End If

        If tok.IsStatementStart() Then
            Select Case tok.ID
                Case TokenID.TK_LET : Parse_LET() : Exit Sub
                Case TokenID.TK_PRINT, TokenID.TK_INPUT : Parse_PRINT_INPUT(tok.ID) : Exit Sub : Exit Sub
                Case TokenID.TK_IF : Parse_If() : Exit Sub
                Case TokenID.TK_GOTO : Parse_Goto() : Exit Sub
                Case TokenID.TK_GOSUB : Parse_Gosub() : Exit Sub
                Case TokenID.TK_RETURN : Parse_Return() : Exit Sub
                Case TokenID.TK_RESTORE : Parse_Restore() : Exit Sub
                Case TokenID.TK_READ : Parse_Read() : Exit Sub
                Case TokenID.TK_DATA : Parse_Data() : Exit Sub
                Case TokenID.TK_STOP : Parse_Stop() : Exit Sub
                Case TokenID.TK_FOR : Parse_For() : Exit Sub
                Case TokenID.TK_NEXT : Parse_Next() : Exit Sub
                Case TokenID.TK_REM : Parse_REM() : Exit Sub
                Case TokenID.TK_CLEAR : Parse_Clear() : Exit Sub
                Case TokenID.TK_DIM : Parse_Dim() : Exit Sub
                Case TokenID.TK_RANDOMIZE : Parse_Randomize() : Exit Sub
                Case TokenID.TK_BEEP : Parse_Beep() : Exit Sub

                Case TokenID.TK_CLS : Parse_SimpleStmt(tok.ID) : Exit Sub
                Case TokenID.TK_BORDER : Parse_UnaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_PAUSE : Parse_UnaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_INK : Parse_UnaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_PAPER : Parse_UnaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_BRIGHT : Parse_UnaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_FLASH : Parse_UnaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_INVERSE : Parse_UnaryStmt(tok.ID) : Exit Sub


                Case TokenID.TK_CIRCLE : Parse_Graphics(tok.ID) : Exit Sub  'Circulo........: CIRCLE x,y,radio
                Case TokenID.TK_DRAW : Parse_Graphics(tok.ID) : Exit Sub    'Línea..........: DRAW x,y,radio
                Case TokenID.TK_PLOT : Parse_Graphics(tok.ID) : Exit Sub    'Punto..........: PLOT x,y
                Case TokenID.TK_POINT : Parse_Graphics(tok.ID) : Exit Sub   'Color del punto: POINT x,y retorna 0 si es color paper, 1 si es ink

                Case TokenID.TK_POKE : Parse_BinaryStmt(tok.ID) : Exit Sub
                Case TokenID.TK_OUT : Parse_BinaryStmt(tok.ID) : Exit Sub

                Case TokenID.TK_RUN : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_LIST : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_LOAD : ParseFileStmt(tok.ID, True) : Exit Sub
                Case TokenID.TK_SAVE : ParseFileStmt(tok.ID, True) : Exit Sub
                Case TokenID.TK_MERGE : ParseFileStmt(tok.ID, True) : Exit Sub

                Case TokenID.TK_COPY : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_OPEN : ParseFileStmt(tok.ID, True) : Exit Sub
                Case TokenID.TK_CLOSE : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_MOVE : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_ERASE : ParseFileStmt(tok.ID, True) : Exit Sub
                Case TokenID.TK_CAT : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_FORMAT : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_FAST : ParseFileStmt(tok.ID, False) : Exit Sub
                Case TokenID.TK_SLOW : ParseFileStmt(tok.ID, False) : Exit Sub

            End Select

            ErrorSintactico(TokenColumna, $"Comando no reconocido: {tok.ID.ToString()}, Valor: {tok.Value}")
            Exit Sub
        End If

        ' LET no puede ser implícito
        If tipo = TokenID.TES_IDENT AndAlso PeekTid() = TokenID.TOP_EQ Then
            ErrorSintactico(ncol, "Sentencia no válida ¿Falta el LET?")
            Exit Sub
        End If

        ErrorSintactico(ncol, $"Sentencia no válida: {tok.ID.ToString()}, Valor: {tok.Value}")
    End Sub


    ' ============================================================
    ' SENTENCIAS
    ' ============================================================
    Private Sub Parse_REM()
        NextToken() ' consumir REM

        Dim comentario As String = ""

        If Tid() = TokenID.TES_STRING Then
            comentario = TokenValor()
            If comentario.StartsWith(Constantes.C_COMILLAS) Then
                comentario = comentario.Substring(1, comentario.Length - 2)
            End If
            NextToken()
        End If

        GuardarIRP_Token_Valor(TokenID.TK_REM, comentario)

        ' consumir hasta EOL por seguridad
        While Tid() <> TokenID.TCO_EOL AndAlso idx < tokensLinea.Count
            NextToken()
        End While
    End Sub

    Private Sub Parse_Return()
        GuardarIRP_Token(TokenID.TK_RETURN)
        NextToken()
    End Sub

    Private Sub Parse_Stop()
        GuardarIRP_Token(TokenID.TK_STOP)
        NextToken()
    End Sub

    Private Sub Parse_Clear()
        'CLEAR        ; borra variables
        'CLEAR n      ; borra variables y fija RAMTOP = n



        ' Consumir CLEAR
        NextToken()

        ' Argumento opcional
        Dim expr As List(Of RPN.RPN_Node) = Nothing

        If Tid() <> TokenID.TCO_EOL AndAlso Tid() <> TokenID.TSP_DOSPUNTOS Then
            If Not ParseExprTexto(False, expr, False) Then
                Exit Sub
            End If
        End If

        ' Emitir IR estructural
        GuardarIRP_CLEAR(expr)
    End Sub


    Private Sub Parse_Dim()

        ' Consumir DIM
        NextToken()

        ' Nombre del array
        If Tid() <> TokenID.TES_IDENT Then
            ErrorSintactico(TokenColumna, "Se esperaba identificador en DIM")
            Exit Sub
        End If

        Dim arrayName As String = TokenValor()
        NextToken()

        ' Debe venir '('
        If Tid() <> TokenID.TSP_PAR_ABIERTO Then
            ErrorSintactico(TokenColumna, "Se esperaba '(' en DIM")
            Exit Sub
        End If
        NextToken()

        ' Lista de dimensiones (cada una es RPN)
        Dim dims As New List(Of List(Of RPN.RPN_Node))

        Do
            Dim exprDim As List(Of RPN.RPN_Node) = Nothing

            If Not ParseExprTexto(False, exprDim, False, ",)") Then
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
            ErrorSintactico(TokenColumna, "Se esperaba ')' en DIM")
            Exit Sub
        End If
        NextToken()

        ' Emitir IR DIM estructural
        GuardarIRP_DIM(arrayName, dims)
    End Sub

    Private Sub Parse_LET()

        ' Consumir LET solo si es explícito
        If Tid() = TokenID.TK_LET Then
            NextToken()
        End If

        ' Lado izquierdo (variable o array)
        Dim name As String = Nothing
        Dim indices As List(Of List(Of RPN.RPN_Node)) = Nothing

        If Not ParseLValue(name, indices) Then Exit Sub

        ' Debe venir '='
        If Tid() <> TokenID.TOP_EQ Then
            ErrorSintactico(TokenColumna, "Se esperaba '=' en LET")
            Exit Sub
        End If
        NextToken() ' consumir '='

        ' Lado derecho: expresión RPN tipada
        Dim rpn As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, rpn, False) Then Exit Sub

        ' Emitir IR estructural
        GuardarIRP_LET(name, indices, rpn)
    End Sub

    Private Function ParseLValue(ByRef name As String, ByRef indices As List(Of List(Of RPN.RPN_Node))) As Boolean

        indices = New List(Of List(Of RPN.RPN_Node))

        ' Debe empezar por identificador
        If Tid() <> TokenID.TES_IDENT OrElse Not Char.IsLetter(TokenValor()(0)) Then
            ErrorSintactico(TokenColumna, "Nombre de variable inválido")
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
                If Not ParseExprTexto(False, exprIdx, False, ",)") Then
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
                ErrorSintactico(TokenColumna, "Se esperaba ')'")
                Return False
            End If
            NextToken()
        End If

        Return True
    End Function

    Private Sub Parse_Randomize()

        ' Consumir RANDOMIZE
        NextToken()

        Dim modoUSR As Boolean = False
        Dim expr As List(Of RPN.RPN_Node) = Nothing

        ' ¿RANDOMIZE solo?
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_RANDOMIZE(False, Nothing)
            Exit Sub
        End If

        ' ¿RANDOMIZE USR ... ?
        If Tid() = TokenID.TK_USR Then
            modoUSR = True
            NextToken()
        End If

        ' Argumento obligatorio si no era RANDOMIZE solo
        If Not ParseExprTexto(False, expr, False) Then Exit Sub

        GuardarIRP_RANDOMIZE(modoUSR, expr)
    End Sub

    Private Function Parse_PRINT_INPUT(tkID As TokenID) As Boolean

        ' Consumir el Token
        NextToken()

        Dim items As New List(Of PrintItem)
        Dim startIdx As Integer = idx


        While idx < tokensLinea.Count AndAlso
              Tid() <> TokenID.TCO_EOL AndAlso
              Tid() <> TokenID.TSP_DOSPUNTOS

            Dim item As New PrintItem

            ' --------------------------------
            ' PRINT #canal
            ' --------------------------------
            If Tid() = TokenID.TK_CANAL Then

                NextToken() ' consumir #

                Dim canalExpr As List(Of RPN_Node) = Nothing

                If Not ParseExprTexto(False, canalExpr, False, ";,") Then
                    Return False
                End If


                item.prID = TokenID.TK_CANAL
                item.prChannel = canalExpr
                item.prExpr1 = canalExpr

                'El Separador
                NextToken()
                Select Case Tid()
                    Case TokenID.TSP_COMA : item.prSeparator = PrintSeparator.C
                    Case TokenID.TSP_PUNTOYCOMA : item.prSeparator = PrintSeparator.P
                End Select

                items.Add(item)
                Continue While
            End If
            ' -------------------------------------------------
            ' Directivas PRINT (AT, TAB, INK, PAPER, etc.)
            ' -------------------------------------------------
            If Ttk().IsPrintDirective() Then
                item.prID = Tid()
                NextToken()

                Select Case item.prID
                    ' -----------------------------
                    ' AT: dos argumentos (Y , X)
                    ' -----------------------------
                    Case TokenID.TK_AT

                        ' Y
                        If Not ParseExprTexto(False, item.prExpr1, False, ",") Then Return False
                        If Tid() <> TokenID.TSP_COMA Then
                            ErrorSintactico(TokenColumna, $"Se esperaba una coma en AT")
                            Return False
                        End If
                        NextToken() ' consumir sepatador

                        ' X
                        If Not ParseExprTexto(False, item.prExpr2, False, ";,") Then Return False

                    ' -----------------------------
                    ' TAB: un argumento, admite TAB n y TAB(n)
                    ' -----------------------------
                    Case TokenID.TK_TAB

                        ' TAB expr
                        If Not ParseExprTexto(False, item.prExpr1, False, ";,") Then Return False

                        ' -----------------------------
                        ' Resto de directivas PRINT
                        ' (INK, PAPER, OVER, INVERSE, ...)
                        ' -----------------------------
                    Case Else

                        ' Un único argumento
                        If Not ParseExprTexto(False, item.prExpr1, False, ";,") Then Return False


                End Select

                ' -----------------------------
                ' Separador tras la directiva
                ' -----------------------------
                If Tid() = TokenID.TSP_PUNTOYCOMA Then
                    item.prSeparator = PrintSeparator.P
                    NextToken()
                ElseIf Tid() = TokenID.TSP_COMA Then
                    item.prSeparator = PrintSeparator.C
                    NextToken()
                Else
                    item.prSeparator = PrintSeparator.N
                End If

                items.Add(item)
                Continue While

            End If

            ' --------------------------------
            ' EXPRESIÓN IMPRIMIBLE NORMAL
            ' --------------------------------
            item.prID = tkID

            If Not ParseExprTexto(False, item.prExpr1, False, ";,") Then
                Return False
            End If

            ' Separador
            If Tid() = TokenID.TSP_PUNTOYCOMA Then
                item.prSeparator = PrintSeparator.P
                NextToken()
            ElseIf Tid() = TokenID.TSP_COMA Then
                item.prSeparator = PrintSeparator.C
                NextToken()
            Else
                item.prSeparator = PrintSeparator.N
            End If

            items.Add(item)

        End While


        If idx = startIdx Then
            ErrorSintactico(TokenColumna, "PRINT no pudo consumir tokens")
            Return False
        End If

        ' --------------------------------
        ' Emitir IR: UNA LÍNEA POR ACTION
        ' --------------------------------
        For Each it In items
            GuardarIRP_PRINT_INPUT(tkID, it)
        Next

        Return True
    End Function



    Private Sub Parse_If()
        ' Consumir IF
        NextToken()

        ' Parsear condición como RPN tipado
        Dim condicion As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, condicion, False) Then
            Exit Sub
        End If

        ' Debe venir THEN
        If Tid() <> TokenID.TK_THEN Then
            ErrorSintactico(TokenColumna, "Se esperaba THEN en IF")
            Exit Sub
        End If

        ' Consumir THEN
        '+++NextToken()
        'Cambiamos THEN por : y actua como separador de sentencias estándar
        Dim tk As Token = tokensLinea(idx)
        tk.ID = TokenID.TSP_DOSPUNTOS
        tokensLinea(idx) = tk

        ' Emitir IF como sentencia independiente, con condición RPN
        GuardarIRP_IF(condicion)
    End Sub

    Private Sub Parse_Goto()

        NextToken()
        Dim ln As String = TokenValor()
        NextToken()
        GuardarIRP_Token_Valor(TokenID.TK_GOTO, $"{ln}")
    End Sub


    Private Sub Parse_Gosub()
        NextToken()
        Dim ln As String = TokenValor()
        NextToken()
        GuardarIRP_Token_Valor(TokenID.TK_GOSUB, $"{ln}")
    End Sub

    ' ------------------------------------------------------------
    ' FOR I = expr TO expr [STEP expr]
    ' ------------------------------------------------------------
    Private Sub Parse_For()

        ' Consumir FOR
        NextToken()

        ' --- FOR var = 
        Dim startPos As Integer = TokenColumna()
        Dim tkVar As New Token(TokenID.TCO_UNKNOWN)
        Dim varName As New StringBuilder

        While Tid() <> TokenID.TOP_EQ AndAlso Tid() <> TokenID.TCO_EOL
            If (Tid() = TokenID.TES_IDENT) And (tkVar.ID = TokenID.TCO_UNKNOWN) Then
                tkVar = Ttk()
            End If

            Select Case Tid()
                Case TokenID.TES_IDENT : varName.Append(Ttk.Value)
                Case TokenID.TSP_PAR_ABIERTO : varName.Append("(")
                Case TokenID.TSP_PAR_CERRADO : varName.Append(")")
                Case TokenID.TSP_COMA : varName.Append(",")
                Case Else
                    ErrorSintactico(startPos, $"Token no válido para variable de control del FOR : {Ttk.ID}")
                    Exit Sub
            End Select
            NextToken()
        End While

        If Tid() <> TokenID.TOP_EQ Then
            ErrorSintactico(startPos, "FOR sin '=' tras variable de control")
            Exit Sub
        End If

        NextToken() ' consumir '='

        'Validar la variable de control del bucle
        ValidarVariableFor(varName.ToString, startPos)

        ' --- FOR var = Expresión 
        Dim exprInit As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, exprInit, False) Then Exit Sub

        ' --- FOR var = Expresión TO expresion
        If Tid() <> TokenID.TK_TO Then
            ErrorSintactico(TokenColumna, "Se esperaba TO en FOR")
            Exit Sub
        End If
        NextToken()

        Dim exprLimit As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, exprLimit, False) Then Exit Sub

        ' --- FOR var = Expresión TO expresion STEP expresion (opcional)
        Dim exprStep As List(Of RPN.RPN_Node) = Nothing
        If Tid() = TokenID.TK_STEP Then
            NextToken()
            If Not ParseExprTexto(True, exprStep, False) Then Exit Sub
        End If

        ' Emitir IR FOR (estructural, no textual)
        GuardarIRP_FOR(tkVar, exprInit, exprLimit, exprStep)
    End Sub

    ' ------------------------------------------------------------
    ' NEXT [I]
    ' ------------------------------------------------------------
    Private Sub Parse_Next()
        ' Consumir NEXT
        NextToken()

        ' Variable 
        Dim sb As String = ""
        If Tid() <> TokenID.TES_IDENT Then
            ErrorSintactico(TokenColumna(), $"Token incorrecto en NEXT '{Tid()}', debe ser una variable")
        Else
            sb = TokenValor()
            NextToken()
        End If

        'Validar la variable de control del bucle
        ValidarVariableFor(sb, 0)

        GuardarIRP_Token_Valor(TokenID.TK_NEXT, sb.ToString())
    End Sub

    Private Sub ValidarVariableFor(varName As String, startPos As Integer)
        'Validar la variable de control del bucle
        If varName.Length = 0 Then
            ErrorSintactico(startPos, "Se esperaba variable de control en FOR/NEXT")
            Exit Sub
        End If

        'Regla ZX: solo simples de una letra y deben ser numéricas
        If varName.ToString().Contains(Constantes.C_DOLAR) Then
            ErrorSintactico(TokenColumna(), $"Variable {varName} no válida en FOR/NEXT, solo admite variables numéricas simples de una letra")
            Exit Sub
        End If

        If varName.ToString().Contains("(") Then
            ErrorSintactico(startPos, $"FOR/NEXT no admite variables de tipo arreglo: '{varName}'")
            Exit Sub
        End If

        If varName.Length <> 1 Then
            ErrorSintactico(startPos, $"Variable '{varName}' no válida en FOR/NEXT, debe ser una sola letra")
            Exit Sub
        End If
    End Sub

    Private Sub Parse_Restore()
        ' Consumir RESTORE
        NextToken()

        ' ¿Hay número de línea?
        If Tid() = TokenID.TES_NUMBER Then
            Dim ln As String = TokenValor()
            NextToken()
            GuardarIRP_Token_Valor(TokenID.TK_RESTORE, $"{ln}")
        Else
            GuardarIRP_Token(TokenID.TK_RESTORE)
        End If

    End Sub

    Private Sub Parse_Read()

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
                    ErrorSintactico(TokenColumna, "Sintaxis inválida en READ")
                    Exit Sub

            End Select
            NextToken()

        End While
        GuardarIRP_Token_Valor(TokenID.TK_READ, sb.ToString())
    End Sub

    Private Sub Parse_Data()

        ' Consumir DATA
        NextToken()

        Dim items As New List(Of List(Of RPN.RPN_Node))

        ' DATA puede ir hasta EOL (no ":" como separador de sentencias)
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL

            Dim expr As List(Of RPN.RPN_Node) = Nothing

            ' Cada elemento es una expresión
            If Not ParseExprTexto(False, expr, True, ",") Then
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
        GuardarIRP_DATA(items)
    End Sub

    Private Sub Parse_Beep()

        ' Consumir BEEP
        NextToken()

        ' Primer argumento: duración
        Dim exprDuration As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, exprDuration, False, ",") Then
            Exit Sub
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(TokenColumna, "Se esperaba ',' en BEEP")
            Exit Sub
        End If
        NextToken()

        ' Segundo argumento: tono
        Dim exprPitch As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, exprPitch, False) Then
            Exit Sub
        End If

        ' Emitir IR BEEP estructural
        GuardarIRP_BEEP(exprDuration, exprPitch)
    End Sub

    Private Sub Parse_Run()

        ' Consumir RUN
        NextToken()

        ' ¿RUN sin argumento?
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_RUN(Nothing)
            Exit Sub
        End If

        ' RUN con expresión (línea de inicio)
        Dim expr As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, expr, False) Then
            Exit Sub
        End If

        GuardarIRP_RUN(expr)
    End Sub

    Private Sub Parse_List()

        ' Consumir LIST
        NextToken()

        ' LIST sin argumentos
        If Tid() = TokenID.TCO_EOL OrElse Tid() = TokenID.TSP_DOSPUNTOS Then
            GuardarIRP_LIST(Nothing, Nothing)
            Exit Sub
        End If

        ' Primer argumento (línea inicial)
        Dim exprStart As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, exprStart, False, ",") Then
            Exit Sub
        End If

        ' ¿Rango?
        If Tid() = TokenID.TSP_COMA Then
            NextToken()

            Dim exprEnd As List(Of RPN.RPN_Node) = Nothing
            If Not ParseExprTexto(True, exprEnd, False) Then
                Exit Sub
            End If

            GuardarIRP_LIST(exprStart, exprEnd)
            Exit Sub
        End If

        ' Solo una expresión
        GuardarIRP_LIST(exprStart, Nothing)
    End Sub

    Private Sub ParseFileStmt(id As TokenID, wFileNAme As Boolean)

        ' Consumir LOAD / SAVE / MERGE
        NextToken()

        ' Argumentos
        Dim expr As List(Of RPN.RPN_Node) = Nothing

        ' Si tiene un canal consumirlo
        If Tid() = TokenID.TK_CANAL Then
            NextToken()

            'El nro del canal
            If Tid() <> TokenID.TES_NUMBER Then
                ErrorSintactico(TokenColumna, "Falta el número tras el canal #")
            End If
            NextToken()

            'debe ir un separador
            If Tid() = TokenID.TSP_COMA Or Tid() = TokenID.TSP_PUNTOYCOMA Then
                NextToken()
            End If

            ' Argumento obligatorio: nombre de fichero
            If wFileNAme And Not ParseExprTexto(True, expr, False) Then
                Exit Sub
            End If
        End If

        ' Emitir IR estructural
        GuardarIRP_FILE(id, expr)
    End Sub

    Private Sub Parse_Graphics(id As TokenID)
        ' Consumir la palabra clave (PLOT, CIRCLE, etc.)
        NextToken()

        Dim nrArg As Integer = 0
        Select Case id
            Case TokenID.TK_CIRCLE : nrArg = 3   'Circulo........: CIRCLE x,y,radio
            Case TokenID.TK_DRAW : nrArg = -3    'Línea..........: DRAW x,y [,radio]
            Case TokenID.TK_PLOT : nrArg = 2     'Punto..........: PLOT x,y
            Case TokenID.TK_POINT : nrArg = 2    'Color del punto: POINT (x,y)  FUNCION que retorna 0 si es color paper, 1 si es ink
        End Select

        If (id = TokenID.TK_POINT) Then
            If Tid() <> TokenID.TSP_PAR_ABIERTO Then
                ErrorSintactico(TokenColumna(), "Point debe tener las coordenadas entre paréntesis")
            End If
        End If

        ' Primer argumento
        Dim expreUno As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, expreUno, False, ",") Then
            Exit Sub
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(TokenColumna, $"Se esperaba ',' en {id}")
            Exit Sub
        End If
        NextToken()

        ' Segundo argumento
        Dim expreDos As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, expreDos, False) Then
            Exit Sub
        End If

        'Tercer argumento en algunos
        Dim expreTres As List(Of RPN.RPN_Node) = Nothing
        If nrArg = 3 Then
            ' Coma obligatoria
            If Tid() <> TokenID.TSP_COMA Then
                ErrorSintactico(TokenColumna, $"Se esperaba ',' en {id}")
                Exit Sub
            End If
            NextToken()

            ' Tercer argumento
            If Not ParseExprTexto(False, expreDos, False) Then
                Exit Sub
            End If
        End If

        If nrArg = -3 Then
            ' Si hay Coma hay tercer parámetro
            If Tid() = TokenID.TSP_COMA Then
                NextToken()
                ' Tercer argumento
                If Not ParseExprTexto(False, expreDos, False) Then
                    Exit Sub
                End If
            End If

        End If


        If (id = TokenID.TK_POINT) Then
            If Tid() <> TokenID.TSP_PAR_CERRADO Then
                ErrorSintactico(TokenColumna(), "Point debe tener las coordenadas entre paréntesis")
            End If
        End If

        ' Emitir IR estructural
        GuardarIRP_Graphics(id, expreUno, expreDos, expreTres)
    End Sub

    Private Sub Parse_SimpleStmt(id As TokenID)
        NextToken()
        GuardarIRP_Token(id)
    End Sub

    Private Sub Parse_UnaryStmt(id As TokenID)

        ' Consumir la palabra clave (INK, PAPER, BRIGHT, etc.)
        NextToken()

        ' Argumento obligatorio: una expresión
        Dim expr As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, expr, False) Then
            Exit Sub
        End If

        ' Emitir IR estructural
        GuardarIRP_UNARY(id, expr)
    End Sub

    Private Sub Parse_BinaryStmt(id As TokenID)

        ' Consumir la palabra clave (POKE, OUT, etc.)
        NextToken()

        ' Primer argumento
        Dim exprLeft As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, exprLeft, False, ",") Then
            Exit Sub
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(TokenColumna, $"Se esperaba ',' en {id}")
            Exit Sub
        End If
        NextToken()

        ' Segundo argumento
        Dim exprRight As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, exprRight, False) Then
            Exit Sub
        End If

        ' Emitir IR estructural
        GuardarIRP_BINARY(id, exprLeft, exprRight)
    End Sub


    ' ============================================================
    ' PARSE DE EXPRESIONES en RPN
    ' ============================================================
    Private Function ParseExprTexto(permiteComaExterior As Boolean,
                                    ByRef resultado As List(Of RPN.RPN_Node),
                                    modoDATA As Boolean,
                                    Optional stopTokens As String = "") As Boolean

        Dim operators As New Stack(Of RPN_Node)
        resultado = New List(Of RPN_Node)
        Dim ultimoFueOperando As Boolean = False

        While idx < tokensLinea.Count AndAlso
          Tid() <> TokenID.TCO_EOL AndAlso
          Tid() <> TokenID.TSP_DOSPUNTOS AndAlso
          Not IsControlKeyword(Tid())

            ' -----------------------------------------
            ' STOP TOKENS
            ' -----------------------------------------
            If CheckStopTokens(stopTokens) Then Exit While

            ' -----------------------------------------
            ' DATA (lo dejamos inline)
            ' -----------------------------------------
            If modoDATA AndAlso Tid() = TokenID.TSP_COMA Then

                While operators.Count > 0 AndAlso operators.Peek().Value <> "("
                    resultado.Add(operators.Pop())
                End While

                resultado.Add(New RPN_Node With {
                    .Kind = RPNKind.DATA_SEP,
                    .Value = ","
                })

                ultimoFueOperando = False
                NextToken()
                Continue While
            End If

            ' -----------------------------------------
            ' BLOQUES
            ' -----------------------------------------
            If HandleIdent(resultado) Then Continue While
            If HandleFunction(resultado, ultimoFueOperando) Then Continue While
            If HandleConstant(resultado) Then Continue While
            If HandleOpenParen(operators, ultimoFueOperando) Then Continue While
            If HandleCloseParen(operators, resultado, stopTokens, ultimoFueOperando) Then Continue While
            If HandleOperator(operators, resultado, ultimoFueOperando) Then Continue While

            Return ErrorToken(1)

        End While

        Return FinalizeOperators(operators, resultado)

    End Function

    Private Function FinalizeOperators(operators As Stack(Of RPN_Node), resultado As List(Of RPN_Node)) As Boolean
        While operators.Count > 0
            If operators.Peek().Value = "(" Then
                Return ErrorParentesis(3)
            End If
            resultado.Add(operators.Pop())
        End While
        Return True
    End Function

    Private Function CheckStopTokens(stopTokens As String) As Boolean

        If stopTokens = "" Then Return False

        If Tid() = TokenID.TSP_PAR_CERRADO AndAlso stopTokens.Contains(")") Then Return True
        If Tid() = TokenID.TSP_COMA AndAlso stopTokens.Contains(",") Then Return True
        If Tid() = TokenID.TSP_PUNTOYCOMA AndAlso stopTokens.Contains(";") Then Return True

        Return False

    End Function


    Private Function HandleIdent(ByRef resultado As List(Of RPN_Node)) As Boolean

        If Tid() <> TokenID.TES_IDENT Then Return False

        Dim nombre As String = TokenValor()
        NextToken()

        If Tid() = TokenID.TSP_PAR_ABIERTO Then
            Return ParseArrayAccess(nombre, resultado)
        End If

        resultado.Add(New RPN_Node With {
            .Kind = RPNKind.VAR,
            .Value = nombre
        })

        Return True

    End Function

    Private Function ParseArrayAccess(nombre As String,
                                  ByRef resultado As List(Of RPN_Node)) As Boolean

        NextToken() ' (

        Dim args As New List(Of List(Of RPN_Node))

        Do
            Dim exprArg As List(Of RPN_Node) = Nothing

            If Not ParseExprTexto(False, exprArg, False, ",)") Then Return False

            args.Add(exprArg)

            If Tid() = TokenID.TSP_COMA Then
                NextToken()
                Continue Do
            End If

            Exit Do
        Loop

        If Tid() <> TokenID.TSP_PAR_CERRADO Then
            Return ErrorCierreParentesis()
        End If

        NextToken()

        ' construir RPN
        resultado.Add(New RPN_Node With {
            .Kind = RPNKind.VAR,
            .Value = nombre,
            .Arity = args.Count
        })

        For Each a In args
            resultado.AddRange(a)
        Next

        resultado.Add(New RPN_Node With {
            .Kind = RPNKind.IDX,
            .Arity = args.Count
        })

        Return True

    End Function


    Private Function HandleFunction(ByRef resultado As List(Of RPN_Node), ByRef ultimoFueOperando As Boolean) As Boolean

        If Not Ttk().IsFunction() Then Return False

        Dim funcToken = Tid()
        Dim funcName = Ttk().TokenName()
        Dim arity As Integer = Ttk().getAridad()

        NextToken()

        ' =====================================================
        ' ✅ ARIDAD 0  →  RND, PI
        ' =====================================================
        If arity = 0 Then

            resultado.Add(New RPN_Node With {
                .Kind = RPNKind.FUN_CALL,
                .Value = funcName,
                .TokenID = funcToken,
                .Arity = 0
            })

            ultimoFueOperando = True
            Return True
        End If

        ' =====================================================
        ' ✅ ARIDAD 1  →  SIN, INT, ...
        ' =====================================================
        If arity = 1 Then

            Dim exprArg As List(Of RPN_Node) = Nothing

            ' ✅ SIEMPRE consumir una expresión
            ' 👉 tanto si hay ( ) como si no
            If Not ParseExprTexto(False, exprArg, False, ".,;:)") Then Return False

            resultado.AddRange(exprArg)

            resultado.Add(New RPN_Node With {
                .Kind = RPNKind.FUN_CALL,
                .Value = funcName,
                .TokenID = funcToken,
                .Arity = 1
            })

            ultimoFueOperando = True
            Return True
        End If

        ' =====================================================
        ' ✅ ARIDAD 2  →  ATTR, SCREEN$, POINT...
        ' =====================================================
        If arity = 2 Then

            Dim arg1 As List(Of RPN_Node) = Nothing
            Dim arg2 As List(Of RPN_Node) = Nothing

            ' ✅ Primer argumento
            If Not ParseExprTexto(False, arg1, False, ",") Then Return False

            If Tid() <> TokenID.TSP_COMA Then
                Return ErrorToken(2)
            End If

            NextToken()

            ' ✅ Segundo argumento
            If Not ParseExprTexto(False, arg2, False, ")") Then Return False

            resultado.AddRange(arg1)
            resultado.AddRange(arg2)

            resultado.Add(New RPN_Node With {
                .Kind = RPNKind.FUN_CALL,
                .Value = funcName,
                .TokenID = funcToken,
                .Arity = 2
            })

            ultimoFueOperando = True
            Return True
        End If

        ' =====================================================
        ' ERROR
        ' =====================================================
        Return ErrorToken(3)

    End Function


    Private Function HandleConstant(ByRef resultado As List(Of RPN_Node)) As Boolean

        If Tid() <> TokenID.TES_NUMBER AndAlso Tid() <> TokenID.TES_STRING Then Return False

        resultado.Add(New RPN_Node With {
            .Kind = RPNKind.CTE,
            .Value = TokenValor()
        })

        NextToken()
        Return True

    End Function

    Private Function HandleOpenParen(operators As Stack(Of RPN_Node),
                                ByRef ultimoFueOperando As Boolean) As Boolean

        ' ❗ No aceptar si viene justo después de IDENT o FUNCTION
        If Tid() <> TokenID.TSP_PAR_ABIERTO Then Return False

        operators.Push(New RPN_Node With {.Value = "("})

        NextToken()
        ultimoFueOperando = False

        Return True

    End Function

    Private Function HandleCloseParen(operators As Stack(Of RPN_Node),
                                      resultado As List(Of RPN_Node),
                                      stopTokens As String,
                                      ByRef ultimoFueOperando As Boolean) As Boolean

        If Tid() = TokenID.TSP_PAR_CERRADO Then

            ' 🔥 si este ')' es delimitador externo, salir SIN consumir
            If stopTokens.Contains(")") Then Return False

            While operators.Count > 0 AndAlso operators.Peek().Value <> "("
                resultado.Add(operators.Pop())
            End While

            If operators.Count = 0 Then Return ErrorParentesis(1)

            operators.Pop()

            NextToken()
            ultimoFueOperando = True

            Return True
        End If

        If Tid() <> TokenID.TSP_PAR_CERRADO Then Return False

        While operators.Count > 0 AndAlso operators.Peek().Value <> "("
            resultado.Add(operators.Pop())
        End While

        If operators.Count = 0 Then Return ErrorParentesis(2)

        operators.Pop() ' quitar '('
        NextToken()

        ultimoFueOperando = True
        Return True

    End Function

    Private Function HandleOperator(operators As Stack(Of RPN_Node),
                                resultado As List(Of RPN_Node),
                                ByRef ultimoFueOperando As Boolean) As Boolean

        If Not Ttk().IsOperator() Then Return False

        Dim opToken As TokenID = Tid()
        Dim opText As String = GetTextoOperador(opToken)

        Dim kind As RPNKind

        ' ---------------------------------
        ' 🔥 distinguir unario / binario
        ' ---------------------------------
        If opToken = TokenID.TOP_MINUS AndAlso Not ultimoFueOperando Then
            kind = RPNKind.UNARY_OP
            opText = RPN.UNARY_MINUS
        Else
            kind = RPNKind.BINARY_OP
        End If

        Dim node As New RPN_Node With {
        .Kind = kind,
        .TokenID = opToken,
        .Value = opText
    }

        While operators.Count > 0 AndAlso
          operators.Peek().Value <> "(" AndAlso
          RPN.PrecedenciaFromTxt(operators.Peek().Value) >= RPN.PrecedenciaFromTxt(opText)

            resultado.Add(operators.Pop())
        End While

        operators.Push(node)

        ' ---------------------------------
        ' IMPORTANTE
        ' ---------------------------------
        ultimoFueOperando = False

        NextToken()
        Return True

    End Function

    Private Function ErrorParentesis(n As Integer) As Boolean
        ErrorSintactico(TokenColumna(), $"({n}) Paréntesis desequilibrados")
        Return False
    End Function

    Private Function ErrorCierreParentesis() As Boolean
        ErrorSintactico(TokenColumna(), "Cierre de paréntesis inesperado")
        Return False
    End Function

    Private Function ErrorToken(n As Integer) As Boolean
        ErrorSintactico(TokenColumna(), $"Token inesperado en expresión ({n})")
        Return False
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

    ' ----------------------------------------------------------
    ' Helpers
    ' ----------------------------------------------------------
    Private Function TokenEsStopChar(stopChars As String) As Boolean
        If stopChars = "" Then Return False

        Select Case Tid()
            Case TokenID.TSP_COMA : Return stopChars.Contains(Constantes.C_COMA)
            Case TokenID.TSP_PUNTOYCOMA : Return stopChars.Contains(Constantes.C_PUNTOYCOMA)
            Case TokenID.TSP_PAR_CERRADO : Return stopChars.Contains(Constantes.C_PAR_CIE)
            Case TokenID.TSP_PAR_ABIERTO : Return stopChars.Contains(Constantes.C_PAR_APE)
            Case TokenID.TSP_DOSPUNTOS : Return stopChars.Contains(Constantes.C_DOSPUNTOS)
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
    Private Sub WarningSintactico(columna As Integer, descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        If (columna <> 0) Then
            columna = columna - 1
        End If
        MensajeError(opts, stReader, stWriter, True, NroLineaFichero, columna, LineaParaMostrar,
                     New String(Constantes.C_ESPACIO, columna) & Constantes.Marca_Error & descripcion, False)

    End Sub

    Private Sub ErrorSintactico(columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna > 0) Then
            columna = columna - 1
        End If

        Dim espacios As String
        If columna > 0 Then espacios = New String(Constantes.C_ESPACIO, columna) Else espacios = ""

        MostrarError(opts, stReader, stWriter, NroLineaPrograma, columna, LineaParaMostrar,
                     espacios & Constantes.Marca_Error & descripcion)

        ' EVITAR BUCLES INFINITOS, IR AL FIN DE LA LINEA
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL
            NextToken()
        End While
    End Sub

    Private Sub GuardarIRP_Token(ID As TokenID)
        GuardarIRP(New Token(ID, ""), New PrintItem(TokenID.TCO_UNKNOWN))
    End Sub

    Private Sub GuardarIRP_Token_Valor(ID As TokenID, valor As String)
        GuardarIRP(New Token(ID, valor), New PrintItem(TokenID.TCO_UNKNOWN))
    End Sub

    Private Sub GuardarIRP(tok As Token, pi As PrintItem)
        Dim idNum As Integer = CInt(tok.ID)
        Dim value As String = If(tok.Value IsNot Nothing, tok.Value, "")
        Dim Linea As String = ""
        Dim Comentario As String = ""

        If pi.prID = TokenID.TCO_UNKNOWN Then
            Linea = $"{idNum} {value}"
            Comentario = $"{tok.ID.ToString()}"
        Else
            Linea = $"{idNum} {pi.ToText} {value}"
            Comentario = $"{tok.ID.ToString()} {pi.prID.ToString()}"
        End If

        If Len(Linea) < Constantes.Separacion_Comentario Then
            Linea &= Space(Constantes.Separacion_Comentario - Len(Linea)) & $"{Constantes.Marca_Comentario} {Comentario}"
            GuardarIRP_Texto(Linea)
        Else
            GuardarIRP_Texto(Linea)
            Linea = Space(Constantes.Separacion_Comentario) & $"{Constantes.Marca_Comentario} {Comentario}"
            GuardarIRP_Texto(Linea)
        End If
    End Sub

    Private Sub GuardarIRP_Texto(linea As String)
        stWriter.WriteLine(linea)
        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If

    End Sub

    '*************************************************************
    '* GUARDAR LOS IRP DE CADA TIPO
    '*************************************************************

    Private Sub GuardarIRP_DIM(arrayName As String, dims As List(Of List(Of RPN.RPN_Node)))

        Dim listaRPN As New List(Of RPN_Node)

        ' ---------------------------------
        ' 1. variable base
        ' ---------------------------------
        listaRPN.Add(New RPN_Node With {
        .Kind = RPNKind.VAR,
        .Value = arrayName,
        .Arity = dims.Count
    })

        ' ---------------------------------
        ' 2. dimensiones
        ' ---------------------------------
        For Each dimExpr In dims
            listaRPN.AddRange(dimExpr)
        Next

        ' ---------------------------------
        ' 3. operador IDX
        ' ---------------------------------
        listaRPN.Add(New RPN_Node With {
        .Kind = RPNKind.IDX,
        .Arity = dims.Count
    })

        ' ---------------------------------
        ' emitir RPN correcto
        ' ---------------------------------
        GuardarIRP_Token_Valor(TokenID.TK_DIM, RPN.RPN_ToText(listaRPN))

    End Sub

    Private Sub GuardarIRP_LET(name As String, indices As List(Of List(Of RPN.RPN_Node)), expr As List(Of RPN.RPN_Node))

        Dim listaRPN As New List(Of RPN_Node)

        ' -----------------------------
        ' 1. Variable base
        ' -----------------------------
        listaRPN.Add(New RPN_Node With {
            .Kind = RPNKind.VAR,
            .Value = name
        })

        ' -----------------------------
        ' 2. Índices → RPN plana
        ' -----------------------------
        If indices IsNot Nothing AndAlso indices.Count > 0 Then

            For Each idxExpr In indices
                listaRPN.AddRange(idxExpr)
            Next

            listaRPN.Add(New RPN_Node With {
                .Kind = RPNKind.IDX,
                .Arity = indices.Count
            })

        End If


        ' -----------------------------
        ' 2. ASIGNACIÓN 
        ' -----------------------------
        listaRPN.Add(New RPN_Node With {
            .Kind = RPNKind.ASSIGN,
            .Value = "="
        })

        ' -----------------------------
        ' 3. RHS
        ' -----------------------------
        listaRPN.AddRange(expr)


        ' -----------------------------
        ' Emitir IR textual
        ' -----------------------------
        GuardarIRP_Token_Valor(TokenID.TK_LET, RPN.RPN_ToText(listaRPN))

    End Sub

    Private Sub GuardarIRP_PRINT_INPUT(tkID As TokenID, item As PrintItem)

        If item.prID = tkID Then
            item.prID = DetectarTipoPrintItem(tkID, item.prExpr1)
        End If

        Dim sb As New StringBuilder()


        If item.prID = TokenID.TK_CANAL Then
            item.prValue = RPN.RPN_ToText(item.prExpr1)
            GuardarIRP(New Token(tkID, ""), item)
            Return
        End If

        If item.prID = TokenID.TK_AT Then

            Dim sepNode As New RPN_Node With {
                .Kind = RPNKind.DATA_SEP,
                .Value = ",",
                .Arity = 0
            }

            sb.Append(RPN.RPN_ToText(item.prExpr1))
            sb.Append(" ")
            sb.Append(RPN.RPN_ToText(New List(Of RPN_Node) From {sepNode}))
            sb.Append(" ")
            sb.Append(RPN.RPN_ToText(item.prExpr2))
        Else
            sb.Append(RPN.RPN_ToText(item.prExpr1))
        End If

        item.prValue = sb.ToString()

        GuardarIRP(New Token(tkID, ""), item)

    End Sub

    Private Function DetectarTipoPrintItem(tkID As TokenID, expr As List(Of RPN.RPN_Node)) As TokenID

        If expr Is Nothing OrElse expr.Count = 0 Then
            Return TokenID.TCO_UNKNOWN
        End If

        Dim first = expr(expr.Count - 1) ' en RPN el resultado final

        Select Case first.Kind
            Case RPNKind.VAR
                Return TokenID.TES_IDENT

            Case RPNKind.CTE
                If first.TokenID = TokenID.TES_STRING Then
                    Return TokenID.TES_STRING
                Else
                    Return TokenID.TES_NUMBER
                End If

            Case RPNKind.FUN_CALL
                Return first.TokenID

            Case Else
                Return tkID

        End Select

    End Function


    Private Sub GuardarIRP_IF(condicion As List(Of RPN.RPN_Node))
        Dim text As String = RPN.RPN_ToText(condicion)
        GuardarIRP_Token_Valor(TokenID.TK_IF, text)
    End Sub


    Private Sub GuardarIRP_FOR(varName As Token,
                               exprInit As List(Of RPN.RPN_Node),
                               exprLimit As List(Of RPN.RPN_Node),
                               exprStep As List(Of RPN.RPN_Node))
        Dim sb As New StringBuilder()

        sb.Append($"{GetKindLetter(RPNKind.VAR)}({varName.Value}) ")
        sb.Append($"{GetKindLetter(RPNKind.ASSIGN)}({Constantes.C_IGUAL}) {RPN.RPN_ToText(exprInit)} ")
        sb.Append($"{GetKindLetter(RPNKind.FOR_TO)}(TO) {RPN.RPN_ToText(exprLimit)} ")

        If exprStep IsNot Nothing Then
            sb.Append($"{GetKindLetter(RPNKind.FOR_STEP)}(STEP) {RPN.RPN_ToText(exprStep)} ")
        End If

        GuardarIRP_Token_Valor(TokenID.TK_FOR, sb.ToString())
    End Sub

    Private Sub GuardarIRP_CLEAR(expr As List(Of RPN.RPN_Node))
        Dim text As String = ""

        If expr IsNot Nothing Then
            text = RPN.RPN_ToText(expr)
        End If

        GuardarIRP_Token_Valor(TokenID.TK_CLEAR, text)
    End Sub

    Private Sub GuardarIRP_RANDOMIZE(modoUSR As Boolean, expr As List(Of RPN.RPN_Node))


        Dim sb As New StringBuilder()

        If modoUSR Then
            sb.Append("USR ")
        End If

        If expr IsNot Nothing Then
            sb.Append(RPN.RPN_ToText(expr))
        End If

        GuardarIRP_Token_Valor(TokenID.TK_RANDOMIZE, sb.ToString())

    End Sub

    Private Sub GuardarIRP_DATA(items As List(Of List(Of RPN.RPN_Node)))


        Dim sb As New StringBuilder()

        For i = 0 To items.Count - 1
            If i > 0 Then sb.Append(" , ")
            sb.Append(RPN.RPN_ToText(items(i)))
        Next

        GuardarIRP_Token_Valor(TokenID.TK_DATA, sb.ToString())

    End Sub

    Private Sub GuardarIRP_BEEP(exprDuration As List(Of RPN.RPN_Node), exprPitch As List(Of RPN.RPN_Node))

        Dim sb As New StringBuilder()

        sb.Append(RPN.RPN_ToText(exprDuration))
        sb.Append(" , ")
        sb.Append(RPN.RPN_ToText(exprPitch))

        GuardarIRP_Token_Valor(TokenID.TK_BEEP, sb.ToString())

    End Sub

    Private Sub GuardarIRP_RUN(expr As List(Of RPN.RPN_Node))

        Dim text As String = ""

        If expr IsNot Nothing Then
            text = RPN.RPN_ToText(expr)
        End If

        GuardarIRP_Token_Valor(TokenID.TK_RUN, text)

    End Sub

    Private Sub GuardarIRP_LIST(exprStart As List(Of RPN.RPN_Node), exprEnd As List(Of RPN.RPN_Node))


        Dim sb As New StringBuilder()

        If exprStart IsNot Nothing Then
            sb.Append(RPN.RPN_ToText(exprStart))
        End If

        If exprEnd IsNot Nothing Then
            sb.Append(" , ")
            sb.Append(RPN.RPN_ToText(exprEnd))
        End If

        GuardarIRP_Token_Valor(TokenID.TK_LIST, sb.ToString())

    End Sub

    Private Sub GuardarIRP_FILE(id As TokenID, expr As List(Of RPN.RPN_Node))

        Dim text As String = RPN.RPN_ToText(expr)
        GuardarIRP_Token_Valor(id, text)

    End Sub

    Private Sub GuardarIRP_UNARY(id As TokenID, expr As List(Of RPN.RPN_Node))

        Dim text As String = RPN.RPN_ToText(expr)
        GuardarIRP_Token_Valor(id, text)

    End Sub

    Private Sub GuardarIRP_BINARY(id As TokenID,
                                  exprLeft As List(Of RPN.RPN_Node),
                                  exprRight As List(Of RPN.RPN_Node))

        Dim sb As New StringBuilder()

        sb.Append(RPN.RPN_ToText(exprLeft))
        sb.Append(" , ")
        sb.Append(RPN.RPN_ToText(exprRight))

        GuardarIRP_Token_Valor(id, sb.ToString())

    End Sub

    Private Sub GuardarIRP_Graphics(id As TokenID,
                                    exprUno As List(Of RPN.RPN_Node),
                                    exprDos As List(Of RPN.RPN_Node),
                                    exprTres As List(Of RPN.RPN_Node))
        Dim sb As New StringBuilder()

        sb.Append(RPN.RPN_ToText(exprUno))
        sb.Append(" , ")
        sb.Append(RPN.RPN_ToText(exprDos))
        sb.Append(" , ")
        sb.Append(RPN.RPN_ToText(exprTres))

        GuardarIRP_Token_Valor(id, sb.ToString())

    End Sub

End Module