@echo off
rem Builds and runs SerialScope's automated tests (bin\tests\SerialScope.Tests.exe).
rem Exit code is 0 when every test passes.

setlocal enabledelayedexpansion
cd /d "%~dp0.."

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist bin\tests mkdir bin\tests

rem Every source file except the app's entry point, plus the tests
set SOURCES=
for %%f in (src\*.cs) do if /i not "%%~nxf"=="Program.cs" set SOURCES=!SOURCES! "%%f"

"%CSC%" /nologo /target:exe /codepage:65001 /nowarn:649 /out:bin\tests\SerialScope.Tests.exe ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll ^
  %SOURCES% tests\*.cs
if errorlevel 1 (
  echo Test build failed.
  exit /b 1
)

bin\tests\SerialScope.Tests.exe
exit /b %errorlevel%
