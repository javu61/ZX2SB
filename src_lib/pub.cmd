@echo off
setlocal

REM =====================================================
REM CONFIGURACIÓN
REM =====================================================

set OUTDIR=C:\Proyectos\ZX2SB\EXE
set CONFIG=Release
set RUNTIME=win-x64

REM Crear directorio de salida si no existe
if not exist "%OUTDIR%" (
    mkdir "%OUTDIR%"
)

echo.
echo ============================================
echo Publicando modulos ZX2SB en %OUTDIR%
echo ============================================
echo.

REM =====================================================
REM FUNCION PARA PUBLICAR UN MODULO
REM =====================================================

call :Publish ZX2SB.Lexer
call :Publish ZX2SB.Parser
call :Publish ZX2SB.Semantic
call :Publish ZX2SB.Generator
call :Publish ZX2SB.Renumerador

echo.
echo ============================================
echo Publicacion completada
echo ============================================
echo.

pause
exit /b 0

REM =====================================================
REM SUBRUTINA
REM =====================================================
:Publish
set MODULO=%1

echo.
echo ---- Publicando %MODULO% ----

dotnet publish "%MODULO%\%MODULO%.vbproj" ^
  -c %CONFIG% ^
  -r %RUNTIME% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  -o "%OUTDIR%\%MODULO%"

if errorlevel 1 (
    echo ERROR publicando %MODULO%
    exit /b 1
)

echo OK: %MODULO%
exit /b 0