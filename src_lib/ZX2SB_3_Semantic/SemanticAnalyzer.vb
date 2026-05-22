Imports System
Imports System.IO
Imports System.Linq.Expressions
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Xml

Public Module SemanticAnalyzer
    Dim opts As CmdOptions
    Dim NroErrores As Integer = 0
    Dim NroWarnings As Integer = 0
    Dim NroLineaFichero As Integer = 0
    Dim NroLineaPrograma As Integer = 0
    Dim LineaParaMostrar As String = ""
    Dim UltimaLineaZX As Integer = -1

    ' Contexto semántico global
    Dim ctx As SemanticContext

    'Control de estructura del programa
    Dim ForStack As New Stack(Of String)
    Dim GosubStack As New Stack(Of Integer)

    'Writer del fichero de salida
    Dim stReader As StreamReader
    Dim stWriter As StreamWriter
    Dim strOpen As Boolean
    Dim stwOpen As Boolean

    ' ============================================================
    ' Punto de entrada
    ' ============================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        opts = _opts

        NroLineaFichero = 0
        NroErrores = 0

        ' --- Inicializar contexto ---
        InicializarContexto()

        ' ============================================================
        ' PRIMERA PASADA (Recolección estructural)
        ' ============================================================
        opts.Pasada = 1
        ProcesarIR()
        fClose()
        If NroErrores <> 0 Then
            Return 1
        End If


        ' ============================================================
        ' SEGUNDA PASADA (Análisis semántico + generación IRS)
        ' ============================================================
        opts.Pasada = 2
        ProcesarIR()
        GenerarIRP_Token(New Token(TokenID.TCO_EOF))
        fClose()
        If NroErrores <> 0 Then
            Return 1
        End If

        Guardar_Auxiliares()

        ' Los avisos de variables son warnings
        EmitirWarnings_Variables()
        EmitirWarnings_For()

        Return 0

    End Function

    ' ============================================================
    ' INICIALIZACIÓN DEL CONTEXTO
    ' ============================================================
    Private Sub InicializarContexto()
        ctx.Variables = New Dictionary(Of String, VariableInfo)
        ctx.FuncionesAuxiliares = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        ctx.DataNodes = New List(Of DataNode)
        ctx.ListaFOR = New List(Of ForNextInfo)
    End Sub

    ' ============================================================
    ' BUCLE PRINCIPAL DE LECTURA DEL IR
    ' ============================================================

    Private Sub ProcesarIR()
        Dim primeralinea As Boolean = True
        Dim EOFRecibido As Boolean = False

        NroLineaFichero = 0
        fOpen(ObtenerFicheroEntrada(opts), ObtenerFicheroSalida(opts))
        While Not stReader.EndOfStream
            Dim LineaLeida As String = stReader.ReadLine()

            If String.IsNullOrWhiteSpace(LineaLeida) Then Continue While

            ' ---------------------------------------------
            ' Cabecera IRP
            ' ---------------------------------------------

            If primeralinea Then
                Dim resultado As String = ""
                If Not GetVersion(opts, LineaLeida, resultado) Then
                    ErrorSemantico(resultado)
                Else
                    If opts.Pasada = 2 Then
                        GuardarIRS_Texto(resultado)
                    End If
                End If
                primeralinea = False
                Continue While
            End If

            ' ---------------------------------------------
            ' Línea fuente original (contexto de error)
            ' ---------------------------------------------
            If LineaLeida.StartsWith(Marca_SRC) Then
                LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, LineaLeida)
                If opts.Pasada = 2 Then
                    GuardarIRS_Texto($"{Constantes.Marca_SRC} {LineaParaMostrar}")
                End If
                Continue While
            End If

            ' ----------------------------------------------------------------------------
            ' Línea del IR, montar Token auxliar para descomponer la línea correctamente
            ' ----------------------------------------------------------------------------
            If LineaLeida.StartsWith("2154") Then
                Console.WriteLine($"::0:{LineaLeida}")
            End If
            Dim auxTok As New Token(LineaLeida)

            Select Case auxTok.ID
                Case TokenID.TCO_NONE
                    'Línea en blanco, no haemos nada
                Case TokenID.TCO_EOF
                    ' fin del fichero
                    EOFRecibido = True
                    Exit While
                Case TokenID.TCO_LINE
                    ' LINE n
                    Select Case opts.Pasada
                        Case 1
                            Dim n As Integer
                            If Integer.TryParse(auxTok.Value, n) Then
                                If opts.Pasada = 1 Then
                                    ' Validación de numeración correlativa en ZX BASIC
                                    If n <= UltimaLineaZX Then
                                        ErrorSemantico($"Numeración de líneas no válida: la línea {n} aparece después de {UltimaLineaZX}")
                                    End If
                                    UltimaLineaZX = n
                                End If
                            Else
                                ErrorSemantico($"LINE inválido en IRP: {LineaLeida}")
                            End If
                        Case 2
                            P2_AnalizarYEmitirStmt(auxTok, LineaParaMostrar)
                    End Select
                Case Else
                    Select Case opts.Pasada
                        Case 1
                            P1_Recolectar_DesdeStmt(auxTok)
                        Case 2
                            P2_AnalizarYEmitirStmt(auxTok, LineaParaMostrar)
                        Case Else
                            Throw New InvalidOperationException("Pasada semántica desconocida")
                    End Select
            End Select
        End While

        'Console.WriteLine($"EOF....{EOFRecibido}")

        If Not EOFRecibido Then
            ErrorSemantico($"El contenido no termina por un EOF, posible fichero inválido")
        End If

    End Sub

    ' ============================================================
    ' PRIMERA PASADA (Recolección estructural)
    ' ============================================================
    ' - Leer IRP
    ' - Construir LineMap
    ' - Registrar variables
    ' - Registrar DATA
    ' - Marcar flags de uso (PRINT, READ, etc.)
    Private Sub P1_Recolectar_DesdeStmt(tk As Token)
        Select Case tk.ID
            Case TokenID.TK_LET
                Recolectar_LET(tk)

            Case TokenID.TK_FOR
                Recolectar_FOR(tk)

            Case TokenID.TK_NEXT
                Recolectar_NEXT(tk)

            Case TokenID.TK_IF
                Recolectar_IF(tk)

            Case TokenID.TK_DIM
                Recolectar_DIM(tk)

            Case TokenID.TK_PRINT
                Recolectar_PRINT(tk)

            Case TokenID.TK_DATA
                Recolectar_DATA(tk)

            Case TokenID.TK_READ
                Recolectar_READ(tk)
        End Select

        ' El resto de sentencias no relevantes para la pasada 1 se ignoran

    End Sub

    Private Sub Recolectar_LET(tk As Token)

        Dim lhs As List(Of RPN.RPN_Node) = Nothing
        Dim rhs As List(Of RPN.RPN_Node) = Nothing

        ' Separación estructural usando RPN
        If Not Descomponer.dLET(tk.RPN, lhs, rhs) Then
            Exit Sub
        End If

        MarcarVariablesDesdeRPN(lhs, True)
        MarcarVariablesDesdeRPN(rhs, False)
    End Sub

    Private Sub Recolectar_IF(tk As Token)

        ' En pasada 1 solo nos interesa marcar
        ' las variables usadas en la condición

        If tk.RPN Is Nothing OrElse tk.RPN.Count = 0 Then
            Exit Sub
        End If
        MarcarVariablesDesdeRPN(tk.RPN, False)
    End Sub


    Private Sub Recolectar_FOR(tk As Token)
        'En un FOR el índice solo puede ser una variable simple de una sola letra
        Dim varName As String = ""
        Dim initExpr As New List(Of RPN_Node)
        Dim limitExpr As New List(Of RPN_Node)
        Dim stepExpr As New List(Of RPN_Node)

        If Not Descomponer.dFOR(tk.RPN, varName, initExpr, limitExpr, stepExpr) Then
            Exit Sub
        End If

        If varName = "" Then Exit Sub

        ' -----------------------------
        ' Marcar uso de variables
        ' -----------------------------
        ' Registrar variable
        AddVariable(varName, True)

        ' Registrar variable en la pila del FOR/NEXT
        ctx.ListaFOR.Add(New ForNextInfo With {
            .Linea = NroLineaPrograma,
            .VarName = varName,
            .Tipo = TipoForNext.tpFor
        })

        ' Marcar uso
        If initExpr IsNot Nothing Then
            MarcarVariablesDesdeRPN(initExpr, False)
        End If

        If limitExpr IsNot Nothing Then
            MarcarVariablesDesdeRPN(limitExpr, False)
        End If

        If stepExpr IsNot Nothing Then
            MarcarVariablesDesdeRPN(stepExpr, False)
        End If
    End Sub

    Private Sub Recolectar_NEXT(tk As Token)
        ' Registrar variable en la pila del FOR/NEXT
        ctx.ListaFOR.Add(New ForNextInfo With {
            .Linea = NroLineaPrograma,
            .VarName = tk.Value,
            .Tipo = TipoForNext.tpNext
        })

    End Sub

    Private Sub Recolectar_PRINT(tk As Token)

        Dim item As New PrintItem(tk)

        If item.prExpr1 IsNot Nothing Then
            MarcarVariablesDesdeRPN(item.prExpr1, False)
        End If

        If item.prExpr2 IsNot Nothing Then
            MarcarVariablesDesdeRPN(item.prExpr2, False)
        End If


    End Sub

    Private Sub Recolectar_DIM(tk As Token)
        ' Ejemplos de stmt: DIM A(10), DIM B$(40,2)
        ' Recibimos para DIM F(3) -> V(f,1) I(C(3))   

        Dim resto As String = tk.Value.Trim()

        ' Buscar V(...)
        Dim posV As Integer = resto.IndexOf("V(")
        If posV < 0 Then Exit Sub

        Dim posClose As Integer = resto.IndexOf(")", posV)
        If posClose < 0 Then Exit Sub

        Dim contenido As String = resto.Substring(posV + 2, posClose - (posV + 2))

        ' Separar nombre y dimensiones
        Dim partes = contenido.Split(","c)

        Dim varName As String = partes(0).Trim()
        Dim dimUsed As Integer = 0
        If partes.Length > 1 Then
            Integer.TryParse(partes(1), dimUsed)
        End If

        ' Registrar variable
        AddVariable(varName, True, dimUsed)

    End Sub

    Private Sub Recolectar_DATA(tk As Token)
        If tk.Value = "" Then
            ErrorSemantico("DATA no puede estar vacio")
        End If

        Dim actual As New List(Of RPN_Node)

        For Each node In tk.RPN
            If node.Kind = RPNKind.DATA_SEP Then
                If actual.Count = 0 Then
                    ErrorSemantico("DATA no puede comenzar por coma")
                End If
                ProcesarElemento_DATA(actual)
                actual.Clear()
            Else
                actual.Add(node)
            End If

        Next

        If actual.Count > 0 Then
            ProcesarElemento_DATA(actual)
        End If

    End Sub

    Private Sub ProcesarElemento_DATA(expr As List(Of RPN_Node))
        If expr Is Nothing OrElse expr.Count = 0 Then
            ErrorSemantico("DATA no puede contener grupos vacíos")
            Exit Sub
        End If

        Dim sb As String = RPN.RPN_ToText(expr)

        Dim node As New DataNode
        node.dnLine = UltimaLineaZX
        node.dnValue = sb ' RPN.RPNToInfix(expr)

        Procesar_DATA(expr)

        ' Marcar uso de variables
        For Each n In expr
            If n.Kind = RPNKind.VAR Then
                AddVariable(n.Value, False)   ' solo uso
            End If
        Next
    End Sub


    Private Sub Procesar_DATA(expr As List(Of RPN_Node))
        Dim node As New DataNode
        node.dnLine = UltimaLineaZX

        If expr.Count = 1 Then

            Dim n = expr(0)

            Select Case n.Kind

                Case RPNKind.CTE
                    If n.Value.StartsWith(Constantes.C_COMILLAS) Then
                        node.dnKind = DataKind.dtString
                    Else
                        node.dnKind = DataKind.dtNumber
                    End If
                    node.dnValue = n.Value

                Case RPNKind.VAR
                    node.dnKind = DataKind.dtVariable
                    node.dnValue = n.Value

            End Select

        Else
            ' expresión compleja
            node.dnKind = DataKind.dtRPN
            node.dnValue = RPN.RPN_ToText(expr)
        End If

        ctx.DataNodes.Add(node)
    End Sub

    Private Sub Recolectar_READ(tk As Token)

        ' Formato: READ A , B$ , C
        Dim vars = tk.Value.Split(Constantes.C_COMA)

        For Each v In vars
            Dim name = v.Trim()
            AddVariable(name, True)
        Next

    End Sub

    ' -----------------------------------------------------------------------------------------------------
    ' --- SEGUNDA PASADA
    ' -----------------------------------------------------------------------------------------------------

    Private Sub P2_AnalizarYEmitirStmt(tk As Token, lineaSRC As String)
        If tk.ID = TokenID.TCO_LINE Then
            GenerarIRP_Token(tk)
            Return
        End If
        If tk.ID = TokenID.TCO_EOL Then
            GenerarIRP_Token(tk)
            Return
        End If

        If tk.ID = TokenID.TCO_UNKNOWN Then
            ErrorSemantico($"Sentencia desconocida en '{lineaSRC}': {tk}")
            Return
        End If

        ' Avisos antes de generar
        Select Case tk.GetFamily()
            Case TokenFamily.TF_NOSOPORTADO   ' No soportadas
                WarningSemantico($"Sentencia no soportada: {tk}")
            Case TokenFamily.TF_ESPECIALES   ' La especiales no se tratan
                ErrorSemantico($"Token especial no soportado: {tk}")
        End Select

        Try
            ' Las que necesitan semántica especial
            If tk.ID = TokenID.TK_LET Then
                Generar_LET(tk, lineaSRC)
                Return
            End If

            If tk.ID = TokenID.TK_PRINT Then
                Generar_PRINT(tk)
                Return
            End If

            If tk.ID = TokenID.TK_IF Then
                Generar_IF(tk)
                Return
            End If

            If tk.ID = TokenID.TK_FOR Then
                Generar_FOR(tk)
                Return
            End If

            If tk.ID = TokenID.TK_NEXT Then
                Generar_NEXT(tk)
                Return
            End If

            If tk.ID = TokenID.TK_GOSUB Then
                Generar_GOSUB(tk)
                Return
            End If

            If tk.ID = TokenID.TK_RETURN Then
                Generar_RETURN(tk, lineaSRC)
                Return
            End If

            If tk.ID = TokenID.TK_GOTO OrElse tk.ID = TokenID.TK_STOP Then
                GenerarIRP_Token(tk)
                Return
            End If

            If tk.ID = TokenID.TK_READ Then
                Generar_READ(tk)
                Return
            End If

            ' CLEAR

            If tk.ID = TokenID.TK_CLEAR_RAM Then
                WarningSemantico($"CLEAR con parámetro no soportado directamente en SuperBASIC")
                GenerarIRP_Token(tk)
                Return
            End If

            If tk.ID = TokenID.TK_CLEAR Then
                GenerarIRP_Token(tk)
                Return
            End If

            'RANDOMIZE

            If tk.ID = TokenID.TK_RANDOMIZE Then
                GenerarIRP_Token(tk)
                Return
            End If

            ' RANDOMIZE USR (ZX-only)
            If tk.ID = TokenID.TK_RANDOMIZE_USR Then
                WarningSemantico($"RANDOMIZE USR no soportado directamente en QL")
                GenerarIRP_Token(tk)
                Return
            End If


            If tk.ID = TokenID.TK_DATA Then
                Generar_DATA(tk)
                Return
            End If


            ' el resto pasan directos
            GenerarIRP_Token(tk)
            Return

        Catch ex As Exception
            ErrorSemantico($"Error semántico en '{lineaSRC}': {ex.Message}")
        End Try

    End Sub

    Private Sub AddVariable(pName As String, pWasAssigned As Boolean, Optional pNrDims As Integer = 0)
        Dim wa As Boolean
        Dim wu As Boolean

        If pNrDims = 0 Then
            wa = pWasAssigned
            wu = Not pWasAssigned
        Else
            wa = False
            wu = False
        End If

        If Not ctx.Variables.ContainsKey(pName) Then
            ctx.Variables.Add(
                pName,
                New VariableInfo With {
                    .Name = pName,
                    .IsString = pName.EndsWith(Constantes.C_DOLAR),
                    .WasAssigned = wa,
                    .WasUsed = wu,
                    .NrDim = pNrDims
                })
        Else
            Dim vi As VariableInfo = ctx.Variables(pName)
            vi.WasAssigned = vi.WasAssigned Or wa
            vi.WasUsed = vi.WasUsed Or wu
            If (pNrDims <> 0) Then
                vi.NrDim = pNrDims
            End If
            ctx.Variables(pName) = vi
        End If
    End Sub

    ' ============================================================
    ' En la SEGUNDA PASADA
    ' - Emitir sección VARS
    ' - Emitir sección DATA
    ' - Emitir sección FOR
    ' ============================================================
    Private Function Guardar_Auxiliares() As Boolean
        Dim IDToken As Integer = 0
        Dim atributos As String = ""

        ' --------------------------------------------------------
        ' Sección de variables
        ' --------------------------------------------------------
        If ctx.Variables.Count <> 0 Then
            opts.Fase = SubFases.Variables
            fOpen("", ObtenerFicheroSalida(opts))


            Dim resultado As String = ""
            GuardarIRS_Texto(GetVersion(opts))

            GuardarIRS_VAR("-")
            For Each kv In ctx.Variables.OrderBy(Function(x) x.Key)
                Dim v = kv.Value

                Dim nomb As String = $"{v.Name} "
                Dim flag As String = $"[{If(v.IsString, "S", "N")}{If(v.WasAssigned, "A", " ")}{If(v.WasUsed, "U", " ")}{If(v.NrDim = 0, "  ", $"D{ v.NrDim}")}] "
                GuardarIRS_VAR(flag & nomb)

            Next
            GuardarIRS_VAR("-")
            GuardarIRS_VAR("ENDVARS")
            fClose()
        End If

        ' --------------------------------------------------------
        ' Sección DATA
        ' --------------------------------------------------------
        If ctx.DataNodes.Count <> 0 Then
            opts.Fase = SubFases.Data
            fOpen("", ObtenerFicheroSalida(opts))

            GuardarIRS_Texto(GetVersion(opts))

            GuardarIRS_DATA("-")
            For Each d In ctx.DataNodes
                Select Case d.dnKind
                    Case DataKind.dtNumber : GuardarIRS_DATA($"NODE {d.dnLine} NUM:{d.dnValue}")
                    Case DataKind.dtString : GuardarIRS_DATA($"NODE {d.dnLine} STR:{d.dnValue}")
                    Case DataKind.dtVariable : GuardarIRS_DATA($"NODE {d.dnLine} VAR:{d.dnValue}")
                    Case DataKind.dtRPN : GuardarIRS_DATA($"NODE {d.dnLine} RPN:{d.dnValue}")
                End Select
            Next
            GuardarIRS_DATA("-")
            GuardarIRS_DATA("ENDDATA")
            fClose()
        End If

        ' --------------------------------------------------------
        ' Sección FOR/NEXT
        ' --------------------------------------------------------
        If ctx.ListaFOR.Count <> 0 Then
            opts.Fase = SubFases.ForNext
            fOpen("", ObtenerFicheroSalida(opts))

            GuardarIRS_Texto(GetVersion(opts))

            GuardarIRS_FOR("-")
            For Each f In ctx.ListaFOR
                If f.Tipo = TipoForNext.tpFor Then
                    GuardarIRS_FOR($"{f.Linea} For {f.VarName}")
                Else
                    GuardarIRS_FOR($"{f.Linea} Next {f.VarName}")
                End If
            Next
            GuardarIRS_FOR("-")
            GuardarIRS_FOR("ENDDATA")
            fClose()
        End If

        Return (NroErrores = 0)

    End Function

    Private Sub Generar_LET(tk As Token, lineaSRC As String)

        ' tk.RPN YA está reconstruida automáticamente
        Dim rpn As List(Of RPN.RPN_Node) = tk.RPN

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            GenerarIRP_Token(tk)
            Exit Sub
        End If

        Dim idxAssign = rpn.FindIndex(Function(n) n.Kind = RPNKind.ASSIGN)

        If idxAssign <= 0 Then
            Throw New Exception("Let inválido: falta := o mal formado")
        End If


        Dim lhs As New List(Of RPN.RPN_Node)
        Dim rhs As New List(Of RPN.RPN_Node)
        If Not Descomponer.dLET(rpn, lhs, rhs) Then
            Throw New Exception("LET inválido: mal formado")
        End If


        ' --- Extraer y marcar la variable base ---
        Dim baseVarName As String = ""
        If lhs.Count > 0 AndAlso lhs(0).Kind = RPNKind.VAR Then
            baseVarName = lhs(0).Value
        End If

        ' --- Comprobación de tipos (usando RPN) ---
        Dim varType As VarType = If(baseVarName.EndsWith(Constantes.C_DOLAR), VarType.StringType, VarType.Numeric)
        Dim expType As VarType = GetExprType(rhs)


        If varType <> expType Then

            Dim tipoL As String = TipoATexto(varType)
            Dim tipoR As String = TipoATexto(expType)

            Dim lhsExpr As String = RPNToInfix(lhs)
            Dim rhsExpr As String = RPNToInfix(rhs)

            WarningSemantico($"Asignación incompatible {tipoL}={tipoR} ({lhsExpr}={rhsExpr})")

        End If

        ' --- Emitir IR tal cual ---
        GenerarIRP_Token(tk)
    End Sub

    Private Function TipoATexto(t As VarType) As String
        Select Case t
            Case VarType.StringType : Return "cadena"
            Case VarType.Numeric : Return "numero"
            Case Else : Return "desconocido"
        End Select
    End Function


    Private Function ExtraerVariableBaseDesdeLET(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return ""
        End If

        Dim i As Integer = 0

        ' Avanzar mientras sean caracteres válidos de nombre
        While i < value.Length AndAlso
          (Char.IsLetterOrDigit(value(i)) OrElse value(i) = Constantes.C_DOSPUNTOS)
            i += 1
        End While

        Return value.Substring(0, i)
    End Function

    Private Sub Generar_PRINT(tk As Token)
        GenerarIRP_Token(tk)
    End Sub


    Private Sub Generar_IF(tk As Token)
        ' Emitir IF estructural tal cual
        GenerarIRP_Token(tk)
    End Sub

    Private Sub Generar_FOR(tk As Token)
        Dim varName As String = ""
        Dim initExpr As New List(Of RPN_Node)
        Dim limitExpr As New List(Of RPN_Node)
        Dim stepExpr As New List(Of RPN_Node)

        If Not Descomponer.dFOR(tk.RPN, varName, initExpr, limitExpr, stepExpr) Then
            Exit Sub
        End If

        ' Puedes añadir validaciones aquí
        GenerarIRP_Token(tk)

    End Sub

    Private Sub Generar_NEXT(tk As Token)
        ' Emitir tal cual
        GenerarIRP_Token(tk)
    End Sub

    Private Sub Generar_GOSUB(tk As Token)

        ' Solo marcamos que hay una llamada GOSUB pendiente de RETURN
        GosubStack.Push(1)

        GenerarIRP_Token(New Token(TokenID.TK_GOSUB, tk.Value))

    End Sub

    Private Sub Generar_RETURN(tk As Token, lineaSRC As String)

        If GosubStack.Count = 0 Then
            WarningSemantico($"RETURN sin GOSUB previo: {lineaSRC}")
        Else
            GosubStack.Pop()
        End If

        GenerarIRP_Token(New Token(TokenID.TK_RETURN, tk.Value))

    End Sub


    Private Sub Generar_READ(tk As Token)
        ' Formato: READ A , B$ , C
        Dim stmt As String = tk.Value
        GenerarIRP_Token(New Token(TokenID.TK_READ, stmt))
    End Sub

    Private Sub Generar_DATA(tk As Token)
        ' Formato: DATA e1,e2,....   donde los elemento serán contantes o variables
        Dim stmt As String = tk.Value
        GenerarIRP_Token(New Token(TokenID.TK_DATA, stmt))
    End Sub

    Private Sub MarcarVariablesDesdeRPN(rpn As List(Of RPN_Node), asignacion As Boolean)

        For Each node In rpn
            If node.Kind = RPNKind.VAR Then
                AddVariable(node.Value, asignacion) ' Solo la primera será asignada si lo necesita
                asignacion = False
            End If
        Next

    End Sub



    ' ============================================================
    ' Helpers de expresiones
    ' ============================================================

    Private Function GetExprType(rpn As List(Of RPN_Node)) As VarType

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            Return VarType.Unknown
        End If

        Dim last = rpn(rpn.Count - 1)

        Select Case last.Kind

            Case RPNKind.VAR
                If last.Value.EndsWith(Constantes.C_DOLAR) Then
                    Return VarType.StringType
                Else
                    Return VarType.Numeric
                End If

            Case RPNKind.CTE
                If last.Value.StartsWith("""") Then
                    Return VarType.StringType
                Else
                    Return VarType.Numeric
                End If

            Case RPNKind.FUN_CALL
                ' asume funciones numéricas (o ajusta si quieres)
                Return VarType.Numeric

            Case RPNKind.BINARY_OP, RPNKind.UNARY_OP
                ' operadores → resultado numérico
                Return VarType.Numeric

        End Select

        Return VarType.Numeric

    End Function


    ' ============================================================
    ' GESTIÓN DE ERRORES / AVISOS
    ' ============================================================
    Private Sub EmitirWarnings_Variables()
        Dim Lista1 As New StringBuilder
        Dim Lista2 As New StringBuilder
        Dim Lista3 As New StringBuilder

        For Each kv In ctx.Variables

            Dim v = kv.Value

            ' Usada pero nunca asignada
            If v.WasUsed AndAlso Not v.WasAssigned Then
                Lista1.Append($"{v.Name},")

                ' 🔍 Posible confusión entre variable numérica y string ($)
                Dim base = NombreBase(v.Name)

                For Each kv2 In ctx.Variables
                    Dim other = kv2.Value

                    If other.WasAssigned AndAlso
                       NombreBase(other.Name) = base AndAlso
                       other.IsString <> v.IsString Then
                        Lista3.Append($"{other.Name}' por '{v.Name}, ")
                    End If
                Next
            End If

            ' Asignada pero nunca usada
            If v.WasAssigned AndAlso Not v.WasUsed Then
                Lista2.Append($"{v.Name},")
            End If

        Next

        Dim cadena As String = ""
        If Lista1.Length <> 0 Then
            Lista1.Remove(Lista1.Length - 1, 1)
            cadena &= $"{Space(36)}--- Variables usadas pero nunca asignadas: {Lista1}" & vbCrLf
        End If
        If Lista2.Length <> 0 Then
            Lista2.Remove(Lista2.Length - 1, 1)
            cadena &= $"{Space(36)}--- Variables asignadas pero nunca usadas: {Lista2}" & vbCrLf
        End If
        If Lista3.Length <> 0 Then
            Lista3.Remove(Lista3.Length - 1, 1)
            cadena &= $"{Space(36)}--- Posibles usos erróneos de nombres: {Lista3}" & vbCrLf
        End If
        If cadena <> "" Then
            WarningVariables("Posibles usos erróneos de variables: " & vbCrLf & cadena)
        End If
    End Sub

    Private Function NombreBase(varName As String) As String
        If varName.EndsWith(Constantes.C_DOLAR) Then
            Return varName.Substring(0, varName.Length - 1)
        End If
        Return varName
    End Function

    Private Sub EmitirWarnings_For()
        Dim forCount As New Dictionary(Of String, Integer)
        Dim nextCount As New Dictionary(Of String, Integer)

        For Each item In ctx.ListaFOR
            If item.Tipo = TipoForNext.tpFor Then
                If Not forCount.ContainsKey(item.VarName) Then
                    forCount(item.VarName) = 0
                End If
                forCount(item.VarName) += 1
            Else
                If Not nextCount.ContainsKey(item.VarName) Then
                    nextCount(item.VarName) = 0
                End If
                nextCount(item.VarName) += 1
            End If
        Next

        ' NEXT sin FOR
        Dim Lista1 As String = ""
        For Each kv In nextCount
            If Not forCount.ContainsKey(kv.Key) Then
                Lista1 &= $"{kv.Key},"
            End If
        Next
        If Lista1.Length <> 0 Then
            Lista1.Remove(Lista1.Length - 1, 1)
            WarningVariables($"Next sin FOR: {Lista1}")
        End If

        ' FOR sin NEXT
        Dim Lista2 As String = ""
        For Each kv In forCount
            If Not nextCount.ContainsKey(kv.Key) Then
                Lista2 &= $"{kv.Key},"
            End If
        Next
        If Lista2.Length <> 0 Then
            Lista2.Remove(Lista1.Length - 1, 1)
            WarningVariables($"FOR: {Lista2}")
        End If
    End Sub

    Private Sub ErrorSemantico(descripcion As String)
        NroErrores += 1
        MensajeError(opts, stReader, stWriter, False, NroLineaFichero, 0, LineaParaMostrar, descripcion, False)
    End Sub

    Private Sub WarningSemantico(descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        MensajeError(opts, stReader, stWriter, True, NroLineaFichero, 0, LineaParaMostrar, descripcion, False)

    End Sub

    Private Sub WarningVariables(descripcion As String)
        NroWarnings += 1
        If opts.NoPararPorError Or opts.SinWarnings Then
            Exit Sub
        End If

        MensajeError(opts, Nothing, Nothing, True, 0, 0, "", "[Variables] " & descripcion, False)
    End Sub


    ' ============================================================
    ' GRABAR EN DESTINO
    ' ============================================================

    Private Sub GenerarIRP_Token(tk As Token)
        Dim pi As New PrintItem
        If tk.ID = TokenID.TK_PRINT Then
            pi = pi.FromToken(tk)
        Else
            pi.prID = TokenID.TCO_UNKNOWN
        End If

        GuardarIRS(tk, pi)
    End Sub

    Private Sub GuardarIRS(tk As Token, pi As PrintItem)
        Dim idNum As Integer = CInt(tk.ID)
        Dim value As String = If(tk.Value IsNot Nothing, tk.Value, "")
        Dim Linea As String = ""
        Dim Comentario As String = ""

        If pi.prID = TokenID.TCO_UNKNOWN Then
            Linea = $"{idNum} {value}"
            Comentario = $"{tk.ID.ToString()}"
        Else
            Linea = $"{idNum} {value}" ' Linea = $"{idNum} {pi.ToText} {value}"
            Comentario = $"{tk.ID.ToString()} {pi.prID.ToString()}"
        End If

        If Len(Linea) < Constantes.Separacion_Comentario Then
            Linea &= Space(Constantes.Separacion_Comentario - Len(Linea)) & $"{Constantes.Marca_Comentario} {Comentario}"
            GuardarIRS_Texto(Linea)
        Else
            GuardarIRS_Texto(Linea)
            Linea = Space(Constantes.Separacion_Comentario) & $"{Constantes.Marca_Comentario} {Comentario}"
            GuardarIRS_Texto(Linea)
        End If
    End Sub

    Private Sub GuardarIRS_VAR(linea As String)
        GuardarIRS_Aux("VAR", linea)
    End Sub

    Private Sub GuardarIRS_DATA(linea As String)
        GuardarIRS_Aux("DATA", linea)
    End Sub

    Private Sub GuardarIRS_FOR(linea As String)
        GuardarIRS_Aux("F/N", linea)
    End Sub

    Private Sub GuardarIRS_Aux(tipo As String, linea As String)
        If (linea <> "-") Then
            GuardarIRS_Texto(tipo & " " & linea)
        Else
            GuardarIRS_Texto("")
        End If
    End Sub

    Private Sub GuardarIRS_Texto(linea As String)

        If stWriter Is Nothing Then
            Throw New InvalidOperationException("OutWriter no inicializado")
        End If

        stWriter.WriteLine(linea)

        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If
    End Sub

    ' -----------------------------------------------------------------------------
    ' Manejo de ficheros
    ' -----------------------------------------------------------------------------

    Private Sub fOpen(fRead As String, fWrite As String)
        If fRead <> "" Then
            stReader = New StreamReader(fRead)
            strOpen = True
        End If
        stWriter = New StreamWriter(fWrite, False, New UTF8Encoding(False))
        stwOpen = True
    End Sub

    Private Sub fClose()
        If (strOpen) Then
            stReader.Close()
        End If
        If (stwOpen) Then
            stWriter.Flush()
            stWriter.Close()
            stwOpen = False
        End If
    End Sub


End Module