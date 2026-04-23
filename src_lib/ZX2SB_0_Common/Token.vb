
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

    Public Sub New(linea As String)
        LineToTok(linea)
    End Sub

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

        If Me.ID <> TokenID.TE_UNKNOWN Then
            aux = aux & Space(50 - Len(aux)) & " ; " & Me.ID.ToString
        End If

        Return aux
    End Function

    Private Function LineToTok(linea As String) As Token
        ' Ejemplo esperado:
        ' 1012 [12,5] PRINT

        ' Quitar comentario desde el ; hasta el final
        If linea.Contains(" ; ") Then
            For i As Integer = linea.Length - 1 To 0 Step -1
                If linea(i) = ";"c Then
                    linea = linea.Substring(0, i).TrimEnd()
                    Exit For
                End If
            Next
        End If


        Dim parts = linea.Split(" "c, 3)

        Dim id As TokenID = CType(Integer.Parse(parts(0)), TokenID)

        Dim pos = parts(1).Trim("["c, "]"c).Split(","c)
        Dim line As Integer = Integer.Parse(pos(0))
        Dim col As Integer = Integer.Parse(pos(1))

        Dim value As String = ""
        If parts.Length = 3 Then value = parts(2)

        Me.ID = id
        Me.Line = line
        Me.Col = col
        Me.Value = value

    End Function

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
    Public Function TokenName(id As TokenID) As String
        Dim name As String = [Enum].GetName(GetType(TokenID), id)
        If name Is Nothing Then
            Return "UNKNOWN(" & CInt(id).ToString("00") & ")"
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
    Public Function GetTipo(id As TokenID) As TokenTipo
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
        If Me.ID = TokenID.TE_STRING OrElse
           Me.ID = TokenID.TE_IDENT OrElse
           Me.ID = TokenID.TE_NUMBER OrElse
           Me.IsOperator() Then
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

End Structure

