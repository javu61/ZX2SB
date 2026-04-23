ZX2SB

ZX2SB es un TRANSPILADOR enre dos lenguajes Basic, un conversor experimental de ZXBasic (Spectrum) a SuperBASIC (Sinclair QL), diseñado con un enfoque de compilador real: fases separadas, reglas semánticas explícitas y generación de código editable y extensible.
El objetivo principal del proyecto es traducir programas ZX BASIC manteniendo su semántica, pero produciendo como salida SuperBASIC limpio, legible y adecuado para su posterior evolución.

NOTA: Un TRANSPILADOR es un compilador que genera en su salida código en un lenguaje de alto nivel, en lugar de generar un ejecutable o un programa en ensamblador.

*** Objetivos del proyecto

Convertir código ZX BASIC (Spectrum) a SuperBASIC (QL).
Mantener la semántica original del programa siempre que sea posible.
Generar código editable, no solo ejecutable.
Detectar errores y situaciones dudosas mediante warnings semánticos.

*** Diseño y arquitectura
ZX2SB está organizado como un pipeline clásico de compilador:
- Analizador Léxico (Lexer)....: Genera los tokens a partir del texto
- Analizador Semántico (Parser): General el AST a partir de los tokens
- Analizador Sintáctico........: Revisa que el programa sea correcto y ajusta el AST
- Generador de código..........: Genera el código final.

Se ha diseñado para respetar al máximo la sintaxis del ZXBasic, sin añadidos no cosméticos:
- Normalización léxica con eliminación de espacios innecesarios fuera de cadenas.
- Posibilidad de no migrar los comentarios
- Posibilidad de añadir como comentarios las sentencias del programa original pero sin Nro de línea.
- Separación de los IF encadenados en varias líneas
- Generación de funciones auxiliares para sentencias que no existen en el QL
- Generación de funciones auxiliares para sentencias no soportadas en el QL, a elección:
  - Error en la ejecución
  - Mensaje en la ejecución
  - Son ignoradas
- Renumeración del código.

*** Decisión de diseño importante
- Solo se admiten sentencias válidas en el ZXBasic. 
- Las asignaciones son con LET, pero se puede omitir en el generador
- No se permite usar palabras reservadas como nombres de variables.
- No se permiten arrays ni strings con nombres de más de una letra.


** Estado del proyecto
- Lexer: Completado
- Parser: Completado
- Semántico: Completado
- Generador: Modificando temas
- Renumerador: Completado,a falta de revisar tras los cambios en el generador si fuera necesrio.

El proyecto se encuentra en una fase funcional y estable, listo para evolucionar hacia el backend de generación QL.


*** Notas finales
ZX2SB no es un conversor rápido basado en sustituciones de texto. Es un transpilador.
Es un proyecto deliberadamente estructurado como un compilador, pensado para ser mantenible, extensible y correcto.
Cualquier mejora futura (optimizaciones, nuevos backends, modos estrictos, etc.) se apoya en esta base sólida.


Autor: Jose Antonio Vaque Urbaneja
Repositorio: https://github.com/javu61/ZX2SB
