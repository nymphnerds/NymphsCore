param(
    [string] $Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Maintainer note:
# If you are invoking builds from WSL, you can use the Windows SDK directly with
# `dotnet.exe` against the `\\wsl.localhost\...` project path instead of needing
# a Linux .NET install inside WSL.

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$publishBase = Join-Path $scriptRoot "publish"
$publishRoot = Join-Path $scriptRoot ("publish\" + $Runtime)
$projectPath = Join-Path $scriptRoot "NymphsCoreManager.csproj"
$binRoot = Join-Path $scriptRoot "bin\Release"
$objRoot = Join-Path $scriptRoot "obj\Release"

function Convert-WslUncPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalized = $Path.TrimEnd("\")
    if ($normalized -notmatch '^\\\\wsl(?:\.localhost)?\\([^\\]+)\\(.+)$') {
        return $null
    }

    return [pscustomobject]@{
        Distro = $Matches[1]
        LinuxPath = "/" + ($Matches[2] -replace '\\', '/')
    }
}

function Restore-WslExecutableBits {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PublishRootPath
    )

    $wslPath = Convert-WslUncPath -Path $PublishRootPath
    if ($null -eq $wslPath) {
        return
    }

    $escapedPublishRoot = "'" + ($wslPath.LinuxPath -replace "'", "'\''") + "'"
    $chmodCommand = "set -e; chmod +x -- $escapedPublishRoot/NymphsCoreManager.exe; if [ -d $escapedPublishRoot/scripts ]; then chmod +x -- $escapedPublishRoot/scripts/*.sh; fi"
    & wsl.exe -d $wslPath.Distro -- bash -lc $chmodCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore executable bits in WSL publish output."
    }
}

function Remove-BuildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $wslPath = Convert-WslUncPath -Path $Path
    if ($null -ne $wslPath) {
        $escapedPath = "'" + ($wslPath.LinuxPath -replace "'", "'\''") + "'"
        & wsl.exe -d $wslPath.Distro -- bash -lc "rm -rf -- $escapedPath"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to remove WSL build path: $Path"
        }

        return
    }

    Remove-Item -Path $Path -Recurse -Force
}

Write-Host "Publishing NymphsCore Manager for $Runtime..."

# Build the project
if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

# Force a clean rebuild so linked WPF resources like sidebar logos always refresh.
Remove-BuildPath -Path $publishRoot
Remove-BuildPath -Path $binRoot
Remove-BuildPath -Path $objRoot

dotnet publish $projectPath -c Release -r $Runtime -o $publishRoot -p:NoIncremental=true
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
    if (Test-Path $scriptsDestination) {
        Remove-Item -Path $scriptsDestination -Recurse -Force
    }

    New-Item -ItemType Directory -Path $scriptsDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $scriptsSource "*") -Destination $scriptsDestination -Recurse -Force

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

Restore-WslExecutableBits -PublishRootPath $publishRoot

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
