Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Reflection.Emit
Imports System.Text
Imports ZX2SB
Imports ZX2SB.Constantes
Imports ZX2SB.Lexer
Imports ZX2SB.Parser
Imports ZX2SB.Semantic
Imports ZX2SB.Generator
Imports ZX2SB.Renumerador

Module Program_Director

    ' ===============================
    ' Punto de entrada del Director
    ' ===============================
    Sub Main(args As String())

        ' --- UTF-8 siempre ---
        Console.OutputEncoding = Encoding.UTF8
        Console.InputEncoding = Encoding.UTF8

        Dim opts As CmdOptions = Nothing
        Dim NroErrores As Integer = 0

        ' --- Procesar argumentos ---

        ' PARA DEBUG Simple
        '#If DEBUG Then
        If args.Length = 0 Then
            args = New String() {
            "C:\Proyectos\zx2sb\ejemplos\hello.bas",
            "-p"
            }
        End If
        '#End If



        Dim ListaProcesos As List(Of Procesos) = ProcesarArgs(Constantes.MDir, args, opts)

        Try

            opts.DesdeDirector = True
            NroErrores = 0

            For Each p In ListaProcesos
                MostrarMensaje(opts, $"[DIRECTOR] Ejecutando {NombreProceso(p)}")

                Dim errores As Integer = EjecutarProceso(p, opts)
                NroErrores += errores

                If errores <> 0 Then
                    Throw New ApplicationException($"Errores en {NombreProceso(p)}")
                End If

            Next

        Catch ex As Exception

            If opts.ModoDebug Then
                'Mostrar la traza completa
                Console.WriteLine("[DIRECTOR] EXCEPCIÓN NO CONTROLADA:")
                Console.WriteLine(ex.Message)
                Console.WriteLine("[DIRECTOR] ---- STACK TRACE ----")
                Console.WriteLine(ex.StackTrace)
                Throw
            Else
                ' Nunca propagamos excepciones
                MostrarMensaje(opts, " ")
                MostrarError(opts, Nothing, Nothing, 0, 0, ex.Message, "")
                NroErrores += 1
            End If
        End Try

        MensajeFinal(opts, NroErrores)
        Environment.Exit(NroErrores)

    End Sub

    Private Function EjecutarProceso(p As Procesos, ByRef opts As CmdOptions) As Integer

        Select Case p

            Case Procesos.Lexer
                opts.Modulo = Constantes.MLex
                Return Lexer.Ejecutar(opts)

            Case Procesos.Normalizador
                opts.Modulo = Constantes.MNor
                Return NormalizadorZX.Ejecutar(opts)

            Case Procesos.Parser
                opts.Modulo = Constantes.MPar
                Return Parser.Ejecutar(opts)

            Case Procesos.Semantico
                opts.Modulo = Constantes.MSem
                Return Semantic.Ejecutar(opts)

            Case Procesos.Generador
                opts.Modulo = Constantes.MGSB
                Return Generator.Ejecutar(opts)

            Case Procesos.Renumerador
                opts.Modulo = Constantes.MRen
                Return Renumerador.Ejecutar(opts)

        End Select

        Return 0

    End Function


End Module
