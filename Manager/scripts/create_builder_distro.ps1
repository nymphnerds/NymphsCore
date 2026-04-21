param(
    [Parameter(Mandatory = $true)] [string] $BuilderInstallLocation,
    [string] $BuilderDistroName = "Ubuntu-24.04",
    [string] $RepoBranch = "main",
    [string] $HelperRepoUrl = "https://github.com/nymphnerds/NymphsCore.git",
    [string] $Hunyuan2RepoUrl = "https://github.com/nymphnerds/Hunyuan3D-2.git",
    [string] $ZImageRepoUrl = "https://github.com/nymphnerds/Nymphs2D2.git",
    [string] $TrellisRepoUrl = "https://github.com/microsoft/TRELLIS.2.git",
    [switch] $Force
)

$ErrorActionPreference = "Stop"

function Get-WslDistroNames {
    $distros = & wsl -l -q 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query installed WSL distros."
    }

    $names = @()
    foreach ($line in @($distros)) {
        $name = ("$line" -replace [char]0, "").Trim()
        if ($name) {
            $names += $name
        }
    }

    return $names
}

function Invoke-NativeOrThrow {
    param(
        [Parameter(Mandatory = $true)] [string] $FilePath,
        [Parameter()] [string[]] $ArgumentList = @(),
        [Parameter(Mandatory = $true)] [string] $FailureMessage
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)."
    }
}

$existingDistros = @(Get-WslDistroNames)
$fullBuilderInstallLocation = [System.IO.Path]::GetFullPath($BuilderInstallLocation)
$parentDir = Split-Path -Parent $fullBuilderInstallLocation
if (-not (Test-Path $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

if ($existingDistros -contains $BuilderDistroName) {
    if (-not $Force.IsPresent) {
        throw "Builder distro '$BuilderDistroName' already exists. Use -Force to replace it."
    }

    Write-Host "Unregistering existing builder distro '$BuilderDistroName'..."
    Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--unregister", $BuilderDistroName) -FailureMessage "Failed to unregister existing builder distro"
}

if (Test-Path $fullBuilderInstallLocation) {
    if (-not $Force.IsPresent) {
        throw "Builder install location already exists: $fullBuilderInstallLocation. Use -Force to replace it."
    }

    Remove-Item -Recurse -Force $fullBuilderInstallLocation
}

Write-Host "Installing fresh builder distro '$BuilderDistroName' to $fullBuilderInstallLocation ..."
Write-Host "This uses a fresh Ubuntu install on the target drive and avoids exporting your full working Ubuntu."
Write-Host "Windows may download Ubuntu files and this can take a few minutes."
Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "--distribution", $BuilderDistroName, "--location", $fullBuilderInstallLocation, "--no-launch") -FailureMessage "Failed to install fresh builder distro"

$bootstrapCommand = @'
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y git ca-certificates curl wget sudo python3 python3-venv python3-pip
mkdir -p /opt/nymphs3d /opt/nymphs3d/runtime
rm -rf /opt/nymphs3d/Nymphs3D
git clone --branch "__REPO_BRANCH__" --single-branch "__HELPER_REPO_URL__" /opt/nymphs3d/Nymphs3D
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/Nymphs3D
export NYMPHS3D_RUNTIME_ROOT=/opt/nymphs3d/runtime
export NYMPHS3D_H2_REPO_URL="__H2_REPO_URL__"
export NYMPHS3D_N2D2_REPO_URL="__Z_IMAGE_REPO_URL__"
export NYMPHS3D_TRELLIS_REPO_URL="__TRELLIS_REPO_URL__"
/bin/bash /opt/nymphs3d/Nymphs3D/scripts/prepare_fresh_builder_distro.sh
'@

$bootstrapCommand = $bootstrapCommand.Replace("__REPO_BRANCH__", $RepoBranch)
$bootstrapCommand = $bootstrapCommand.Replace("__HELPER_REPO_URL__", $HelperRepoUrl)
$bootstrapCommand = $bootstrapCommand.Replace("__H2_REPO_URL__", $Hunyuan2RepoUrl)
$bootstrapCommand = $bootstrapCommand.Replace("__Z_IMAGE_REPO_URL__", $ZImageRepoUrl)
$bootstrapCommand = $bootstrapCommand.Replace("__TRELLIS_REPO_URL__", $TrellisRepoUrl)

Write-Host "Bootstrapping fresh builder distro contents..."
Write-Host "This clones only the helper repo and backend source repos into /opt/nymphs3d."
try {
    Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("-d", $BuilderDistroName, "--user", "root", "--", "bash", "-lc", $bootstrapCommand) -FailureMessage "Builder bootstrap failed"
} catch {
    throw "Builder bootstrap failed. If WSL asks for first-launch user setup on this distro, open '$BuilderDistroName' once manually, then rerun this command. Details: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Fresh builder distro is ready."
Write-Host "Builder distro: $BuilderDistroName"
Write-Host "Builder install location: $fullBuilderInstallLocation"
Write-Host "Next step: export it with scripts/export_builder_distro.ps1"
