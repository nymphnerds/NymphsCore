param(
    [string] $TarPath,
    [string] $DistroName = "NymphsCore_Lite",
    [Parameter(Mandatory = $true)] [string] $InstallLocation,
    [string] $LinuxUser,
    [string] $HelperRepoUrl = "https://github.com/nymphnerds/NymphsCore.git",
    [string] $HelperRepoBranch = "main",
    [string] $BootstrapDistribution = "Ubuntu-24.04",
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

function ConvertTo-WslPath {
    param(
        [string] $WindowsPath
    )

    if ([string]::IsNullOrWhiteSpace($WindowsPath)) {
        return $null
    }

    $normalized = $WindowsPath -replace '\\', '/'
    if ($normalized -match '^([A-Za-z]):/(.*)$') {
        $drive = $matches[1].ToLowerInvariant()
        $rest = $matches[2]
        return "/mnt/$drive/$rest"
    }

    $converted = & wsl wslpath -a $WindowsPath 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return (@($converted) | Select-Object -First 1).Trim()
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

function Get-DefaultLinuxUserName {
    return "nymph"
}

function Get-UniqueDistroName {
    param(
        [Parameter(Mandatory = $true)] [string] $BaseName,
        [Parameter(Mandatory = $true)] [string[]] $ExistingNames
    )

    if ($ExistingNames -notcontains $BaseName) {
        return $BaseName
    }

    $suffix = 2
    while ($ExistingNames -contains "$BaseName-$suffix") {
        $suffix++
    }

    return "$BaseName-$suffix"
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
install -d -m 0755 /opt/nymphs3d
if [ -d /opt/nymphs3d/Nymphs3D ] && [ ! -e /opt/nymphs3d/NymphsCore ]; then
  ln -s /opt/nymphs3d/Nymphs3D /opt/nymphs3d/NymphsCore
fi
cat > /etc/profile.d/nymphscore.sh <<'EOF'
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/Nymphs3D
export NYMPHS3D_RUNTIME_ROOT="$HOME"
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

function Invoke-FreshBootstrapImport {
    param(
        [Parameter(Mandatory = $true)] [string] $TargetDistroName,
        [Parameter(Mandatory = $true)] [string] $TargetInstallLocation,
        [Parameter(Mandatory = $true)] [string] $BootstrapDistribution,
        [Parameter(Mandatory = $true)] [string] $HelperRepoUrl,
        [Parameter(Mandatory = $true)] [string] $HelperRepoBranch
    )

    $bootstrapScriptPath = Join-Path $PSScriptRoot "bootstrap_fresh_distro_root.sh"
    if (-not (Test-Path $bootstrapScriptPath)) {
        throw "Bootstrap script was not found: $bootstrapScriptPath"
    }

    $bootstrapScriptWslPath = ConvertTo-WslPath -WindowsPath $bootstrapScriptPath
    if ([string]::IsNullOrWhiteSpace($bootstrapScriptWslPath)) {
        throw "Failed to convert bootstrap script path for WSL access: $bootstrapScriptPath"
    }

    $existingDistros = @(Get-WslDistroNames)
    $bootstrapDistroName = Get-UniqueDistroName -BaseName "$TargetDistroName-bootstrap" -ExistingNames $existingDistros
    $bootstrapInstallLocation = "$TargetInstallLocation-bootstrap"
    $tempTarPath = Join-Path ([System.IO.Path]::GetTempPath()) ("{0}-bootstrap-{1}.tar" -f $TargetDistroName, [Guid]::NewGuid().ToString("N"))

    if (Test-Path $bootstrapInstallLocation) {
        Remove-InstallLocationWithRetries -Path $bootstrapInstallLocation
    }

    New-Item -ItemType Directory -Path $bootstrapInstallLocation -Force | Out-Null

    $bootstrapCommand = @'
set -euo pipefail
export NYMPHS3D_HELPER_REPO_URL="__HELPER_REPO_URL__"
export NYMPHS3D_HELPER_REPO_BRANCH="__HELPER_REPO_BRANCH__"
export NYMPHS3D_BOOTSTRAP_PREPARE_RUNTIME_REPOS=0
/bin/bash "__BOOTSTRAP_SCRIPT_PATH__"
'@
    $bootstrapCommand = $bootstrapCommand.Replace("__HELPER_REPO_URL__", $HelperRepoUrl)
    $bootstrapCommand = $bootstrapCommand.Replace("__HELPER_REPO_BRANCH__", $HelperRepoBranch)
    $bootstrapCommand = $bootstrapCommand.Replace("__BOOTSTRAP_SCRIPT_PATH__", $bootstrapScriptWslPath)

    try {
        Write-Host "No base tar was found. Bootstrapping a fresh Ubuntu base locally..."
        Write-Host "Temporary bootstrap distro: $bootstrapDistroName"
        Write-Host "Bootstrap install location: $bootstrapInstallLocation"
        try {
            Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "--distribution", $BootstrapDistribution, "--name", $bootstrapDistroName, "--location", $bootstrapInstallLocation, "--no-launch") -FailureMessage "Failed to install temporary bootstrap distro"
        }
        catch {
            throw "Failed to create a temporary '$BootstrapDistribution' bootstrap distro. This no-tar path requires a recent WSL build that supports named installs. Details: $($_.Exception.Message)"
        }

        try {
            Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("-d", $bootstrapDistroName, "--user", "root", "--", "bash", "-lc", $bootstrapCommand) -FailureMessage "Bootstrap preparation failed"
        }
        catch {
            throw "Bootstrap preparation failed. If WSL asks for first-launch user setup on '$bootstrapDistroName', open it once manually, then rerun this command. Details: $($_.Exception.Message)"
        }

        Write-Host "Exporting temporary bootstrap distro..."
        Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--export", $bootstrapDistroName, $tempTarPath) -FailureMessage "Failed to export temporary bootstrap distro"

        Write-Host "Importing managed distro '$TargetDistroName'..."
        Write-Host "Install location: $TargetInstallLocation"
        Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--import", $TargetDistroName, $TargetInstallLocation, $tempTarPath) -FailureMessage "Fresh bootstrap import failed"
    }
    finally {
        $finalDistros = @()
        try {
            $finalDistros = @(Get-WslDistroNames)
        }
        catch {
            $finalDistros = @()
        }

        if ($finalDistros -contains $bootstrapDistroName) {
            Write-Host "Cleaning up temporary bootstrap distro '$bootstrapDistroName'..."
            & wsl --unregister $bootstrapDistroName 2>$null
        }

        if (Test-Path $bootstrapInstallLocation) {
            try {
                Remove-InstallLocationWithRetries -Path $bootstrapInstallLocation
            }
            catch {
                Write-Warning "Temporary bootstrap install location could not be removed automatically: $bootstrapInstallLocation"
            }
        }

        if (Test-Path $tempTarPath) {
            Remove-Item -Force $tempTarPath -ErrorAction SilentlyContinue
        }
    }
}

$fullInstallLocation = [System.IO.Path]::GetFullPath($InstallLocation)
$parentDir = Split-Path -Parent $fullInstallLocation
$existingDistros = @(Get-WslDistroNames)
$reuseExisting = $existingDistros -contains $DistroName -and $RepairExisting.IsPresent
$useBaseTar = -not [string]::IsNullOrWhiteSpace($TarPath) -and (Test-Path $TarPath)

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
        # the previous install folder and provision the fresh base.
        & wsl --shutdown 2>$null
        Start-Sleep -Milliseconds 1000
    }
}

if (-not $reuseExisting) {
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

    if ($useBaseTar) {
        Write-Host "Importing base distro '$DistroName'..."
        Write-Host "Source tar: $TarPath"
        Write-Host "Install location: $fullInstallLocation"
        & wsl --import $DistroName $fullInstallLocation $TarPath
        if ($LASTEXITCODE -ne 0) {
            throw "WSL import failed."
        }
    }
    else {
        Invoke-FreshBootstrapImport -TargetDistroName $DistroName -TargetInstallLocation $fullInstallLocation -BootstrapDistribution $BootstrapDistribution -HelperRepoUrl $HelperRepoUrl -HelperRepoBranch $HelperRepoBranch
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
elseif ($useBaseTar) {
    Write-Host "Base distro import complete."
}
else {
    Write-Host "Fresh bootstrap import complete."
}
Write-Host "Default Linux user: $effectiveLinuxUser"

if (-not $RunFinalize.IsPresent) {
    Write-Host ""
    Write-Host "Next step: run the finalize script inside the provisioned distro."
    Write-Host "Expected path inside the distro should match one of:"
    Write-Host "  /opt/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh"
    Write-Host "  /opt/nymphs3d/NymphsCore/scripts/finalize_imported_distro.sh"
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
$finalizeCandidates = @(
    "/opt/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh",
    "/opt/nymphs3d/NymphsCore/scripts/finalize_imported_distro.sh"
)
$quotedCandidates = ($finalizeCandidates | ForEach-Object { "'$_'" }) -join " "
$command = @"
set -euo pipefail
finalize_path=""
for candidate in $quotedCandidates; do
  if [ -x "`$candidate" ]; then
    finalize_path="`$candidate"
    break
  fi
done
if [ -z "`$finalize_path" ]; then
  echo "Finalize script not found in expected locations." >&2
  exit 1
fi
"`$finalize_path" $quotedFinalizeArgs
"@

Write-Host "Running post-import finalizer..."
$wslUserArgs = @(Build-WslUserArgs -UserName $effectiveLinuxUser)
& wsl -d $DistroName @wslUserArgs -- bash -lc $command
if ($LASTEXITCODE -ne 0) {
    throw "Post-import finalizer failed."
}

Write-Host "Post-import finalizer complete."
