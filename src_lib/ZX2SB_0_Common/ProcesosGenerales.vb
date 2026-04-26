Imports System.Data
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Runtime.Intrinsics
Imports System.Runtime.Intrinsics.Arm
Imports System.Text

Public Module ProcesosGenerales

    Public Sub ProcesarArgs(Modulo As String, args() As String, ByRef opts As CmdOptions)

        Dim i As Integer
        Dim aux As String

        opts.Modulo = Modulo
        opts.FEntrada = ""
        opts.FSalidaLex = ""
        opts.FSalidaPar = ""
        opts.FSalidaSem = ""
        opts.FSalidaGSB = ""
        opts.FSalidaRen = ""
        opts.FSalida = ""
        opts.Opciones = ""
        opts.Batch = False
        opts.Silencioso = False
        opts.Verbose = False
        opts.Batch = False
        opts.SinWarnings = False
        opts.NoPararPorError = False
        opts.SinComentarios = False
        opts.ModoDebug = False
        opts.Funciones = 0
        opts.Ren_Paso = 10
        opts.Ren_Base = 1000
        opts.Ren_IND = 2

        ' Sin argumentos -> mostrar uso
        If args Is Nothing OrElse args.Length = 0 Then
            MostrarUso(opts)
        End If

        ' Primer argumento Obligatorio: fichero de entrada 
        opts.FEntrada = args(0)
        If opts.FEntrada.StartsWith("-") Then
            MostrarMensaje(opts, "ERROR: El primer argumento debe ser el fichero de entrada.")
            MostrarUso(opts)
        End If
        i = 1

        ' Segundo argumento opcional: fichero de salida
        If (args.Length > 1) Then
            If Not args(1).StartsWith("-") Then
                opts.FSalida = args(1)
                i += 1
            End If
        End If

        ' Procesar opts.Opciones
        Dim HayError As Boolean = False
        While i < args.Length
            HayError = Not args(i).StartsWith("-")

            If args(i).ToLower.StartsWith(Constantes.opFuncion) Then
                ' Si es Fx
                If args(i).Length > 1 AndAlso Char.IsDigit(args(i)(Constantes.opFuncion.Length)) Then
                    opts.Funciones = CInt(Char.GetNumericValue(args(i)(Constantes.opFuncion.Length)))
                    If (opts.Funciones <> opFuncion_Err) And
                           (opts.Funciones <> opFuncion_Msg) And
                           (opts.Funciones <> opFuncion_Ign) Then
                        HayError = True
                    Else
                        opts.Opciones &= args(i) & " "
                    End If
                Else
                    HayError = True
                End If
            ElseIf args(i).ToLower.StartsWith(Constantes.opBase) Then
                ' Si es RBx
                aux = args(i).Substring(Constantes.opBase.Length)
                Try
                    opts.Ren_Base = CInt(aux)
                Catch ex As Exception
                    opts.Ren_Base = 0
                    HayError = True
                End Try
            ElseIf args(i).ToLower.StartsWith(Constantes.opPaso) Then
                ' Si es RPx
                aux = args(i).Substring(Constantes.opPaso.Length)
                Try
                    opts.Ren_Paso = CInt(aux)
                Catch ex As Exception
                    opts.Ren_Paso = 0
                    HayError = True
                End Try
            ElseIf args(i).ToLower.StartsWith(Constantes.opIND) Then
                ' Si es RPx
                aux = args(i).Substring(Constantes.opIND.Length)
                Try
                    opts.Ren_IND = CInt(aux)
                Catch ex As Exception
                    opts.Ren_IND = 0
                    HayError = True
                End Try
            Else
                Select Case args(i).ToLower()
                    Case Constantes.opSilencioso
                        opts.Silencioso = True
                        opts.Opciones &= args(i) & " "

                    Case Constantes.opVerbose
                        opts.Verbose = True
                        opts.Opciones &= args(i) & " "

                    Case Constantes.opBath
                        opts.Batch = True
                        opts.Opciones &= args(i) & " "

                    Case Constantes.opNoWarnings
                        opts.SinWarnings = True
                        opts.Opciones &= args(i) & " "

                    Case Constantes.opContinuarSError
                        opts.NoPararPorError = True
                        opts.Opciones &= args(i) & " "

                    Case Constantes.opSinComentarios
                        opts.SinComentarios = True
                        opts.Opciones &= args(i) & " "

                    Case Constantes.opDebug
                        opts.ModoDebug = True
                        opts.Opciones &= args(i) & " "

                    Case Else
                        HayError = True
                End Select
            End If

            If HayError Then
                MostrarMensaje(opts, "Errores en parámetros: Opción desconocida: " & args(i))
                MostrarUso(opts)
                Environment.Exit(1)
            End If

            i += 1
        End While

        ' Precedencias de opts.Opciones
        If opts.Verbose Then
            opts.Silencioso = False
        End If

        If opts.Batch Then
            opts.Silencioso = True
            opts.NoPararPorError = True
            opts.Verbose = False
            opts.SinWarnings = False
        End If

        If opts.ModoDebug = True Then
            opts.SinComentarios = False
        End If

        ' Preparar el fichero de salida
        PrepararFicheros(opts)

        MostrarInicio(opts)
    End Sub

    Private Sub MostrarInicio(opts As CmdOptions)
        If Not opts.Batch Then
            MostrarMensaje(opts, " ")
            MostrarMensaje(opts, "- Entrada.: " & ObtenerFicheroEntrada(opts))
            MostrarMensaje(opts, "- Salida..: " & ObtenerFicheroSalida(opts, opts.Modulo))
            If (opts.Opciones <> "") Then
                MostrarMensaje(opts, "- Opciones: " & opts.Opciones)
            End If

            MostrarMensaje(opts, " ")
            MostrarMensaje(opts, " ")
        End If

    End Sub
    Private Sub MostrarUso(opts As CmdOptions)
        Dim Letra As String = opts.Modulo.Substring(1, 1)
        Dim Descripcion As String = ""
        Dim Extension As String = ""

        Select Case opts.Modulo
            Case Constantes.MDir
                Descripcion = "Director del proceso"
                Extension = Constantes.GQL_EXTENSION
            Case Constantes.MLex
                Descripcion = "Analizador Léxico"
                Extension = Constantes.LEX_EXTENSION
            Case Constantes.MPar
                Descripcion = "Analizador Sintáctico"
                Extension = Constantes.PAR_EXTENSION
            Case Constantes.MSem
                Descripcion = "Analizador Semántico"
                Extension = Constantes.SEM_EXTENSION
            Case Constantes.MGSB
                Descripcion = "Generador de SuperBasic"
                Extension = Constantes.GQL_EXTENSION
            Case Constantes.MRen
                Descripcion = "Renumerador"
                Extension = Constantes.REN_EXTENSION
        End Select

        'Versión 
        MostrarMensaje(opts, "Transpilador ZX. " & Descripcion & " v." & Constantes.VER_PROG & " (c)2026 javu61 ")

        ' Objetivos según módulos
        If opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Director del proceso de generación, llama a los componentes individuales necesarios:")
        End If
        If opts.Modulo = Constantes.MLex Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.LEX_EXTENSION & " con los tokens")
        End If
        If opts.Modulo = Constantes.MPar Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.PAR_EXTENSION & " con el arbol EDPen modo de texto IR")
        End If
        If opts.Modulo = Constantes.MSem Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.SEM_EXTENSION & " con el arbol EDP ajustado en modo de texto IR")
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.VAR_EXTENSION & " con las variables detectadas")
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.DATA_EXTENSION & " con los DATA detectados")
        End If
        If opts.Modulo = Constantes.MGSB Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.GQL_EXTENSION & " con los DATA detectados")
        End If
        If opts.Modulo = Constantes.MRen Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.REN_EXTENSION & " con el programa renumerado")
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.LIN_EXTENSION & " con las equivalencias de líneas")
        End If

        MostrarMensaje(opts, "Genera: un fichero " & Constantes.LOG_EXTENSION & " con los errores detectados")
        MostrarMensaje(opts, " ")
        MostrarMensaje(opts, "Uso:")
        If InStr("X___G", Letra) > 0 Then MostrarMensaje(opts, "  " & opts.Modulo & " Entrada [Salida] [opts.Opciones]")
        If InStr("_LPS_", Letra) > 0 Then MostrarMensaje(opts, "  " & opts.Modulo & " Entrada [opts.Opciones]")
        MostrarMensaje(opts, " ")
        MostrarMensaje(opts, "  Entrada Fichero a procesar (obligatorio)")
        If InStr("X___G", Letra) > 0 Then MostrarMensaje(opts, "  Salida  Fichero resultado (opcional). Si no se indica será Fichero_Entrada" & Extension)
        If InStr("_LPS_", Letra) > 0 Then MostrarMensaje(opts, "  Salida  Fichero resultador = <Entrada>" & Extension)
        MostrarMensaje(opts, "")

        ' Convención de módulos:
        ' Z = ZX2SB (Director)
        ' L = JLexer
        ' P = JParser
        ' S = JSemantic
        ' G = JGenerator
        ' R = Renumerador
        '
        ' Las cadenas "X__S__", etc., indican qué módulos soportan cada opción

        If InStr("XLPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opSilencioso) &
                                                               "Modo Silencioso. No muestra las líneas mientras se procesan.")
        If InStr("XLPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opVerbose) &
                                                               "Modo Verbose. Muestra más información. Anula " & "-s")
        If InStr("X_PSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opNoWarnings) &
                                                               "Modo No Warnings. No se muestran los warnings ni paran el proceso.")
        If InStr("XLPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opContinuarSError) &
                                                               "Modo Continuar si Errores. No se para al encontrar un error, el resultado se ve en el LOG.")
        If InStr("XLPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opBath) &
                                                               "Modo Batch. No muestra nada en pantalla ni interactúa. " &
                                                               "Activa " & Constantes.opSilencioso & " " &
                                                                           Constantes.opContinuarSError & " y " &
                                                               "Anula " & Constantes.opVerbose & " " &
                                                                        If(Letra <> "L", Constantes.opNoWarnings, ""))
        If InStr("X__SG_", Letra) > 0 Then MostrarMensaje(opts, " ")
        If InStr("X___G_", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opSinComentarios) &
                                                               "Modo Sin Comentarios. No añade los comentarios del fichero de origen.")
        If InStr("X___G_", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opDebug) &
                                                               "Modo Debug. Añade la línea del fichero original como un comentario sin " &
                                                               "número de línea.")
        If InStr("X___G_", Letra) > 0 Then MostrarMensaje(opts, " ")
        If InStr("X___G_", Letra) > 0 Then
            MostrarMensaje(opts, MostrarOpcion(Constantes.opFuncion & "n") & "Las sentencias no soportadas generan:")
            MostrarMensaje(opts, $"            {Constantes.opFuncion & Constantes.opFuncion_Err}: un error en la ejecución (si no se indica nada se usa esta opción).")
            MostrarMensaje(opts, $"            {Constantes.opFuncion & Constantes.opFuncion_Msg}: un mensaje en la ejecución.")
            MostrarMensaje(opts, $"            {Constantes.opFuncion & Constantes.opFuncion_Ign}: son ignoradas.")
        End If
        If InStr("X____R", Letra) > 0 Then
            MostrarMensaje(opts, MostrarOpcion(Constantes.opBase & "n") & "La renumeración comienza en n (si se omite será " & Constantes.opBase & "1000)")
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPaso & "n") & "La renumeración irá de n en n (si se omite será " & Constantes.opPaso & "10)")
            MostrarMensaje(opts, MostrarOpcion(Constantes.opIND & "n") & "Se indenta de n en n columnas (si se omite será " & Constantes.opIND & "2)")

        End If


        Environment.Exit(1)
    End Sub

    Private Function MostrarOpcion(opcion As String) As String
        Return ("     " & opcion & New String(" "c, 5 - opcion.Length))
    End Function

    Public Function ObtenerFicheroEntrada(opts As CmdOptions) As String
        Select Case opts.Modulo
            Case Constantes.MDir : Return opts.FEntrada
            Case Constantes.MLex : Return opts.FEntrada
            Case Constantes.MPar : Return opts.FSalidaLex
            Case Constantes.MSem : Return opts.FSalidaPar
            Case Constantes.MGSB : Return opts.FSalidaSem
            Case Constantes.MRen : Return opts.FSalidaGSB
        End Select
        Return ""
    End Function

    Private Function ObtenerFicheroSalida(opts As CmdOptions, Modulo As String) As String
        Select Case Modulo
            Case Constantes.MDir : Return opts.FSalida
            Case Constantes.MLex : Return opts.FSalidaLex
            Case Constantes.MPar : Return opts.FSalidaPar
            Case Constantes.MSem : Return opts.FSalidaSem
            Case Constantes.MGSB : Return opts.FSalidaGSB
            Case Constantes.MRen : Return opts.FSalidaRen
        End Select
        Return ""
    End Function

    Private Sub PrepararFicheros(ByRef opts As CmdOptions)

        If Not File.Exists(opts.FEntrada) Then
            Console.WriteLine("ERROR: No existe el fichero de entrada. Proceso finalizado")
            Environment.Exit(1)
        End If

        opts.FSalidaLex = Path.ChangeExtension(opts.FEntrada, Constantes.LEX_EXTENSION)
        opts.FSalidaPar = Path.ChangeExtension(opts.FEntrada, Constantes.PAR_EXTENSION)
        opts.FSalidaSem = Path.ChangeExtension(opts.FEntrada, Constantes.SEM_EXTENSION)
        opts.FSalidaGSB = Path.ChangeExtension(opts.FEntrada, Constantes.GQL_EXTENSION)
        If (opts.FSalida = "") Then
            opts.FSalidaRen = Path.ChangeExtension(opts.FEntrada, "")
            opts.FSalidaRen = opts.FSalidaRen & Constantes.REN_EXTENSION
        Else
            opts.FSalidaRen = opts.FSalida
        End If
        opts.FVar = Path.ChangeExtension(opts.FEntrada, Constantes.VAR_EXTENSION)
        opts.FData = Path.ChangeExtension(opts.FEntrada, Constantes.DATA_EXTENSION)
        opts.FLines = Path.ChangeExtension(opts.FEntrada, Constantes.LIN_EXTENSION)
        opts.FLog = Path.ChangeExtension(opts.FEntrada, Constantes.LOG_EXTENSION)    ' Fichero de log es común a todos

        ' Comprobar existencia de los ficheros de salida y preguntar sobrescribir / abortar
        Dim Eliminar As Boolean = opts.DesdeDirector
        Dim ListaFicheros As New List(Of String)
        Dim resp As String = "S"

        If opts.Modulo = Constantes.MLex Or opts.Modulo = Constantes.MDir Then
            ListaFicheros.Add(opts.FSalidaLex)
        End If

        If opts.Modulo = Constantes.MPar Or opts.Modulo = Constantes.MDir Then
            ListaFicheros.Add(opts.FSalidaPar)
        End If

        If opts.Modulo = Constantes.MSem Or opts.Modulo = Constantes.MDir Then
            ListaFicheros.Add(opts.FSalidaSem)
            ListaFicheros.Add(opts.FVar)
            ListaFicheros.Add(opts.FData)
        End If

        If opts.Modulo = Constantes.MGSB Or opts.Modulo = Constantes.MDir Then
            ListaFicheros.Add(opts.FSalidaGSB)
        End If

        If opts.Modulo = Constantes.MRen Or opts.Modulo = Constantes.MDir Then

            ListaFicheros.Add(opts.FSalidaRen)
            ListaFicheros.Add(opts.FLines)
        End If

        ListaFicheros.Add(opts.FLog)

        If ListaFicheros.Count <> 0 Then
            If (Not opts.Batch) Then
                Dim existen As Boolean = False
                For Each fichero In ListaFicheros
                    If File.Exists(fichero) Then
                        Console.WriteLine($">> {fichero} ya existe.")
                        existen = True
                    End If
                Next
                If (existen) Then
                    Console.Write("¿Desea borrarlo" & If(ListaFicheros.Count = 1, "", "s") & "? [S/N]: ")
                    resp = Console.ReadLine().Trim().ToUpper()

                    If (resp <> "S") And (resp <> "") Then
                        Console.WriteLine("Proceso cancelado por el usuario.")
                        Environment.Exit(1)
                    End If
                End If
            End If

            For Each fichero In ListaFicheros
                BorrarFichero(fichero, opts)
            Next

        End If

    End Sub

    Private Sub BorrarFichero(Fichero As String, opts As CmdOptions)
        If (Fichero <> "") Then
            If File.Exists(Fichero) Then
                Try
                    File.Delete(Fichero)
                Catch ex As Exception
                    MostrarMensaje(opts, $"[{opts.Modulo}][WARNING] No se pudo eliminar el fichero {Fichero}: {ex.Message}")
                End Try
                MostrarMensaje(opts, $"Fichero {Fichero} borrado")
            End If
        End If
    End Sub

    ' --- Eliminar el fichero generado si hay errores
    Public Sub EliminarFicheroErroneo(fichero As String, opts As CmdOptions)
        MostrarMensaje(opts, $"[{opts.Modulo}] Error detectado: eliminando fichero generado")
        BorrarFichero(fichero, opts)
    End Sub

    ' ============================================================
    ' Normaliza una línea para su visualización
    ' ============================================================
    Public Function NormalizarLinea(opts As CmdOptions, ByRef NroLineaFichero As Integer,
                                    ByRef NroLineaPrograma As Integer, linea As String) As String

        Dim sb As New StringBuilder(linea.Length)

        If linea.StartsWith(MarcaSRC) Then
            linea = linea.Substring(Len(MarcaSRC)).Trim()
        End If

        NroLineaFichero += 1
        For Each ch As Char In linea
            Select Case ch
                Case ControlChars.Tab
                    sb.Append("_"c)
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("#"c)
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next

        Dim partes = linea.Split(" "c, 2)
        Integer.TryParse(partes(0), NroLineaPrograma)

        Dim mostrar As Boolean = Not opts.Silencioso
        If (mostrar) And (opts.Modulo = Constantes.MGSB) And (Not opts.Verbose) Then
            mostrar = False
        End If

        If mostrar Then
            MostrarMensaje(opts, "" & (NroLineaFichero + 1).ToString("D3") & ": " & sb.ToString)
        End If

        Return sb.ToString().Trim()
    End Function

    Public Sub MostrarError(opts As CmdOptions, writer As StreamWriter, nLin As Integer, nCol As Integer, Linea1 As String, Line2 As String)
        MensajeError(opts, writer, False, nLin, nCol, Linea1, Line2)
    End Sub

    Public Sub MostrarWarning(opts As CmdOptions, writer As StreamWriter, nLin As Integer, nCol As Integer, Linea1 As String, Line2 As String)
        MensajeError(opts, writer, True, nLin, nCol, Linea1, "")
    End Sub


    Public Sub MensajeError(opts As CmdOptions, writer As StreamWriter, Warning As Boolean, nLin As Integer, nCol As Integer, Linea1 As String, Linea2 As String)
        Dim Msg As String = ""

        If nLin <> 0 And nCol <> 0 Then
            MostrarLineaDeError(opts, $"En linea {nLin} columna {nCol} ", Warning)
        ElseIf nLin <> 0 Then
            MostrarLineaDeError(opts, $"En linea {nLin} ", Warning)
        ElseIf nCol <> 0 Then
            MostrarLineaDeError(opts, $"En columna {nCol} ", Warning)
        End If

        If Linea1 <> "" Then MostrarLineaDeError(opts, Linea1, Warning)
        If Linea2 <> "" Then MostrarLineaDeError(opts, Linea2, Warning)
        MostrarMensaje(opts, "-")

        If Not opts.Batch AndAlso Not opts.NoPararPorError Then
            Console.Write("                 ¿Desea continuar (S/N)? ")
            Dim resp = Console.ReadLine().Trim().ToUpper()
            If (resp = "N") Then
                If writer IsNot Nothing Then writer.Close()
                EliminarFicheroErroneo(opts.FSalida, opts)
                Environment.Exit(1)
            End If
        End If
    End Sub

    Private Sub MostrarLineaDeError(opts As CmdOptions, msg As String)
        MostrarMensaje(opts, msg, 2)
    End Sub

    Private Sub MostrarLineaDeError(opts As CmdOptions, msg As String, Warning As Boolean)
        MostrarMensaje(opts, msg, If(Warning, 1, 2))
    End Sub

    Public Sub MostrarMensaje(opts As CmdOptions, msg As String)
        MostrarMensaje(opts, msg, 0)
    End Sub

    Public Sub MostrarVerbose(opts As CmdOptions, msg As String)
        If (msg <> " ") Then
            msg = "     → " & msg
        End If
        MostrarMensaje(opts, msg)
    End Sub

    Private Sub MostrarMensaje(opts As CmdOptions, msg As String, tipo As Integer)
        If (msg <> "") Then
            If msg = "-" Then
                Console.WriteLine("")
            Else
                If (tipo = 2) Then
                    msg = "[ERROR] " & msg
                End If
                If (tipo = 1) Then
                    msg = "[WARNING] " & msg
                End If
                GrabarLog(opts, msg)
                If Not opts.Batch Then
                    Console.WriteLine(Separador(opts, msg))
                End If
            End If
        End If
    End Sub

    Public Sub GrabarLog(opts As CmdOptions, msg As String)
        If (msg <> "") AndAlso (Not opts.FLog = Nothing) Then
            Using writer As New StreamWriter(opts.FLog, True, New UTF8Encoding(False))
                Dim aux As String = Separador(opts, msg)
                writer.WriteLine(aux)
            End Using
        End If
    End Sub

    Public Function Separador(opts As CmdOptions, msg As String) As String
        Dim Pasada As String = If(opts.Pasada = 0, "", $".{opts.Pasada}")
        Dim aux As String = If(msg = "-", " ", "[" & opts.Modulo & Pasada & "] " & msg)
        Return (aux)
    End Function
End Module

