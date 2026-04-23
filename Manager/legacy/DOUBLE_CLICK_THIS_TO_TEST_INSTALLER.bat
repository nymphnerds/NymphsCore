@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%Install_NymphsCore.ps1"

if not exist "%PS_SCRIPT%" (
  echo Could not find:
  echo %PS_SCRIPT%
  echo.
  pause
  exit /b 1
)

start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -RepoBranch installer_test_drive_choice -SkipWslConfig
exit /b 0
