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
    Dim NroErrores As Integer = 0
    Dim LineaParaMostrar As String = ""
    Dim NroLineaPrograma As Integer = 0
    Dim NroLineaFichero As Integer = 0
    Dim encontradoEOF As Boolean = False
    Dim bufferLinea As New List(Of Token)
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

            ' --------------------------------------------
            ' Primera línea, versión
            ' --------------------------------------------  
            If PrimeraLinea Then
                Dim resultado As String = ""
                If Not GetVersion(opts, lineaLeida, resultado) Then
                    ErrorNormalizador(0, resultado)
                Else
                    GuardaSalida(resultado)
                End If
                PrimeraLinea = False
                Continue While
            End If

            ' --------------------------------------------
            ' Línea original (contexto para el  error)
            ' --------------------------------------------            
            If lineaLeida.StartsWith(Marca_SRC) Then
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, lineaLeida)
                GuardaSalida(lineaLeida)
                Continue While
            End If

            ' --------------------------------------------
            ' Procesar el resto de Líneas. Token normal
            ' --------------------------------------------
            Dim tok As New Token(lineaLeida)
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
                ParsearLineaTokens(bufferLinea, NroLineaFichero)
                bufferLinea.Clear()
            End If

        End While

        If NroErrores = 0 AndAlso Not encontradoEOF Then
            MostrarMensaje(opts, "[ERROR PARSER] Fichero TOK incompleto: falta EOF, posible fichero truncado")
            Return 1
        End If


        AddTokenEOFL()
        stWriter.Flush()
        stReader.Close()
        stWriter.Close()

        Return NroErrores
    End Function

    ' ============================================================
    ' PROCESAR DE UNA LÍNEA ZX (lista de strings hasta EOL)
    ' ============================================================
    Private Sub ParsearLineaTokens(lineaTokens As List(Of Token), IndiceLineaAST As Integer)

        ' -----------------------------------------
        ' Normalizar GOTO / GOSUB
        ' -----------------------------------------
        lineaTokens = ProcesarSaltos(lineaTokens)

        ' -----------------------------------------
        ' Normalizar nombres (variables con espacios)
        ' -----------------------------------------
        lineaTokens = ProcesarNombres(lineaTokens)

        ' -----------------------------------------
        ' Ajustar los paréntesis de las funciones
        ' -----------------------------------------
        ValidarParentesis(lineaTokens)                         'Valida que los paréntesis esten equilibrados para evitar problemas en el análisis posterior
        lineaTokens = ProcesarFuncionesRecursivo(lineaTokens)  'Añade [] a las funciones para evitar ambiguedades

        ' -----------------------------------------
        ' Emitir resultado
        ' -----------------------------------------
        For Each tk In lineaTokens
            GuardaToken(tk)
        Next

    End Sub


    ' ============================================================================
    ' === Ajusta las funciones añadiendo los paréntesis necesarios             ===
    ' ============================================================================

    
    Private Function ValidarParentesis(tokens As List(Of Token)) As Boolean

        Dim nivel As Integer = 0

        For Each tk In tokens

            If tk.ID = TokenID.TSP_PAR_ABIERTO Then
                nivel += 1
            End If

            If tk.ID = TokenID.TSP_PAR_CERRADO Then
                nivel -= 1

                ' ❌ cierre sin apertura
                If nivel < 0 Then
                    ErrorNormalizador(tk.Col, "Cierre de paréntesis inesperado")
                    Return False
                End If
            End If

        Next

        ' ❌ no se cerraron todos
        If nivel <> 0 Then
            ErrorNormalizador(0, "Paréntesis no equilibrados")
            Return False
        End If

        Return True

    End Function

    Private Function ProcesarFuncionesRecursivo(ByRef tokens As List(Of Token)) As List(Of Token)

        Dim resultado As New List(Of Token)
        Dim i As Integer = 0

        While i < tokens.Count

            Dim tk = tokens(i)

            ' -----------------------------------------
            ' FUNCIÓN
            ' -----------------------------------------
            If tk.IsFunction() Then

                Dim arity As Integer = tk.getAridad()

                ' insertar inicio
                resultado.Add(New Token(TokenID.TES_INI_PAR, "["))

                resultado.Add(tk)

                ' -------------------------------------
                ' ARIDAD 0 → sin argumento
                ' -------------------------------------
                If arity = 0 Then

                    ' cerrar inmediatamente
                    resultado.Add(New Token(TokenID.TES_FIN_PAR, "]"))

                    i += 1
                    Continue While
                End If

                ' -------------------------------------
                ' ARIDAD 1 → un operando
                ' -------------------------------------
                resultado.Add(New Token(TokenID.TES_INI_PAR, "["))

                i += 1 ' avanzar al operando

                Dim fin As Integer = i

                ' resolver el operando (posiblemente recursivo)
                Dim subTokens = ExtraerOperando(tokens, fin)

                Dim procesado = ProcesarFuncionesRecursivo(subTokens)

                resultado.AddRange(procesado)

                resultado.Add(New Token(TokenID.TES_FIN_PAR, "]"))
                resultado.Add(New Token(TokenID.TES_FIN_PAR, "]"))

                i = fin
                Continue While
            End If

            ' -----------------------------------------
            ' PARÉNTESIS ()
            ' -----------------------------------------
            If tk.ID = TokenID.TSP_PAR_ABIERTO Then

                Dim fin As Integer = i
                Dim subTokens = ExtraerParentesis(tokens, fin)

                Dim procesado = ProcesarFuncionesRecursivo(subTokens)

                resultado.Add(New Token(TokenID.TSP_PAR_ABIERTO, "("))
                resultado.AddRange(procesado)
                resultado.Add(New Token(TokenID.TSP_PAR_CERRADO, ")"))

                i = fin
                Continue While
            End If

            ' -----------------------------------------
            ' NORMAL
            ' -----------------------------------------
            resultado.Add(tk)
            i += 1

        End While

        Return resultado

    End Function

    Private Function ExtraerOperando(tokens As List(Of Token), ByRef pos As Integer) As List(Of Token)

        Dim salida As New List(Of Token)

        If pos >= tokens.Count Then Return salida

        Dim tk = tokens(pos)

        ' -----------------------------------------
        ' CASO 1: paréntesis
        ' -----------------------------------------
        If tk.ID = TokenID.TSP_PAR_ABIERTO Then
            Return ExtraerParentesis(tokens, pos)
        End If

        ' -----------------------------------------
        ' CASO 2: función (recursivo)
        ' -----------------------------------------
        If tk.IsFunction() Then

            ' la función completa es el operando
            Dim inicio As Integer = pos
            pos += 1 ' saltar función

            Dim subl = ExtraerOperando(tokens, pos)

            salida.AddRange(tokens.GetRange(inicio, pos - inicio))
            Return salida
        End If

        ' -----------------------------------------
        ' CASO 3: unario (-)
        ' -----------------------------------------
        If tk.ID = TokenID.TOP_MINUS Then

            salida.Add(tk)
            pos += 1

            Dim subl = ExtraerOperando(tokens, pos)
            salida.AddRange(subl)

            Return salida
        End If

        ' -----------------------------------------
        ' CASO 4: operando simple
        ' -----------------------------------------
        salida.Add(tk)
        pos += 1

        Return salida

    End Function

    Private Function ExtraerParentesis(tokens As List(Of Token),
                                   ByRef pos As Integer) As List(Of Token)

        Dim salida As New List(Of Token)
        Dim nivel As Integer = 0

        Do
            Dim tk = tokens(pos)

            If tk.ID = TokenID.TSP_PAR_ABIERTO Then nivel += 1
            If tk.ID = TokenID.TSP_PAR_CERRADO Then nivel -= 1

            salida.Add(tk)
            pos += 1

        Loop While pos < tokens.Count AndAlso nivel > 0

        ' quitar los paréntesis externos
        salida.RemoveAt(0)
        salida.RemoveAt(salida.Count - 1)

        Return salida

    End Function

    ' ============================================================================
    ' === Ajusta GOTO/GOSUB a partir de GO TO, GO SUB, GOTOn, GOSUBn           ===
    ' ============================================================================

    Private Function ProcesarSaltos(tokens As List(Of Token)) As List(Of Token)

        Dim salida As New List(Of Token)
        Dim i As Integer = 0

        While i < tokens.Count

            Dim tk = tokens(i)
            If tk.ID = TokenID.TES_IDENT Then

                Dim txt As String = tk.Value.ToUpperInvariant()

                ' -----------------------------------------
                ' GOTOn
                ' -----------------------------------------
                If txt.StartsWith("GOTO") AndAlso txt.Length > 4 Then

                    Dim resto = txt.Substring(4)

                    If IsNumeric(resto) Then
                        salida.Add(New Token(TokenID.TK_GOTO, "", tk.Lin, tk.Col))
                        salida.Add(New Token(TokenID.TES_NUMBER, resto, tk.Lin, tk.Col))
                        i += 1
                        Continue While
                    End If
                End If

                ' -----------------------------------------
                ' GOSUBn
                ' -----------------------------------------
                If txt.StartsWith("GOSUB") AndAlso txt.Length > 5 Then

                    Dim resto = txt.Substring(5)

                    If IsNumeric(resto) Then
                        salida.Add(New Token(TokenID.TK_GOSUB, "", tk.Lin, tk.Col))
                        salida.Add(New Token(TokenID.TES_NUMBER, resto, tk.Lin, tk.Col))
                        i += 1
                        Continue While
                    End If
                End If

                ' -----------------------------------------
                ' GO ...
                ' -----------------------------------------
                If txt = "GO" AndAlso i < tokens.Count - 1 Then

                    Dim tk2 = tokens(i + 1)

                    ' -------------------------
                    ' GO TO
                    ' -------------------------
                    If tk2.ID = TokenID.TK_TO Then
                        salida.Add(New Token(TokenID.TK_GOTO, "", tk.Lin, tk.Col))
                        i += 2
                        Continue While
                    End If

                    ' -------------------------
                    ' GO SUB
                    ' -------------------------
                    If tk2.ID = TokenID.TES_IDENT AndAlso tk2.Value.ToUpperInvariant() = "SUB" Then
                        salida.Add(New Token(TokenID.TK_GOSUB, "", tk.Lin, tk.Col))
                        i += 2
                        Continue While
                    End If

                    ' -------------------------
                    ' GO TOn
                    ' -------------------------
                    If tk2.ID = TokenID.TES_IDENT Then

                        Dim txt2 As String = tk2.Value.ToUpperInvariant()

                        If txt2.StartsWith("TO") AndAlso txt2.Length > 2 Then

                            Dim resto = txt2.Substring(2)

                            If IsNumeric(resto) Then
                                salida.Add(New Token(TokenID.TK_GOTO, "", tk.Lin, tk.Col))
                                salida.Add(New Token(TokenID.TES_NUMBER, resto, tk2.Lin, tk2.Col))
                                i += 2
                                Continue While
                            End If
                        End If

                        ' -------------------------
                        ' GO SUBn
                        ' -------------------------
                        If txt2.StartsWith("SUB") AndAlso txt2.Length > 3 Then

                            Dim resto = txt2.Substring(3)

                            If IsNumeric(resto) Then
                                salida.Add(New Token(TokenID.TK_GOSUB, "", tk.Lin, tk.Col))
                                salida.Add(New Token(TokenID.TES_NUMBER, resto, tk2.Lin, tk2.Col))
                                i += 2
                                Continue While
                            End If
                        End If

                    End If

                End If

            End If

            ' =========================================
            ' Caso normal
            ' =========================================
            salida.Add(tk)
            i += 1

        End While

        Return salida

    End Function

    ' ============================================================================
    ' === Unifica los nombres de variables con espacios                        ===
    ' ============================================================================

    Private Function ProcesarNombres(tokens As List(Of Token)) As List(Of Token)

        Dim salida As New List(Of Token)
        Dim acumulado As String = ""
        Dim firstToken As Token = Nothing

        For Each tk In tokens

            If tk.ID = TokenID.TES_IDENT Then

                If acumulado = "" Then firstToken = tk
                acumulado &= tk.Value

            Else

                If acumulado <> "" Then
                    salida.Add(New Token(TokenID.TES_IDENT, acumulado, firstToken.Lin, firstToken.Col))
                    acumulado = ""
                End If

                salida.Add(tk)

            End If

        Next

        If acumulado <> "" Then
            salida.Add(New Token(TokenID.TES_IDENT, acumulado, firstToken.Lin, firstToken.Col))
        End If

        Return salida

    End Function

    ' ============================================================================
    ' === Guardar en salida y errores                                          ===
    ' ============================================================================
    Private Sub ErrorNormalizador(columna As Integer, descripcion As String)
        NroErrores += 1
        If (columna <> 0) Then
            columna = columna - 1
        End If
        MostrarError(opts, stReader, stWriter, NroLineaPrograma, columna, LineaParaMostrar,
                     New String(Constantes.C_ESPACIO, columna) & Constantes.Marca_Error & descripcion)
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