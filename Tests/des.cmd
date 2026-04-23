@echo off
setlocal

set ZX2SB=..\ZX2SB.exe
set TESTDIR=C:\proyectos\zx2sb\tests
set FAIL=0

cls
echo ====================================
echo EJECUTANDO TESTS ZX2SB
echo ====================================
echo %TESTDIR%
echo.

for /d %%T in (%TESTDIR%\*) do (
    echo ---- Test %%~nT ----
    echo ejecutando: C:\Proyectos\ZX2SB\bin\Debug\net8.0\zx2sb.exe C:\Proyectos\ZX2SB\Tests\text01\input.bas C:\Proyectos\ZX2SB\Tests\text01\output.sb -d -b
    C:\Proyectos\ZX2SB\bin\Debug\net8.0\zx2sb.exe C:\Proyectos\ZX2SB\Tests\text01\input.bas C:\Proyectos\ZX2SB\Tests\text01\output.sb -d -b
    echo comparando: fc "%%T\output.sb" "%%T\expected.sb" 
    fc "%%T\output.sb" "%%T\expected.sb" >nul
    if errorlevel 1 (
        echo FAIL: %%~nT
        set FAIL=1
        fc "%%T\output.sb" "%%T\expected.sb"
    ) else (
        echo OK
    )

    echo.
)

echo ====================================
if %FAIL%==0 (
    echo RESULTADO: TODOS LOS TESTS PASARON
) else (
    echo RESULTADO: ALGUNOS TESTS FALLARON
)
echo ====================================
exit /b %FAIL%