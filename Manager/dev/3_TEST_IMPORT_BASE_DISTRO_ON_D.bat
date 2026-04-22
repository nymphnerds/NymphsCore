@echo off
setlocal
pushd "%~dp0"

echo NymphsCore base distro test import
echo.
echo This imports the exported base distro tar as a separate test distro.
echo.
echo Test distro name: NymphsCore_Lite-Test
echo Test install location: D:\WSL\NymphsCore_Lite-Test
echo Source tar: D:\WSL\NymphsCore.tar
echo.
echo This should not touch your normal Ubuntu distro.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "..\scripts\import_base_distro.ps1" -TarPath "D:\WSL\NymphsCore.tar" -DistroName "NymphsCore_Lite-Test" -InstallLocation "D:\WSL\NymphsCore_Lite-Test" -LinuxUser "nymph" -Force
set "exit_code=%ERRORLEVEL%"

echo.
if not "%exit_code%"=="0" (
    echo Base distro test import failed with exit code %exit_code%.
    pause
    popd
    exit /b %exit_code%
)

echo Test distro import complete.
echo.
echo You can confirm it in PowerShell with:
echo wsl -l -v
pause
popd
exit /b 0
