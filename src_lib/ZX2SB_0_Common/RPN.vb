Imports System.Formats

Public Module RPN

    Public Enum RPNKind
        VAR          ' Variable o símbolo (A, B$, ARR)
        CTE          ' Constante literal (5, "HELLO")
        UNARY_OP     ' Operador unario (UNARY_MINUS, NOT)
        BINARY_OP    ' Operador binario (+, -, *, AND, =, etc.)
        FUN_CALL     ' Llamada a función o acceso a array
        ASSIGN       ' Asignación en un LET, separa L y R
        IDX          ' Indices
        FOR_TO       ' TO del FOR
        FOR_STEP     ' STEP del FOR
    End Enum

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
            Dim kindLetter As Char = text(i)
            i += 1

            ' --- esperar '(' ---
            If i >= text.Length OrElse text(i) <> "("c Then
                Throw New FormatException(
                $"IR: ParseRPN inválido: esperaba '(' tras '{kindLetter}'")
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
            Select Case kindLetter

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
                        .Value = content,
                        .Arity = 1
                    })

                Case GetKindLetter(RPNKind.BINARY_OP)
                    rpn.Add(New RPN_Node With {
                        .Kind = RPNKind.BINARY_OP,
                        .Value = content,
                        .Arity = 2
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
                        .Arity = parts.Count
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
                Case Else
                    Throw New FormatException($"IR RPN inválido: tipo desconocido '{kindLetter}'")

            End Select

        End While

        Return rpn

    End Function


    Public Function RPNToInfix(rpn As List(Of RPN_Node)) As String

        If rpn Is Nothing OrElse rpn.Count = 0 Then
            Return ""
        End If

        Dim stack As New Stack(Of String)

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
                    stack.Push($"{a}{n.Value}{b}")

                Case RPNKind.UNARY_OP
                    If stack.Count < 1 Then Continue For
                    Dim a = stack.Pop()
                    stack.Push($"{n.Value}{a}")

                Case RPNKind.FUN_CALL
                    ' Función tipo F(c,1)
                    Dim args As New List(Of String)

                    For i As Integer = 1 To n.Arity
                        If stack.Count > 0 Then
                            args.Insert(0, stack.Pop())
                        End If
                    Next

                    stack.Push($"{n.Value}({String.Join(",", args)})")

                Case RPNKind.IDX
                    ' IDX → array A(i,j)
                    Dim args As New List(Of String)

                    For i As Integer = 1 To n.Arity
                        If stack.Count > 0 Then
                            args.Insert(0, stack.Pop())
                        End If
                    Next

                    ' El nombre base de la variable está justo antes
                    If stack.Count > 0 Then
                        Dim arr = stack.Pop()
                        stack.Push($"{arr}({String.Join(",", args)})")
                    End If

            End Select

        Next

        Return If(stack.Count > 0, stack.Pop(), "")

    End Function

    Public Function RPN_To_Infix(expr As String) As String

        Dim stack As New Stack(Of String)

        ' Tokenizar por espacios de nivel superior
        Dim tokens As List(Of String) = TokenizarRPN(expr)

        For Each tk In tokens

            ' --------------------------
            ' Variable: V(x)
            ' --------------------------
            If tk.StartsWith("V(") Then
                stack.Push(ExtraerContenido(tk))

                ' --------------------------
                ' Constante: C(n)
                ' --------------------------
            ElseIf tk.StartsWith("C(") Then
                stack.Push(ExtraerContenido(tk))

                ' --------------------------
                ' Operador binario: B(+), B(*), B(^)...
                ' --------------------------
            ElseIf tk.StartsWith("B(") Then
                Dim op As String = ExtraerContenido(tk)
                Dim rhs As String = stack.Pop()
                Dim lhs As String = stack.Pop()
                stack.Push($"{lhs}{op}{rhs}")

                ' --------------------------
                ' Función: F(nombre,argc)
                ' --------------------------
            ElseIf tk.StartsWith("F(") Then
                Dim inner As String = ExtraerContenido(tk)
                Dim parts = inner.Split(Constantes.C_COMA)
                Dim fname As String = parts(0)
                Dim argc As Integer = Integer.Parse(parts(1))

                Dim args As New List(Of String)
                For i As Integer = 1 To argc
                    args.Insert(0, stack.Pop())
                Next

                stack.Push($"{fname}({String.Join(Constantes.C_COMA, args)})")

                ' --------------------------
                ' I(...) → índices de array
                ' --------------------------
            ElseIf tk.StartsWith("I(") Then
                Dim inner As String = ExtraerContenido(tk)

                ' Separar índices de nivel superior
                Dim indices = SplitTopLevel(inner, Constantes.C_COMA)
                Dim idxExpr As New List(Of String)

                For Each idx In indices
                    idxExpr.Add(RPN_To_Infix(idx.Trim()))
                Next

                Dim baseVar As String = stack.Pop()
                stack.Push($"{baseVar}({String.Join(Constantes.C_COMA, idxExpr)})")

            Else
                Throw New Exception($"Token RPN desconocido: {tk}")
            End If

        Next

        If stack.Count <> 1 Then
            Throw New Exception("RPN inválida: pila no reducida a 1 elemento")
        End If

        Return stack.Pop()
    End Function

    Private Function TokenizarRPN(text As String) As List(Of String)
        Dim res As New List(Of String)
        Dim level As Integer = 0
        Dim start As Integer = 0

        For i As Integer = 0 To text.Length - 1
            Select Case text(i)
                Case Constantes.C_PAR_CIE
                    level += 1
                Case Constantes.C_PAR_CIE
                    level -= 1
                Case Constantes.C_ESPACIO
                    If level = 0 Then
                        res.Add(text.Substring(start, i - start).Trim())
                        start = i + 1
                    End If
            End Select
        Next

        If start < text.Length Then
            res.Add(text.Substring(start).Trim())
        End If

        Return res.Where(Function(s) s <> "").ToList()
    End Function

    Private Function ExtraerContenido(tk As String) As String
        Dim p1 = tk.IndexOf(Constantes.C_PAR_APE)
        Dim p2 = tk.LastIndexOf(Constantes.C_PAR_CIE)
        Return tk.Substring(p1 + 1, p2 - p1 - 1)
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

End Module
