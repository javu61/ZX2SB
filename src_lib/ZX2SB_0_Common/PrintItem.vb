
' ===========================================
'  Definición de Elementos del PRINT
' ===========================================

Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Text
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
        Dim p As PrintItem = FromToken(tk)
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


    Public Function FromToken(tk As Token) As PrintItem

        If tk.ID = TokenID.TCO_UNKNOWN Then
            Throw New ArgumentNullException(NameOf(tk))
        End If

        If String.IsNullOrEmpty(tk.Value) Then
            Throw New FormatException("PRINT inválido: Value vacío")
        End If

        ' Formato esperado:
        ' ID,Separator,ValueRPN

        Dim parts = tk.Value.Split(","c, 3)

        If parts.Length < 3 Then
            Throw New FormatException($"PRINT inválido: formato incorrecto → {tk.Value}")
        End If

        Dim item As New PrintItem

        item.ID = CType(Integer.Parse(parts(0)), TokenID)
        item.Separator = CType([Enum].Parse(GetType(PrintSeparator), parts(1)), PrintSeparator)

        Dim rpnText As String = parts(2)

        'Si es AT tiene dos partes, el resto una


        If item.ID = TokenID.TK_AT Then
            Dim partes = SplitTopLevel(rpnText, ","c)

            ' AT debe tener dos expresiones
            If partes.Count <> 2 Then
                Throw New FormatException($"PRINT AT inválido: esperaba 2 argumentos → {rpnText}")
            End If

            item.Expr1 = ParseRPN_Texto(partes(0).Trim())
            item.Expr2 = ParseRPN_Texto(partes(1).Trim())

        Else

            ' Caso normal
            item.Expr1 = ParseRPN_Texto(rpnText)

        End If




        Return item

    End Function

    Private Shared Function SplitTopLevel(text As String, sep As Char) As List(Of String)

        Dim res As New List(Of String)
        Dim level As Integer = 0
        Dim start As Integer = 0

        For i As Integer = 0 To text.Length - 1

            Dim c As Char = text(i)

            Select Case c

                Case "("c
                    level += 1

                Case ")"c
                    level -= 1

                Case sep
                    If level = 0 Then
                        res.Add(text.Substring(start, i - start))
                        start = i + 1
                    End If

            End Select

        Next

        res.Add(text.Substring(start))

        Return res

    End Function

    Private Function ParseRPN_Texto(text As String) As List(Of RPN_Node)

        Dim res As New List(Of RPN_Node)
        Dim i As Integer = 0

        While i < text.Length

            If Char.IsWhiteSpace(text(i)) Then
                i += 1
                Continue While
            End If

            ' Tipo (V, C, F, B)
            Dim tipo As Char = text(i)
            i += 1

            If i >= text.Length OrElse text(i) <> "("c Then
                Throw New Exception($"RPN inválido: se esperaba '(' tras '{tipo}'")
            End If

            i += 1 ' saltar '('

            ' Leer contenido hasta cierre
            Dim contenido As New StringBuilder()
            Dim nivel As Integer = 1

            While i < text.Length AndAlso nivel > 0
                If text(i) = "("c Then
                    nivel += 1
                ElseIf text(i) = ")"c Then
                    nivel -= 1
                    If nivel = 0 Then Exit While
                End If

                contenido.Append(text(i))
                i += 1
            End While

            i += 1 ' saltar ')'

            ' Construir nodo
            Dim nodo As New RPN_Node

            Select Case tipo

                Case "V"c
                    nodo.Kind = RPNKind.VAR
                    nodo.Value = contenido.ToString()

                Case "C"c
                    nodo.Kind = RPNKind.CTE
                    nodo.Value = contenido.ToString()

                Case "F"c
                    Dim parts = contenido.ToString().Split(","c)
                    nodo.Kind = RPNKind.FUN_CALL
                    nodo.Value = parts(0)
                    nodo.Arity = Integer.Parse(parts(1))

                Case "B"c
                    nodo.Kind = RPNKind.BINARY_OP
                    nodo.Value = contenido.ToString()

            End Select

            res.Add(nodo)

        End While

        Return res

    End Function


End Structure
