
' ===========================================
'  Definición de Elementos del PRINT
' ===========================================

Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Xml

Public Enum PrintSeparator
    N      ' fin
    P      ' ;
    C      ' ,
End Enum
Public Structure PrintItem

    Public ID As TokenID
    Public Value As String
    Public Expr1 As List(Of RPN.RPN_Node)
    Public Expr2 As List(Of RPN.RPN_Node)
    Public Separator As PrintSeparator

    Public Sub New(type As TokenID, valor As String, sep As PrintSeparator)
        Me.ID = type
        Me.Value = valor
        Me.Separator = sep
    End Sub


    Public Sub New(type As TokenID)
        Me.ID = type
        Me.Value = ""
        Me.Separator = PrintSeparator.N
    End Sub

    Public Sub New(tk As Token)
        Dim p = FromToken(tk)
        Me.ID = p.ID
        Me.Value = p.Value
        Me.Expr1 = p.Expr1
        Me.Separator = p.Separator
    End Sub

    Public Function ToText() As String
        ' Formato: ID,Separator,Value
        ' Solo se interpretan la primera y segunda coma.
        ' El resto del texto pertenece íntegramente a Value.

        Return $"{CInt(Me.ID)},{Me.Separator},{Me.Value}"
    End Function


    Public Shared Function FromToken(tk As Token) As PrintItem
        If tk.ID = TokenID.TCO_UNKNOWN Then
            Throw New ArgumentNullException(NameOf(tk))
        End If

        If tk.RPN Is Nothing OrElse tk.RPN.Count = 0 Then
            Throw New FormatException($"PRINT inválido: RPN vacía")
        End If

        Return New PrintItem With {
            .ID = tk.ID,
            .Expr1 = tk.RPN
        }
    End Function




End Structure
