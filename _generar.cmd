del C:\Proyectos\ZX2SB\EXE\ZX2SB.exe
cd C:\Proyectos\ZX2SB\src_director\ZX2SB.Director
dotnet publish -c Release -r win-x64 -o C:\Proyectos\ZX2SB\EXE
del C:\Proyectos\ZX2SB\EXE\*.pdb
copy C:\Proyectos\ZX2SB\manual.txt C:\Proyectos\ZX2SB\EXE
pause "."