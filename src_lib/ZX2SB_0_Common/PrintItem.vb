
' ===========================================
'  Definición de Elementos del PRINT
' ===========================================

Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Formats
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Xml

Public Enum PrintSeparator
    N      ' fin
    P      ' ;
    C      ' ,
End Enum
Public Structure PrintItem

    Public prID As TokenID
    Public prValue As String
    Public prChannel As List(Of RPN.RPN_Node)
    Public prExpr1 As List(Of RPN.RPN_Node)
    Public prExpr2 As List(Of RPN.RPN_Node)
    Public prSeparator As PrintSeparator

    Public Sub New(type As TokenID, valor As String, sep As PrintSeparator)
        Me.prID = type
        Me.prValue = valor
        Me.prSeparator = sep
    End Sub


    Public Sub New(type As TokenID)
        Me.prID = type
        Me.prValue = ""
        Me.prSeparator = PrintSeparator.N
    End Sub

    Public Sub New(tk As Token)
        Dim p As PrintItem = FromToken(tk)
        Me.prID = p.prID
        Me.prValue = p.prValue
        Me.prExpr1 = p.prExpr1
        Me.prExpr2 = p.prExpr2
        Me.prSeparator = p.prSeparator
    End Sub

    Public Function ToText() As String
        ' Formato: ID,Separator,Value
        ' Solo se interpretan la primera y segunda coma.
        ' El resto del texto pertenece íntegramente a Value.

        Return $"{CInt(Me.prID)},{Me.prSeparator},{Me.prValue}"
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

        Dim item As New PrintItem()
        item.prID = CType(Integer.Parse(parts(0)), TokenID)
        item.prSeparator = CType([Enum].Parse(GetType(PrintSeparator), parts(1)), PrintSeparator)


        Dim rpnText As String = parts(2)


        Dim partes = SepararPorSeparador(item.prExpr1)
        If item.prID = TokenID.TK_AT Then
            'Si es AT tiene dos partes, el resto una
            If partes.Count <> 2 Then
                Throw New FormatException($"PRINT AT inválido: esperaba 2 argumentos → {rpnText}")
            End If

            item.prExpr1 = partes(0)
            item.prExpr2 = partes(1)
        Else
            ' Caso normal
            If partes.Count <> 1 Then
                Throw New FormatException($"PRINT Comando inválido: esperaba 1 argumento → {rpnText}")
            End If

            item.prExpr1 = partes(0)
        End If
        Return item

    End Function


    Private Function SepararPorSeparador(rpn As List(Of RPN_Node)) As List(Of List(Of RPN_Node))

        Dim resultado As New List(Of List(Of RPN_Node))
        Dim actual As New List(Of RPN_Node)

        For Each node In rpn

            If node.Kind = RPNKind.DATA_SEP Then

                If actual.Count > 0 Then
                    resultado.Add(New List(Of RPN_Node)(actual))
                    actual.Clear()
                End If

            Else
                actual.Add(node)
            End If

        Next

        If actual.Count > 0 Then
            resultado.Add(actual)

        End If

        Return resultado

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

                Case GetKindLetter(RPNKind.VAR)
                    nodo.Kind = RPNKind.VAR
                    nodo.Value = contenido.ToString()

                Case GetKindLetter(RPNKind.CTE)
                    nodo.Kind = RPNKind.CTE
                    nodo.Value = contenido.ToString()

                Case GetKindLetter(RPNKind.FUN_CALL)
                    Dim parts = contenido.ToString().Split(","c)
                    nodo.Kind = RPNKind.FUN_CALL
                    nodo.Value = parts(0)
                    nodo.Arity = Integer.Parse(parts(1))

                    ' Reconstruir TokenID automáticamente
                    Dim tkid As TokenID
                    If [Enum].TryParse(parts(0), True, tkid) Then
                        nodo.TokenID = tkid
                    Else
                        nodo.TokenID = TokenID.TCO_NONE
                    End If

                Case GetKindLetter(RPNKind.BINARY_OP)
                    nodo.Kind = RPNKind.BINARY_OP
                    nodo.Value = contenido.ToString()

                Case GetKindLetter(RPNKind.UNARY_OP)
                    nodo.Kind = RPNKind.UNARY_OP
                    nodo.Value = contenido.ToString()
                Case GetKindLetter(RPNKind.ASSIGN)
                    nodo.Kind = RPNKind.ASSIGN
                    nodo.Value = contenido.ToString()
                Case GetKindLetter(RPNKind.IDX)
                    nodo.Kind = RPNKind.IDX
                    nodo.Value = contenido.ToString()
                Case GetKindLetter(RPNKind.FOR_TO)
                    nodo.Kind = RPNKind.FOR_TO
                    nodo.Value = contenido.ToString()
                Case GetKindLetter(RPNKind.FOR_STEP)
                    nodo.Kind = RPNKind.FOR_STEP
                    nodo.Value = contenido.ToString()
                Case GetKindLetter(RPNKind.DATA_SEP)
                    nodo.Kind = RPNKind.DATA_SEP
                    nodo.Value = contenido.ToString()
            End Select

            res.Add(nodo)

        End While

        Return res

    End Function

    Public Function IsPrintDirective() As Boolean
        Dim tk As New Token(Me.prID)
        Return tk.IsPrintDirective
    End Function

End Structure
