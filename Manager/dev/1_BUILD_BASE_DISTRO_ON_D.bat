@echo off
setlocal
pushd "%~dp0"

echo NymphsCore base distro build
echo.
echo This creates a fresh small Ubuntu builder distro on D:
echo and bootstraps only the lean NymphsCore runtime files.
echo.
echo It does NOT export your huge working Ubuntu distro.
echo.
echo Target builder distro: Ubuntu-24.04
echo Target builder location: D:\WSL\NymphsCore-Builder
echo.
echo Run this from an Administrator PowerShell or Administrator Command Prompt.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "..\scripts\create_builder_distro.ps1" -BuilderInstallLocation "D:\WSL\NymphsCore-Builder" -Force
set "exit_code=%ERRORLEVEL%"

echo.
if not "%exit_code%"=="0" (
    echo Base distro build step failed with exit code %exit_code%.
    pause
    popd
    exit /b %exit_code%
)

echo Base builder distro is ready.
echo Next step: run 2_EXPORT_BASE_DISTRO_TO_D.bat
pause
popd
exit /b 0
