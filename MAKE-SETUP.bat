@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Installer.ps1"
if errorlevel 1 (
  echo.
  echo Setup creation failed. Review the error above.
  pause
  exit /b 1
)

echo.
echo Harbor Browser setup wizard created in the dist folder.
pause
