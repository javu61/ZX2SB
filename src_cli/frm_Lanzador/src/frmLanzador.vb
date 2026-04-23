Imports System.Drawing
Imports System.IO
Imports System.Reflection.Metadata
Imports System.Windows.Forms
Imports System.Windows.Forms.VisualStyles.VisualStyleElement

Imports ZX2SB


Public Class frmLanzador
    'Estas dos contantes deben coincidir con las usadas en la impresión de AST y de Warnings, están definidas en el fichero de constantes
    Private Const MarcaAST As String = ChrW(&H2192)                   ' Marca para imprimir los AST  
    Private Const MarcaWarning As String = ChrW(&H21D2)               ' Marca para imprimir los Warnings 

    Private Sub frmLanzador_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        tbOrigen.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        tbOrigen.AutoCompleteSource = AutoCompleteSource.FileSystem
        rbGenerarSB.Checked = True
        tbSalida.ReadOnly = True         ' No se puede escribir
        tbSalida.BackColor = Color.White ' Para que no aparezca gris
        tbSalida.Cursor = Cursors.IBeam  ' Permite seleccionar texto
        tbSalida.Font = New Font("Consolas", 10, FontStyle.Regular)

        ' Para las pruebas
        tbOrigen.Text = "C:\Proyectos\ZX2SB\Ejemplos\Hello.bas"
        tbDestino.Text = "C:\Users\JoseAntonio\Desktop\mdv1\b"

        cbLanzarLexer.Checked = True
        cbLanzarParser.Checked = True
        cbLanzarSemantico.Checked = True
        cbLanzarGenerador.Checked = True

        ActualizarEstados()
    End Sub

    Private Sub AppendColoredLine(line As String)
        If line.Contains("[TOKEN]") Then
            PrintTokenLine(line)
        Else

            Dim color As Color = Color.Black

            If line.Contains("[ERROR") Then
                color = Color.Red
            ElseIf line.Contains("[Lanzador]") Then
                color = Color.BlueViolet
            ElseIf line.Contains("[Director]") Then
                color = Color.DarkGreen
            ElseIf line.Contains("[Lexer]") Then
                color = Color.DarkCyan
            ElseIf line.Contains("[Parser]") Then
                color = Color.Goldenrod
            ElseIf line.Contains("[AST]") Or line.StartsWith(MarcaAST) Then
                color = Color.DarkKhaki
            ElseIf line.Contains("[SEM]") Then
                color = Color.DarkMagenta
            ElseIf line.Contains("[VAR]") Then
                color = Color.Coral
            ElseIf line.StartsWith(MarcaWarning) Then
                color = Color.Brown
            End If

            AppendColoredText(line & vbCrLf, color)
        End If
    End Sub

    Private Sub AppendColoredText(line As String, Color As Color)
        tbSalida.SelectionStart = tbSalida.TextLength
        tbSalida.SelectionLength = 0
        tbSalida.SelectionColor = Color
        tbSalida.AppendText(line)
        tbSalida.SelectionColor = tbSalida.ForeColor
    End Sub

    Private Sub PrintTokenLine(linea As String)

        ' Eliminar el salto de línea final si lo trae
        linea = linea.TrimEnd(ControlChars.Cr, ControlChars.Lf)

        ' Separar partes
        ' [TOKEN]     Keyword REM
        '   0          1       2...
        Dim partes() As String =
        linea.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)

        If partes.Length < 2 Then
            ' Algo raro: pintamos todo normal
            AppendColoredText(linea & vbCrLf, Color.Black)
            Return
        End If

        Dim etiqueta As String = partes(0)        '[TOKEN]'
        Dim tipo As String = partes(1)            'Keyword
        Dim valor As String = ""

        If partes.Length > 2 Then
            valor = String.Join(" ", partes.Skip(2))
        End If

        ' Escribimos con colores
        AppendColoredText("        " & etiqueta & " ", Color.DodgerBlue)
        AppendColoredText(tipo & " ", Color.ForestGreen)

        If valor <> "" Then
            AppendColoredText(valor, Color.Black)
        End If

        ' Fin de línea
        AppendColoredText(vbCrLf, Color.Black)

    End Sub

    Private Sub CapturarSalida(sender As Object, e As DataReceivedEventArgs)
        If e.Data IsNot Nothing Then
            Me.Invoke(Sub()
                          AppendColoredLine(e.Data)
                      End Sub)
        End If
    End Sub


    Private Sub ActualizarEstados()
        tbDestino.Enabled = (tbOrigen.Text.Trim() <> "")
        btnLanzar.Enabled = (tbOrigen.Text.Trim() <> "" AndAlso tbDestino.Text.Trim() <> "")
    End Sub

    Private Sub tbOrigen_TextChanged(sender As Object, e As EventArgs) Handles tbOrigen.TextChanged

        Dim txt As String = tbOrigen.Text.Trim()

        If txt = "" Then
            tbDestino.Text = ""
            ActualizarEstados()
            Exit Sub
        End If

        ' Si hay punto, el último será el que marque el tipo de fichero
        Dim i As Integer = txt.LastIndexOf(".")
        If i <> -1 Then
            tbDestino.Text = txt.Substring(0, i + 1) & "sb"
        Else
            tbDestino.Text = txt & ".sb"
        End If

        ActualizarEstados()

    End Sub

    Private Sub tbDestino_TextChanged(sender As Object, e As EventArgs) Handles tbDestino.TextChanged
        ActualizarEstados()
    End Sub

    Private Sub btnBuscarEntrada_Click(sender As Object, e As EventArgs) Handles btnBuscarEntrada.Click
        Dim dlg As New OpenFileDialog()

        dlg.Title = "Seleccionar fichero de entrada"
        dlg.Filter = "Ficheros BAS (*.bas)|*.bas|Todos los archivos (*.*)|*.*"
        dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)

        If dlg.ShowDialog() = DialogResult.OK Then
            tbOrigen.Text = dlg.FileName
        End If
    End Sub

    Private Sub btnLanzar_Click(sender As Object, e As EventArgs) Handles btnLanzar.Click

        tbSalida.Clear()

        Dim origen As String = tbOrigen.Text.Trim()
        Dim destino As String = tbDestino.Text.Trim()

        ' Tipo seleccionado
        Dim tipo As String = If(rbGenerarSB.Checked, "SuperBASIC", "")

        ' Ruta base del ejecutable del lanzador
        Dim lanzadorBin As String = AppContext.BaseDirectory

        ' Subimos 3 niveles hasta llegar a ZX2SB (carpeta raíz)
        Dim root As String = IO.Path.GetFullPath(IO.Path.Combine(lanzadorBin, "..", "..", "..", ".."))

        ' Detectar Debug o Release
        Dim config As String
        If lanzadorBin.ToLower().Contains("release") Then
            config = "Release"
        Else
            config = "Debug"
        End If

        ' Ejecutables correctos según el tipo
        Dim exe As String = Nothing
        Dim ListaProcesos As New List(Of String)

        ' Base donde está el launcher
        Dim launcherPath As String = AppContext.BaseDirectory
        ' Subimos a bin\Debug\
        Dim binDebugPath As String = Path.GetFullPath(Path.Combine(launcherPath, ".."))
        ' Directorio de los EXE CLI (net8.0)
        Dim toolsPath As String = Path.Combine(binDebugPath, "net8.0")

        ' Añadir fases en orden canónico
        If cbLanzarDirector.Checked Then
            ListaProcesos.Add(Constantes.MDir)
        Else
            If cbLanzarLexer.Checked Then
                ListaProcesos.Add(Constantes.MLex)
            End If

            If cbLanzarParser.Checked Then
                ListaProcesos.Add(Constantes.MPar)
            End If

            If cbLanzarSemantico.Checked Then
                ListaProcesos.Add(Constantes.MSem)
            End If

            If cbLanzarGenerador.Checked Then
                ListaProcesos.Add(Constantes.MGSB)
            End If
        End If

        'Si hay algo que lanzar
        If ListaProcesos.Count = 0 Then
            tbSalida.AppendText("[Lanzador] ERROR: No hay ninguna fase seleccionada." & vbCrLf)
            Exit Sub
        End If

        tbSalida.AppendText("------------------------------------------------------------------------------------------------" & vbCrLf)
        tbSalida.AppendText(vbCrLf)

        ' Lanzar procesos
        Dim args As String
        For Each exeName As String In ListaProcesos
            ' Construir argumentos
            If (exeName = Constantes.MDir) Or (exeName = Constantes.MGSB) Then
                args = MontarArgumentos(origen, destino)
            Else
                args = MontarArgumentos(origen)
            End If

            ' Montar el Path completo con el ejecutable
            Dim exePath As String = IO.Path.Combine(toolsPath, exeName & ".exe")

            If Not IO.File.Exists(exePath) Then
                tbSalida.AppendText("[Lanzador] ERROR: No se encontró el ejecutable:" & vbCrLf & "-> " & exePath & vbCrLf)
                Exit Sub
            End If

            Dim p As New Process()
            p.StartInfo.FileName = exePath
            p.StartInfo.Arguments = args
            p.StartInfo.UseShellExecute = False
            p.StartInfo.RedirectStandardOutput = True
            p.StartInfo.RedirectStandardError = True
            p.StartInfo.CreateNoWindow = True

            p.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8
            p.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8

            p.Start()

            ' Leer salida completa (modo seguro)
            Dim output As String = p.StandardOutput.ReadToEnd()
            Dim err As String = p.StandardError.ReadToEnd()

            p.WaitForExit()

            ' Mostrar salida
            If output <> "" Then
                For Each line In output.Split({vbCrLf}, StringSplitOptions.RemoveEmptyEntries)
                    AppendColoredLine(line)
                Next
            End If

            If err <> "" Then
                For Each line In err.Split({vbCrLf}, StringSplitOptions.RemoveEmptyEntries)
                    AppendColoredLine("[ERROR] " & line)
                Next
            End If

            ' Mensaje FINALIZADO → SOLO AQUÍ, UNA VEZ
            tbSalida.AppendText("[Lanzador] Finalizado: " & exeName & " (ExitCode=" & p.ExitCode & ")" & vbCrLf)

            ' CORTE DEL PIPELINE (CLAVE)
            If p.ExitCode <> 0 Then
                tbSalida.AppendText("[Lanzador] ERROR: Ejecución detenida." & vbCrLf)
                Exit Sub
            End If
        Next
    End Sub

    Private Function MontarArgumentos(origen As String) As String
        Return MontarArgumentos(origen, "")
    End Function

    Private Function MontarArgumentos(origen As String, destino As String) As String
        ' Construir argumentos
        Dim args As String = ""
        If origen <> "" Then args &= $" ""{origen}"""
        If destino <> "" Then args &= $" ""{destino}"""

        If cbIncluirComentarios.Checked Then args &= " " & Constantes.opSinComentarios
        If cbModoSinWarnings.Checked Then args &= " " & Constantes.opNoWarnings
        If cbModoSilencioso.Checked Then args &= " " & Constantes.opSilencioso
        If cbModoVerbose.Checked Then args &= " " & Constantes.opVerbose

        args &= " " & "-b" ' Modo batch para evitar colores y otras cosas que puedan interferir al estar en modo formulario
        Return args
    End Function
End Class

