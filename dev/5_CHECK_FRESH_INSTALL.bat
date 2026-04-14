@echo off
setlocal
pushd "%~dp0" >nul

echo Fresh NymphsCore install check
echo.
echo This will verify:
echo - the NymphsCore WSL distro exists
echo - the default user is nymph
echo - runtime folders exist
echo - core backend Python envs exist
echo - CUDA 13.0 path exists
echo - whether models were prefetched or skipped
echo.
echo Press any key to continue . . .
pause >nul

powershell -NoProfile -ExecutionPolicy Bypass -File "..\scripts\check_fresh_install.ps1" %*
set EXITCODE=%ERRORLEVEL%

if not "%EXITCODE%"=="0" (
  echo.
  echo Fresh install check failed with exit code %EXITCODE%.
  pause
  exit /b %EXITCODE%
)

echo.
echo Fresh install check completed.
pause
popd >nul
