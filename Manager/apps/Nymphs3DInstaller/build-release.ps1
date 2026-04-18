param(
    [string] $Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$publishBase = Join-Path $scriptRoot "publish"
$publishRoot = Join-Path $scriptRoot ("publish\" + $Runtime)

Write-Host "Publishing NymphsCore Manager for $Runtime..."

# Build the project
dotnet publish . -c Release -r $Runtime -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$exePath = Join-Path $publishRoot "NymphsCoreManager.exe"
if (-not (Test-Path $exePath)) {
    throw "Release executable not found: $exePath"
}

# Copy scripts from repo
$scriptsSource = Join-Path $repoRoot "scripts"
$scriptsDestination = Join-Path $publishRoot "scripts"
if (Test-Path $scriptsSource) {
    Copy-Item -Path $scriptsSource -Destination $scriptsDestination -Recurse -Force
    
    # Clean up Python cache
    Get-ChildItem -Path $scriptsDestination -Recurse -Directory -Filter "__pycache__" |
        Remove-Item -Recurse -Force
    Get-ChildItem -Path $scriptsDestination -Recurse -File -Include "*.pyc", "*.pyo" |
        Remove-Item -Force
        
    # Remove legacy wrappers if exists
    $legacyPartsWrappers = Join-Path $scriptsDestination "hunyuan_parts_wrappers"
    if (Test-Path $legacyPartsWrappers) {
        Remove-Item -Path $legacyPartsWrappers -Recurse -Force
    }
}

# Create zip
$zipPath = Join-Path $publishBase ("NymphsCoreManager-" + $Runtime + ".zip")
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

$tempZipPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName() + ".zip")

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

# Validate and move zip
$zipHeader = [System.IO.File]::ReadAllBytes($tempZipPath)
if ($zipHeader.Length -lt 4 -or $zipHeader[0] -ne 0x50 -or $zipHeader[1] -ne 0x4B) {
    throw "Release archive validation failed: output is not a ZIP file."
}

Move-Item -Path $tempZipPath -Destination $zipPath -Force

Write-Host ""
Write-Host "Release build ready:"
Write-Host "  EXE: $exePath"
Write-Host "  ZIP: $zipPath"
