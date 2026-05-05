Option Strict On
Option Explicit On

Imports System
Imports System.Diagnostics.Eventing
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Xml

Public Module NormalizadorZX
    Dim opts As CmdOptions
    Dim pos As Integer
    Dim NroErrores As Integer = 0
    Dim LineaParaMostrar As String = ""
    Dim NroLineaPrograma As Integer = 0
    Dim NroLineaFichero As Integer = 0
    Dim LastToken As Token
    Dim NroTokens As Integer = 0
    Dim necesitaNormalizacionZX As Boolean = False
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
        Dim nombreAcumulado As String = ""

        While Not stReader.EndOfStream
            Dim lineaLeida As String = stReader.ReadLine()

            ' 🔒 Ignorar líneas en blanco o solo con espacios
            If String.IsNullOrWhiteSpace(lineaLeida) Then
                Continue While
            End If

            If PrimeraLinea Then
                If Not lineaLeida.StartsWith(Constantes.LEX_NOMBRE) Then
                    ErrorNormalizador(0, "[ERROR] No es un fichero " & Constantes.LEX_NOMBRE & ": " & lineaLeida)
                    Return (1)
                End If

                If Not lineaLeida.StartsWith(Constantes.LEX_NOMBRE & " " & Constantes.LEX_VERSION) Then
                    ErrorNormalizador(0, "[ERROR] Versión incorrecta del fichero " & Constantes.LEX_NOMBRE & ": " & lineaLeida)
                    Return (1)
                End If

                GuardaSalida($"{Constantes.TKZ_NOMBRE} {Constantes.TKZ_VERSION}")

                PrimeraLinea = False
                Continue While
            End If

            ' --------------------------------------------
            ' Línea original (contexto para el  error)
            ' --------------------------------------------            
            If lineaLeida.StartsWith(MarcaSRC) Then
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, lineaLeida)
                GuardaSalida(lineaLeida)
                Continue While
            End If

            ' --------------------------------------------
            ' Procesar el resto de Líneas
            ' -------------------------------------------- 
            procesarLinea(lineaLeida, nombreAcumulado)
        End While

        ' Vaciar acumulado final
        GuardarVariable(nombreAcumulado)

        AddTokenEOFL()
        stReader.Close()
        stWriter.Close()

        Return NroErrores
    End Function

    Private Sub procesarLinea(lineaLeida As String, ByRef nombreAcumulado As String)

        ' 1) GO TO / GO SUB (consume token)
        If BuscarConEspacio(lineaLeida, nombreAcumulado) Then
            nombreAcumulado = ""
            Exit Sub
        End If

        ' 2) Acumulación normal
        If EsTokenIdentificador(lineaLeida) Then
            nombreAcumulado &= valorToken(lineaLeida)
            Exit Sub
        End If

        ' 3) GOTOn / GOSUBn
        If BuscarConNumero(lineaLeida, nombreAcumulado) Then
            nombreAcumulado = ""
            GuardaSalida(lineaLeida)
            Exit Sub
        End If

        ' 4) Volcado normal
        GuardarVariable(nombreAcumulado)
        nombreAcumulado = ""
        GuardaSalida(lineaLeida)
    End Sub

    ' -------------------------------------------------------------------------------------------------
    ' PROCESOS AUXILIARES PARA NORMALZIAR
    ' -------------------------------------------------------------------------------------------------
    Private Function BuscarConEspacio(lineaLeida As String, nombreAcumulado As String) As Boolean
        If nombreAcumulado = "" Then
            Return False
        End If

        Dim id As String = nombreAcumulado.ToUpperInvariant()

        ' Caso GO TO / GO SUB
        If id = "GO" Then

            ' GO TO
            If EsTokenTO(lineaLeida) Then
                GuardaToken(New Token(TokenID.TK_GOTO, ""))
                Return True
            End If

            ' GO SUB
            If EsTokenIdentificador(lineaLeida) AndAlso
               valorToken(lineaLeida).ToUpperInvariant() = "SUB" Then
                GuardaToken(New Token(TokenID.TK_GOSUB, ""))
                Return True
            End If

        End If

        Return False
    End Function

    Private Function BuscarConNumero(lineaLeida As String,
                                 nombreAcumulado As String) As Boolean
        If nombreAcumulado = "" Then
            Return False
        End If

        Dim id As String = nombreAcumulado.ToUpperInvariant()

        ' Caso GOTOn
        If id.StartsWith("GOTO") AndAlso id <> "GOTO" Then
            Dim resto As String = nombreAcumulado.Substring(4)
            If IsNumeric(resto) Then
                GuardaToken(New Token(TokenID.TK_GOTO, ""))
                GuardaToken(New Token(TokenID.TES_NUMBER, resto))
                Return True
            End If
        End If

        ' Caso GOSUBn
        If id.StartsWith("GOSUB") AndAlso id <> "GOSUB" Then
            Dim resto As String = nombreAcumulado.Substring(5)
            If IsNumeric(resto) Then
                GuardaToken(New Token(TokenID.TK_GOSUB, ""))
                GuardaToken(New Token(TokenID.TES_NUMBER, resto))
                Return True
            End If
        End If

        Return False
    End Function

    Private Sub GuardarVariable(nombre As String)

        If nombre.Length = 0 Then Exit Sub

        Dim id As TokenID

        If ReservedWords.GetTokenID(nombre, id) Then
            ErrorNormalizador(0, $"La variable '{nombre}' escrita con espacios es una palabra reservada.")
            Exit Sub
        End If

        GuardaToken(New Token(TokenID.TES_IDENT, nombre))

    End Sub

    Private Function EsTokenIdentificador(linea As String) As Boolean
        Return (EsToken(TokenID.TES_IDENT, linea))
    End Function

    Private Function EsTokenTO(linea As String) As Boolean
        Return (EsToken(TokenID.TK_TO, linea))
    End Function

    Private Function EsToken(tkID As TokenID, linea As String) As Boolean
        Dim tk As New Token(linea)
        If tk.ID = tkID Then
            Return True
        End If
        Return False
    End Function

    Private Function valorToken(linea As String) As String
        Dim tk As New Token(linea)
        Return tk.Value
    End Function

    ' ---------------------------------------------------------------------------------------
    ' Guardar en salida y errores
    ' ---------------------------------------------------------------------------------------
    Private Sub ErrorNormalizador(columna As Integer, descripcion As String)
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
        If Not necesitaNormalizacionZX And NroTokens <> 0 Then
            If LastToken.ID = TokenID.TES_IDENT And tk.ID = TokenID.TES_IDENT Then
                necesitaNormalizacionZX = True
            End If
        End If
        NroTokens += 1
        LastToken = tk

        GuardaSalida(tk.TokToLine())
    End Sub


    Private Sub GuardaSalida(linea As String)

        stWriter.WriteLine(linea)

        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If
    End Sub


End Module