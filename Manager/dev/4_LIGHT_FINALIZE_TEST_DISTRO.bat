@echo off
setlocal
pushd "%~dp0"

echo NymphsCore light finalize test
echo.
echo This runs the light finalize step inside:
echo NymphsCore-Test
echo.
echo It will:
echo - install system dependencies
echo - skip CUDA
echo - skip backend Python environment creation
echo - skip model downloads
echo - skip verification
echo.
echo This avoids typing commands inside the Linux root shell.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -File "..\scripts\run_finalize_in_distro.ps1" -DistroName "NymphsCore-Test" -LinuxUser "nymph" -SystemOnly
set "exit_code=%ERRORLEVEL%"

echo.
if not "%exit_code%"=="0" (
    echo Light finalize test failed with exit code %exit_code%.
    pause
    popd
    exit /b %exit_code%
)

echo Light finalize test completed.
pause
popd
exit /b 0
