Option Strict On
Option Explicit On

Imports System.Data.SqlTypes
Imports System.Formats.Asn1
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml

Public Module Renumerador

    Dim opts As CmdOptions
    Dim Equivalencias As New List(Of Integer)
    Dim NroLineaFichero As Integer
    Dim NroLineaPrograma As Integer
    Dim NroErrores As Integer
    Dim LineaSTOPFinal As Integer = 0
    Dim AddStop As Boolean = False
    Dim indentLevel As Integer = 0
    Dim stReader As StreamReader
    Dim stWriter As StreamWriter

    ' ==========================================================
    ' Punto de entrada
    ' ==========================================================
    Public Function Ejecutar(_opts As CmdOptions) As Integer
        opts = _opts
        stWriter = New StreamWriter(ObtenerFicheroSalida(opts), False, New UTF8Encoding(False))
        stReader = New StreamReader(ObtenerFicheroEntrada(opts))
        NroLineaFichero = 0
        NroErrores = 0

        Try
            ' --------------------------------------------------
            ' PASADA 1: construir tabla de equivalencias
            ' --------------------------------------------------
            opts.Pasada = 1

            If opts.Ren_Base = 0 Then
                opts.Ren_Base = 1000
            End If

            If opts.Ren_Paso = 0 Then
                opts.Ren_Paso = 10
            End If

            If opts.Ren_IND = 0 Then
                opts.Ren_IND = 2
            End If

            Using ficEntrada As New StreamReader(opts.FSalidaGSB)

                Dim LineaParaMostrar As String = ""
                While Not ficEntrada.EndOfStream
                    Dim linea As String = ficEntrada.ReadLine()
                    LineaParaMostrar = NormalizarLinea(opts, NroLineaFichero, NroLineaPrograma, linea)
                    AñadirEquivalencia(NroLineaFichero, NroLineaPrograma)
                End While

                LineaSTOPFinal = CalcularNuevo(Equivalencias.Count + 1)
            End Using


            ' --------------------------------------------------
            ' PASADA 2: reescritura del programa
            ' --------------------------------------------------
            opts.Pasada = 2
            Using ficEntrada As New StreamReader(opts.FSalidaGSB)

                stWriter.NewLine = vbLf

                While Not ficEntrada.EndOfStream
                    Dim linea As String = ficEntrada.ReadLine()
                    Dim nroAntiguo As Integer

                    If ExtraerNumeroLinea(linea, nroAntiguo) Then
                        Dim nroNuevo As Integer = BuscarEquivalencia(nroAntiguo)
                        Dim resto As String = linea.Substring(nroAntiguo.ToString().Length).TrimStart()

                        resto = RenumerarSaltos(resto)
                        GuardarLinea(nroNuevo, resto)
                    Else
                        GuardarLinea(0, linea)
                    End If
                End While

                ' Linea de stop final, si debe existir
                If (AddStop) And (LineaSTOPFinal <> 0) Then
                    GuardarLinea(LineaSTOPFinal - opts.Ren_Paso, "REM *** ZX2SB FINAL DEL PROGRAMA ***")
                    GuardarLinea(LineaSTOPFinal, "STOP")
                End If
            End Using

        Catch ex As Exception
            Console.WriteLine(ex.ToString)
            NroErrores += 1
        End Try

        stWriter.Flush()
        stReader.Close()
        stWriter.Close()

        Return NroErrores

    End Function

    ' ==========================================================
    ' Helpers
    ' ==========================================================
    Private Function CalcularNuevo(numero As Integer) As Integer
        Return (opts.Ren_Base + ((numero - 1 + 1) * opts.Ren_Paso))
    End Function

    Private Function BuscarEquivalencia(NroAntiguo As Integer) As Integer
        Dim pos As Integer = Equivalencias.IndexOf(NroAntiguo)
        If (pos <> -1) Then
            Return (CalcularNuevo(pos))
        Else
            Return 0
        End If
    End Function

    Private Sub AñadirEquivalencia(NroLinea As Integer, NroAntiguo As Integer)
        Dim pos As Integer = Equivalencias.IndexOf(NroAntiguo)

        If pos = -1 Then
            Equivalencias.Add(NroAntiguo)
            GuardarEquivalencia(NroAntiguo, CalcularNuevo(NroLinea))
        End If
    End Sub

    Private Function ExtraerNumeroLinea(linea As String, ByRef numero As Integer) As Boolean
        numero = 0

        If String.IsNullOrEmpty(linea) Then
            Return False
        End If

        Dim i As Integer = 0
        Dim encontrado As Boolean = False

        ' Leer dígitos consecutivos desde el inicio
        While i < linea.Length AndAlso Char.IsDigit(linea(i))
            encontrado = True
            numero = numero * 10 + (Asc(linea(i)) - Asc("0"c))
            i += 1
        End While

        Return encontrado
    End Function

    ' ----------------------------------------------------------
    ' Renumerar saltos básicos
    ' ----------------------------------------------------------
    ' TODO:
    ' - ON expr GOTO a,b,c
    ' - ON expr GOSUB a,b,c
    ' - IF ... THEN <número>
    Private Function RenumerarSaltos(texto As String) As String

        Dim i As Integer = 0
        Dim resultado As String = ""
        Dim enCadena As Boolean = False
        Dim enComentario As Boolean = False

        While i < texto.Length

            ' --------------------------------------------------
            ' Gestión de cadena
            ' --------------------------------------------------
            If texto(i) = Constantes.C_COMILLAS Then
                resultado &= texto(i)
                enCadena = Not enCadena
                i += 1
                Continue While
            End If

            ' --------------------------------------------------
            ' Gestión de REM (solo si no estamos en cadena)
            ' --------------------------------------------------
            If Not enCadena AndAlso CoincidePalabraLiteral(texto, i, "REM") Then
                ' Desde REM hasta fin de línea es comentario
                resultado &= texto.Substring(i)
                Exit While
            End If

            ' --------------------------------------------------
            ' Si estamos en cadena o comentario → copiar literal
            ' --------------------------------------------------
            If enCadena OrElse enComentario Then
                resultado &= texto(i)
                i += 1
                Continue While
            End If

            ' --------------------------------------------------
            ' Intentar renumerar saltos SOLO dentro del código
            ' --------------------------------------------------
            If Not CoincidePalabra(texto, i, resultado, "GOTO") Then
                If Not CoincidePalabra(texto, i, resultado, "GOSUB") Then
                    If Not CoincidePalabra(texto, i, resultado, "RESTORE") Then
                        resultado &= texto(i)
                        i += 1
                    End If
                End If
            End If
        End While

        Return resultado
    End Function

    Private Function CoincidePalabra(texto As String, ByRef i As Integer, ByRef resultado As String, palabra As String) As Boolean

        ' Usar la detección literal ya centralizada
        If Not CoincidePalabraLiteral(texto, i, palabra) Then
            Return False
        End If

        ' ✅ Coincide: procesar el salto completo
        resultado &= ProcesarSalto(texto, i, palabra)

        Return True
    End Function
    Private Function CoincidePalabraLiteral(texto As String, posicion As Integer, palabra As String) As Boolean
        ' Comprobar que cabe la palabra
        If posicion + palabra.Length > texto.Length Then
            Return False
        End If

        ' Comparar carácter a carácter (sin distinguir mayúsculas/minúsculas)
        For j As Integer = 0 To palabra.Length - 1
            If Char.ToUpper(texto(posicion + j)) <> palabra(j) Then
                Return False
            End If
        Next

        ' Asegurar que no es parte de un identificador mayor
        ' Ejemplo: REMX no es REM
        Dim fin As Integer = posicion + palabra.Length
        If fin < texto.Length AndAlso Char.IsLetterOrDigit(texto(fin)) Then
            Return False
        End If

        Return True
    End Function

    Private Function ProcesarSalto(texto As String, ByRef i As Integer, palabra As String) As String

        Dim inicio As Integer = i
        Dim salida As String = ""

        ' Copiar la palabra clave
        salida &= palabra
        i += palabra.Length

        ' Copiar espacios
        While i < texto.Length AndAlso texto(i) = Constantes.C_ESPACIO
            salida &= texto(i)
            i += 1
        End While

        ' Leer número de línea
        Dim NroAnterior As Integer = 0
        Dim encontrado As Boolean = False

        While i < texto.Length AndAlso Char.IsDigit(texto(i))
            encontrado = True
            NroAnterior = NroAnterior * 10 + (Asc(texto(i)) - Asc("0"c))
            i += 1
        End While

        If encontrado Then
            Dim destino As Integer = ResolverSalto(NroAnterior)
            salida &= destino.ToString()
        End If

        ' ✅ Avanzar índice completamente aquí
        i = inicio + LongitudSalto(texto, inicio, palabra)

        Return salida
    End Function

    Private Function LongitudSalto(texto As String, inicio As Integer, palabra As String) As Integer

        Dim i As Integer = inicio + palabra.Length

        While i < texto.Length AndAlso texto(i) = Constantes.C_ESPACIO
            i += 1
        End While

        While i < texto.Length AndAlso Char.IsDigit(texto(i))
            i += 1
        End While

        Return i - inicio
    End Function


    Function ResolverSalto(NroAlQueSaltar As Integer) As Integer
        ' 1) Coincidencia exacta
        Dim pos As Integer = Equivalencias.IndexOf(NroAlQueSaltar)
        If pos <> -1 Then
            Return CalcularNuevo(pos)
        End If

        ' 2) Buscar la siguiente línea existente (por posición)
        For i As Integer = 0 To Equivalencias.Count - 1
            If Equivalencias(i) > NroAlQueSaltar Then
                Return CalcularNuevo(i)
            End If
        Next

        ' 3) Fuera de todo rango → STOP final (ya renumerado)
        AddStop = True
        Return LineaSTOPFinal
    End Function

    Private Sub GuardarEquivalencia(origen As Integer, destino As Integer)
        Dim linea As String = $"{Fix(origen / 100)} > {origen}:{destino}"
        GuardarFicheroEQ(linea)
    End Sub

    Private Sub GuardarLinea(nroNuevo As Integer, linea As String)
        Dim stmt As String = linea.Trim()

        ' 1) Si es cierre, desindentar ANTES
        If EsCierre(stmt) Then
            indentLevel = Math.Max(0, indentLevel - 1)
        End If

        ' 2) Construir indentación
        Dim indent As String = New String(Constantes.C_ESPACIO, indentLevel * opts.Ren_IND)

        ' 3) Construir línea final
        Dim lineaFinal As String
        If nroNuevo <> 0 Then
            lineaFinal = $"{nroNuevo} {indent}{stmt}"
        Else
            ' Líneas sin número (comentarios, etc.)
            lineaFinal = $"{stmt}"
        End If

        ' 4) Emitir
        GuardarFichero(lineaFinal)

        ' 5) Si es apertura, indentar DESPUÉS
        If EsApertura(stmt) Then
            indentLevel += 1
        End If
    End Sub


    Private Sub GuardarFichero(linea As String)
        stWriter.WriteLine(linea)
        If opts.Verbose Then
            MostrarVerbose(opts, linea)
        End If
    End Sub

    Private Sub GuardarFicheroEQ(linea As String)
        If opts.ModoDebug Then
            linea = "[EQ] " & linea
            stWriter.WriteLine(linea)
            If opts.Verbose Then
                MostrarVerbose(opts, linea)
            End If
        End If
    End Sub

    ' --- INDENTACION ----------------------------------------------
    Private Function EsApertura(stmt As String) As Boolean
        Dim s = stmt.Trim().ToUpperInvariant()
        Return (s.StartsWith("IF ") AndAlso s.EndsWith(" THEN")) _
            OrElse (s.StartsWith("FOR ") AndAlso Not s.Contains(":")) _
            OrElse (s.StartsWith("DEFINE "))
    End Function

    Private Function EsCierre(stmt As String) As Boolean
        Dim s = stmt.Trim().ToUpperInvariant()
        Return s.StartsWith("END IF") OrElse
               s.StartsWith("END FOR") OrElse
               s.StartsWith("END DEFINE")
    End Function


End Module