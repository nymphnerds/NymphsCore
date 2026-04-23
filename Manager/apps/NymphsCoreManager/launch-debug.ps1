param(
    [switch] $BuildFirst
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $scriptRoot "bin\Debug\net8.0-windows10.0.19041.0\NymphsCoreManager.exe"

if ($BuildFirst.IsPresent -or -not (Test-Path $exePath)) {
    Write-Host "Building NymphsCore Manager..."
    & dotnet build $scriptRoot
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}

if (-not (Test-Path $exePath)) {
    throw "Debug executable not found: $exePath"
}

Write-Host "Launching detached debug app..."
Start-Process -FilePath $exePath -WorkingDirectory $scriptRoot
