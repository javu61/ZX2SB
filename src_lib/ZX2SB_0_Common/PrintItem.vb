
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

    Public ItemType As TokenID
    Public Value As String
    Public Separator As PrintSeparator

    Public Sub New(type As TokenID, valor As String, sep As PrintSeparator)
        Me.ItemType = type
        Me.Value = valor
        Me.Separator = sep
    End Sub


    Public Sub New(type As TokenID)
        Me.ItemType = type
        Me.Value = ""
        Me.Separator = PrintSeparator.N
    End Sub

    Public Sub New(linea As String)
        Dim p = FromText(linea)
        Me.ItemType = p.ItemType
        Me.Value = p.Value
        Me.Separator = p.Separator
    End Sub

    Public Function ToText() As String
        ' Formato: ID,Separator,Value
        ' Solo se interpretan la primera y segunda coma.
        ' El resto del texto pertenece íntegramente a Value.

        Return $"{CInt(Me.ItemType)},{Me.Separator},{Me.Value}"
    End Function

    Public Shared Function FromText(text As String) As PrintItem
        Dim c1 As Integer = text.IndexOf(","c)
        If c1 < 0 Then
            Throw New FormatException($"PrintItem inválido: {text}")
        End If

        Dim c2 As Integer = text.IndexOf(","c, c1 + 1)
        If c2 < 0 Then
            Throw New FormatException($"PrintItem inválido: {text}")
        End If

        Dim id As TokenID = CType(Integer.Parse(text.Substring(0, c1)), TokenID)
        Dim valueText As String = text.Substring(c2 + 1)
        Dim sepText As String = text.Substring(c1 + 1, c2 - c1 - 1)
        Dim sep As PrintSeparator = CType([Enum].Parse(GetType(PrintSeparator), sepText), PrintSeparator)

        Dim result As New PrintItem With {.ItemType = id, .Value = valueText, .Separator = sep}

        Return result
    End Function


End Structure
