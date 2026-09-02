@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo CSC_NOT_FOUND
    exit /b 1
)

echo Compiling GameAudioSwitcher.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ ^
  /out:GameAudioSwitcher.exe ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Core.dll ^
  Program.cs AudioCore.cs Config.cs GameMonitor.cs HotkeyManager.cs HotkeyCaptureForm.cs
if errorlevel 1 (
    echo BUILD_FAIL_MAIN
    exit /b 1
)

echo Compiling AuditAudio.exe ...
"%CSC%" /nologo /target:exe /platform:anycpu /codepage:65001 /optimize+ ^
  /out:AuditAudio.exe ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  AudioCore.cs AuditAudio.cs
if errorlevel 1 (
    echo BUILD_FAIL_AUDIT
    exit /b 1
)

echo Compiling SetTest.exe ...
"%CSC%" /nologo /target:exe /platform:anycpu /codepage:65001 /optimize+ ^
  /out:SetTest.exe ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  AudioCore.cs SetTest.cs
if errorlevel 1 (
    echo BUILD_FAIL_SETTEST
    exit /b 1
)

echo BUILD_OK
exit /b 0
