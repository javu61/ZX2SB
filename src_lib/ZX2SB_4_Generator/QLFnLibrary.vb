' ==============================================================
'  ZX2SB - QL Auxiliary Functions Generator
'  Genera implementaciones SuperBasic para funciones auxiliares
'     MEJORABLES: VAL, BIN, CODE
' ==============================================================

Imports System.ComponentModel
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml
Imports ZX2SB.Generator.QLFnLibrary

Public Module QLFnLibrary

    Public Structure stData
        Public Numero As Integer
        Public Cadena As Boolean
        Public Valor As String
    End Structure


    ' ------------------------------------------------------------
    ' Genera el bloque completo DEFine PROC ...
    ' ------------------------------------------------------------
    Public Function GenerateFnProcedure(startLine As Integer, tk As Token, ManejoFunciones As Integer) As List(Of String)

        Dim lines As New List(Of String)

        ' ¿Devuelve valor?
        Dim EsFuncion As Boolean = tk.IsFunction()

        ' Lista de parámetros
        Dim paramList As String = ""

        Select Case tk.getAridad
            Case TokenAridad.TA_NR1 : paramList = "(a)"                     ' Funciones con UN parámetro NUMÉRICO
            Case TokenAridad.TA_ST1 : paramList = "(a$)"                    ' Funciones con UN parámetro CADENA
            Case TokenAridad.TA_NR2 : paramList = "(a,b)"                   ' Funciones con DOS parámetros NUMÉRICOS
            Case Else : paramList = ""                                      ' Sin parámetros en el resto de casos
        End Select

        ' Cabecera
        If tk.ID = TokenID.TCO_INIT Then
            lines.Add($"{startLine} DEFine PROC " & Constantes.GQL_INIT)
        Else
            If EsFuncion Then
                lines.Add($"{startLine} DEFine Function {tk.Mnemonic}{paramList}")
            Else
                lines.Add($"{startLine} DEFine PROC {tk.Mnemonic}{paramList}")
            End If
        End If

        startLine += 10

        ' Cuerpo
        For Each bodyLine In GenerateFnBody(tk, EsFuncion, ManejoFunciones)
            lines.Add($"{startLine} {bodyLine}")
            startLine += 10
        Next

        ' Cierre
        If tk.ID = TokenID.TCO_INIT Then
            lines.Add($"{startLine} End DEFine " & Constantes.GQL_INIT)
        Else
            lines.Add($"{startLine} End DEFine {tk.Mnemonic}")
        End If

        Return lines
    End Function


    ' ------------------------------------------------------------
    ' Devuelve el cuerpo de la función auxiliar en SuperBasic QL
    ' ------------------------------------------------------------
    Private Function GenerateFnBody(tk As Token, isFunction As Boolean, ManejoFunciones As Integer) As List(Of String)

        Select Case tk.ID

            ' ====================================================
            ' INIT es la rutina que inicializa el sistema
            ' ====================================================
            Case TokenID.TCO_INIT
                Return Generate_INIT()

            ' ====================================================
            ' B — Funciones ZX con comportamiento distinto en QL
            ' ====================================================

            Case TokenID.TK_STR_S   ' -> ZX BASIC antepone un espacio a positivos, QL no.
                Return Generate_Str()
            Case TokenID.TK_VAL  'Retorna el valor numérico de una cadena -> QL lo hace al asignar una cadena a una numérica
                Return Generate_Val()

            Case TokenID.TK_BIN  'BIN: Convierte un número binario en decimal. -> No existe en el QL
                Return Generate_BIN()

                ' ====================================================
                ' Atributos de pantalla
                ' ====================================================
            Case TokenID.TK_INK
                Return New List(Of String) From {
                        "  REM ZX attribute INK aproximado",
                        "  INK a"
                    }
            Case TokenID.TK_PAPER
                Return New List(Of String) From {
                    "  REM ZX attribute PAPER aproximado",
                    "  PAPER a"
                    }
            Case TokenID.TK_BRIGHT
                Return New List(Of String) From {
                    "  REM ZX attribute BRIGHT"
                    }
            Case TokenID.TK_FLASH
                Return New List(Of String) From {
                    "  REM ZX attribute FLASH"
                    }
            Case TokenID.TK_OVER
                Return New List(Of String) From {
                    "  REM ZX attribute OVER"
                    }
            Case TokenID.TK_INVERSE
                Return New List(Of String) From {
                    "  REM ZX attribute INVERSE"
                    }
            Case TokenID.TK_CLEAR_RAM
                Return New List(Of String) From {
                    "  CLEAR"
                    }

                ' ====================================================
                ' FUNCIONES NO IMPLEMENTADAS (stub con ERROR)
                ' ====================================================
            Case Else
                Dim aux As New List(Of String)
                Dim msg As String = Constantes.C_COMILLAS & "Función ZX_BASIC " & tk.Mnemonic & " no soportada en el QL" & Constantes.C_COMILLAS
                Select Case ManejoFunciones
                    Case Constantes.opFuncion_Err
                        aux.Add($"  PRINT " & msg)
                        aux.Add($"  Stop")
                    Case Constantes.opFuncion_Msg
                        aux.Add($"  PRINT " & msg)
                    Case Constantes.opFuncion_Ign
                        aux.Add($"  REM " & msg)
                End Select
                Return aux
        End Select

    End Function

    Private Function Generate_Str() As List(Of String)

        Dim Lineas As New List(Of String)

        Lineas.Add("  IF (a < 0) THEN")
        Lineas.Add("    RETurn STR$(a)")
        Lineas.Add("  ELSE")
        Lineas.Add("    RETurn " & Constantes.C_COMILLAS & " " & Constantes.C_COMILLAS & " STR$(a)")
        Lineas.Add("  END IF")

        Return Lineas
    End Function
    Private Function Generate_Val() As List(Of String)

        Dim Lineas As New List(Of String)

        Lineas.Add("  LOCal i, slen, sign, n, dec, div, d")
        Lineas.Add("  LOCal digitfound")
        Lineas.Add("  :")

        Lineas.Add("  slen = LEN(a$)")
        Lineas.Add("  i = 1")
        Lineas.Add("  sign = 1")
        Lineas.Add("  n = 0")
        Lineas.Add("  dec = 0")
        Lineas.Add("  div = 1")
        Lineas.Add("  digitfound = 0")
        Lineas.Add("  :")

        Lineas.Add("  REM 1) Saltar espacios iniciales")
        Lineas.Add("  REPeat skip_spaces")
        Lineas.Add("    IF i > slen THEN EXIT skip_spaces")
        Lineas.Add("    IF a$(i) <> " & Constantes.S_ESPACIO & " THEN EXIT skip_spaces")
        Lineas.Add("    i = i + 1")
        Lineas.Add("  END REPeat skip_spaces")
        Lineas.Add("  :")

        Lineas.Add("  REM 2) Signo opcional")
        Lineas.Add("  IF i <= slen THEN")
        Lineas.Add("    IF a$(i) = " & Constantes.S_MENOS & " THEN")
        Lineas.Add("      sign = -1")
        Lineas.Add("      i = i + 1")
        Lineas.Add("    ELSEIF a$(i) = " & Constantes.S_MAS & " THEN")
        Lineas.Add("      i = i + 1")
        Lineas.Add("    END IF")
        Lineas.Add("  END IF")
        Lineas.Add("  :")

        Lineas.Add("  REM 3) Parte entera")
        Lineas.Add("  REPeat int_part")
        Lineas.Add("    IF i > slen THEN EXIT int_part")
        Lineas.Add("    d = CODE(a$(i)) - 48")
        Lineas.Add("    IF d < 0 OR d > 9 THEN EXIT int_part")
        Lineas.Add("    n = n * 10 + d")
        Lineas.Add("    digitfound = 1")
        Lineas.Add("    i = i + 1")
        Lineas.Add("  END REPeat int_part")
        Lineas.Add("  :")

        Lineas.Add("  REM 4) Parte decimal")
        Lineas.Add("  IF i <= slen THEN")
        Lineas.Add("    IF a$(i) = " & Constantes.C_COMILLAS & "." & Constantes.C_COMILLAS & " THEN")
        Lineas.Add("      i = i + 1")
        Lineas.Add("      REPeat dec_part")
        Lineas.Add("        IF i > slen THEN EXIT dec_part")
        Lineas.Add("        d = CODE(a$(i)) - 48")
        Lineas.Add("        IF d < 0 Or d > 9 THEN EXIT dec_part")
        Lineas.Add("        dec = dec * 10 + d")
        Lineas.Add("        div = div * 10")
        Lineas.Add("        digitfound = 1")
        Lineas.Add("        i = i + 1")
        Lineas.Add("      END REPeat dec_part")
        Lineas.Add("    END IF")
        Lineas.Add("  END IF")
        Lineas.Add("  :")

        Lineas.Add("  REM 5) Si no hay dígitos válidos → 0")
        Lineas.Add("  IF digitfound = 0 THEN")
        Lineas.Add("    RETurn 0")
        Lineas.Add("  END IF")
        Lineas.Add("  :")

        Lineas.Add("  REM 6) Resultado final")
        Lineas.Add("  RETurn sign * (n + dec / div)")
        Lineas.Add("  :")

        Return Lineas
    End Function
    Private Function Generate_BIN_Cadena() As List(Of String)
        ' rem Esta función no se usa en ZX, pero la pongo por completar, nunca se va a generar realmente
        Dim Lineas As New List(Of String)

        Lineas.Add(" REMark ======== FUNCIONES AUXILIARES ==========")
        Lineas.Add(" DEFine FuNction ZX2SB_BIN$(n)")
        Lineas.Add("  LOCal v, bit, pow10, res$")
        Lineas.Add("  v = ABS(INT(n))")
        Lineas.Add("  IF v = 0 THEN ")
        Lineas.Add("    RETurn " & Constantes.S_CERO)
        Lineas.Add("  END IF ")
        Lineas.Add("  res$ = " & Constantes.S_VACIA)
        Lineas.Add("  pow10 = 1")
        Lineas.Add("  REPeat loop")
        Lineas.Add("    IF v = 0 THEN EXIT loop")
        Lineas.Add("    q   = v DIV 2")
        Lineas.Add("    bit = v - q*2          : REMark resto seguro (0 o 1)")
        Lineas.Add("    res$ = CHR$(48+bit) & res$")
        Lineas.Add("    v = INT(v/2)")
        Lineas.Add("  END REPeat loop")
        Lineas.Add("  RETurn res$")
        Lineas.Add(" END DEFine ")

        Return Lineas
    End Function

    Private Function Generate_BIN() As List(Of String)
        Dim Lineas As New List(Of String)

        Lineas.Add("   LOCal nro, dec, bit, pot")
        Lineas.Add("   :")
        Lineas.Add("   nro = a")
        Lineas.Add("   dec = 0")
        Lineas.Add("   pot = 0")
        Lineas.Add("   REPeat loop")
        Lineas.Add("     IF nro = 0 THEN EXIT loop")
        Lineas.Add("     REMark no podemos usar bit = n DIV 10 por desbordamiento")
        Lineas.Add("     REMark QL guarda nros en notaci–n exponencial, cuidado al dividir")
        Lineas.Add("     bit = INT(((nro/10) - INT(nro/10))*10 + .1)")
        Lineas.Add("     IF (bit <> 0) AND (bit <> 1) THEN ")
        Lineas.Add("       PRINT " & Constantes.C_COMILLAS & "Error en BIN, el nro " &
                           Constantes.C_COMILLAS & ";a;" & Constantes.C_COMILLAS & " no es binario " & Constantes.C_COMILLAS)
        Lineas.Add("       STOP")
        Lineas.Add("     END IF ")
        Lineas.Add("     nro = INT(nro/10)")
        Lineas.Add("     dec = dec + ((2^pot)*bit)")
        Lineas.Add("     pot = pot + 1")
        Lineas.Add("   END REPeat loop")
        Lineas.Add("   :")
        Lineas.Add("   RETurn dec")

        Return Lineas
    End Function


    ' -----------------------------------------------------------------------
    ' Inicialización del sistema
    ' -----------------------------------------------------------------------
    Public Function Generate_ProgramInit() As List(Of String)
        Dim Lin As Integer = 0
        Dim lon = 60
        Dim Lineas As New List(Of String)

        MontarRem(Lineas, Lin, lon, StrDup(lon, "*"))
        MontarRem(Lineas, Lin, lon, $" GENERADO POR ZX2SB {Constantes.VER_PROG} by javu61 ({Now.ToString})")
        MontarRem(Lineas, Lin, lon, StrDup(lon, "*"))
        MontarRem(Lineas, Lin, lon, " Si el resultado no es correcto, por favor contacta")
        MontarRem(Lineas, Lin, lon, " javu61@hotmail.com, intentaré solucionarlo")
        MontarRem(Lineas, Lin, lon, StrDup(lon, "*"))
        AddLinea(Lineas, Lin, 1, Constantes.GQL_INIT)

        Return Lineas
    End Function

    Private Sub MontarRem(ByRef Lineas As List(Of String), ByRef Lin As Integer, lon As Integer, texto As String)
        AddLinea(Lineas, Lin, 1, $"REM *" & texto & Space(lon - Len(texto)) & "*")
    End Sub


    Private Function Generate_INIT() As List(Of String)
        Dim Lineas As New List(Of String)
        Lineas.Add("MODE 8:WINDOW 512,256,0,0:CLS")
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & "######  #     #    #####    #####  ###### " & Constantes.C_COMILLAS)
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & "     #   #   #         #   #       #     #" & Constantes.C_COMILLAS)
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & "    #     # #          #   #       #     #" & Constantes.C_COMILLAS)
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & "   #       #       #####    #####  ###### " & Constantes.C_COMILLAS)
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & "  #       # #      #             # #     #" & Constantes.C_COMILLAS)
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & " #       #   #     #             # #     #" & Constantes.C_COMILLAS)
        Lineas.Add("PRINT " & Constantes.C_COMILLAS & "######  #     #    #####    #####  ###### " & Constantes.C_COMILLAS)
        Lineas.Add("PRINT:PRINT " & Constantes.C_COMILLAS & "ZX2SB v" & Constantes.VER_PROG & Constantes.C_COMILLAS & ":PRINT " & Constantes.C_COMILLAS & "by javu61@hotmail.com" & Constantes.C_COMILLAS)
        Lineas.Add("PAUSE 75:WINDOW 405,250,52,3:BORDER 5,0,1:PAPER 0:CLS")
        Return Lineas
    End Function

    Private Sub AddLinea(Lineas As List(Of String), ByRef nroLinea As Integer, paso As Integer, texto As String)
        nroLinea += paso
        Lineas.Add($"{nroLinea} {texto}")
    End Sub

End Module


