param(
    [string] $DistroName,
    [switch] $SkipWslConfig,
    [switch] $CreateNewDistro,
    [string] $InstallDrive,
    [string] $RepoBranch = "main",
    [switch] $NonInteractive
)

$ErrorActionPreference = "Stop"

trap {
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    if (-not $NonInteractive.IsPresent) {
        Read-Host "Press Enter to close"
    }
    exit 1
}

$RepoZipUrl = "https://github.com/nymphnerds/NymphsCore/archive/refs/heads/$RepoBranch.zip"
$InstallRoot = Join-Path $env:USERPROFILE "NymphsCoreManager"
$ZipPath = Join-Path $InstallRoot "NymphsCore-$RepoBranch.zip"
$ExtractRoot = Join-Path $InstallRoot "extracted"
$RepoDir = Join-Path $ExtractRoot "NymphsCore-$RepoBranch"
$InstallerPath = Join-Path $RepoDir "scripts\install_one_click_windows.ps1"

if (-not $NonInteractive.IsPresent) {
    Write-Host ""
    Write-Host "Welcome to the NymphsCore installer."
    Write-Host ""
    Write-Host "This installer sets up the local backend needed for NymphsCore on this PC."
    Write-Host ""
    Write-Host "Windows will open a PowerShell window and ask for administrator permission."
    Write-Host "Click Yes to continue."
    Write-Host ""
    Write-Host "During install, you may see:"
    Write-Host "- Ubuntu/Linux password prompts inside WSL"
    Write-Host "- large downloads that can take a while"
    Write-Host ""
    Write-Host "Please keep this window open until the installer finishes."
    Write-Host ""

    $continueResponse = Read-Host "Press Enter to continue, or type N to cancel"
    if ($continueResponse -match '^(?i:n|no|q|quit)$') {
        Write-Host ""
        Write-Host "Installer cancelled before download."
        exit 0
    }
}

Write-Host "Preparing NymphsCore installer..."

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
New-Item -ItemType Directory -Path $ExtractRoot -Force | Out-Null

if (Test-Path $ZipPath) {
    Remove-Item -Force $ZipPath
}
if (Test-Path $RepoDir) {
    Remove-Item -Recurse -Force $RepoDir
}

Write-Host "Downloading installer files from GitHub. This can take a minute..."
$previousProgressPreference = $ProgressPreference
$ProgressPreference = "SilentlyContinue"
try {
    Invoke-WebRequest -Uri $RepoZipUrl -OutFile $ZipPath
} finally {
    $ProgressPreference = $previousProgressPreference
}

Write-Host "Extracting installer bundle..."
Expand-Archive -Path $ZipPath -DestinationPath $ExtractRoot -Force

if (-not (Test-Path $InstallerPath)) {
    throw "Could not find installer script after extraction: $InstallerPath"
}

Write-Host "Launching one-click installer..."
$argList = @(
    "-NoProfile"
    "-ExecutionPolicy", "Bypass"
    "-File", $InstallerPath
)

if ($DistroName) {
    $argList += @("-DistroName", $DistroName)
}

if ($SkipWslConfig.IsPresent) {
    $argList += "-SkipWslConfig"
}

if ($CreateNewDistro.IsPresent) {
    $argList += "-CreateNewDistro"
}

if ($InstallDrive) {
    $argList += @("-InstallDrive", $InstallDrive)
}

if ($RepoBranch) {
    $argList += @("-RepoBranch", $RepoBranch)
}

if ($NonInteractive.IsPresent) {
    $argList += "-NonInteractive"
}

& powershell @argList
if ($LASTEXITCODE -ne 0) {
    throw "The one-click installer failed with exit code $LASTEXITCODE."
}
