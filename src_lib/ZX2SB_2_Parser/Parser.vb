Imports System
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
                    ErrorSintactico(0, resultado)
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
            bufferLinea.Add(tok)

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
            ErrorSintactico(0, "Línea sin número")
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
    Private Sub ParseStatement()
        Dim tok = tokensLinea(idx)
        Dim tipo As TokenID = Tid()

        If tipo = TokenID.TCO_EOL Then
            NextToken()
            Exit Sub
        End If

        If tok.IsStatementStart() Then
            Select Case tok.ID
                Case TokenID.TK_LET : Parse_Let() : Exit Sub
                Case TokenID.TK_PRINT : Parse_PRINT() : Exit Sub
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

                Case TokenID.TK_CLS : Parse_SimpleStmt(TokenID.TK_CLS) : Exit Sub

                Case TokenID.TK_BORDER : Parse_UnaryStmt(TokenID.TK_BORDER) : Exit Sub
                Case TokenID.TK_PAUSE : Parse_UnaryStmt(TokenID.TK_PAUSE) : Exit Sub
                Case TokenID.TK_INK : Parse_UnaryStmt(TokenID.TK_INK) : Exit Sub
                Case TokenID.TK_PAPER : Parse_UnaryStmt(TokenID.TK_PAPER) : Exit Sub
                Case TokenID.TK_BRIGHT : Parse_UnaryStmt(TokenID.TK_BRIGHT) : Exit Sub
                Case TokenID.TK_FLASH : Parse_UnaryStmt(TokenID.TK_FLASH) : Exit Sub
                Case TokenID.TK_INVERSE : Parse_UnaryStmt(TokenID.TK_INVERSE) : Exit Sub

                Case TokenID.TK_POKE : Parse_BinaryStmt(TokenID.TK_POKE) : Exit Sub
                Case TokenID.TK_OUT : Parse_BinaryStmt(TokenID.TK_OUT) : Exit Sub

                Case TokenID.TK_RUN : Parse_Run() : Exit Sub
                Case TokenID.TK_LIST : Parse_List() : Exit Sub
                Case TokenID.TK_LOAD : Parse_Load() : Exit Sub
                Case TokenID.TK_SAVE : Parse_Save() : Exit Sub
                Case TokenID.TK_MERGE : Parse_Merge() : Exit Sub

            End Select

            ErrorSintactico(TokenColumna, $"Comando no reconocido: {tok.ID.ToString()}, Valor: {tok.Value}")
            Exit Sub
        End If

        ' LET no puede ser implícito
        If tipo = TokenID.TES_IDENT AndAlso PeekTid() = TokenID.TOP_EQ Then
            ErrorSintactico(0, "Sentencia no válida ¿Falta el LET?")
            Exit Sub
        End If

        ErrorSintactico(0, "Sentencia no válida")
    End Sub


    ' ============================================================
    ' SENTENCIAS
    ' ============================================================
    Private Sub Parse_REM()
        NextToken() ' consumir REM

        Dim comentario As String = ""

        If Tid() = TokenID.TES_STRING Then
            comentario = TokenValor()
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
            If Not ParseExprTexto(False, expr) Then
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
            ErrorSintactico(TokenColumna, "Se esperaba nombre de array en DIM")
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

            If Not ParseExprTexto(False, exprDim, ",)") Then
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

    Private Sub Parse_Let()

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
        If Not ParseExprTexto(False, rpn) Then Exit Sub

        ' Emitir IR estructural
        GuardarIRP_LET(name, indices, rpn)
    End Sub

    Private Function ParseLValue(ByRef name As String,
                                 ByRef indices As List(Of List(Of RPN.RPN_Node))
                                ) As Boolean

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
                If Not ParseExprTexto(False, exprIdx, ",)") Then
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
        If Not ParseExprTexto(False, expr) Then Exit Sub

        GuardarIRP_RANDOMIZE(modoUSR, expr)
    End Sub



    Private Function Parse_PRINT() As Boolean

        ' Consumir TK_PRINT
        NextToken()

        Dim items As New List(Of PrintItem)
        Dim startIdx As Integer = idx

        While idx < tokensLinea.Count AndAlso
              Tid() <> TokenID.TCO_EOL AndAlso
              Tid() <> TokenID.TSP_DOSPUNTOS


            ' -------------------------------------------------
            ' Directivas PRINT (AT, TAB, INK, PAPER, etc.)
            ' -------------------------------------------------
            If Ttk().IsPrintDirective() Then

                Dim item As New PrintItem
                item.ID = Tid()
                NextToken()

                Select Case item.ID
                    ' -----------------------------
                    ' AT: dos argumentos (Y , X)
                    ' -----------------------------
                    Case TokenID.TK_AT

                        ' Y
                        If Not ParseExprTexto(False, item.Expr1, ",") Then Return False
                        If Tid() <> TokenID.TSP_COMA Then
                            ErrorSintactico(TokenColumna, "Se esperaba ',' en AT")
                            Return False
                        End If
                        NextToken() ' consumir coma

                        ' X
                        If Not ParseExprTexto(False, item.Expr2, ";,") Then Return False

                    ' -----------------------------
                    ' TAB: un argumento, admite TAB n y TAB(n)
                    ' -----------------------------
                    Case TokenID.TK_TAB

                        ' TAB expr
                        If Not ParseExprTexto(False, item.Expr1, ";,") Then Return False

                        ' -----------------------------
                        ' Resto de directivas PRINT
                        ' (INK, PAPER, OVER, INVERSE, ...)
                        ' -----------------------------
                    Case Else

                        ' Un único argumento
                        If Not ParseExprTexto(False, item.Expr1, ";,") Then Return False


                End Select

                ' -----------------------------
                ' Separador tras la directiva
                ' -----------------------------
                If Tid() = TokenID.TSP_PUNTOYCOMA Then
                    item.Separator = PrintSeparator.P
                    NextToken()
                ElseIf Tid() = TokenID.TSP_COMA Then
                    item.Separator = PrintSeparator.C
                    NextToken()
                Else
                    item.Separator = PrintSeparator.N
                End If

                items.Add(item)
                Continue While

            End If

            ' --------------------------------
            ' EXPRESIÓN IMPRIMIBLE NORMAL
            ' --------------------------------
            Dim p As New PrintItem
            p.ID = TokenID.TK_PRINT

            If Not ParseExprTexto(False, p.Expr1, ";,") Then
                Return False
            End If

            ' Separador
            If Tid() = TokenID.TSP_PUNTOYCOMA Then
                p.Separator = PrintSeparator.P
                NextToken()
            ElseIf Tid() = TokenID.TSP_COMA Then
                p.Separator = PrintSeparator.C
                NextToken()
            Else
                p.Separator = PrintSeparator.N
            End If

            items.Add(p)

        End While


        If idx = startIdx Then
            ErrorSintactico(TokenColumna, "PRINT no pudo consumir tokens")
            Return False
        End If

        ' --------------------------------
        ' Emitir IR: UNA LÍNEA POR ACTION
        ' --------------------------------
        For Each it In items
            GuardarIRP_PRINT(it)
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

    Private Function ParseAT() As PrintItem

        Dim pi As New PrintItem(TokenID.TK_AT)

        ' Consumir AT
        NextToken()

        ' Primera expresión (Y)
        Dim exprY As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, exprY, ",") Then
            Return pi
        End If

        ' Coma obligatoria
        If Tid() <> TokenID.TSP_COMA Then
            ErrorSintactico(TokenColumna, "Se esperaba ',' en AT")
            Return pi
        End If
        NextToken()

        ' Segunda expresión (X)
        Dim exprX As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, exprX, ",;") Then
            Return pi
        End If

        ' Guardar estructura en el PrintItem
        pi.Expr1 = exprY
        pi.Expr2 = exprX
        pi.Separator = PrintSeparator.N

        Return pi
    End Function


    Private Sub Parse_If()
        ' Consumir IF
        NextToken()

        ' Parsear condición como RPN tipado
        Dim condicion As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(False, condicion) Then
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
        If varName.Length = 0 Then
            ErrorSintactico(startPos, "Se esperaba variable de control en FOR")
            Exit Sub
        End If

        'Regla ZX: solo simples de una letra y deben ser numéricas
        If varName.ToString().Contains(Constantes.C_DOLAR) Then
            ErrorSintactico(0, $"Variable {varName} no válida en FOR, solo admite variables numéricas simples de una letra")
            Exit Sub
        End If

        If varName.ToString().Contains("(") Then
            ErrorSintactico(startPos, $"FOR no admite arrays: '{varName}'")
            Exit Sub
        End If

        If varName.Length <> 1 Then
            ErrorSintactico(startPos, $"Variable '{varName}' no válida en FOR, debe ser una letra")
            Exit Sub
        End If

        ' --- FOR var = Expresión 
        Dim exprInit As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, exprInit) Then Exit Sub

        ' --- FOR var = Expresión TO expresion
        If Tid() <> TokenID.TK_TO Then
            ErrorSintactico(TokenColumna, "Se esperaba TO en FOR")
            Exit Sub
        End If
        NextToken()

        Dim exprLimit As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, exprLimit) Then Exit Sub

        ' --- FOR var = Expresión TO expresion STEP expresion (opcional)
        Dim exprStep As List(Of RPN.RPN_Node) = Nothing
        If Tid() = TokenID.TK_STEP Then
            NextToken()
            If Not ParseExprTexto(True, exprStep) Then Exit Sub
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

        Dim sb As String = ""
        ' Variable opcional
        If Tid() = TokenID.TES_IDENT Then
            sb = TokenValor()
            NextToken()
        End If
        GuardarIRP_Token_Valor(TokenID.TK_NEXT, sb.ToString())

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
            If Not ParseExprTexto(False, expr, ",") Then
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
        If Not ParseExprTexto(False, exprDuration, ",") Then
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
        If Not ParseExprTexto(False, exprPitch) Then
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
        If Not ParseExprTexto(True, expr) Then
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
        If Not ParseExprTexto(True, exprStart, ",") Then
            Exit Sub
        End If

        ' ¿Rango?
        If Tid() = TokenID.TSP_COMA Then
            NextToken()

            Dim exprEnd As List(Of RPN.RPN_Node) = Nothing
            If Not ParseExprTexto(True, exprEnd) Then
                Exit Sub
            End If

            GuardarIRP_LIST(exprStart, exprEnd)
            Exit Sub
        End If

        ' Solo una expresión
        GuardarIRP_LIST(exprStart, Nothing)
    End Sub

    Private Sub Parse_Load()
        ParseFileStmt(TokenID.TK_LOAD)
    End Sub
    Private Sub Parse_Save()
        ParseFileStmt(TokenID.TK_SAVE)
    End Sub
    Private Sub Parse_Merge()
        ParseFileStmt(TokenID.TK_MERGE)
    End Sub

    Private Sub ParseFileStmt(id As TokenID)

        ' Consumir LOAD / SAVE / MERGE
        NextToken()

        ' Argumento obligatorio: expresión (nombre de fichero)
        Dim expr As List(Of RPN.RPN_Node) = Nothing
        If Not ParseExprTexto(True, expr) Then
            Exit Sub
        End If

        ' Emitir IR estructural
        GuardarIRP_FILE(id, expr)
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
        If Not ParseExprTexto(True, expr) Then
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
        If Not ParseExprTexto(False, exprLeft, ",") Then
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
        If Not ParseExprTexto(False, exprRight) Then
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

                        ' Empujar el símbolo de la función
                        resultado.Add(New RPN.RPN_Node With {
                            .Kind = RPNKind.VAR,
                            .TokenID = TokenID.TES_IDENT,
                            .Value = nombre,
                            .Arity = 0
                        })


                        NextToken() ' consumir '('

                        Dim exprArg As List(Of RPN.RPN_Node) = Nothing
                        Dim argCount As Integer = 0

                        Do
                            If Not ParseExprTexto(False, exprArg, ",)") Then Return False
                            resultado.AddRange(exprArg)
                            argCount += 1
                            If Tid() = TokenID.TSP_COMA Then
                                NextToken()
                                Continue Do
                            End If
                            Exit Do
                        Loop


                        If Tid() <> TokenID.TSP_PAR_CERRADO Then
                            ErrorSintactico(TokenColumna, "Se esperaba ')'")
                            Return False
                        End If
                        NextToken()

                        ' Insertar argumentos RPN
                        'resultado.AddRange(exprArg)

                        ' Insertar nodo CALLFUN
                        resultado.Add(New RPN.RPN_Node With {
                            .Kind = RPN.RPNKind.FUN_CALL,
                            .TokenID = TokenID.TES_IDENT,
                            .Value = nombre,
                            .Arity = argCount
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
                        ErrorSintactico(TokenColumna, "Paréntesis desequilibrados")
                        Return False
                    End If
                    operators.Pop() ' quitar "("
                    ultimoFueOperando = True
                    NextToken()

                    ' =====================================================
                    ' FUNCIONES Y OPERADORES
                    ' =====================================================
                Case Else


                    If Ttk().IsFunction() Then
                        Dim funcToken As TokenID = Tid()
                        Dim funcName As String = Ttk().TokenName()

                        NextToken()   ' consumir la función

                        ' Argumento obligatorio: expresión
                        Dim exprArg As List(Of RPN.RPN_Node) = Nothing
                        If Not ParseExprTexto(False, exprArg, ";,") Then
                            Return False
                        End If


                        resultado.AddRange(exprArg)

                        resultado.Add(New RPN.RPN_Node With {
                                .Kind = RPNKind.FUN_CALL,
                                .TokenID = funcToken,
                                .Value = funcName,
                                .Arity = 1
                            })

                        ultimoFueOperando = True
                        Continue While
                    End If


                    If Ttk().IsOperator() Then
                        Dim opToken As TokenID = Tid()
                        Dim opText As String = GetTextoOperador(opToken)
                        Dim kind As RPN.RPNKind
                        Dim arity As Integer

                        If opToken = TokenID.TOP_MINUS AndAlso Not ultimoFueOperando Then
                            kind = RPN.RPNKind.UNARY_OP
                            arity = 1
                            opText = "UNARY_MINUS"
                        ElseIf opToken = TokenID.TK_NOT Then
                            kind = RPN.RPNKind.UNARY_OP
                            arity = 1
                        Else
                            kind = RPN.RPNKind.BINARY_OP
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
                        If nivelParentesis = 0 Then
                            ' Fin válido de expresión si:
                            ' - es stopToken explícito
                            ' - o es una directiva PRINT (AT, INK, TAB, etc.) cuando estamos en contexto PRINT
                            If TokenEsStopChar(stopTokens) _
                               OrElse (stopTokens <> "" AndAlso Ttk().IsPrintDirective()) Then
                                Exit While
                            Else
                                ErrorSintactico(TokenColumna, "Token inesperado en expresión")
                                Return False
                            End If
                        Else
                            ErrorSintactico(TokenColumna, "Token inesperado en expresión")
                            Return False
                        End If
                    End If
            End Select
        End While

        If nivelParentesis <> 0 Then
            ErrorSintactico(TokenColumna, "Paréntesis desequilibrados en expresión")
            Return False
        End If

        While operators.Count > 0
            If operators.Peek().Value = "(" Then
                ErrorSintactico(TokenColumna, "Paréntesis desequilibrados en expresión")
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
            Case TokenID.TSP_COMA : Return stopChars.Contains(Constantes.C_COMILLAS)
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
    Private Sub ErrorSintactico(columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If

        Dim espacios As String
        If columna <> 0 Then espacios = New String(Constantes.C_ESPACIO, columna) Else espacios = ""

        MostrarError(opts, stReader, stWriter, NroLineaPrograma, columna, LineaParaMostrar,
                     espacios & Constantes.Marca_Error & descripcion)

        ' EVITAR BUCLES INFINITOS, IR AL FIN DE LA LINEA
        While idx < tokensLinea.Count AndAlso Tid() <> TokenID.TCO_EOL
            NextToken()
        End While
    End Sub

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

        If pi.ID = TokenID.TCO_UNKNOWN Then
            Linea = $"{idNum} {value}"
            Comentario = $"{tok.ID.ToString()}"
        Else
            Linea = $"{idNum} {pi.ToText} {value}"
            Comentario = $"{tok.ID.ToString()} {pi.ID.ToString()}"
        End If

        If Len(Linea) < 49 Then
            Linea &= Space(50 - Len(Linea)) & $"{Constantes.Marca_Comentario} {Comentario}"
            GuardarIRP_Texto(Linea)
        Else
            GuardarIRP_Texto(Linea)
            Linea = Space(50) & $"{Constantes.Marca_Comentario} {Comentario}"
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

    Private Function RPN_ToText(rpn As List(Of RPN.RPN_Node)) As String
        Dim sb As New StringBuilder()

        For Each n In rpn

            sb.Append($"{GetKindLetter(n.Kind)}")
            Select Case n.Kind
                Case RPNKind.VAR,
                     RPNKind.CTE,
                     RPNKind.UNARY_OP,
                     RPNKind.BINARY_OP
                    sb.Append($"({n.Value}) ")

                Case RPNKind.FUN_CALL
                    sb.Append($"({n.Value},{n.Arity}) ")

            End Select
        Next

        Return sb.ToString().Trim()
    End Function

    Private Sub AddIndices(sb As StringBuilder, indices As List(Of List(Of RPN.RPN_Node)))

        If indices Is Nothing OrElse indices.Count = 0 Then
            Exit Sub
        End If

        sb.Append($" {GetKindLetter(RPNKind.IDX)}(")

        For i = 0 To indices.Count - 1
            If i > 0 Then sb.Append(",")
            sb.Append(RPN_ToText(indices(i)))
        Next

        sb.Append(")")
    End Sub

    Private Sub GuardarIRP_LET(
                               name As String,
                               indices As List(Of List(Of RPN.RPN_Node)),
                               expr As List(Of RPN.RPN_Node)
                               )

        Dim sb As New StringBuilder()

        ' --- LValue ---
        sb.Append($"V({name})")
        AddIndices(sb, indices)     ' Índices (si existen)

        ' --- Asignación ---
        sb.Append(" ")
        sb.Append($"{GetKindLetter(RPNKind.ASSIGN)}({Constantes.C_IGUAL})")
        sb.Append(" ")

        ' --- RValue ---
        sb.Append(RPN_ToText(expr))

        ' Emitir IR textual tipado
        GuardarIRP_Token_Valor(TokenID.TK_LET, sb.ToString())

    End Sub

    Private Sub GuardarIRP_PRINT(item As PrintItem)

        If item.ID = TokenID.TK_PRINT Then
            item.ID = DetectarTipoPrint(item.Expr1)
        End If

        Dim sb As New StringBuilder()

        If item.Expr1 IsNot Nothing Then
            sb.Append(RPN_ToText(item.Expr1))
        End If

        If item.ID = TokenID.TK_AT Then
            sb.Append(" , ")
            sb.Append(RPN_ToText(item.Expr2))
        End If

        item.Value = sb.ToString()

        GuardarIRP(New Token(TokenID.TK_PRINT, ""), item)

    End Sub

    Private Function DetectarTipoPrint(expr As List(Of RPN.RPN_Node)) As TokenID

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
                Return TokenID.TK_PRINT   ' fallback
        End Select

    End Function


    Private Sub GuardarIRP_IF(condicion As List(Of RPN.RPN_Node))
        Dim text As String = RPN_ToText(condicion)
        GuardarIRP_Token_Valor(TokenID.TK_IF, text)
    End Sub


    Private Sub GuardarIRP_FOR(varName As Token,
                               exprInit As List(Of RPN.RPN_Node),
                               exprLimit As List(Of RPN.RPN_Node),
                               exprStep As List(Of RPN.RPN_Node))
        Dim sb As New StringBuilder()

        sb.Append($"{GetKindLetter(RPNKind.VAR)}({varName.Value}) ")
        sb.Append($"{GetKindLetter(RPNKind.ASSIGN)}({Constantes.C_IGUAL}) ")
        sb.Append(RPN_ToText(exprInit))
        sb.Append($"{GetKindLetter(RPNKind.FOR_TO)}({RPN_ToText(exprLimit)}) ")

        If exprStep IsNot Nothing Then
            sb.Append($"{GetKindLetter(RPNKind.FOR_STEP)}({RPN_ToText(exprStep)}) ")
        End If

        GuardarIRP_Token_Valor(TokenID.TK_FOR, sb.ToString())
    End Sub

    Private Sub GuardarIRP_CLEAR(expr As List(Of RPN.RPN_Node))
        Dim text As String = ""

        If expr IsNot Nothing Then
            text = RPN_ToText(expr)
        End If

        GuardarIRP_Token_Valor(TokenID.TK_CLEAR, text)
    End Sub


    Private Sub GuardarIRP_DIM(arrayName As String, dims As List(Of List(Of RPN.RPN_Node)))

        Dim sb As New StringBuilder()

        sb.Append($"V({arrayName})")
        AddIndices(sb, dims)

        GuardarIRP_Token_Valor(TokenID.TK_DIM, sb.ToString())
    End Sub


    Private Sub GuardarIRP_RANDOMIZE(modoUSR As Boolean, expr As List(Of RPN.RPN_Node))


        Dim sb As New StringBuilder()

        If modoUSR Then
            sb.Append("USR ")
        End If

        If expr IsNot Nothing Then
            sb.Append(RPN_ToText(expr))
        End If

        GuardarIRP_Token_Valor(TokenID.TK_RANDOMIZE, sb.ToString())

    End Sub

    Private Sub GuardarIRP_DATA(items As List(Of List(Of RPN.RPN_Node)))


        Dim sb As New StringBuilder()

        For i = 0 To items.Count - 1
            If i > 0 Then sb.Append(" , ")
            sb.Append(RPN_ToText(items(i)))
        Next

        GuardarIRP_Token_Valor(TokenID.TK_DATA, sb.ToString())

    End Sub

    Private Sub GuardarIRP_BEEP(exprDuration As List(Of RPN.RPN_Node), exprPitch As List(Of RPN.RPN_Node))

        Dim sb As New StringBuilder()

        sb.Append(RPN_ToText(exprDuration))
        sb.Append(" , ")
        sb.Append(RPN_ToText(exprPitch))

        GuardarIRP_Token_Valor(TokenID.TK_BEEP, sb.ToString())

    End Sub

    Private Sub GuardarIRP_RUN(expr As List(Of RPN.RPN_Node))

        Dim text As String = ""

        If expr IsNot Nothing Then
            text = RPN_ToText(expr)
        End If

        GuardarIRP_Token_Valor(TokenID.TK_RUN, text)

    End Sub

    Private Sub GuardarIRP_LIST(exprStart As List(Of RPN.RPN_Node), exprEnd As List(Of RPN.RPN_Node))


        Dim sb As New StringBuilder()

        If exprStart IsNot Nothing Then
            sb.Append(RPN_ToText(exprStart))
        End If

        If exprEnd IsNot Nothing Then
            sb.Append(" , ")
            sb.Append(RPN_ToText(exprEnd))
        End If

        GuardarIRP_Token_Valor(TokenID.TK_LIST, sb.ToString())

    End Sub

    Private Sub GuardarIRP_FILE(id As TokenID, expr As List(Of RPN.RPN_Node))

        Dim text As String = RPN_ToText(expr)
        GuardarIRP_Token_Valor(id, text)

    End Sub

    Private Sub GuardarIRP_UNARY(id As TokenID, expr As List(Of RPN.RPN_Node))

        Dim text As String = RPN_ToText(expr)
        GuardarIRP_Token_Valor(id, text)

    End Sub

    Private Sub GuardarIRP_BINARY(id As TokenID, exprLeft As List(Of RPN.RPN_Node),
                                  exprRight As List(Of RPN.RPN_Node))

        Dim sb As New StringBuilder()

        sb.Append(RPN_ToText(exprLeft))
        sb.Append(" , ")
        sb.Append(RPN_ToText(exprRight))

        GuardarIRP_Token_Valor(id, sb.ToString())

    End Sub


End Module