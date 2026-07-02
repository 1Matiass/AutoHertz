@echo off
REM Recompila o AutoHertz.exe usando o compilador que ja vem no Windows.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /out:"%~dp0..\AutoHertz.exe" ^
  /win32icon:"%~dp0AutoHertz.ico" ^
  /win32manifest:"%~dp0app.manifest" ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  "%~dp0AutoHertz.cs"

if %ERRORLEVEL%==0 (echo. & echo Compilado com sucesso: ..\AutoHertz.exe) else (echo. & echo ERRO na compilacao.)
pause
