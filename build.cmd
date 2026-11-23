@echo off
rem Builds bin\7zTM.exe with the C# compiler that ships with .NET Framework 3.5.
rem The result runs on .NET 2.0 through 4.x, so Windows XP SP2 or later.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
if not exist "%CSC%" (
  echo .NET Framework 3.5 compiler not found at %CSC%
  echo On Windows 10/11 turn on ".NET Framework 3.5" in Windows Features.
  exit /b 1
)
cd /d "%~dp0"
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:bin\7zTM.exe /win32manifest:src\app.manifest ^
  /win32icon:src\app.ico /resource:src\app.ico,SevenZipThemeManager.app.ico ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  src\*.cs || exit /b 1
copy /y src\app.config bin\7zTM.exe.config >nul
echo Built bin\7zTM.exe
