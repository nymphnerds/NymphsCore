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

Write-Host "Publishing NymphsCore Manager for $Runtime..."

# Build the project
if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

# Force a clean rebuild so linked WPF resources like sidebar logos always refresh.
if (Test-Path $publishRoot) {
    Remove-Item -Path $publishRoot -Recurse -Force
}
if (Test-Path $binRoot) {
    Remove-Item -Path $binRoot -Recurse -Force
}
if (Test-Path $objRoot) {
    Remove-Item -Path $objRoot -Recurse -Force
}

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
    $managerScriptPaths = @(
        "bootstrap_fresh_distro_root.sh",
        "finalize_imported_distro.sh",
        "import_base_distro.ps1",
        "install_cuda_13_wsl.sh",
        "install_nymph_module_from_registry.sh",
        "install_system_deps.sh",
        "monitor_query.sh",
        "preflight_wsl.sh",
        "run_finalize_in_distro.ps1",
        "uninstall_nymph_module.sh"
    )

    foreach ($relativePath in $managerScriptPaths) {
        $source = Join-Path $scriptsSource $relativePath
        if (-not (Test-Path $source)) {
            throw "Required Manager script not found: $source"
        }

        Copy-Item -Path $source -Destination (Join-Path $scriptsDestination $relativePath) -Force
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
