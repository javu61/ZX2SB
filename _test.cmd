@echo off
setlocal

set ZX2SB=..\ZX2SB.exe
set TESTDIR=C:\proyectos\zx2sb\tests
set Prj=C:\Proyectos\ZX2SB\EXE\zx2sb.exe
set exp=expected_sb
set res=output_sb
set Inp=C:\Proyectos\ZX2SB\Tests\text01\input.bas
set out=C:\Proyectos\ZX2SB\Tests\text01\output_sb
set FAIL=0

cls
echo ====================================
echo EJECUTANDO TESTS ZX2SB
echo ====================================
echo %TESTDIR%
echo.

for /d %%T in (%TESTDIR%\*) do (
    echo ---- Test %%~nT ----
    echo ejecutando: %Prj% %Inp% %out% -d -b
    %Prj% %Inp% %out% -d -b
    echo comparando: fc "%%T\%res%" "%%T\%exp%" 
    fc "%%T\%res%" "%%T\%exp%" >nul
    if errorlevel 1 (
        echo FAIL: %%~nT
        set FAIL=1
        fc "%%T\%res%" "%%T\%exp%"
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
pause
exit /b %FAIL%
