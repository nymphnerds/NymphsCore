@echo off
setlocal
pushd "%~dp0"

echo NymphsCore base distro export
echo.
echo This exports the prepared builder distro to:
echo D:\WSL\NymphsCore.tar
echo.
echo Temporary builder distro name: Ubuntu-24.04
echo.
echo The temporary builder distro will be removed after export.
echo.
echo Run this after 1_BUILD_BASE_DISTRO_ON_D.bat succeeds.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "..\scripts\export_builder_distro.ps1" -BuilderDistroName "Ubuntu-24.04" -OutputTarPath "D:\WSL\NymphsCore.tar" -Force -UnregisterBuilderAfterExport
set "exit_code=%ERRORLEVEL%"

echo.
if not "%exit_code%"=="0" (
    echo Base distro export failed with exit code %exit_code%.
    pause
    popd
    exit /b %exit_code%
)

echo Base distro tar created:
echo D:\WSL\NymphsCore.tar
echo.
echo Temporary builder distro removed.
echo.
echo Next step: run 3_TEST_IMPORT_BASE_DISTRO_ON_D.bat
pause
popd
exit /b 0
