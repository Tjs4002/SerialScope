@echo off
rem Builds bin\SerialScope.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio or SDK needed.

setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.x is required.
  exit /b 1
)

if not exist bin mkdir bin

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:bin\SerialScope.exe ^
  /win32icon:src\app.ico ^
  /win32manifest:src\app.manifest ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll ^
  src\*.cs

if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)

echo Built bin\SerialScope.exe
