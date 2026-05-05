Public Module RPN

    Public Enum RPNKind
        VAR          ' Variable o símbolo (A, B$, ARR)
        CTE          ' Constante literal (5, "HELLO")
        UNARY_OP     ' Operador unario (UNARY_MINUS, NOT)
        BINARY_OP    ' Operador binario (+, -, *, AND, =, etc.)
        FUN_CALL     ' Llamada a función o acceso a array
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
        Return (kind.ToString)(0)
        'Return [Enum].GetName(GetType(RPNKind), kind)(0)
    End Function


    Public Function ParseRPN(text As String) As List(Of RPN.RPN_Node)
        Dim rpn As New List(Of RPN.RPN_Node)

        If String.IsNullOrWhiteSpace(text) Then
            Return rpn
        End If

        Dim i As Integer = 0

        While i < text.Length
            ' Saltar espacios
            If text(i) = " "c Then
                i += 1
                Continue While
            End If

            ' Tipo de nodo (una letra)
            Dim kindLetter As Char = text(i)
            i += 1

            If i >= text.Length OrElse text(i) <> "("c Then
                Throw New FormatException($"IR: ParseRPN inválido: esperaba '(' tras '{kindLetter}'")
            End If

            i += 1 ' consumir '('
            Dim start As Integer = i

            ' Buscar ')'
            While i < text.Length AndAlso text(i) <> ")"c
                i += 1
            End While

            If i >= text.Length Then
                Throw New FormatException("IR: ParseRPN inválido: paréntesis sin cerrar")
            End If

            Dim content As String = text.Substring(start, i - start)
            i += 1 ' consumir ')'

            ' Construir nodo
            Select Case kindLetter

                Case GetKindLetter(RPNKind.VAR)
                    rpn.Add(New RPN.RPN_Node With {
                    .Kind = RPNKind.VAR,
                    .Value = content,
                    .Arity = 0
                })

                Case GetKindLetter(RPNKind.CTE)
                    rpn.Add(New RPN.RPN_Node With {
                    .Kind = RPNKind.CTE,
                    .Value = content,
                    .Arity = 0
                })

                Case GetKindLetter(RPNKind.UNARY_OP)
                    rpn.Add(New RPN.RPN_Node With {
                    .Kind = RPNKind.UNARY_OP,
                    .Value = content,
                    .Arity = 1
                })

                Case GetKindLetter(RPNKind.BINARY_OP)
                    rpn.Add(New RPN.RPN_Node With {
                    .Kind = RPNKind.BINARY_OP,
                    .Value = content,
                    .Arity = 2
                })

                Case GetKindLetter(RPNKind.FUN_CALL)
                    Dim parts = content.Split(","c)
                    rpn.Add(New RPN.RPN_Node With {
                    .Kind = RPNKind.FUN_CALL,
                    .Value = parts(0),
                    .Arity = Integer.Parse(parts(1))
                })

                Case Else
                    Throw New FormatException($"IR RPN inválido: tipo desconocido '{kindLetter}'")
            End Select

        End While

        Return rpn
    End Function


End Module
