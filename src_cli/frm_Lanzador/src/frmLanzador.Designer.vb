<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmLanzador
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        btnLanzar = New System.Windows.Forms.Button()
        cbLanzarParser = New System.Windows.Forms.CheckBox()
        cbIncluirComentarios = New System.Windows.Forms.CheckBox()
        Label1 = New System.Windows.Forms.Label()
        tbOrigen = New System.Windows.Forms.TextBox()
        tbDestino = New System.Windows.Forms.TextBox()
        Label2 = New System.Windows.Forms.Label()
        Label3 = New System.Windows.Forms.Label()
        Label4 = New System.Windows.Forms.Label()
        tbSalida = New System.Windows.Forms.RichTextBox()
        Label5 = New System.Windows.Forms.Label()
        grpTipo = New System.Windows.Forms.GroupBox()
        rbGenerarSB = New System.Windows.Forms.RadioButton()
        btnBuscarEntrada = New System.Windows.Forms.Button()
        cbLanzarLexer = New System.Windows.Forms.CheckBox()
        grVerbose = New System.Windows.Forms.GroupBox()
        cbLanzarDirector = New System.Windows.Forms.CheckBox()
        cbLanzarGenerador = New System.Windows.Forms.CheckBox()
        cbLanzarSemantico = New System.Windows.Forms.CheckBox()
        cbModoSinWarnings = New System.Windows.Forms.CheckBox()
        GroupBox1 = New System.Windows.Forms.GroupBox()
        cbModoVerbose = New System.Windows.Forms.CheckBox()
        cbModoSilencioso = New System.Windows.Forms.CheckBox()
        Label6 = New System.Windows.Forms.Label()
        grpTipo.SuspendLayout()
        grVerbose.SuspendLayout()
        GroupBox1.SuspendLayout()
        SuspendLayout()
        ' 
        ' btnLanzar
        ' 
        btnLanzar.Anchor = System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right
        btnLanzar.Location = New System.Drawing.Point(722, 23)
        btnLanzar.Name = "btnLanzar"
        btnLanzar.Size = New System.Drawing.Size(83, 27)
        btnLanzar.TabIndex = 0
        btnLanzar.Text = "Lanzar"
        ' 
        ' cbLanzarParser
        ' 
        cbLanzarParser.AutoSize = True
        cbLanzarParser.Location = New System.Drawing.Point(6, 33)
        cbLanzarParser.Name = "cbLanzarParser"
        cbLanzarParser.Size = New System.Drawing.Size(58, 19)
        cbLanzarParser.TabIndex = 6
        cbLanzarParser.Text = "Parser"
        ' 
        ' cbIncluirComentarios
        ' 
        cbIncluirComentarios.AutoSize = True
        cbIncluirComentarios.Checked = True
        cbIncluirComentarios.CheckState = System.Windows.Forms.CheckState.Checked
        cbIncluirComentarios.Location = New System.Drawing.Point(6, 14)
        cbIncluirComentarios.Name = "cbIncluirComentarios"
        cbIncluirComentarios.Size = New System.Drawing.Size(149, 19)
        cbIncluirComentarios.TabIndex = 7
        cbIncluirComentarios.Text = "No Incluir Comentarios"
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Location = New System.Drawing.Point(12, 27)
        Label1.Name = "Label1"
        Label1.Size = New System.Drawing.Size(99, 15)
        Label1.TabIndex = 20
        Label1.Text = "Fichero de origen"
        ' 
        ' tbOrigen
        ' 
        tbOrigen.Anchor = System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right
        tbOrigen.Location = New System.Drawing.Point(117, 24)
        tbOrigen.Name = "tbOrigen"
        tbOrigen.Size = New System.Drawing.Size(557, 23)
        tbOrigen.TabIndex = 1
        ' 
        ' tbDestino
        ' 
        tbDestino.Anchor = System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right
        tbDestino.Location = New System.Drawing.Point(117, 53)
        tbDestino.Name = "tbDestino"
        tbDestino.Size = New System.Drawing.Size(557, 23)
        tbDestino.TabIndex = 3
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Location = New System.Drawing.Point(6, 56)
        Label2.Name = "Label2"
        Label2.Size = New System.Drawing.Size(105, 15)
        Label2.TabIndex = 17
        Label2.Text = "Fichero de Destino"
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Location = New System.Drawing.Point(370, 108)
        Label3.Name = "Label3"
        Label3.Size = New System.Drawing.Size(48, 15)
        Label3.TabIndex = 16
        Label3.Text = "Generar"
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Location = New System.Drawing.Point(52, 182)
        Label4.Name = "Label4"
        Label4.Size = New System.Drawing.Size(57, 15)
        Label4.TabIndex = 15
        Label4.Text = "Opciones"
        ' 
        ' tbSalida
        ' 
        tbSalida.Anchor = System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left Or System.Windows.Forms.AnchorStyles.Right
        tbSalida.Location = New System.Drawing.Point(117, 210)
        tbSalida.Name = "tbSalida"
        tbSalida.Size = New System.Drawing.Size(695, 229)
        tbSalida.TabIndex = 10
        tbSalida.Text = ""
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Location = New System.Drawing.Point(50, 210)
        Label5.Name = "Label5"
        Label5.Size = New System.Drawing.Size(59, 15)
        Label5.TabIndex = 14
        Label5.Text = "Resultado"
        ' 
        ' grpTipo
        ' 
        grpTipo.Controls.Add(rbGenerarSB)
        grpTipo.Location = New System.Drawing.Point(424, 91)
        grpTipo.Name = "grpTipo"
        grpTipo.Size = New System.Drawing.Size(250, 45)
        grpTipo.TabIndex = 12
        grpTipo.TabStop = False
        ' 
        ' rbGenerarSB
        ' 
        rbGenerarSB.AutoSize = True
        rbGenerarSB.Checked = True
        rbGenerarSB.Enabled = False
        rbGenerarSB.Location = New System.Drawing.Point(6, 15)
        rbGenerarSB.Name = "rbGenerarSB"
        rbGenerarSB.Size = New System.Drawing.Size(105, 19)
        rbGenerarSB.TabIndex = 5
        rbGenerarSB.TabStop = True
        rbGenerarSB.Text = "QL SuperBASIC"
        ' 
        ' btnBuscarEntrada
        ' 
        btnBuscarEntrada.Anchor = System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right
        btnBuscarEntrada.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        btnBuscarEntrada.Location = New System.Drawing.Point(682, 23)
        btnBuscarEntrada.Name = "btnBuscarEntrada"
        btnBuscarEntrada.Size = New System.Drawing.Size(32, 23)
        btnBuscarEntrada.TabIndex = 2
        btnBuscarEntrada.Text = "..."
        ' 
        ' cbLanzarLexer
        ' 
        cbLanzarLexer.AutoSize = True
        cbLanzarLexer.Location = New System.Drawing.Point(6, 16)
        cbLanzarLexer.Name = "cbLanzarLexer"
        cbLanzarLexer.Size = New System.Drawing.Size(54, 19)
        cbLanzarLexer.TabIndex = 22
        cbLanzarLexer.Text = "Lexer"
        ' 
        ' grVerbose
        ' 
        grVerbose.Controls.Add(cbLanzarDirector)
        grVerbose.Controls.Add(cbLanzarGenerador)
        grVerbose.Controls.Add(cbLanzarSemantico)
        grVerbose.Controls.Add(cbLanzarLexer)
        grVerbose.Controls.Add(cbLanzarParser)
        grVerbose.Location = New System.Drawing.Point(117, 77)
        grVerbose.Name = "grVerbose"
        grVerbose.Size = New System.Drawing.Size(202, 90)
        grVerbose.TabIndex = 23
        grVerbose.TabStop = False
        ' 
        ' cbLanzarDirector
        ' 
        cbLanzarDirector.AutoSize = True
        cbLanzarDirector.Location = New System.Drawing.Point(115, 41)
        cbLanzarDirector.Name = "cbLanzarDirector"
        cbLanzarDirector.Size = New System.Drawing.Size(68, 19)
        cbLanzarDirector.TabIndex = 26
        cbLanzarDirector.Text = "Director"
        ' 
        ' cbLanzarGenerador
        ' 
        cbLanzarGenerador.AutoSize = True
        cbLanzarGenerador.Location = New System.Drawing.Point(6, 68)
        cbLanzarGenerador.Name = "cbLanzarGenerador"
        cbLanzarGenerador.Size = New System.Drawing.Size(81, 19)
        cbLanzarGenerador.TabIndex = 25
        cbLanzarGenerador.Text = "Generador"
        ' 
        ' cbLanzarSemantico
        ' 
        cbLanzarSemantico.AutoSize = True
        cbLanzarSemantico.Location = New System.Drawing.Point(6, 50)
        cbLanzarSemantico.Name = "cbLanzarSemantico"
        cbLanzarSemantico.Size = New System.Drawing.Size(86, 19)
        cbLanzarSemantico.TabIndex = 23
        cbLanzarSemantico.Text = "Sermántico"
        ' 
        ' cbModoSinWarnings
        ' 
        cbModoSinWarnings.AutoSize = True
        cbModoSinWarnings.Location = New System.Drawing.Point(455, 14)
        cbModoSinWarnings.Name = "cbModoSinWarnings"
        cbModoSinWarnings.Size = New System.Drawing.Size(90, 19)
        cbModoSinWarnings.TabIndex = 24
        cbModoSinWarnings.Text = "Sin Warning"
        cbModoSinWarnings.UseVisualStyleBackColor = True
        ' 
        ' GroupBox1
        ' 
        GroupBox1.Controls.Add(cbModoVerbose)
        GroupBox1.Controls.Add(cbModoSilencioso)
        GroupBox1.Controls.Add(cbModoSinWarnings)
        GroupBox1.Controls.Add(cbIncluirComentarios)
        GroupBox1.Location = New System.Drawing.Point(117, 167)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New System.Drawing.Size(695, 39)
        GroupBox1.TabIndex = 26
        GroupBox1.TabStop = False
        ' 
        ' cbModoVerbose
        ' 
        cbModoVerbose.AutoSize = True
        cbModoVerbose.Location = New System.Drawing.Point(262, 14)
        cbModoVerbose.Name = "cbModoVerbose"
        cbModoVerbose.Size = New System.Drawing.Size(67, 19)
        cbModoVerbose.TabIndex = 27
        cbModoVerbose.Text = "Verbose"
        cbModoVerbose.UseVisualStyleBackColor = True
        ' 
        ' cbModoSilencioso
        ' 
        cbModoSilencioso.AutoSize = True
        cbModoSilencioso.Location = New System.Drawing.Point(335, 14)
        cbModoSilencioso.Name = "cbModoSilencioso"
        cbModoSilencioso.Size = New System.Drawing.Size(114, 19)
        cbModoSilencioso.TabIndex = 26
        cbModoSilencioso.Text = "Modo Silencioso"
        cbModoSilencioso.UseVisualStyleBackColor = True
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Location = New System.Drawing.Point(63, 91)
        Label6.Name = "Label6"
        Label6.Size = New System.Drawing.Size(41, 15)
        Label6.TabIndex = 27
        Label6.Text = "Lanzar"
        ' 
        ' frmLanzador
        ' 
        ClientSize = New System.Drawing.Size(817, 446)
        Controls.Add(Label6)
        Controls.Add(GroupBox1)
        Controls.Add(grVerbose)
        Controls.Add(btnBuscarEntrada)
        Controls.Add(grpTipo)
        Controls.Add(Label5)
        Controls.Add(tbSalida)
        Controls.Add(Label4)
        Controls.Add(Label3)
        Controls.Add(Label2)
        Controls.Add(tbDestino)
        Controls.Add(tbOrigen)
        Controls.Add(Label1)
        Controls.Add(btnLanzar)
        Name = "frmLanzador"
        Text = "Lanzador"
        grpTipo.ResumeLayout(False)
        grpTipo.PerformLayout()
        grVerbose.ResumeLayout(False)
        grVerbose.PerformLayout()
        GroupBox1.ResumeLayout(False)
        GroupBox1.PerformLayout()
        ResumeLayout(False)
        PerformLayout()

    End Sub

    Friend WithEvents btnLanzar As System.Windows.Forms.Button
    Friend WithEvents cbLanzarParser As System.Windows.Forms.CheckBox
    Friend WithEvents cbIncluirComentarios As System.Windows.Forms.CheckBox
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents tbOrigen As System.Windows.Forms.TextBox
    Friend WithEvents tbDestino As System.Windows.Forms.TextBox
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents Label3 As System.Windows.Forms.Label
    Friend WithEvents Label4 As System.Windows.Forms.Label
    Friend WithEvents tbSalida As System.Windows.Forms.RichTextBox
    Friend WithEvents Label5 As System.Windows.Forms.Label
    Friend WithEvents grpTipo As System.Windows.Forms.GroupBox
    Friend WithEvents rbGenerarSB As System.Windows.Forms.RadioButton
    Friend WithEvents btnBuscarEntrada As System.Windows.Forms.Button
    Friend WithEvents cbLanzarLexer As System.Windows.Forms.CheckBox
    Friend WithEvents grVerbose As System.Windows.Forms.GroupBox
    Friend WithEvents cbLanzarSemantico As System.Windows.Forms.CheckBox
    Friend WithEvents cbModoSinWarnings As System.Windows.Forms.CheckBox
    Friend WithEvents GroupBox1 As System.Windows.Forms.GroupBox
    Friend WithEvents Label6 As System.Windows.Forms.Label
    Friend WithEvents cbModoSilencioso As System.Windows.Forms.CheckBox
    Friend WithEvents cbLanzarGenerador As System.Windows.Forms.CheckBox
    Friend WithEvents cbModoVerbose As System.Windows.Forms.CheckBox
    Friend WithEvents cbLanzarDirector As System.Windows.Forms.CheckBox

End Class