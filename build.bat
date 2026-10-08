@echo off
rem ============================================================
rem  Build VniTyping.exe bằng csc.exe có sẵn trong Windows
rem  (.NET Framework 4.x) - không cần cài Visual Studio.
rem  Kết quả: dist\VniTyping.exe
rem ============================================================
setlocal
chcp 65001 >nul
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Khong tim thay csc.exe cua .NET Framework 4.x
  exit /b 1
)
if not exist dist mkdir dist

set "REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll"

echo [1/3] Build bo test engine...
"%CSC%" /nologo /codepage:65001 /target:exe /optimize+ /out:dist\VniTyping.Tests.exe %REFS% src\Engine\*.cs tests\TestRunner.cs
if errorlevel 1 exit /b 1

echo [2/3] Chay bo test chung (tests\cases.json)...
dist\VniTyping.Tests.exe tests\cases.json
if errorlevel 1 exit /b 1

echo [3/3] Build VniTyping.exe...
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:dist\VniTyping.exe /win32icon:assets\VniTyping.ico /win32manifest:assets\app.manifest %REFS% src\*.cs src\Engine\*.cs src\UI\*.cs
if errorlevel 1 exit /b 1

echo.
echo Xong: dist\VniTyping.exe
endlocal
