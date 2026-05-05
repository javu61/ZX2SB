Imports System
Imports System.IO
Imports System.Text
Imports ZX2SB
Imports ZX2SB.Constantes


Module Program_Parser

    ' ===============================
    ' Punto de entrada del Parser
    ' ===============================
    Sub Main(args As String())

        ' --- FORZAR UTF-8 ---
        Console.OutputEncoding = Encoding.UTF8
        Console.InputEncoding = Encoding.UTF8

        Dim opts As CmdOptions = Nothing
        Dim NroErrores As Integer

        ' --- Procesar argumentos ---
        ProcesarArgs(Constantes.MPar, args, opts)

        ' --- Llamada al PARSER ---
        Try
            NroErrores = Parser.Ejecutar(opts)
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
                MostrarError(opts, Nothing, Nothing, 0, 0, ex.Message, "")
                NroErrores += 1
            End If
        End Try

        MensajeFinal(opts, NroErrores)
        Environment.Exit(NroErrores)

    End Sub

End Module
