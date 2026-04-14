param(
    [Parameter(Mandatory = $true)] [string] $BuilderDistroName,
    [Parameter(Mandatory = $true)] [string] $OutputTarPath,
    [switch] $Force,
    [switch] $UnregisterBuilderAfterExport
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

$existingDistros = @(Get-WslDistroNames)
if ($existingDistros -notcontains $BuilderDistroName) {
    throw "Builder distro '$BuilderDistroName' was not found."
}

$fullOutputTarPath = [System.IO.Path]::GetFullPath($OutputTarPath)
$parentDir = Split-Path -Parent $fullOutputTarPath
if (-not (Test-Path $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

if (Test-Path $fullOutputTarPath) {
    if (-not $Force.IsPresent) {
        throw "Output tar already exists: $fullOutputTarPath. Use -Force to overwrite it."
    }

    Remove-Item -Force $fullOutputTarPath
}

Write-Host "Exporting builder distro '$BuilderDistroName' to $fullOutputTarPath ..."
& wsl --export $BuilderDistroName $fullOutputTarPath
if ($LASTEXITCODE -ne 0) {
    throw "WSL export failed for '$BuilderDistroName'."
}

Write-Host "Builder distro export complete."

if ($UnregisterBuilderAfterExport.IsPresent) {
    Write-Host "Unregistering builder distro '$BuilderDistroName'..."
    & wsl --unregister $BuilderDistroName
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to unregister builder distro '$BuilderDistroName'."
    }

    Write-Host "Builder distro removed."
}
