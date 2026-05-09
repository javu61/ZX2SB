
' ===========================================
'  Definición de Token del lenguaje ZX2SB
' ===========================================

Imports System.Runtime.CompilerServices
Imports System.Xml

Public Structure Token

    Public ID As TokenID
    Public Value As String
    Public Line As Integer
    Public Col As Integer
    Dim _RPN As List(Of RPN_Node)

    Public Sub New(id As TokenID, valor As String, linea As Integer, columna As Integer)
        Me.ID = id
        Me.Value = valor
        Me.Line = linea
        Me.Col = columna
    End Sub

    Public Sub New(id As TokenID, valor As String)
        Me.ID = id
        Me.Value = valor
        Me.Line = 0
        Me.Col = 0
    End Sub

    Public Sub New(id As TokenID)
        Me.ID = id
        Me.Value = ""
        Me.Line = 0
        Me.Col = 0
    End Sub

    Public Sub New(linea As String)
        LineToTok(linea)
    End Sub

    ReadOnly Property Mnemonic As String
        Get
            Return GetNombreSentencia().ToUpperInvariant()
        End Get
    End Property

    ReadOnly Property Canonico As String
        Get
            Return Value.Replace("_", "")
        End Get
    End Property
    ReadOnly Property FNMnemonic As String
        Get
            Return ("ZX2SB_" & Mnemonic)
        End Get
    End Property

    ReadOnly Property EsModificadorPrint As Boolean
        Get
            Return GetEsModificadorPrint()
        End Get
    End Property

    ReadOnly Property GetValor As String
        Get
            Select Case Me.ID
                Case TokenID.TES_STRING
                    Return ($"{Constantes.C_COMILLAS}{Me.Value}{Constantes.C_COMILLAS}")
                Case Else
                    Return Me.Value
            End Select
        End Get
    End Property


    Public ReadOnly Property RPN As List(Of RPN_Node)
        Get
            If _RPN Is Nothing AndAlso DebeTenerRPN(Me.ID) Then
                _RPN = ParseRPN(Me.Value)
            End If
            Return _rpn
        End Get
    End Property


    ' ---------------------------------------------------
    '  Serialización EXACTA al fichero .tok
    '  Formato:
    '     <ID> [<linea>,<columna>] [<Valor>] [ ; Token]
    ' ---------------------------------------------------
    Public Function TokToLine() As String
        Dim aux As String = CInt(ID).ToString("D4") & " [" & Line & "," & Col & "]"

        If Not String.IsNullOrEmpty(Value) Then
            aux &= " " & Value
        End If

        If Me.ID <> TokenID.TCO_UNKNOWN Then
            aux = aux & Space(50 - Len(aux)) & Constantes.Marca_Comentario & Me.ID.ToString
        End If

        Return aux
    End Function

    Private Sub LineToTok(linea As String)
        ' Ejemplos esperados:
        '2142 V(pi1) := C(3)                                ; --  TK_LET
        '2150 1700,C,V(pi1)                                 ; --  TK_PRINT TES_IDENT


        Me.ID = TokenID.TCO_NONE  'Si no hay nada, es vacío
        Me.Line = -1
        Me.Col = -1
        Me.Value = ""

        ' Quitar comentario desde el último ; hasta el final
        If linea.Contains(Constantes.Marca_Comentario) Then
            For i As Integer = linea.Length - 1 To 0 Step -1
                If linea(i) = Constantes.Sep_Comentario Then
                    linea = linea.Substring(0, i).TrimEnd()
                    Exit For
                End If
            Next
        End If

        If String.IsNullOrEmpty(linea) Then
            Exit Sub
        End If

        Dim parts = linea.Split(Constantes.C_ESPACIO, 2)
        Dim id As TokenID = CType(Integer.Parse(parts(0)), TokenID)
        Dim line As Integer = -1
        Dim col As Integer = -1
        Dim value As String = ""


        If parts.Length = 2 Then
            Dim rest As String = parts(1).Trim()

            ' ¿Empieza por [l,c]?
            If rest.StartsWith("[") Then
                Dim endPos = rest.IndexOf("]"c)
                If endPos > 0 Then
                    Dim pos = rest.Substring(1, endPos - 1).Split(","c)
                    If pos.Length = 2 Then
                        Integer.TryParse(pos(0), line)
                        Integer.TryParse(pos(1), col)
                    End If
                    value = rest.Substring(endPos + 1).Trim()
                Else
                    ' No contiene linea y columna
                    value = rest
                End If
            Else
                value = rest
            End If
        End If

        Me.ID = id
        Me.Line = line
        Me.Col = col
        Me.Value = value
    End Sub

    ' ---------------------------------------------------
    '  Representación legible para depuración (VERBOSE)
    '  NO afecta al fichero .tok ni al IR
    ' ---------------------------------------------------
    Public Overrides Function ToString() As String
        If String.IsNullOrEmpty(Value) Then
            Return TokenName(ID)
        Else
            Return TokenName(ID) & "(" & Value & ")"
        End If
    End Function

    ' ===========================================
    '  Nombres simbólicos de tokens (solo debug)
    ' ===========================================
    Public Function TokenName() As String
        Dim name As String = [Enum].GetName(GetType(TokenID), Me.ID)
        If name Is Nothing Then
            Return "UNKNOWN(" & CInt(ID).ToString("00") & ")"
        End If
        Return name
    End Function

    ' ===========================================
    '  Helpers. DecodeToken
    '  Devuelve las tres partes de un TokenID.
    ' ===========================================
    Public Structure DecodedToken
        Public Family As TokenFamily
        Public Tipo As TokenTipo
        Public Index As Integer   ' NN
    End Structure

    Public Function DecodeToken(id As TokenID) As DecodedToken
        Dim value As Integer = CInt(id)

        Return New DecodedToken With {
            .Family = CType((value \ 1000) * 1000, TokenFamily),
            .Tipo = CType((value \ 100) Mod 10 * 100, TokenTipo),
            .Index = value Mod 100
        }
    End Function

    ' ===========================================
    '  Helpers Directos
    ' ===========================================

    'Obtener la familia
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function GetFamily(id As TokenID) As TokenFamily
        Return CType((CInt(id) \ 1000) * 1000, TokenFamily)
    End Function

    ' Obtener el tipo
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Shared Function GetTipo(id As TokenID) As TokenTipo
        Return CType(((CInt(id) \ 100) Mod 10) * 100, TokenTipo)
    End Function

    'Obtener el índice (NN)
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function GetIndex(id As TokenID) As Integer
        Return (CInt(id) Mod 100)
    End Function

    ' ===========================================
    '  Helpers Semánticos
    ' ===========================================
    ' Es interno/virtual
    Public Function IsInternalToken() As Boolean
        Return GetFamily(Me.ID) = TokenFamily.TF_ESPECIALES
    End Function

    'Es Sentencia
    Public Function IsStatement() As Boolean
        Return GetTipo(Me.ID) = TokenTipo.TT_SENTENCIA
    End Function

    Public Function IsStatementStart() As Boolean
        Return IsStatement() OrElse IsProcedure()
    End Function

    'Es Funcion
    Public Function IsFunction() As Boolean
        Return GetTipo(Me.ID) = TokenTipo.TT_FUNCION
    End Function

    'Es Procedimiento
    Public Function IsProcedure() As Boolean
        Return GetTipo(Me.ID) = TokenTipo.TT_PROCEDIMIENTO
    End Function

    'Es Operador
    Public Function IsOperator() As Boolean
        Return GetTipo(Me.ID) = TokenTipo.TT_OPERADOR
    End Function

    'Es Directiva 
    Public Function IsPrintDirective() As Boolean
        Return GetTipo(Me.ID) = TokenTipo.TT_DIRECTIVA
    End Function

    ' ===========================================
    ' Helpers para control de compatibilidad
    ' ===========================================
    Public Function IsUnsupported() As Boolean
        Return GetFamily(ID) = TokenFamily.TF_NOSOPORTADO
    End Function

    ' ===========================================
    ' Helpers para control de usos específicos
    ' ===========================================
    Public Function CanAppearInPrint() As Boolean
        ' Directivas propias
        If Me.IsPrintDirective() Then Return True

        ' Las funciones son imprimibles
        If Me.IsFunction() Then Return True

        ' Literales, variables, números y operadores
        If Me.ID = TokenID.TES_STRING OrElse
           Me.ID = TokenID.TES_IDENT OrElse
           Me.ID = TokenID.TES_NUMBER OrElse
           Me.IsOperator() Then
            Return True
        End If

        ' Paréntesis para variables y funciones
        If Me.ID = TokenID.TSP_PAR_ABIERTO OrElse
           Me.ID = TokenID.TSP_PAR_CERRADO Then
            Return True
        End If

        ' Procedimientos solo si afectan al formato PRINT
        If Me.IsProcedure() Then
            Select Case Me.ID
                Case TokenID.TK_INK,
                     TokenID.TK_PAPER,
                     TokenID.TK_FLASH,
                     TokenID.TK_BRIGHT,
                     TokenID.TK_INVERSE
                    Return True
            End Select

            Return False
        End If

        Return False
    End Function

    ' ----------------------------------------------------------------
    ' Para ver si es una palabra reservada
    ' ----------------------------------------------------------------
    Private Shared Keywords As HashSet(Of String) = Nothing

    Public Shared Function IsKeyword(name As String) As Boolean
        If String.IsNullOrWhiteSpace(name) Then Return False

        If Keywords Is Nothing Then
            Keywords = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each tk In [Enum].GetNames(GetType(TokenID))
                If tk.StartsWith("TK_", StringComparison.OrdinalIgnoreCase) Then
                    Keywords.Add(tk.Substring(3))
                End If
            Next
        End If

        Return Keywords.Contains(name.Trim())
    End Function


    Private Function GetNombreSentencia() As String
        'Dos excepciones porque la misma sentencia se trata de dos maneras
        Select Case Me.ID
            Case TokenID.TK_CLEAR_RAM
                Return "CLEAR"
            Case TokenID.TK_RANDOMIZE_USR
                Return "RANDOMIZE USR"
        End Select

        Dim s As String = Me.ID.ToString()

        ' Regla general: solo tokens TK_ generan sentencia
        If s.StartsWith("TK_", StringComparison.Ordinal) Then
            s = s.Substring(3)
            'Si terminan por _S son de cadena, cambio por $
            If s.EndsWith("_S", StringComparison.Ordinal) Then
                s = s.Substring(0, s.Length - 2) & "$"
            End If
            Return s
        End If
        ' No es una sentencia emitible
        Return ""
    End Function

    ' Indica si pueden aparecer dentro de un PRINT
    ' TAB y AT se tratan como casos especiales, esto generan una sentencia independiente
    Private Function GetEsModificadorPrint() As Boolean
        Select Case Me.ID
            Case TokenID.TK_INK,
                 TokenID.TK_PAPER,
                 TokenID.TK_BRIGHT,
                 TokenID.TK_FLASH,
                 TokenID.TK_OVER,
                 TokenID.TK_INVERSE
                Return True
        End Select
        Return False
    End Function

    Private Function DebeTenerRPN(id As TokenID) As Boolean

        Select Case id
            Case TokenID.TK_LET,
                 TokenID.TK_IF,
                 TokenID.TK_PRINT,
                 TokenID.TK_FOR

                Return True

        End Select

        Return False

    End Function


End Structure

