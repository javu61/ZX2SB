Imports System.Data
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Runtime.Intrinsics
Imports System.Runtime.Intrinsics.Arm
Imports System.Text
Imports System.Xml

Public Module ProcesosGenerales

    Public Function ProcesarArgs(Modulo As String, args() As String, ByRef opts As CmdOptions) As List(Of Procesos)
        Dim ListaProcesos As New List(Of Procesos)
        Dim i As Integer
        Dim aux As String

        opts.Modulo = Modulo
        opts.Fase = SubFases.Base
        opts.FEntrada = ""
        opts.FSalidaLex = ""
        opts.FSalidaNor = ""
        opts.FSalidaPar = ""
        opts.FSalidaSem = ""
        opts.FSalidaVar = ""
        opts.FSalidaDat = ""
        opts.FSalidaFor = ""
        opts.FSalidaGSB = ""
        opts.FSalidaRen = ""
        opts.FSalida = ""
        opts.Opciones = ""

        opts.Ej_Proceso = Procesos.Ninguno
        opts.Ej_Hasta = False

        opts.Batch = False
        opts.Silencioso = False
        opts.Verbose = False
        opts.ZX = False
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
        If args(0).StartsWith("-") Then
            MostrarMensaje(opts, "ERROR: El primer argumento debe ser el fichero de entrada.")
            MostrarUso(opts)
        End If
        opts.FEntrada = args(0)
        i = 1

        If args(0) = "*" Then
            opts.FEntrada = "C:\Proyectos\ZX2SB\Ejemplos\hello.bas"
        End If


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
                Dim op As String = args(i).ToLower()
                Dim hasta As Boolean = False

                If (op.StartsWith("--")) Then
                    op = op.Substring(1)
                    hasta = True
                End If

                Select Case op
                    Case Constantes.opPrLex : SetProceso(opts, Procesos.Lexer, hasta)
                    Case Constantes.opPrNor : SetProceso(opts, Procesos.Normalizador, hasta)
                    Case Constantes.opPrPar : SetProceso(opts, Procesos.Parser, hasta)
                    Case Constantes.opPrSem : SetProceso(opts, Procesos.Semantico, hasta)
                    Case Constantes.opPrGen : SetProceso(opts, Procesos.Generador, hasta)
                    Case Constantes.opPrRen : SetProceso(opts, Procesos.Renumerador, hasta)

                    Case Constantes.opSilencioso : opts.Silencioso = True
                    Case Constantes.opVerbose : opts.Verbose = True
                    Case Constantes.opBath : opts.Batch = True
                    Case Constantes.opZX : opts.ZX = True
                    Case Constantes.opNoWarnings : opts.SinWarnings = True
                    Case Constantes.opContinuarSError : opts.NoPararPorError = True
                    Case Constantes.opSinComentarios : opts.SinComentarios = True
                    Case Constantes.opDebug : opts.ModoDebug = True
                    Case Else : HayError = True
                End Select
            End If

            If HayError Then
                MostrarMensaje(opts, "Errores en parámetros: Opción desconocida: " & args(i))
                MostrarUso(opts)
                Environment.Exit(1)
            Else
                opts.Opciones &= args(i) & " "
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
            opts.ZX = True
        End If

        If opts.ModoDebug = True Then
            opts.SinComentarios = False
        End If

        ' Construir pipeline
        If opts.Ej_Proceso = Procesos.Ninguno Then 'Si no se indica nada, se lanzan todos
            opts.Ej_Proceso = Procesos.Ultimo
            opts.Ej_Hasta = True
        End If

        Dim procesoInicial As Procesos = opts.Ej_Proceso
        Dim procesoFinal As Procesos = opts.Ej_Proceso

        If (procesoInicial < Procesos.Primero) Then
            procesoInicial = Procesos.Primero
        ElseIf (procesoInicial > Procesos.Ultimo) Then
            procesoInicial = Procesos.Ultimo
        End If

        If procesoFinal < procesoInicial Then
            procesoFinal = procesoInicial
        ElseIf (procesoFinal > Procesos.Ultimo) Then
            procesoFinal = Procesos.Ultimo
        End If

        ' Modo "hasta"
        If opts.Ej_Hasta Then
            procesoInicial = Procesos.Primero
        End If

        For p As Procesos = procesoInicial To procesoFinal
            ListaProcesos.Add(p)
        Next

        ' Preparar el fichero de salida
        PrepararFicheros(opts, ListaProcesos)

        MostrarInicio(opts)

        Return (ListaProcesos)
    End Function

    Private Sub SetProceso(ByRef opts As CmdOptions, p As Procesos, hasta As Boolean)

        ' Si este argumento es "hasta" explícito
        If hasta Then
            opts.Ej_Hasta = True
        End If

        ' Si ya había un proceso distinto, activamos modo "hasta"
        If opts.Ej_Proceso <> Procesos.Ninguno AndAlso p <> opts.Ej_Proceso Then
            opts.Ej_Hasta = True
        End If

        ' Elegir el proceso más avanzado
        If p > opts.Ej_Proceso Then
            opts.Ej_Proceso = p
        End If

    End Sub


    Private Sub MostrarInicio(opts As CmdOptions)
        If Not opts.Batch Then
            MostrarMensaje(opts, " ")
            MostrarMensaje(opts, "- Entrada.: " & ObtenerFicheroEntrada(opts))
            MostrarMensaje(opts, "- Salida..: " & ObtenerFicheroSalida(opts))
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
            Case Constantes.MNor
                Descripcion = "Normalizador ZX"
                Extension = Constantes.NOR_EXTENSION
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
            MostrarMensaje(opts, "Genera: opcionalmente un fichero " & Constantes.NOR_EXTENSION & " con los tokens normalizados")
        End If
        If opts.Modulo = Constantes.MNor Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.NOR_EXTENSION & " con los tokens normalizados")
        End If
        If opts.Modulo = Constantes.MPar Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.PAR_EXTENSION & " con el arbol EDPen modo de texto IR")
        End If
        If opts.Modulo = Constantes.MSem Or opts.Modulo = Constantes.MDir Then
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.SEM_EXTENSION & " con el arbol EDP ajustado en modo de texto IR")
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.VAR_EXTENSION & " con las variables detectadas")
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.DATA_EXTENSION & " con los DATA detectados")
            MostrarMensaje(opts, "Genera: un fichero " & Constantes.FOR_EXTENSION & " con los FOR/NEXT detectados")
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
        ' L = JLexer, FZX
        ' P = JParser
        ' S = JSemantic
        ' G = JGenerator
        ' R = Renumerador
        '
        ' Las cadenas "X__S__", etc., indican qué módulos soportan cada opción

        If InStr("XLNPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opSilencioso) &
                                                                "Modo Silencioso. No muestra las líneas mientras se procesan.")
        If InStr("XLNPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opVerbose) &
                                                                "Modo Verbose. Muestra más información. Anula " & "-s")
        If InStr("XLNPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opNoWarnings) &
                                                                "Modo No Warnings. No se muestran los warnings ni paran el proceso.")
        If InStr("XLNPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opContinuarSError) &
                                                                "Modo Continuar si Errores. No se para al encontrar un error, el resultado se ve en el LOG.")
        If InStr("XLNPSGR", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opBath) &
                                                                "Modo Batch. No muestra nada en pantalla ni interactúa. " &
                                                                "Activa " & Constantes.opSilencioso & " " &
                                                                            Constantes.opZX &
                                                                            Constantes.opContinuarSError & " y " &
                                                                "Anula " & Constantes.opVerbose & " " &
                                                                         If(Letra <> "L", Constantes.opNoWarnings, ""))
        If InStr("XL___G_", Letra) > 0 Then MostrarMensaje(opts, " ")
        If InStr("XL_____", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opZX) &
                                                                 "Modo ZX. Si existen variables con espacio las normaliza sin preguntar.")
        If InStr("X____G_", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opSinComentarios) &
                                                                 "Modo Sin Comentarios. No añade los comentarios del fichero de origen.")
        If InStr("X____G_", Letra) > 0 Then MostrarMensaje(opts, MostrarOpcion(Constantes.opDebug) &
                                                                 "Modo Debug. Añade la línea del fichero original como un comentario sin " &
                                                                 "número de línea.")
        If InStr("X____G_", Letra) > 0 Then MostrarMensaje(opts, " ")
        If InStr("X____G_", Letra) > 0 Then
            MostrarMensaje(opts, MostrarOpcion(Constantes.opFuncion & "n") & "Las sentencias no soportadas generan:")
            MostrarMensaje(opts, $"            {Constantes.opFuncion & Constantes.opFuncion_Err}: un error en la ejecución (si no se indica nada se usa esta opción).")
            MostrarMensaje(opts, $"            {Constantes.opFuncion & Constantes.opFuncion_Msg}: un mensaje en la ejecución.")
            MostrarMensaje(opts, $"            {Constantes.opFuncion & Constantes.opFuncion_Ign}: son ignoradas.")
        End If
        If InStr("X_____R", Letra) > 0 Then MostrarMensaje(opts, " ")
        If InStr("X_____R", Letra) > 0 Then
            MostrarMensaje(opts, MostrarOpcion(Constantes.opBase & "n") & "La renumeración comienza en n (si se omite será " & Constantes.opBase & "1000)")
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPaso & "n") & "La renumeración irá de n en n (si se omite será " & Constantes.opPaso & "10)")
            MostrarMensaje(opts, MostrarOpcion(Constantes.opIND & "n") & "Se indenta de n en n columnas (si se omite será " & Constantes.opIND & "2)")
        End If
        If InStr("XLNPSGR", Letra) > 0 Then MostrarMensaje(opts, " ")
        If InStr("XLNPSGR", Letra) > 0 Then
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPrLex) & "Lanza el proceso " & Opciones.NombreProceso(Opciones.Procesos.Lexer))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPrNor) & "Lanza el proceso " & Opciones.NombreProceso(Opciones.Procesos.Normalizador))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPrPar) & "Lanza el proceso " & Opciones.NombreProceso(Opciones.Procesos.Parser))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPrSem) & "Lanza el proceso " & Opciones.NombreProceso(Opciones.Procesos.Semantico))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPrGen) & "Lanza el proceso " & Opciones.NombreProceso(Opciones.Procesos.Generador))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opPrRen) & "Lanza el proceso " & Opciones.NombreProceso(Opciones.Procesos.Renumerador))
            MostrarMensaje(opts, " ")
            MostrarMensaje(opts, MostrarOpcion(Constantes.opToLex) & "Lanza todos los procesos hasta el " & Opciones.NombreProceso(Opciones.Procesos.Lexer))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opToNor) & "Lanza todos los procesos hasta el " & Opciones.NombreProceso(Opciones.Procesos.Normalizador))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opToPar) & "Lanza todos los procesos hasta el " & Opciones.NombreProceso(Opciones.Procesos.Parser))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opToSem) & "Lanza todos los procesos hasta el " & Opciones.NombreProceso(Opciones.Procesos.Semantico))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opToGen) & "Lanza todos los procesos hasta el " & Opciones.NombreProceso(Opciones.Procesos.Generador))
            MostrarMensaje(opts, MostrarOpcion(Constantes.opToRen) & "Lanza todos los procesos hasta el " & Opciones.NombreProceso(Opciones.Procesos.Renumerador))
        End If

        Environment.Exit(1)
    End Sub

    Private Function MostrarOpcion(opcion As String) As String
        Return ("     " & opcion & New String(Constantes.C_ESPACIO, 5 - opcion.Length))
    End Function

    Public Function ObtenerFicheroEntrada(opts As CmdOptions) As String
        Select Case opts.Modulo
            Case Constantes.MDir : Return opts.FEntrada
            Case Constantes.MLex : Return opts.FEntrada
            Case Constantes.MNor : Return opts.FSalidaLex
            Case Constantes.MPar
                'Seleccionar cual de los dos ficheros de tokens usar
                If File.Exists(opts.FSalidaNor) Then
                    Return opts.FSalidaNor
                Else
                    Return opts.FSalidaLex
                End If
            Case Constantes.MSem : Return opts.FSalidaPar
            Case Constantes.MGSB
                Select Case opts.Fase
                    Case SubFases.Base : Return opts.FSalidaSem
                    Case SubFases.Data : Return opts.FSalidaDat
                End Select


            Case Constantes.MRen : Return opts.FSalidaGSB
        End Select
        Return ""
    End Function

    Public Function ObtenerFicheroSalida(opts As CmdOptions) As String
        Select Case opts.Modulo
            Case Constantes.MDir : Return opts.FSalida
            Case Constantes.MLex : Return opts.FSalidaLex
            Case Constantes.MNor : Return opts.FSalidaNor
            Case Constantes.MPar : Return opts.FSalidaPar
            Case Constantes.MSem
                Select Case opts.Fase
                    Case SubFases.Base : Return opts.FSalidaSem
                    Case SubFases.Variables : Return opts.FSalidaVar
                    Case SubFases.Data : Return opts.FSalidaDat
                    Case SubFases.ForNext : Return opts.FSalidaFor
                End Select
            Case Constantes.MGSB : Return opts.FSalidaGSB
            Case Constantes.MRen : Return opts.FSalidaRen
        End Select
        Return ""
    End Function

    Private Sub PrepararFicheros(ByRef opts As CmdOptions, ListaProcesos As List(Of Procesos))

        If Not File.Exists(opts.FEntrada) Then
            Console.WriteLine("ERROR: No existe el fichero de entrada. Proceso finalizado")
            Environment.Exit(1)
        End If

        opts.FSalidaLex = Path.ChangeExtension(opts.FEntrada, Constantes.LEX_EXTENSION)
        opts.FSalidaNor = Path.ChangeExtension(opts.FEntrada, Constantes.NOR_EXTENSION)
        opts.FSalidaPar = Path.ChangeExtension(opts.FEntrada, Constantes.PAR_EXTENSION)
        opts.FSalidaSem = Path.ChangeExtension(opts.FEntrada, Constantes.SEM_EXTENSION)
        opts.FSalidaVar = Path.ChangeExtension(opts.FEntrada, Constantes.VAR_EXTENSION)
        opts.FSalidaDat = Path.ChangeExtension(opts.FEntrada, Constantes.DATA_EXTENSION)
        opts.FSalidaFor = Path.ChangeExtension(opts.FEntrada, Constantes.FOR_EXTENSION)
        opts.FSalidaGSB = Path.ChangeExtension(opts.FEntrada, Constantes.GQL_EXTENSION)
        If (opts.FSalida = "") Then
            opts.FSalidaRen = Path.ChangeExtension(opts.FEntrada, "")
            opts.FSalidaRen = opts.FSalidaRen & Constantes.REN_EXTENSION
        Else
            opts.FSalidaRen = opts.FSalida
        End If
        opts.FLines = Path.ChangeExtension(opts.FEntrada, Constantes.LIN_EXTENSION)
        opts.FLog = Path.ChangeExtension(opts.FEntrada, Constantes.LOG_EXTENSION)    ' Fichero de log es común a todos

        ' Comprobar existencia de los ficheros de salida y preguntar sobrescribir / abortar
        Dim Eliminar As Boolean = opts.DesdeDirector
        Dim ListaFicheros As New List(Of String)
        Dim resp As String = "S"

        For Each p In ListaProcesos
            Select Case p

                Case Procesos.Lexer
                    ListaFicheros.Add(opts.FSalidaLex)

                Case Procesos.Normalizador
                    ListaFicheros.Add(opts.FSalidaNor)

                Case Procesos.Parser
                    ListaFicheros.Add(opts.FSalidaPar)

                Case Procesos.Semantico
                    ListaFicheros.Add(opts.FSalidaSem)
                    ListaFicheros.Add(opts.FSalidaVar)
                    ListaFicheros.Add(opts.FSalidaDat)
                    ListaFicheros.Add(opts.FSalidaFor)

                Case Procesos.Generador
                    ListaFicheros.Add(opts.FSalidaGSB)

                Case Procesos.Renumerador
                    ListaFicheros.Add(opts.FSalidaRen)
                    ListaFicheros.Add(opts.FLines)
            End Select

        Next
        ' El LOG SIEMPRE
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
                    MostrarMensaje(opts, $"Fichero {Fichero} borrado")
                Catch ex As Exception
                    MostrarMensaje(opts, $"[{opts.Modulo}][WARNING] No se pudo eliminar el fichero {Fichero}: {ex.Message}")
                End Try
            End If
        End If
    End Sub

    ' --- Eliminar los ficheros generados si hay errores
    Public Sub EliminarFicheroErroneo(opts As CmdOptions)
        MostrarMensaje(opts, $"[{opts.Modulo}] Eliminando ficheros generados, revise el log")

        Select Case opts.Modulo
            Case Constantes.MDir
                BorrarFichero(opts.FSalidaLex, opts)
                BorrarFichero(opts.FSalidaNor, opts)
                BorrarFichero(opts.FSalidaPar, opts)
                BorrarFichero(opts.FSalidaSem, opts)
                BorrarFichero(opts.FSalidaGSB, opts)
                BorrarFichero(opts.FSalidaRen, opts)
            Case Constantes.MLex
                BorrarFichero(opts.FSalidaLex, opts)
                BorrarFichero(opts.FSalidaNor, opts)
            Case Else
                BorrarFichero(opts.FSalida, opts)
        End Select

    End Sub

    ' ============================================================
    ' Normaliza una línea para su visualización
    ' ============================================================
    Public Function NormalizarLinea(opts As CmdOptions, ByRef NroLineaFichero As Integer,
                                    ByRef NroLineaPrograma As Integer, linea As String) As String

        Dim sb As New StringBuilder(linea.Length)

        If linea.StartsWith(Marca_SRC) Then
            linea = linea.Substring(Len(Marca_SRC)).Trim()
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

    ' ============================================================
    ' Procesos de salida en pantalla
    ' ============================================================

    Public Function Preguntar(msg As String) As Boolean
        Dim resp As String = ""
        Console.WriteLine("")
        While True
            Console.WriteLine(msg & " (S/N)? ")

            resp = Console.ReadLine()
            resp = resp.Trim().ToUpper()
            If resp = "" Then resp = "S"
            resp = resp.Substring(0, 1)

            If (resp = "S") Or (resp = "N") Then
                Exit While
            End If

            Console.WriteLine("")
            Console.WriteLine("Responda S o N (o bien CR para S)")
        End While

        If (resp = "S") Then
            Return True
        End If
        Return False
    End Function

    Public Sub MensajeFinal(opts As CmdOptions, NroErrores As Integer)
        MostrarMensaje(opts, " ")
        If NroErrores = 0 Then
            MostrarMensaje(opts, "Finalizado correctamente")
        Else
            Dim texto As String = $"Finalizado con {NroErrores} " & If(NroErrores = 1, "error", "errores")
            MensajeError(opts, Nothing, Nothing, False, 0, 0, texto, "", True)
        End If
    End Sub

    Public Sub MostrarError(opts As CmdOptions, reader As StreamReader, writer As StreamWriter, nLin As Integer, nCol As Integer, Linea1 As String, Line2 As String)
        MensajeError(opts, reader, writer, False, nLin, nCol, Linea1, Line2, False)
    End Sub

    Public Sub MostrarWarning(opts As CmdOptions, reader As StreamReader, writer As StreamWriter, nLin As Integer, nCol As Integer, Linea1 As String, Line2 As String)
        MensajeError(opts, reader, writer, True, nLin, nCol, Linea1, "", False)
    End Sub


    Public Sub MensajeError(opts As CmdOptions, reader As StreamReader, writer As StreamWriter, Warning As Boolean, nLin As Integer, nCol As Integer, Linea1 As String, Linea2 As String, final As Boolean)
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

        If (Not opts.Batch) AndAlso (Not opts.NoPararPorError) Then
            Dim mens As String = ""
            If final Then
                mens = "                 ¿Desea mantener el fichero generado"
            Else
                mens = $"                 ¿Desea continuar"
            End If
            If (Not Preguntar(mens)) Then
                If reader IsNot Nothing Then reader.Close()
                If writer IsNot Nothing Then writer.Close()
                EliminarFicheroErroneo(opts)
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
                If (Not opts.Batch) Then
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

    Public Function GetVersion(opts As CmdOptions) As String
        Dim salida As String = ""
        GetVersion(opts, "", salida)
        Return salida
    End Function

    Public Function GetVersion(opts As CmdOptions, linea As String, ByRef salida As String) As Boolean
        Dim prg_Origen As String = ""
        Dim ver_Origen As String = ""
        Dim prg_Destino As String = ""

        Select Case opts.Modulo
            Case Constantes.MDir
                ver_Origen = ""
                prg_Destino = ""
            Case Constantes.MLex
                ver_Origen = ""
                prg_Destino = Constantes.LEX_NOMBRE & " " & Constantes.LEX_VERSION
            Case Constantes.MNor
                ver_Origen = Constantes.LEX_NOMBRE & " " & Constantes.LEX_VERSION
                prg_Destino = Constantes.TKZ_NOMBRE & " " & Constantes.TKZ_VERSION
            Case Constantes.MPar
                ver_Origen = Constantes.TKZ_NOMBRE & " " & Constantes.TKZ_VERSION
                prg_Destino = Constantes.PAR_NOMBRE & " " & Constantes.PAR_VERSION
            Case Constantes.MSem

                Select Case opts.Fase
                    Case SubFases.Base
                        ver_Origen = Constantes.PAR_NOMBRE & " " & Constantes.PAR_VERSION
                        prg_Destino = Constantes.SEM_NOMBRE & " " & Constantes.SEM_VERSION
                    Case SubFases.Variables
                        ver_Origen = ""
                        prg_Destino = Constantes.VAR_NOMBRE & " " & Constantes.VAR_VERSION
                    Case SubFases.Data
                        ver_Origen = ""
                        prg_Destino = Constantes.DATA_NOMBRE & " " & Constantes.DATA_VERSION
                    Case SubFases.ForNext
                        ver_Origen = ""
                        prg_Destino = Constantes.FOR_NOMBRE & " " & Constantes.FOR_VERSION
                End Select

            Case Constantes.MGSB
                Select Case opts.Fase
                    Case SubFases.Base
                        ver_Origen = Constantes.SEM_NOMBRE & " " & Constantes.SEM_VERSION
                        prg_Destino = Constantes.GQL_NOMBRE & " " & Constantes.GQL_VERSION
                    Case SubFases.Data
                        ver_Origen = Constantes.DATA_NOMBRE & " " & Constantes.DATA_VERSION
                        prg_Destino = ""
                End Select

            Case Constantes.MRen
                ver_Origen = Constantes.GQL_NOMBRE & " " & Constantes.GQL_VERSION
                prg_Destino = ""
        End Select

        salida = prg_Destino & " (Generado " & Now.ToString & ")"

        Dim i As Integer = InStr(ver_Origen, " ")
        If (i > 0) Then
            prg_Origen = ver_Origen.Substring(0, i).Trim


            If InStr(linea, prg_Origen) = -1 Then
                salida = $"No es un fichero {prg_Origen} de ZX2SB: {linea}"
                Return False
            End If

            If InStr(linea, ver_Origen) = -1 Then
                salida = $"Versión incorrecta del fichero {prg_Origen} de ZX2SB: {linea}"
                Return False
            End If

        End If

        Return True

    End Function

End Module

