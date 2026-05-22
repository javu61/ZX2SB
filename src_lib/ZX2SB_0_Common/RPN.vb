Imports System.Formats
Imports System.Text

Public Module RPN
    Public Const UNARY_MINUS As String = "UNARY_MINUS"
    Public Enum RPNKind
        VAR          ' Variable o símbolo (A, B$, ARR)
        CTE          ' Constante literal (5, "HELLO")
        UNARY_OP     ' Operador unario (UNARY_MINUS, NOT)
        BINARY_OP    ' Operador binario (+, -, *, AND, =, etc.)
        FUN_CALL     ' Llamada a función 
        ASSIGN       ' Asignación en un LET, separa L y R
        IDX          ' Acceso a array con indices
        FOR_TO       ' TO del FOR
        FOR_STEP     ' STEP del FOR
        DATA_SEP     ' Separador de DATA (la coma)
    End Enum

    Public Function PrecedenciaFromTxt(op As String) As Integer
        Select Case op

            Case "^" : Return 7
            Case RPN.UNARY_MINUS : Return 6
            Case "*", "/" : Return 5
            Case "+", "-" : Return 4
            Case "=", "<>", "<", ">", "<=", ">=" : Return 3
            Case "NOT" : Return 2
            Case "AND" : Return 1
            Case "OR" : Return 0

        End Select
        Return -1
    End Function

    Public Structure RPN_Node
        Public Kind As RPNKind
        Public TokenID As TokenID

        ' Texto base:
        '  - variable: "A", "B$", "ARR"
        '  - constante: "5", """HELLO"""
        '  - operador: "+", "AND", "UNARY_MINUS"
        Public Value As String

        ' Número de operandos que consume:
        '  - 0 para VAR / CTE
        '  - 1 para OPE_UNARY
        '  - 2 para OPE_BINARY
        '  - N para CALL
        Public Arity As Integer
    End Structure


    Public Structure IR_Let
        Public Name As String
        Public Indices As List(Of List(Of RPN.RPN_Node))
        Public Expr As List(Of RPN.RPN_Node)
    End Structure


    Public Function GetKindLetter(kind As RPNKind) As Char
        Select Case kind
            Case RPNKind.VAR : Return "V"c
            Case RPNKind.CTE : Return "C"c
            Case RPNKind.UNARY_OP : Return "U"c
            Case RPNKind.BINARY_OP : Return "B"c
            Case RPNKind.FUN_CALL : Return "F"c
            Case RPNKind.ASSIGN : Return "A"c
            Case RPNKind.IDX : Return "I"c
            Case RPNKind.FOR_TO : Return "T"c
            Case RPNKind.FOR_STEP : Return "S"c
            Case RPNKind.DATA_SEP : Return "M"c
        End Select
        Return CChar(Constantes.CADENAVACIA)
    End Function


    ' ============================================================
    ' Procesar el RPN
    ' ============================================================
    Public Function ParseRPN(text As String) As List(Of RPN.RPN_Node)

        Dim rpn As New List(Of RPN.RPN_Node)

        If String.IsNullOrWhiteSpace(text) Then
            Return rpn
        End If

        Dim i As Integer = 0

        While i < text.Length

            ' --- saltar todo lo que no sea inicio de nodo ---
            While i < text.Length AndAlso Not Char.IsLetter(text(i))
                i += 1
            End While

            If i >= text.Length Then Exit While

            ' --- tipo de nodo ---
            Dim KindRecibido As Char = text(i)
            i += 1

            ' --- esperar '(' ---
            If i >= text.Length OrElse text(i) <> "("c Then
                Throw New FormatException(
                $"IR: ParseRPN inválido: esperaba '(' tras '{KindRecibido}'")
            End If

            i += 1 ' consumir '('
            Dim start As Integer = i

            ' --- leer contenido ---
            Dim inString As Boolean = False

            While i < text.Length

                Dim c As Char = text(i)

                If c = Constantes.C_COMILLAS Then
                    inString = Not inString

                ElseIf Not inString AndAlso c = ")"c Then
                    Exit While
                End If

                i += 1
            End While

            If i >= text.Length Then
                Throw New FormatException("IR: ParseRPN inválido: paréntesis sin cerrar")
            End If

            Dim content As String = text.Substring(start, i - start)
            i += 1 ' consumir ')'

            ' --- construir nodo ---
            Select Case KindRecibido

                Case GetKindLetter(RPNKind.ASSIGN)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.ASSIGN,
                        .Value = content
                    })

                Case GetKindLetter(RPNKind.VAR)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.VAR,
                        .Value = content
                    })

                Case GetKindLetter(RPNKind.CTE)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.CTE,
                        .Value = content
                    })

                Case GetKindLetter(RPNKind.UNARY_OP)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.UNARY_OP,
                        .Value = content
                    })

                Case GetKindLetter(RPNKind.BINARY_OP)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.BINARY_OP,
                        .Value = content
                    })

                Case GetKindLetter(RPNKind.FUN_CALL)
                    Dim parts = content.Split(","c)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.FUN_CALL,
                        .Value = parts(0),
                        .Arity = Integer.Parse(parts(1))
                    })

                Case GetKindLetter(RPNKind.IDX)
                    Dim parts = SplitTopLevel(content, ","c)

                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.IDX,
                        .Value = "",
                        .Arity = Integer.Parse(parts(0))
                    })

                Case GetKindLetter(RPNKind.FOR_TO)
                    Dim parts = SplitTopLevel(content, ","c)

                    rpn.Add(New RPN_Node With {
                            .Kind = RPNKind.FOR_TO,
                            .Value = content
                        })

                Case GetKindLetter(RPNKind.FOR_STEP)
                    Dim parts = SplitTopLevel(content, ","c)

                    rpn.Add(New RPN_Node With {
                            .Kind = RPNKind.FOR_STEP,
                            .Value = content
                        })


                Case GetKindLetter(RPNKind.DATA_SEP)   ' separación DATA
                    rpn.Add(New RPN_Node With {
                            .Kind = RPNKind.DATA_SEP,
                            .Value = ",",
                            .Arity = 0
                        })

                Case Else
                    Throw New FormatException($"IR RPN inválido: tipo desconocido '{KindRecibido}'")

            End Select

        End While

        Return rpn

    End Function

    Public Function RPNToInfix(rpn As List(Of RPN_Node)) As String

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            Return ""
        End If

        Dim stack As New Stack(Of String)

        Dim z As New StringBuilder

        For Each n In rpn
            z.Append($"{n.Kind}_{n.Value}_{n.Arity}:")
        Next

        For Each n In rpn

            Select Case n.Kind

                Case RPNKind.CTE
                    stack.Push(n.Value)

                Case RPNKind.VAR
                    stack.Push(n.Value)

                Case RPNKind.BINARY_OP
                    If stack.Count < 2 Then Continue For

                    Dim b = stack.Pop()
                    Dim a = stack.Pop()
                    Dim op = n.Value.ToUpperInvariant()


                    Dim precOp = GetPrecedence(op)

                    ' ✅ Extraer operadores si los hay (simple detección)
                    Dim aNeedsParens = ExtraerPrecedencia(a) < precOp
                    Dim bNeedsParens = ExtraerPrecedencia(b) < precOp

                    If aNeedsParens Then a = $"({a})"
                    If bNeedsParens Then b = $"({b})"

                    If op = "AND" OrElse op = "OR" OrElse op = "XOR" Then
                        stack.Push($"{a} {op} {b}")
                    Else
                        stack.Push($"{a}{op}{b}")
                    End If

                    ' Opcional: añadir paréntesis para prevenir problemas de precedencia


                Case RPNKind.UNARY_OP
                    If stack.Count < 1 Then Continue For

                    Dim a = stack.Pop()
                    If n.Value = UNARY_MINUS Then n.Value = "-"
                    stack.Push($"{n.Value}{a}")

                Case RPNKind.FUN_CALL
                    Dim args As New List(Of String)

                    For i As Integer = 1 To n.Arity
                        If stack.Count > 0 Then
                            args.Insert(0, stack.Pop())
                        End If
                    Next

                    Dim aux As String = $"{GetNombreFuncion(n.Value)}"
                    If args.Count <> 0 Then
                        aux &= $"({String.Join(",", args)})"
                    End If
                    stack.Push(aux)

                Case RPNKind.IDX
                    Dim args As New List(Of String)

                    ' sacar argumentos (z*4+f, bx+c+5)
                    For i As Integer = 1 To n.Arity
                        Dim st As String = stack.Pop()
                        args.Insert(0, st)
                    Next

                    ' baseVar SIEMPRE es el siguiente
                    If stack.Count = 0 Then Continue For

                    Dim baseVar = stack.Pop()

                    ' construir correctamente
                    stack.Push($"{baseVar}({String.Join(",", args)})")
            End Select

        Next

        If (stack.Count > 0) Then Return stack.Pop() Else Return ""

    End Function

    Private Function GetNombreFuncion(nombre As String) As String
        Dim tkid As TokenID

        If [Enum].TryParse(nombre, True, tkid) Then
            Dim tk As New Token(tkid)
            Return (tk.Mnemonic)
        End If

        Return "ERROR_EN: nombre"
    End Function
    Private Function GetPrecedence(op As String) As Integer
        Select Case op.ToUpperInvariant()
            Case "^" : Return 4
            Case "*", "/" : Return 3
            Case "+", "-" : Return 2
            Case "AND", "OR", "XOR" : Return 1
            Case Else : Return 0
        End Select
    End Function


    Private Function ExtraerPrecedencia(expr As String) As Integer

        ' heurística simple (suficiente para tu caso actual)
        If expr.Contains("+") OrElse expr.Contains("-") Then Return 2
        If expr.Contains("*") OrElse expr.Contains("/") Then Return 3
        If expr.Contains("^") Then Return 4

        Return 5 ' literal o función
    End Function

    Private Function SplitTopLevel(text As String, separator As Char) As List(Of String)
        Dim result As New List(Of String)
        Dim level As Integer = 0
        Dim start As Integer = 0

        For i As Integer = 0 To text.Length - 1
            Dim ch As Char = text(i)

            Select Case ch
                Case Constantes.C_PAR_APE
                    level += 1
                Case Constantes.C_PAR_CIE
                    level -= 1
                Case separator
                    If level = 0 Then
                        result.Add(text.Substring(start, i - start).Trim())
                        start = i + 1
                    End If
            End Select
        Next

        ' Último segmento
        If start < text.Length Then
            result.Add(text.Substring(start).Trim())
        End If

        Return result
    End Function

    Public Function RPN_ToText(rpn As List(Of RPN.RPN_Node)) As String
        Dim sb As New StringBuilder()
        If rpn IsNot Nothing AndAlso rpn.Count <> 0 Then
            For Each n In rpn
                'La letra siempre se usará
                sb.Append($"{GetKindLetter(n.Kind)}")
                'El contenido depende de lo que esté definido
                If (n.Value <> "" And n.Arity <> 0) Then
                    sb.Append($"({n.Value},{n.Arity}) ")
                ElseIf (n.Value = "" And n.Arity <> 0) Then
                    sb.Append($"({n.Arity}) ")
                ElseIf (n.Value <> "" And n.Arity = 0) Then
                    sb.Append($"({n.Value}) ")
                End If
            Next
        End If

        Return sb.ToString().Trim()
    End Function

End Module
