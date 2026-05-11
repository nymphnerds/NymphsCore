param(
    [string] $TarPath,
    [string] $DistroName = "NymphsCore",
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
    if ($normalized -match '^//(?:wsl\.localhost|wsl\$)/([^/]+)/(.*)$') {
        # Never ask WSL to translate a path that belongs to a WSL distro.
        # Source/dev distro paths are not valid inside the target runtime distro.
        return $null
    }

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
cat > /etc/profile.d/nymphscore.sh <<'EOF'
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/NymphsCore
export NYMPHS3D_RUNTIME_ROOT="$HOME"
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

    $bootstrapScriptContent = Get-Content -Path $bootstrapScriptPath -Raw
    if ([string]::IsNullOrWhiteSpace($bootstrapScriptContent)) {
        throw "Bootstrap script was empty: $bootstrapScriptPath"
    }

    New-Item -ItemType Directory -Path $TargetInstallLocation -Force | Out-Null

    $bootstrapCommand = @'
set -euo pipefail
export NYMPHS3D_HELPER_REPO_URL="__HELPER_REPO_URL__"
export NYMPHS3D_HELPER_REPO_BRANCH="__HELPER_REPO_BRANCH__"
export NYMPHS3D_BOOTSTRAP_PREPARE_RUNTIME_REPOS=0
cat >/tmp/nymphscore-bootstrap-fresh-distro-root.sh <<'NYMPHS_BOOTSTRAP_SCRIPT'
__BOOTSTRAP_SCRIPT_CONTENT__
NYMPHS_BOOTSTRAP_SCRIPT
chmod +x /tmp/nymphscore-bootstrap-fresh-distro-root.sh
/bin/bash /tmp/nymphscore-bootstrap-fresh-distro-root.sh
'@
    $bootstrapCommand = $bootstrapCommand.Replace("__HELPER_REPO_URL__", $HelperRepoUrl)
    $bootstrapCommand = $bootstrapCommand.Replace("__HELPER_REPO_BRANCH__", $HelperRepoBranch)
    $bootstrapCommand = $bootstrapCommand.Replace("__BOOTSTRAP_SCRIPT_CONTENT__", $bootstrapScriptContent)

    try {
        Write-Host "Bootstrapping a fresh Ubuntu base locally..."
        Write-Host "Managed distro: $TargetDistroName"
        Write-Host "Install location: $TargetInstallLocation"
        Write-Host "Waiting for WSL to download and register $BootstrapDistribution. This can be quiet while Windows creates ext4.vhdx..."
        try {
            Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "--distribution", $BootstrapDistribution, "--name", $TargetDistroName, "--location", $TargetInstallLocation, "--no-launch") -FailureMessage "Failed to install managed distro"
        }
        catch {
            throw "Failed to create '$TargetDistroName' from '$BootstrapDistribution'. This no-tar path requires a recent WSL build that supports named installs. Details: $($_.Exception.Message)"
        }

        try {
            Write-Host "Running first-boot bootstrap inside '$TargetDistroName'..."
            Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("-d", $TargetDistroName, "--user", "root", "--", "bash", "-lc", $bootstrapCommand) -FailureMessage "Bootstrap preparation failed"
        }
        catch {
            throw "Bootstrap preparation failed inside '$TargetDistroName'. If WSL asks for first-launch user setup on '$TargetDistroName', open it once manually, then rerun this command. Details: $($_.Exception.Message)"
        }

        Write-Host "Fresh distro bootstrap complete."
    }
    finally {
        & wsl --terminate $TargetDistroName 2>$null
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
    Write-Host "Fresh bootstrap provisioning complete."
}
Write-Host "Default Linux user: $effectiveLinuxUser"

if (-not $RunFinalize.IsPresent) {
    Write-Host ""
    Write-Host "Next step: run the finalize script inside the provisioned distro."
    Write-Host "Expected path inside the distro should match one of:"
    Write-Host "  /opt/nymphs3d/NymphsCore/scripts/finalize_imported_distro.sh"
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
    "/opt/nymphs3d/NymphsCore/scripts/finalize_imported_distro.sh",
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
