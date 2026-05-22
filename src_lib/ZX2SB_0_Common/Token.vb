
' ===========================================
'  Definición de Token del lenguaje ZX2SB
' ===========================================

Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Xml

Public Structure Token

    Public ID As TokenID
    Public Value As String
    Public Lin As Integer
    Public Col As Integer
    Dim _RPN As List(Of RPN_Node)

    Public Sub New(id As TokenID, valor As String, linea As Integer, columna As Integer)
        Me.ID = id
        Me.Value = valor
        Me.Lin = linea
        Me.Col = columna
    End Sub

    Public Sub New(id As TokenID, valor As String)
        Me.ID = id
        Me.Value = valor
        Me.Lin = 0
        Me.Col = 0
    End Sub

    Public Sub New(id As TokenID)
        Me.ID = id
        Me.Value = ""
        Me.Lin = 0
        Me.Col = 0
    End Sub

    Public Sub New(linea As String)
        LineToTok(linea)
    End Sub

    Public ReadOnly Property Mnemonic As String
        Get
            Return GetNombreSentencia().ToUpperInvariant()
        End Get
    End Property

    Public ReadOnly Property Canonico As String
        Get
            Return Value.Replace("_", "")
        End Get
    End Property

    Public ReadOnly Property EsModificadorPrint As Boolean
        Get
            Return GetEsModificadorPrint()
        End Get
    End Property

    Public ReadOnly Property GetFamily As TokenFamily
        Get
            Return pGetFamily(Me.ID)
        End Get
    End Property

    Public ReadOnly Property getAridad As Integer
        Get
            'Cuantos argumentos necesita la función, o cero si no es función
            Return pGetAridad(Me.ID)
        End Get
    End Property

    Public ReadOnly Property GetValor As String
        Get
            Select Case Me.ID

                Case TokenID.TES_STRING

                    Dim raw As String = Me.Value

                    ' ---------------------------------
                    ' Formato esperado = :len:"texto"
                    ' ---------------------------------
                    If raw.StartsWith(":") Then
                        Dim p As Integer = raw.IndexOf(":", 1)
                        If p > 1 Then
                            Dim len As Integer = Integer.Parse(raw.Substring(1, p - 1))
                            raw = raw.Substring(p + 1)
                        End If
                    End If

                    Return raw

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
            Return _RPN
        End Get
    End Property


    Public Shared Function GetFamilyFromID(id As TokenID) As TokenFamily
        Return pGetFamily(id)
    End Function

    ' ---------------------------------------------------
    '  Serialización EXACTA al fichero .tok
    '  Formato:
    '     <ID> [<linea>,<columna>] [<Valor>] [ ; Token]
    ' ---------------------------------------------------
    Public Function TokToLine() As String
        Dim aux As String = CInt(ID).ToString("D5") & " [" & Lin & "," & Col & "]"

        If Not String.IsNullOrEmpty(Value) Then
            aux &= " " & Value
        End If

        If Me.ID <> TokenID.TCO_UNKNOWN Then
            If (Len(aux) < Constantes.Separacion_Comentario) Then
                aux = aux & Space(Constantes.Separacion_Comentario - Len(aux)) & Constantes.Marca_Comentario & Me.ID.ToString
            Else
                aux &= vbCrLf & Space(Constantes.Separacion_Comentario) & Constantes.Marca_Comentario & Me.ID.ToString
            End If
        End If

        Return aux
    End Function

    Private Sub LineToTok(linea As String)
        ' Ejemplos: Para LET dinero = 500
        '2132 [27,5]                                                  ; -- TK_LET
        '1700 [27,9] dinero                                           ; -- TES_IDENT
        '1405 [27,15]                                                 ; -- TOP_EQ
        '1701 [27,16] 500                                             ; -- TES_NUMBER


        Me.ID = TokenID.TCO_NONE  'Si no hay nada, es vacío
        Me.Lin = -1
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

        Dim pos As Integer
        Dim resto As String = ""

        'El ID es siempre lo primero
        pos = linea.IndexOf(" "c)
        If pos >= 0 Then
            Me.ID = CType(linea.Substring(0, pos), TokenID)
            resto = linea.Substring(pos + 1).TrimStart()
        End If
        linea = linea.Substring(pos).Trim

        'Si lleva [l,c]
        If linea.StartsWith("[") Then
            Dim endPos = linea.IndexOf("]"c)
            If endPos > 0 Then
                Dim cl = linea.Substring(1, endPos - 1).Split(","c)
                If cl.Length = 2 Then
                    Integer.TryParse(cl(0), Me.Lin)
                    Integer.TryParse(cl(1), Me.Col)
                End If
                Value = linea.Substring(endPos + 1).Trim()
            Else
                ' No contiene linea y columna
                Value = linea
            End If
            linea = linea.Substring(endPos + 1).Trim
        Else
            Me.Lin = 0
            Me.Col = 0
        End If

        'El valor del token
        Me.Value = linea

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
    '  Helpers Directos
    ' ===========================================

    'Obtener la familia
    Private Shared Function pGetFamily(id As TokenID) As TokenFamily
        Return CType((CInt(id) \ 10000) * 10000, TokenFamily)
    End Function

    ' Obtener el tipo
    Private Shared Function pGetTipo(id As TokenID) As TokenTipo
        Return CType(((CInt(id) \ 1000) Mod 10) * 1000, TokenTipo)
    End Function

    ' Obtener la Aridad
    Private Function pGetAridad(id As TokenID) As Integer
        If IsFunction() Then
            Dim aux As TokenAridad = CType(((CInt(id) \ 100) Mod 10) * 100, TokenAridad)
            Select Case aux
                Case TokenAridad.TA_NR1 : Return 1
                Case TokenAridad.TA_NR2 : Return 2
                Case TokenAridad.TA_ST1 : Return 1
                Case TokenAridad.TA_ST2 : Return 2
            End Select
        End If
        Return 0
    End Function


    'Obtener el índice (NN)
    Private Function pGetIndex(id As TokenID) As Integer
        Return (CInt(id) Mod 100)
    End Function

    ' ===========================================
    '  Helpers Semánticos
    ' ===========================================
    ' Es interno/virtual
    Public Function IsInternalToken() As Boolean
        Return pGetFamily(Me.ID) = TokenFamily.TF_ESPECIALES
    End Function

    'Es Sentencia
    Public Function IsStatement() As Boolean
        Return pGetTipo(Me.ID) = TokenTipo.TT_SENTENCIA
    End Function

    Public Function IsStatementStart() As Boolean
        Return IsStatement() OrElse IsProcedure()
    End Function

    'Es Funcion
    Public Function IsFunction() As Boolean
        Return pGetTipo(Me.ID) = TokenTipo.TT_FUNCION
    End Function

    'Es Procedimiento
    Public Function IsProcedure() As Boolean
        If pGetTipo(Me.ID) = TokenTipo.TT_DIRECTIVA Then
            If (Me.ID = TokenID.TK_TAB) Or (Me.ID = TokenID.TK_AT) Then
                Return False
            Else
                Return True
            End If
        End If

        Return pGetTipo(Me.ID) = TokenTipo.TT_PROCEDIMIENTO
    End Function

    'Es Operador
    Public Function IsOperator() As Boolean
        Return pGetTipo(Me.ID) = TokenTipo.TT_OPERADOR
    End Function

    'Es Directiva usable en un PRINT
    Public Function IsPrintDirective() As Boolean
        Return pGetTipo(Me.ID) = TokenTipo.TT_DIRECTIVA
    End Function

    ' ===========================================
    ' Helpers para control de compatibilidad
    ' ===========================================
    Public Function IsUnsupported() As Boolean
        Return pGetFamily(ID) = TokenFamily.TF_NOSOPORTADO
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
    Private Shared mapaTokens As Dictionary(Of String, TokenID)

    Private Shared Sub InitMapa()
        If mapaTokens IsNot Nothing Then Exit Sub

        mapaTokens = New Dictionary(Of String, TokenID)(StringComparer.OrdinalIgnoreCase)
        For Each value As TokenID In [Enum].GetValues(GetType(TokenID))
            Dim nombre = value.ToString()
            ' Solo tokens TK_
            If nombre.StartsWith(Constantes.Marca_Token) Then
                mapaTokens(nombre.Substring(3)) = value
            End If
        Next
    End Sub

    Public Shared Function IsKeyword(Lexema As String) As Boolean
        If String.IsNullOrWhiteSpace(Lexema) Then Return False
        InitMapa()
        Return mapaTokens.ContainsKey(Lexema)
    End Function

    Public Shared Function GetTokenID(lexema As String, ByRef id As TokenID) As Boolean
        InitMapa()
        If mapaTokens.TryGetValue(lexema, id) Then
            Return True
        End If
        Return False
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
        If s.StartsWith(Constantes.Marca_Token, StringComparison.Ordinal) Then
            s = s.Substring(3)
            'Si terminan por _S son de cadena, cambio por $
            If s.EndsWith("_S", StringComparison.Ordinal) Then
                s = s.Substring(0, s.Length - 2) & "$"
            End If

            ' ✅ convertir TK_X → FN_X
            Dim familia = pGetFamily(Me.ID)
            If familia = TokenFamily.TF_GENERAFN OrElse familia = TokenFamily.TF_NOSOPORTADO Then
                s = Constantes.MDir & "_" & s
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
                 TokenID.TK_FOR,
                 TokenID.TK_DATA,
                 TokenID.TK_DIM

                Return True

        End Select

        Return False

    End Function


End Structure

