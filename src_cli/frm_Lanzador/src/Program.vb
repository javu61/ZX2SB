Imports System
Imports System.Windows.Forms

Namespace ZX2SB
    Friend Module Program
        <STAThread>
        Sub Main()
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New frmLanzador())
        End Sub
    End Module
End Namespace
