param(
    [string] $Runtime = "win-x64",
    [switch] $SkipPayloadTar
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$publishBase = Join-Path $scriptRoot "publish"
$publishRoot = Join-Path $scriptRoot ("publish\" + $Runtime)
$scriptsSource = Join-Path $repoRoot "scripts"
$payloadTarCandidates = @(
    (Join-Path $scriptRoot "payload\NymphsCore.tar"),
    (Join-Path $repoRoot "payload\NymphsCore.tar"),
    (Join-Path $scriptRoot "payload\Nymphs3D2.tar"),
    (Join-Path $repoRoot "payload\Nymphs3D2.tar")
)
$zipPath = Join-Path $publishBase ("NymphsCoreManager-" + $Runtime + ".zip")
$tempZipPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName() + ".zip")

Write-Host "Publishing NymphsCore Manager for $Runtime..."

if (Test-Path $publishRoot) {
    Remove-Item -Recurse -Force $publishRoot
}

& dotnet publish $scriptRoot -c Release -r $Runtime -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$exePath = Join-Path $publishRoot "NymphsCoreManager.exe"
if (-not (Test-Path $exePath)) {
    throw "Release executable not found: $exePath"
}

if (-not (Test-Path $scriptsSource)) {
    throw "Required scripts folder not found: $scriptsSource"
}

$scriptsDestination = Join-Path $publishRoot "scripts"
Copy-Item -Path $scriptsSource -Destination $scriptsDestination -Recurse -Force

Get-ChildItem -Path $scriptsDestination -Recurse -Directory -Filter "__pycache__" |
    Remove-Item -Recurse -Force

Get-ChildItem -Path $scriptsDestination -Recurse -File -Include "*.pyc", "*.pyo" |
    Remove-Item -Force

$legacyPartsWrappers = Join-Path $scriptsDestination "hunyuan_parts_wrappers"
if (Test-Path $legacyPartsWrappers) {
    Remove-Item -Path $legacyPartsWrappers -Recurse -Force
}

$payloadTarPath = if ($SkipPayloadTar.IsPresent) {
    $null
} else {
    $payloadTarCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ($SkipPayloadTar.IsPresent) {
    Write-Host "Base tar not bundled because -SkipPayloadTar was requested."
} elseif ($payloadTarPath) {
    Copy-Item -Path $payloadTarPath -Destination (Join-Path $publishRoot "NymphsCore.tar") -Force
    Write-Host "Bundled local base tar into release folder."
} else {
    Write-Host "Base tar not bundled. Users must place NymphsCore.tar next to NymphsCoreManager.exe."
}

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

if (Test-Path $tempZipPath) {
    Remove-Item -Force $tempZipPath
}

$itemsToZip = Get-ChildItem -Path $publishRoot
if ($itemsToZip.Count -gt 0) {
    # Create a temporary folder to hold contents for zipping
    $tempZipContent = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $tempZipContent -Force | Out-Null
    
    # Copy contents (not the folder itself) to temp location
    Copy-Item -Path "$publishRoot\*" -Destination $tempZipContent -Recurse -Force
    
    # Create zip from the temp folder contents
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $tempZipContent,
        $tempZipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )
    
    # Clean up temp folder
    Remove-Item -Path $tempZipContent -Recurse -Force
    
    $zipHeader = [System.IO.File]::ReadAllBytes($tempZipPath)
    if ($zipHeader.Length -lt 4 -or $zipHeader[0] -ne 0x50 -or $zipHeader[1] -ne 0x4B) {
        throw "Release archive validation failed: output is not a ZIP file."
    }

    Move-Item -Path $tempZipPath -Destination $zipPath -Force
}

Write-Host ""
Write-Host "Release build ready:"
Write-Host $exePath
Write-Host "Scripts folder:"
Write-Host $scriptsDestination
Write-Host "Release zip:"
Write-Host $zipPath
