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
    Sub Main1(args As String())

        ' --- UTF-8 siempre ---
        Console.OutputEncoding = Encoding.UTF8
        Console.InputEncoding = Encoding.UTF8

        Dim opts As CmdOptions = Nothing
        Dim NroErrores As Integer = 0

        Try
            ' --- Procesar argumentos ---
            ProcesarArgs(Constantes.MDir, args, opts)
            opts.DesdeDirector = True

            ' ====================================================
            ' 1. LEXER
            ' ====================================================
            'ProcesarArgs(Constantes.MLex, args, opts)
            opts.Modulo = Constantes.MLex
            NroErrores = Lexer.Ejecutar(opts)
            If NroErrores <> 0 Then
                Throw New ApplicationException("Errores en el Lexer")
            End If

            ' ====================================================
            ' 2. PARSER / SINTÁCTICO
            ' ====================================================
            'ProcesarArgs(Constantes.MPar, args, opts)
            opts.Modulo = Constantes.MPar
            NroErrores = Parser.Ejecutar(opts)
            If NroErrores <> 0 Then
                Throw New ApplicationException("Errores en el Parser")
            End If

            ' ====================================================
            ' 3. SEMÁNTICO
            ' ====================================================
            'ProcesarArgs(Constantes.MSem, args, opts)
            Try
                opts.Modulo = Constantes.MSem
                NroErrores = Semantic.Ejecutar(opts)
                If NroErrores <> 0 Then
                    Throw New ApplicationException("Errores semánticos")
                End If
            Catch er As Exception
                Console.WriteLine(er.ToString)
            End Try

            ' ====================================================
            ' 4. GENERACIÓN DE CÓDIGO (lógico, sin numerar)
            ' ====================================================
            'ProcesarArgs(Constantes.MGSB, args, opts)
            opts.Modulo = Constantes.MGSB
            NroErrores = Generator.Ejecutar(opts)
            If NroErrores <> 0 Then
                Throw New ApplicationException("Errores generando")
            End If

            ' ====================================================
            ' 5. RENUMERACIÓN (si aplica al backend)
            ' ====================================================
            'ProcesarArgs(Constantes.MRen, args, opts)
            opts.Modulo = Constantes.MRen
            NroErrores = Renumerador.Ejecutar(opts)
            If NroErrores <> 0 Then
                Throw New ApplicationException("Errores generando")
            End If

            ' ====================================================
            ' 6. INICIALIZACIÓN DEL BACKEND
            ' ====================================================
            'programa = InicializadorDriver.Aplicar(programa, opts)

            ' ====================================================
            ' 7. SALIDA FINAL
            ' ====================================================
            ''EscritorSalida.Escribir(programa, opts)

        Catch ex As Exception
            If opts.ModoDebug Then
                'Mostrar la traza completa
                Console.WriteLine("EXCEPCIÓN NO CONTROLADA:")
                Console.WriteLine(ex.Message)
                Console.WriteLine("---- STACK TRACE ----")
                Console.WriteLine(ex.StackTrace)
                Throw
            Else
                ' Nunca propagamos excepciones
                MostrarMensaje(opts, " ")
                MostrarError(opts, Nothing, 0, 0, ex.Message, "")
                NroErrores += 1
            End If
        End Try

        MostrarMensaje(opts, " ")

        If NroErrores = 0 Then
            MostrarMensaje(opts, "Finalizado correctamente")
        Else
            MostrarError(opts, Nothing, 0, 0, $"Finalizado con {NroErrores} " & If(NroErrores = 1, "error", "errores"), "")
        End If

        Environment.Exit(NroErrores)

    End Sub

End Module
