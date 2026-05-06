Option Strict On
Option Explicit On

Imports System
Imports System.Diagnostics.Eventing
Imports System.IO
Imports System.Text
Imports System.Xml

Public Module Lexer

    Dim opts As CmdOptions
    Dim pos As Integer
    Dim NroErrores As Integer = 0
    Dim LineaParaMostrar As String = ""
    Dim NroLineaPrograma As Integer = 0
    Dim NroLineaFichero As Integer = 0
    Dim stWriter As StreamWriter
    Dim stReader As StreamReader

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        opts = _opts
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        NroLineaFichero = 0
        NroErrores = 0

        Dim PrimeraLinea As Boolean = True
        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()

            ' Ignorar líneas en blanco o solo con espacios
            If String.IsNullOrWhiteSpace(LineaLeida) Then
                Continue While
            End If


            ' ----------------------------------------------------------
            ' Primera línea (en el lexer no hace nada, lo mantengo por unificar)
            ' ----------------------------------------------------------
            If PrimeraLinea Then
                PrimeraLinea = False
            End If

            ' ----------------------------------------------------------
            ' Primera línea
            ' ----------------------------------------------------------
            LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, LineaLeida)
            GuardaSalida($"{Constantes.MarcaSRC} {LineaParaMostrar}")
            AnalizarLinea(LineaLeida, NroLineaFichero)
        End While

        AddTokenEOFL()
        stWriter.Flush()
        stReader.Close()
        stWriter.Close()

        Return NroErrores
    End Function

    ' ============================================================
    ' Analizar UNA línea ZX
    ' ============================================================
    Public Sub AnalizarLinea(LineaAnalizar As String, numLinea As Integer)
        pos = 0

        ' Número de línea obligatorio
        If Not ConsumirNumeroDeLinea(LineaAnalizar) Then
            AddTokenEOL()
            Return
        End If

        While pos < LineaAnalizar.Length

            Dim c As Char = LineaAnalizar(pos)

            If Char.IsWhiteSpace(c) Then
                Avanzar()
                Continue While
            End If

            If EsInicioREM(LineaAnalizar) Then
                ConsumirComentario(LineaAnalizar)
                Exit While
            End If


            If Char.IsDigit(c) OrElse
               (c = "."c AndAlso pos + 1 < LineaAnalizar.Length AndAlso Char.IsDigit(LineaAnalizar(pos + 1))) Then

                Dim col = pos + 1
                Dim tok = ConsumirNumero(col, LineaAnalizar)
                GuardaToken(tok)
                Continue While
            End If


            If Char.IsLetter(c) Then
                Dim col = pos + 1
                Dim tok = ConsumirIdentificador(col, LineaAnalizar)
                GuardaToken(tok)
                Continue While
            End If

            If c = Constantes.C_COMILLAS Then
                Dim col = pos + 1
                Dim tok = ConsumirStringLiteral(col, LineaAnalizar)
                GuardaToken(tok)
                Continue While
            End If

            ' Operadores y símbolos
            Dim colOp = pos + 1
            Dim tokOp = ConsumirOperadorOSimbolo(colOp, LineaAnalizar)
            GuardaToken(tokOp)
        End While

        AddTokenEOL()
    End Sub


    ' ============================================================
    ' Consumir número de línea ZX
    ' ============================================================
    Private Function ConsumirNumeroDeLinea(LineaAnalizar As String) As Boolean

        NroLineaPrograma = 0

        ' Saltar espacios iniciales
        While pos < LineaAnalizar.Length AndAlso Char.IsWhiteSpace(LineaAnalizar(pos))
            Avanzar()
        End While

        ' Debe empezar por dígito
        If pos >= LineaAnalizar.Length OrElse Not Char.IsDigit(LineaAnalizar(pos)) Then
            ErrorLexico(1, "Sentencia sin número de línea")
            Return False
        End If

        Dim sb As New StringBuilder()

        ' Consumir número de línea
        While pos < LineaAnalizar.Length AndAlso Char.IsDigit(LineaAnalizar(pos))
            sb.Append(LineaAnalizar(pos))
            Avanzar()
        End While

        NroLineaPrograma = Integer.Parse(sb.ToString())

        ' ✅ COMPROBACIÓN CLAVE:
        ' Tras el número debe haber espacio, tab o fin de línea
        If pos < LineaAnalizar.Length Then
            Dim ch As Char = LineaAnalizar(pos)
            If Not Char.IsWhiteSpace(ch) Then
                ErrorLexico(pos + 1, "Falta espacio entre número de línea y sentencia")
                Return False
            End If
        End If

        ' Emitir token LINE
        Dim tLinea As New Token(TokenID.TCO_LINE, NroLineaPrograma.ToString(), NroLineaFichero, 1)
        GuardaToken(tLinea)

        ' Consumir espacios / tabs tras el número
        While pos < LineaAnalizar.Length AndAlso Char.IsWhiteSpace(LineaAnalizar(pos))
            Avanzar()
        End While

        Return True
    End Function


    ' ============================================================
    ' Consumidores de tokens
    ' ============================================================
    ' ============================================================
    ' Detectar inicio de REM (comentario)
    ' ============================================================
    Private Function EsInicioREM(LineaAnalizar As String) As Boolean

        ' Necesitamos al menos 3 caracteres
        If pos + 2 >= LineaAnalizar.Length Then Return False

        ' Comprobar REM (case-insensitive)
        If Not String.Equals(LineaAnalizar.Substring(pos, 3), "REM",
                          StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        ' Si REM está al final de línea → válido
        If pos + 3 >= LineaAnalizar.Length Then Return True

        ' El carácter siguiente NO debe ser letra ni dígito
        ' (para evitar aceptar REMARK, REM1, etc.)
        Dim c As Char = LineaAnalizar(pos + 3)
        Return Not Char.IsLetterOrDigit(c)

    End Function


    Private Function ConsumirNumero(col As Integer, LineaAnalizar As String) As Token

        Dim sb As New StringBuilder()
        Dim puntos As Integer = 0

        ' Caso especial: número que empieza por punto (.2)
        If LineaAnalizar(pos) = "."c Then
            sb.Append("0")
        End If

        While pos < LineaAnalizar.Length
            Dim ch = LineaAnalizar(pos)

            If Char.IsDigit(ch) Then
                sb.Append(ch)
                Avanzar()

            ElseIf ch = "."c Then
                puntos += 1
                If puntos > 1 Then
                    ErrorLexico(col, "Número mal formado")
                    Return Nothing
                End If

                sb.Append(ch)
                Avanzar()

            Else
                Exit While
            End If
        End While

        Return New Token(TokenID.TES_NUMBER, sb.ToString(), NroLineaFichero, col)

    End Function


    ' ============================================================
    ' Consumir identificador o palabra reservada (estrategia final)
    ' ============================================================
    Private Function ConsumirIdentificador(col As Integer, LineaAnalizar As String) As Token
        Dim sb As New StringBuilder()
        Dim posInicial As Integer = pos

        ' ---------------------------------------------
        ' 1. Consumir letras / dígitos / $
        ' ---------------------------------------------
        While pos < LineaAnalizar.Length
            Dim ch As Char = LineaAnalizar(pos)

            If Char.IsLetterOrDigit(ch) OrElse ch = "$"c Then
                sb.Append(ch)
                Avanzar()
            Else
                Exit While
            End If
        End While

        '' --------------------------------------------------------
        '' 2. Caso especial: GO TO / GO SUB separados por espacio
        '' --------------------------------------------------------
        'If sb.ToString().ToUpperInvariant() = "GO" Then
        '    If pos < LineaAnalizar.Length AndAlso LineaAnalizar(pos) = " "c Then

        '        Dim savePos As Integer = pos
        '        Avanzar() ' consumir espacio

        '        ' Leer siguiente palabra (TO / SUB)
        '        Dim sb2 As New StringBuilder()
        '        While pos < LineaAnalizar.Length AndAlso Char.IsLetter(LineaAnalizar(pos))
        '            sb2.Append(Char.ToUpperInvariant(LineaAnalizar(pos)))
        '            Avanzar()
        '        End While

        '        Select Case sb2.ToString()
        '            Case "TO"
        '                Return New Token(TokenID.TK_GOTO, "", NroLineaFichero, col)

        '            Case "SUB"
        '                Return New Token(TokenID.TK_GOSUB, "", NroLineaFichero, col)

        '            Case Else
        '                ' No era GO TO / GO SUB → volver atrás
        '                pos = savePos
        '        End Select
        '    End If
        'End If

        ' ---------------------------------------------
        ' 3. Clasificación normal
        ' ---------------------------------------------
        Dim lexeme As String = sb.ToString()
        If lexeme = "" Then
            ErrorLexico(col, "identificador vacío")
            Return Nothing
        End If

        Dim upper As String = lexeme.ToUpperInvariant()
        Dim id As TokenID

        ' Palabra reservada
        If ReservedWords.GetTokenID(upper, id) Then
            Return New Token(id, "", NroLineaFichero, col)
        End If

        ' Identificador normal
        Return New Token(TokenID.TES_IDENT, upper.ToLowerInvariant(), NroLineaFichero, col)

    End Function

    Private Function ConsumirStringLiteral(col As Integer, LineaAnalizar As String) As Token

        ' Consumimos la comilla inicial
        Avanzar()

        Dim sb As New StringBuilder()
        Dim cerrado As Boolean = False

        While pos < LineaAnalizar.Length

            Dim ch = LineaAnalizar(pos)

            If ch = Constantes.C_COMILLAS Then
                ' ¿Comilla escapada ("")?
                If pos + 1 < LineaAnalizar.Length AndAlso LineaAnalizar(pos + 1) = Constantes.C_COMILLAS Then
                    ' "" -> una comilla literal
                    sb.Append(Constantes.C_COMILLAS)
                    Avanzar() ' primera "
                    Avanzar() ' segunda "
                Else
                    ' Cierre real de la cadena
                    Avanzar()
                    cerrado = True
                    Exit While
                End If
            Else
                sb.Append(ch)
                Avanzar()
            End If

        End While

        ' Cadena sin cerrar (comillas desbalanceadas)
        If Not cerrado Then
            ErrorLexico(col, "Cadena sin cerrar (comillas desbalanceadas)")
            Return New Token(TokenID.TCO_UNKNOWN, "", -1, -1)
        End If

        Return New Token(TokenID.TES_STRING, sb.ToString(), NroLineaFichero, col)

    End Function




    Private Sub ConsumirComentario(LineaAnalizar As String)

        Dim col As Integer = pos + 1

        ' Avanzar "REM"
        pos += 3

        ' Opcional: consumir un espacio tras REM
        If pos < LineaAnalizar.Length AndAlso LineaAnalizar(pos) = Constantes.C_ESPACIO Then
            Avanzar()
        End If

        ' Resto de la línea = comentario
        Dim texto As String = LineaAnalizar.Substring(pos)

        ' Token REM (sin payload)
        Dim tRem As New Token(TokenID.TK_REM, "", NroLineaFichero, col)

        ' Token STRING con el texto del comentario
        Dim tCom As New Token(TokenID.TES_STRING, texto, NroLineaFichero, pos + 1)

        GuardaToken(tRem)
        GuardaToken(tCom)

        ' Fin de línea
        pos = LineaAnalizar.Length

    End Sub


    Private Function ConsumirOperadorOSimbolo(col As Integer, LineaAnalizar As String) As Token

        ' --------------------------------------------
        ' Operadores de dos caracteres
        ' --------------------------------------------
        If pos + 1 < LineaAnalizar.Length Then
            Dim pair As String = LineaAnalizar.Substring(pos, 2)
            Select Case pair
                Case "<>"  ' distinto
                    pos += 2
                    Return New Token(TokenID.TOP_NE, "", NroLineaFichero, col)

                Case "<="  ' menor o igual
                    pos += 2
                    Return New Token(TokenID.TOP_LE, "", NroLineaFichero, col)

                Case ">="  ' mayor o igual
                    pos += 2
                    Return New Token(TokenID.TOP_GE, "", NroLineaFichero, col)
            End Select
        End If

        ' --------------------------------------------
        ' Operadores y símbolos de un carácter
        ' --------------------------------------------
        Dim c As Char = LineaAnalizar(pos)
        Avanzar()

        Select Case c
            Case "+"c : Return New Token(TokenID.TOP_PLUS, "", NroLineaFichero, col)
            Case "-"c : Return New Token(TokenID.TOP_MINUS, "", NroLineaFichero, col)
            Case "*"c : Return New Token(TokenID.TOP_MUL, "", NroLineaFichero, col)
            Case "/"c : Return New Token(TokenID.TOP_DIV, "", NroLineaFichero, col)
            Case "^"c : Return New Token(TokenID.TOP_POW, "", NroLineaFichero, col)

            Case "="c : Return New Token(TokenID.TOP_EQ, "", NroLineaFichero, col)
            Case "<"c : Return New Token(TokenID.TOP_LT, "", NroLineaFichero, col)
            Case ">"c : Return New Token(TokenID.TOP_GT, "", NroLineaFichero, col)

            Case "("c : Return New Token(TokenID.TSP_PAR_ABIERTO, "", NroLineaFichero, col)
            Case ")"c : Return New Token(TokenID.TSP_PAR_CERRADO, "", NroLineaFichero, col)

            Case ","c : Return New Token(TokenID.TSP_COMA, "", NroLineaFichero, col)
            Case ";"c : Return New Token(TokenID.TSP_PUNTOYCOMA, "", NroLineaFichero, col)
            Case ":"c : Return New Token(TokenID.TSP_DOSPUNTOS, "", NroLineaFichero, col)

            Case Else
                ErrorLexico(col, "Carácter no válido: '" & c & "'")
                Return New Token(TokenID.TCO_UNKNOWN, c.ToString(), NroLineaFichero, col)
        End Select

    End Function


    Private Sub Avanzar()
        pos += 1
    End Sub


    Private Sub ErrorLexico(columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If
        MostrarError(opts, stReader, stWriter, NroLineaPrograma, columna, LineaParaMostrar,
                     New String(" "c, columna) & "^ " & descripcion)
    End Sub

    Private Sub AddTokenEOL()
        Dim tEOL As New Token(TokenID.TCO_EOL, "", NroLineaFichero, 0)
        GuardaToken(tEOL)
    End Sub

    Private Sub AddTokenEOFL()
        Dim tEOF As New Token(TokenID.TCO_EOF, "", NroLineaFichero, 0)
        GuardaToken(tEOF)
    End Sub

    Private Sub GuardaToken(tk As Token)
        GuardaSalida(tk.TokToLine())
    End Sub


    Private Sub GuardaSalida(linea As String)

        stWriter.WriteLine(linea)

        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If
    End Sub



End Module