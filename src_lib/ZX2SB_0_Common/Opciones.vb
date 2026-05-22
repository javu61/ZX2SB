Public Module Opciones

    Enum Procesos
        Ninguno = -1

        Lexer = 1
        Normalizador = 2
        Parser = 3
        Semantico = 4
        Generador = 5
        Renumerador = 6

        Primero = Lexer
        Ultimo = Renumerador
    End Enum

    Enum SubFases
        Base
        Data
        Variables
        ForNext
    End Enum

    Public Structure CmdOptions

        ' --- Proceso en el que estamos ---
        Public Modulo As String
        Public Fase As SubFases

        ' --- Ficheros de entrada y salida ---
        Public FEntrada As String
        Public FSalidaLex As String
        Public FSalidaNor As String
        Public FSalidaPar As String
        Public FSalidaSem As String
        Public FSalidaDat As String
        Public FSalidaVar As String
        Public FSalidaFor As String
        Public FSalidaGSB As String
        Public FSalidaRen As String
        Public FSalida As String
        Public FLines As String
        Public FLog As String

        ' --- Parámetros recibidos ---
        Public Opciones As String

        ' --- Parámetros que se deben propagar si se lanza desde el Semantico
        Public Pasada As Integer

        ' --- Salida / modo de ejecución ---
        Public Silencioso As Boolean       ' -s
        Public Verbose As Boolean          ' -v
        Public Batch As Boolean            ' -b
        Public ZX As Boolean               ' -zx

        ' --- Control de errores y avisos ---
        Public NoPararPorError As Boolean  ' -ne
        Public SinWarnings As Boolean      ' -nw

        ' --- Comentarios / debug ---
        Public SinComentarios As Boolean   ' -nc
        Public ModoDebug As Boolean        ' -d

        ' --- Proceso a ejecutar --
        Public Ej_Proceso As Procesos      ' -L/N/P/S/G/R Nombre Ejecuta SOLO ese proceso
        Public Ej_Hasta As Boolean         ' +L/N/P/S/G/R Nombre Ejecuta HASTA ese proceso

        ' --- Funciones no soportadas ---
        Public Funciones As Integer        ' 0=Dar error, 1=Mostrar en pantalla y seguir, 2=Ignorar

        ' --- Renumeración e INDENTACION ---
        Public Ren_Base As Integer          ' Primer número a usar
        Public Ren_Paso As Integer          ' Paso entre números
        Public Ren_IND As Integer           ' Columnas de indentación

        ' --- Lanzado desde el Director  ---
        Public DesdeDirector As Boolean     ' Si el proceso se lanzó desde el director

    End Structure

    Public Function NombreProceso(p As Procesos) As String
        Return [Enum].GetName(GetType(Procesos), p)
    End Function

End Module
