@echo off
rem Builds SandboxBridge.exe with the C# compiler that comes with Windows (.NET Framework 4).
rem No Visual Studio needed.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find the .NET Framework 4 C# compiler.
  pause
  exit /b 1
)
cd /d "%~dp0"
"%CSC%" /nologo /optimize+ /target:exe /out:SandboxBridge.exe SandboxBridge.cs
if errorlevel 1 (echo Build failed. & pause & exit /b 1)
echo Built SandboxBridge.exe
pause
