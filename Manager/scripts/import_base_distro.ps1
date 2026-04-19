param(
    [string] $TarPath,
    [string] $DistroName = "NymphsCore",
    [Parameter(Mandatory = $true)] [string] $InstallLocation,
    [string] $LinuxUser,
    [switch] $Force,
    [switch] $RepairExisting,
    [switch] $RunFinalize,
    [switch] $SkipCuda,
    [switch] $SkipModels,
    [switch] $SkipVerify
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

function Build-WslUserArgs {
    param(
        [string] $UserName
    )

    if ($UserName) {
        return @("--user", $UserName)
    }

    return @()
}

function Get-DefaultLinuxUserName {
    return "nymph"
}

function Initialize-DistroUser {
    param(
        [Parameter(Mandatory = $true)] [string] $DistroName,
        [Parameter(Mandatory = $true)] [string] $UserName
    )

    $command = @'
set -euo pipefail
if ! id -u '__USER_NAME__' >/dev/null 2>&1; then
  useradd -m -s /bin/bash '__USER_NAME__'
fi
if getent group sudo >/dev/null 2>&1; then
  usermod -aG sudo '__USER_NAME__'
fi
install -d -m 0755 /etc/sudoers.d
cat > /etc/sudoers.d/90-__USER_NAME__-nymphscore <<'EOF'
__USER_NAME__ ALL=(ALL) NOPASSWD:ALL
EOF
chmod 0440 /etc/sudoers.d/90-__USER_NAME__-nymphscore
cat > /etc/wsl.conf <<'EOF'
[user]
default=__USER_NAME__
[boot]
systemd=true
EOF
'@
    $command = $command.Replace("__USER_NAME__", $UserName)

    Write-Host "Configuring default Linux user '$UserName'..."
    & wsl -d $DistroName --user root -- bash -lc $command
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to configure default Linux user '$UserName' in distro '$DistroName'."
    }
}

function Normalize-DistroShellPaths {
    param(
        [Parameter(Mandatory = $true)] [string] $DistroName
    )

    $command = @'
set -euo pipefail
cat > /etc/profile.d/nymphscore.sh <<'EOF'
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/Nymphs3D
export NYMPHS3D_RUNTIME_ROOT="$HOME"
export NYMPHS3D_H2_DIR="$HOME/Hunyuan3D-2"
export NYMPHS3D_Z_IMAGE_DIR="$HOME/Z-Image"
export NYMPHS3D_N2D2_DIR="$NYMPHS3D_Z_IMAGE_DIR"
export NYMPHS3D_TRELLIS_DIR="$HOME/TRELLIS.2"
EOF
chmod 644 /etc/profile.d/nymphscore.sh
'@

    Write-Host "Normalizing runtime shell paths..."
    & wsl -d $DistroName --user root -- bash -lc $command
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to normalize runtime shell paths in distro '$DistroName'."
    }
}

function Restart-DistroForDefaultUser {
    param(
        [Parameter(Mandatory = $true)] [string] $DistroName
    )

    Write-Host "Restarting distro so the default Linux user setting takes effect..."
    & wsl --terminate $DistroName 2>$null
    Start-Sleep -Milliseconds 750
}

function Remove-InstallLocationWithRetries {
    param(
        [Parameter(Mandatory = $true)] [string] $Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $attempts = 6
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        try {
            Remove-Item -Recurse -Force $Path -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 1) {
                Write-Host "Install location is still busy. Shutting down WSL and retrying cleanup..."
                & wsl --shutdown 2>$null
            }

            if ($attempt -ge $attempts) {
                throw
            }

            Start-Sleep -Milliseconds (750 * $attempt)
        }
    }
}

$fullInstallLocation = [System.IO.Path]::GetFullPath($InstallLocation)
$parentDir = Split-Path -Parent $fullInstallLocation
$existingDistros = @(Get-WslDistroNames)
$reuseExisting = $existingDistros -contains $DistroName -and $RepairExisting.IsPresent

if ($existingDistros -contains $DistroName) {
    if ($reuseExisting) {
        Write-Host "Managed distro '$DistroName' already exists. Reusing it for repair/continue."
    }
    else {
        if (-not $Force.IsPresent) {
            throw "WSL distro '$DistroName' already exists. Use -Force to unregister and replace it, or -RepairExisting to reuse it."
        }

        Write-Host "Unregistering existing distro '$DistroName'..."
        & wsl --unregister $DistroName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to unregister existing distro '$DistroName'."
        }

        # Windows can keep the exported VHD mounted for a moment after unregister.
        # Force a global WSL shutdown here so ext4.vhdx releases before we delete
        # the previous install folder and import the fresh base tar.
        & wsl --shutdown 2>$null
        Start-Sleep -Milliseconds 1000
    }
}

if (-not $reuseExisting) {
    if (-not $TarPath) {
        throw "Base distro tar path is required for a fresh import."
    }
    if (-not (Test-Path $TarPath)) {
        throw "Base distro tar not found: $TarPath"
    }

    if (-not (Test-Path $parentDir)) {
        New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    }

    if (Test-Path $fullInstallLocation) {
        if (-not $Force.IsPresent) {
            throw "Install location already exists: $fullInstallLocation"
        }

        Remove-InstallLocationWithRetries -Path $fullInstallLocation
    }

    New-Item -ItemType Directory -Path $fullInstallLocation -Force | Out-Null

    Write-Host "Importing base distro '$DistroName'..."
    Write-Host "Source tar: $TarPath"
    Write-Host "Install location: $fullInstallLocation"
    & wsl --import $DistroName $fullInstallLocation $TarPath
    if ($LASTEXITCODE -ne 0) {
        throw "WSL import failed."
    }
}
else {
    Write-Host "Skipping base distro import because '$DistroName' is already present."
}

$effectiveLinuxUser = if ($LinuxUser) { $LinuxUser } else { Get-DefaultLinuxUserName }
Initialize-DistroUser -DistroName $DistroName -UserName $effectiveLinuxUser
Normalize-DistroShellPaths -DistroName $DistroName
Restart-DistroForDefaultUser -DistroName $DistroName

if ($reuseExisting) {
    Write-Host "Managed distro repair/continue preparation complete."
}
else {
    Write-Host "Base distro import complete."
}
Write-Host "Default Linux user: $effectiveLinuxUser"

if (-not $RunFinalize.IsPresent) {
    Write-Host ""
    Write-Host "Next step: run the finalize script inside the imported distro."
    Write-Host "Expected path inside the distro should match one of:"
    Write-Host "  /opt/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh"
    exit 0
}

$finalizeArgs = @()
if ($SkipCuda.IsPresent) {
    $finalizeArgs += "--skip-cuda"
}
if ($SkipModels.IsPresent) {
    $finalizeArgs += "--skip-models"
}
if ($SkipVerify.IsPresent) {
    $finalizeArgs += "--skip-verify"
}

$quotedFinalizeArgs = ($finalizeArgs | ForEach-Object { "'$_'" }) -join " "
$finalizeCandidates = @("/opt/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh")
$quotedCandidates = ($finalizeCandidates | ForEach-Object { "'$_'" }) -join " "
$command = @"
set -euo pipefail
finalize_path=""
for candidate in $quotedCandidates; do
  if [ -x "$candidate" ]; then
    finalize_path="$candidate"
    break
  fi
done
if [ -z "$finalize_path" ]; then
  echo "Finalize script not found in expected locations." >&2
  exit 1
fi
"$finalize_path" $quotedFinalizeArgs
"@

Write-Host "Running post-import finalizer..."
$wslUserArgs = @(Build-WslUserArgs -UserName $effectiveLinuxUser)
& wsl -d $DistroName @wslUserArgs -- bash -lc $command
if ($LASTEXITCODE -ne 0) {
    throw "Post-import finalizer failed."
}

Write-Host "Post-import finalizer complete."
