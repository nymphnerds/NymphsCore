param(
    [string] $DistroName,
    [switch] $SkipWslConfig,
    [switch] $CreateNewDistro,
    [string] $InstallDrive,
    [string] $RepoBranch = "main"
)

$ErrorActionPreference = "Stop"

$LogDir = Join-Path $env:LOCALAPPDATA "Nymphs3D"
$LogPath = Join-Path $LogDir "install.log"
$script:TargetDistroName = $null
$script:CreateNewDistroSelected = $false
$script:PendingNewDistroName = $null
$script:PendingNewDistroLocation = $null

function Write-InstallLog {
    param(
        [Parameter(Mandatory = $true)] [AllowEmptyString()] [string] $Message
    )

    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "$timestamp $Message"
    Add-Content -Path $LogPath -Value $line -Encoding ascii
    Write-Host $Message
}

function Ensure-Elevated {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isAdmin) {
        return
    }

    $argList = @(
        "-NoProfile"
        "-ExecutionPolicy", "Bypass"
        "-File", "`"$PSCommandPath`""
    )

    if ($DistroName) {
        $argList += @("-DistroName", "`"$DistroName`"")
    }

    if ($SkipWslConfig.IsPresent) {
        $argList += "-SkipWslConfig"
    }

    if ($CreateNewDistro.IsPresent) {
        $argList += "-CreateNewDistro"
    }

    if ($InstallDrive) {
        $argList += @("-InstallDrive", "`"$InstallDrive`"")
    }

    if ($RepoBranch) {
        $argList += @("-RepoBranch", "`"$RepoBranch`"")
    }

    $proc = Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $proc.ExitCode
}

function Invoke-NativeOrThrow {
    param(
        [Parameter(Mandatory = $true)] [string] $FilePath,
        [Parameter()] [string[]] $ArgumentList = @(),
        [Parameter(Mandatory = $true)] [string] $FailureMessage,
        [Parameter()] [bool] $CaptureOutput = $true,
        [Parameter()] [bool] $StreamOutput = $false,
        [Parameter()] [string] $LogPrefix = $FilePath
    )

    if ($StreamOutput) {
        & $FilePath @ArgumentList 2>&1 | ForEach-Object {
            $text = ("$_" -replace [char]0, "").TrimEnd()
            if ($text) {
                Write-InstallLog "[$LogPrefix] $text"
            }
        }
        $exitCode = $LASTEXITCODE
    } elseif ($CaptureOutput) {
        $output = & $FilePath @ArgumentList 2>&1
        $exitCode = $LASTEXITCODE

        foreach ($line in @($output)) {
            $text = ("$line" -replace [char]0, "").TrimEnd()
            if ($text) {
                Write-InstallLog "[$LogPrefix] $text"
            }
        }
    } else {
        & $FilePath @ArgumentList
        $exitCode = $LASTEXITCODE
    }

    if ($exitCode -ne 0) {
        throw "$FailureMessage (exit code $exitCode)."
    }
}

function Get-WslDistroNames {
    $names = @()

    $distros = & wsl -l -q 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query installed WSL distros."
    }

    foreach ($line in @($distros)) {
        $name = ("$line" -replace [char]0, "").Trim()
        if (-not $name) {
            continue
        }
        $names += $name
    }

    if ($names.Count -gt 0) {
        return $names
    }

    $verboseDistros = & wsl -l -v 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query installed WSL distros."
    }

    foreach ($line in @($verboseDistros)) {
        $text = ("$line" -replace [char]0, "").Trim()
        if (-not $text) {
            continue
        }
        if ($text -match '^(?i:name\s+state\s+version)$') {
            continue
        }
        $text = $text -replace '^\*\s*', ''
        $parts = $text -split '\s{2,}'
        if ($parts.Count -lt 1) {
            continue
        }
        $name = $parts[0].Trim()
        if (-not $name) {
            continue
        }
        $names += $name
    }

    return $names
}

function Test-WslInstalled {
    & wsl --status 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
}

function Resolve-TargetDistro {
    param(
        [string] $RequestedDistroName
    )

    $distroNames = Get-WslDistroNames
    if ($distroNames.Count -gt 0) {
        Write-InstallLog "Visible WSL distros: $($distroNames -join ', ')"
    }

    if ($RequestedDistroName) {
        foreach ($name in @($distroNames)) {
            if ($name -eq $RequestedDistroName) {
                Write-InstallLog "Using explicitly requested distro: $name"
                return $name
            }
        }

        throw "Requested WSL distro '$RequestedDistroName' was not found. Create or import it first, then rerun the installer with -DistroName $RequestedDistroName."
    }

    foreach ($name in @($distroNames)) {
        if ($name -match '^(?i:Ubuntu)(?:$|[- ].*)') {
            Write-InstallLog "Detected Ubuntu distro: $name"
            return $name
        }
    }

    return $null
}

function Read-ChoiceNumber {
    param(
        [Parameter(Mandatory = $true)] [string] $Prompt,
        [Parameter(Mandatory = $true)] [int] $Min,
        [Parameter(Mandatory = $true)] [int] $Max
    )

    while ($true) {
        $value = Read-Host $Prompt
        $parsed = 0
        if ([int]::TryParse($value, [ref] $parsed) -and $parsed -ge $Min -and $parsed -le $Max) {
            return $parsed
        }
        Write-InstallLog "Please enter a number from $Min to $Max."
    }
}

function Get-AvailableInstallDrives {
    return @(Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Root -match '^[A-Z]:\\$' } | Sort-Object Name)
}

function Normalize-InstallDrive {
    param(
        [Parameter(Mandatory = $true)] [string] $DriveInput
    )

    $normalized = $DriveInput.Trim().TrimEnd('\')
    if ($normalized.Length -eq 1) {
        $normalized = "${normalized}:"
    }
    if ($normalized -notmatch '^[A-Za-z]:$') {
        throw "Invalid drive selection '$DriveInput'."
    }

    $root = ($normalized.ToUpper() + "\")
    if (-not (Test-Path $root)) {
        throw "Drive $normalized is not available on this machine."
    }

    return $root
}

function Select-InstallDriveInteractive {
    $drives = @(Get-AvailableInstallDrives)
    if ($drives.Count -eq 0) {
        throw "No Windows filesystem drives were found."
    }

    Write-InstallLog ""
    Write-InstallLog "Choose the Windows drive for the new Ubuntu install:"
    for ($i = 0; $i -lt $drives.Count; $i++) {
        $drive = $drives[$i]
        $freeGb = [math]::Floor($drive.Free / 1GB)
        Write-InstallLog ("{0}. {1} ({2} GB free)" -f ($i + 1), $drive.Root, $freeGb)
    }

    $selection = Read-ChoiceNumber -Prompt "Enter a drive number" -Min 1 -Max $drives.Count
    return ($drives[$selection - 1].Name + ":\")
}

function Select-ExistingDistroInteractive {
    $distros = @(Get-WslDistroNames)
    if ($distros.Count -eq 0) {
        throw "No WSL distros were found."
    }

    Write-InstallLog ""
    Write-InstallLog "Choose the WSL distro to use:"
    for ($i = 0; $i -lt $distros.Count; $i++) {
        Write-InstallLog ("{0}. {1}" -f ($i + 1), $distros[$i])
    }

    $selection = Read-ChoiceNumber -Prompt "Enter a distro number" -Min 1 -Max $distros.Count
    return $distros[$selection - 1]
}

function Get-NewUbuntuCandidateName {
    $installed = @(Get-WslDistroNames)
    $preferred = @("Ubuntu-24.04", "Ubuntu-22.04", "Ubuntu-20.04", "Ubuntu-18.04", "Ubuntu")

    $onlineOutput = & wsl --list --online 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query installable WSL distributions."
    }

    $available = @{}
    foreach ($line in @($onlineOutput)) {
        $text = ("$line" -replace [char]0, "").Trim()
        if (-not $text) {
            continue
        }

        $parts = $text -split '\s{2,}'
        if ($parts.Count -lt 1) {
            continue
        }

        $name = $parts[0].Trim()
        if ($name -match '^(Ubuntu(?:-[0-9]+\.[0-9]+)?)$') {
            $available[$name] = $true
        }
    }

    foreach ($name in $preferred) {
        if ($available.ContainsKey($name) -and $installed -notcontains $name) {
            return $name
        }
    }

    throw "No unused official Ubuntu WSL distro name is available. Existing distros already include: $($installed -join ', '). Use the existing-distro path, or remove an unused Ubuntu release before creating a new one."
}

function Resolve-NewDistroLocation {
    param(
        [Parameter(Mandatory = $true)] [string] $DriveRoot,
        [Parameter(Mandatory = $true)] [string] $DistroName
    )

    $baseDir = Join-Path $DriveRoot "WSL"
    $installDir = Join-Path $baseDir ("Nymphs3D-" + $DistroName)
    if (Test-Path $installDir) {
        throw "The install folder already exists: $installDir"
    }

    return $installDir
}

function Prompt-InstallTargetChoice {
    if ($DistroName -or $CreateNewDistro.IsPresent) {
        return
    }

    if (-not (Test-WslInstalled)) {
        return
    }

    $distros = @(Get-WslDistroNames)
    if ($distros.Count -eq 0) {
        Write-InstallLog ""
        Write-InstallLog "WSL is installed, but no Linux distros were found yet."
        Write-InstallLog "A new Ubuntu for Nymphs3D will be created."

        $selectedDrive = if ($InstallDrive) { Normalize-InstallDrive -DriveInput $InstallDrive } else { Select-InstallDriveInteractive }
        $selectedDistro = Get-NewUbuntuCandidateName

        $script:CreateNewDistroSelected = $true
        $script:PendingNewDistroName = $selectedDistro
        $script:PendingNewDistroLocation = Resolve-NewDistroLocation -DriveRoot $selectedDrive -DistroName $selectedDistro
        Write-InstallLog "A new Ubuntu distro will be created as '$selectedDistro' at $script:PendingNewDistroLocation"
        return
    }

    Write-InstallLog ""
    Write-InstallLog "WSL is already installed on this PC."
    Write-InstallLog "1. Use an existing WSL distro"
    Write-InstallLog "2. Create a new Ubuntu for Nymphs3D on another drive"

    $choice = Read-ChoiceNumber -Prompt "Choose 1 or 2" -Min 1 -Max 2
    if ($choice -eq 1) {
        $script:TargetDistroName = Select-ExistingDistroInteractive
        return
    }

    $selectedDrive = if ($InstallDrive) { Normalize-InstallDrive -DriveInput $InstallDrive } else { Select-InstallDriveInteractive }
    $selectedDistro = Get-NewUbuntuCandidateName

    $script:CreateNewDistroSelected = $true
    $script:PendingNewDistroName = $selectedDistro
    $script:PendingNewDistroLocation = Resolve-NewDistroLocation -DriveRoot $selectedDrive -DistroName $selectedDistro
    Write-InstallLog "A new Ubuntu distro will be created as '$selectedDistro' at $script:PendingNewDistroLocation"
}

function Ensure-NewUbuntuDistro {
    param(
        [string] $RequestedDrive
    )

    if (-not (Test-WslInstalled)) {
        Write-InstallLog "WSL is not installed yet. Installing WSL with Ubuntu support first..."
        Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "--no-distribution") -FailureMessage "Failed to install WSL"
        Write-InstallLog ""
        Write-InstallLog "WSL install started. Reboot if Windows asks for it, then rerun this installer."
        exit 0
    }

    if ($script:PendingNewDistroName -and $script:PendingNewDistroLocation) {
        $selectedDistro = $script:PendingNewDistroName
        $installLocation = $script:PendingNewDistroLocation
    } else {
        if (-not $RequestedDrive) {
            throw "A Windows install drive is required for the create-new-distro flow."
        }

        $selectedDrive = Normalize-InstallDrive -DriveInput $RequestedDrive
        $selectedDistro = Get-NewUbuntuCandidateName
        $installLocation = Resolve-NewDistroLocation -DriveRoot $selectedDrive -DistroName $selectedDistro
    }

    Write-InstallLog "Creating a new Ubuntu distro named '$selectedDistro' at $installLocation"
    Write-InstallLog "Windows is creating and downloading the new Ubuntu environment. This can take several minutes."
    Write-InstallLog "You may see download and install progress below."
    Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "--distribution", $selectedDistro, "--location", $installLocation, "--no-launch") -FailureMessage "Failed to create the new Ubuntu WSL distro" -StreamOutput $true -LogPrefix "wsl"

    $script:PendingNewDistroName = $selectedDistro
    $script:PendingNewDistroLocation = $installLocation
    Write-InstallLog "Created the new Ubuntu distro '$selectedDistro'."
    return $selectedDistro
}

function Get-DriveFreeSpaceGb {
    param(
        [Parameter(Mandatory = $true)] [string] $DriveRoot
    )

    $normalizedDrive = Normalize-InstallDrive -DriveInput $DriveRoot
    $deviceId = $normalizedDrive.Substring(0, 2)
    $disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$deviceId'"
    if (-not $disk) {
        throw "Could not inspect free space for drive $deviceId."
    }

    return [math]::Floor($disk.FreeSpace / 1GB)
}

function Get-RecommendedWslConfig {
    $computer = Get-CimInstance Win32_ComputerSystem
    $totalMemoryGb = [math]::Floor($computer.TotalPhysicalMemory / 1GB)
    $logicalProcessors = [int]$computer.NumberOfLogicalProcessors

    if ($totalMemoryGb -ge 64) {
        $memoryGb = 28
    } elseif ($totalMemoryGb -ge 48) {
        $memoryGb = 24
    } elseif ($totalMemoryGb -ge 32) {
        $memoryGb = 16
    } elseif ($totalMemoryGb -ge 24) {
        $memoryGb = 12
    } else {
        $memoryGb = [math]::Max(8, $totalMemoryGb - 4)
    }

    $processors = [math]::Max(4, [math]::Min(12, $logicalProcessors - 2))
    $swapGb = [math]::Max(4, [math]::Min(16, [math]::Floor($memoryGb / 2)))

    return @{
        MemoryGb = $memoryGb
        Processors = $processors
        SwapGb = $swapGb
    }
}

function Write-WslConfig {
    param(
        [Parameter(Mandatory = $true)] [hashtable] $Config
    )

    $wslConfigPath = Join-Path $HOME ".wslconfig"
    $content = @"
[wsl2]
memory=$($Config.MemoryGb)GB
processors=$($Config.Processors)
swap=$($Config.SwapGb)GB
localhostForwarding=true
"@

    $existing = ""
    if (Test-Path $wslConfigPath) {
        $existing = Get-Content -Raw -Path $wslConfigPath
    }

    if ($existing -ne $content) {
        if (Test-Path $wslConfigPath) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupPath = Join-Path $HOME ".wslconfig.backup-$timestamp"
            Copy-Item -Path $wslConfigPath -Destination $backupPath -Force
            Write-InstallLog "Backed up existing .wslconfig to $backupPath"
        }
        Set-Content -Path $wslConfigPath -Value $content -Encoding ascii
        Write-InstallLog "Wrote machine-specific .wslconfig to $wslConfigPath"
        & wsl --status 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--shutdown") -FailureMessage "Failed to shut down WSL after writing .wslconfig"
            Write-InstallLog "WSL was shut down so the new config can apply."
        } else {
            Write-InstallLog "WSL is not installed yet. The new .wslconfig will apply after WSL is installed."
        }
    } else {
        Write-InstallLog ".wslconfig already matches the recommended values."
    }
}

function Test-WindowsPreflight {
    param(
        [string] $TargetDrive
    )

    $cFreeGb = Get-DriveFreeSpaceGb -DriveRoot "C:"
    if ($TargetDrive) {
        $normalizedTargetDrive = Normalize-InstallDrive -DriveInput $TargetDrive
        $targetFreeGb = Get-DriveFreeSpaceGb -DriveRoot $normalizedTargetDrive

        if ($normalizedTargetDrive -ne "C:\") {
            if ($cFreeGb -lt 20) {
                throw "At least 20 GB free on C: is recommended for the Windows-side installer files. Found ${cFreeGb} GB."
            }
            if ($targetFreeGb -lt 120) {
                throw "At least 120 GB free is recommended on $normalizedTargetDrive for a new Nymphs3D WSL install. Found ${targetFreeGb} GB."
            }
        } elseif ($targetFreeGb -lt 120) {
            throw "At least 120 GB free on C: is recommended. Found ${targetFreeGb} GB."
        }
    } elseif ($cFreeGb -lt 120) {
        throw "At least 120 GB free on C: is recommended. Found ${cFreeGb} GB."
    }

    $gpu = Get-CimInstance Win32_VideoController | Where-Object { $_.Name -match "NVIDIA" } | Select-Object -First 1
    if (-not $gpu) {
        throw "No NVIDIA GPU was detected. This setup currently expects NVIDIA + CUDA on WSL."
    }

    if ($TargetDrive) {
        $normalizedTargetDrive = Normalize-InstallDrive -DriveInput $TargetDrive
        $targetFreeGb = Get-DriveFreeSpaceGb -DriveRoot $normalizedTargetDrive
        Write-InstallLog "Windows preflight passed: ${cFreeGb} GB free on C:, ${targetFreeGb} GB free on $normalizedTargetDrive, NVIDIA GPU detected."
    } else {
        Write-InstallLog "Windows preflight passed: ${cFreeGb} GB free on C:, NVIDIA GPU detected."
    }
}

function Ensure-WslAndUbuntu {
    param(
        [string] $RequestedDistroName,
        [bool] $CreateNew
    )

    if ($CreateNew) {
        $requestedDrive = if ($script:PendingNewDistroLocation) {
            Split-Path -Qualifier $script:PendingNewDistroLocation
        } elseif ($InstallDrive) {
            $InstallDrive
        } else {
            Select-InstallDriveInteractive
        }
        return (Ensure-NewUbuntuDistro -RequestedDrive $requestedDrive)
    }

    $wslStatus = & wsl --status 2>$null
    if ($LASTEXITCODE -ne 0) {
        if ($RequestedDistroName) {
            throw "WSL is not installed, and the requested distro '$RequestedDistroName' cannot be used yet. Install WSL and create or import that distro first."
        }

        Write-InstallLog "WSL is not installed. Installing WSL with Ubuntu..."
        Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "-d", "Ubuntu") -FailureMessage "Failed to install WSL Ubuntu"
        Write-InstallLog ""
        Write-InstallLog "WSL install started. Launch Ubuntu once, create your Linux user, then rerun this installer."
        exit 0
    }

    $ubuntuDistro = Resolve-TargetDistro -RequestedDistroName $RequestedDistroName

    if (-not $ubuntuDistro) {
        Write-InstallLog "Ubuntu distro is not installed. Installing Ubuntu..."
        Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("--install", "-d", "Ubuntu") -FailureMessage "Failed to install the Ubuntu WSL distro"
        Write-InstallLog ""
        Write-InstallLog "Ubuntu install started. Launch Ubuntu once, create your Linux user, then rerun this installer."
        exit 0
    }

    return $ubuntuDistro
}

function Test-WslDistroReady {
    param(
        [Parameter(Mandatory = $true)] [string] $DistroName
    )

    Write-InstallLog "Checking WSL distro readiness for: $DistroName"
    $output = & wsl -d $DistroName -- bash -lc "echo ready" 2>&1
    $exitCode = $LASTEXITCODE

    foreach ($line in @($output)) {
        $text = "$line".TrimEnd()
        if ($text) {
            Write-InstallLog "[wsl-ready] $text"
        }
    }

    Write-InstallLog "WSL distro readiness exit code: $exitCode"
    return ($exitCode -eq 0)
}

function Invoke-WslInstall {
    param(
        [Parameter(Mandatory = $true)] [string] $DistroName
    )

    $repoUrl = "https://github.com/Babyjawz/Nymphs3D.git"
    $repoBranch = $RepoBranch
    $command = @'
set -euo pipefail
REPO_URL='__REPO_URL__'
REPO_BRANCH='__REPO_BRANCH__'
INSTALL_ROOT="${HOME}/.nymphs3d-installer"
REPO_DIR="${INSTALL_ROOT}/Nymphs3D"
BACKUP_ROOT="${INSTALL_ROOT}/backups"
mkdir -p "${INSTALL_ROOT}" "${BACKUP_ROOT}"
if ! command -v git >/dev/null 2>&1; then
  echo "Installing git in WSL so the setup repo can be cloned..."
  sudo apt update
  sudo apt install -y git
fi
if [ -d "${REPO_DIR}/.git" ]; then
  echo "Refreshing managed installer checkout at ${REPO_DIR}"
  git -C "${REPO_DIR}" fetch origin "${REPO_BRANCH}" --prune
  git -C "${REPO_DIR}" reset --hard "origin/${REPO_BRANCH}"
  git -C "${REPO_DIR}" clean -fdx
elif [ -e "${REPO_DIR}" ]; then
  backup="${BACKUP_ROOT}/Nymphs3D-$(date +%Y%m%d-%H%M%S)"
  mv "${REPO_DIR}" "${backup}"
  echo "Moved unexpected installer path to ${backup}"
  git clone --branch "${REPO_BRANCH}" --single-branch "${REPO_URL}" "${REPO_DIR}"
else
  git clone --branch "${REPO_BRANCH}" --single-branch "${REPO_URL}" "${REPO_DIR}"
fi
cd "${REPO_DIR}"
chmod +x scripts/*.sh
./scripts/install_all.sh
'@
    $command = $command.Replace('__REPO_URL__', $repoUrl)
    $command = $command.Replace('__REPO_BRANCH__', $repoBranch)

    Write-InstallLog "WSL-side install is starting now."
    Write-InstallLog "You will see Linux setup progress below. If prompted in the terminal, enter your Linux password."
    Invoke-NativeOrThrow -FilePath "wsl" -ArgumentList @("-d", $DistroName, "--", "bash", "-lc", $command) -FailureMessage "WSL-side install failed" -StreamOutput $true -LogPrefix "wsl-install"
}

try {
    Ensure-Elevated

    Write-InstallLog "Preparing one-click Hunyuan installer..."
    Write-InstallLog "Writing installer log to $LogPath"
    Write-InstallLog "Installer repo branch: $RepoBranch"
    Prompt-InstallTargetChoice
    $createNewFlow = $CreateNewDistro.IsPresent -or $script:CreateNewDistroSelected
    $preflightDrive = if ($script:PendingNewDistroLocation) {
        Split-Path -Qualifier $script:PendingNewDistroLocation
    } elseif ($createNewFlow -and $InstallDrive) {
        Normalize-InstallDrive -DriveInput $InstallDrive
    } else {
        $null
    }
    Test-WindowsPreflight -TargetDrive $preflightDrive
    if ($DistroName) {
        Write-InstallLog "Advanced mode: targeting WSL distro '$DistroName'."
    }
    if ($script:TargetDistroName) {
        $DistroName = $script:TargetDistroName
        Write-InstallLog "Using selected existing WSL distro '$DistroName'."
    }
    if ($createNewFlow) {
        Write-InstallLog "Using selected drive for a new Ubuntu distro."
    }
    if ($createNewFlow) {
        Write-InstallLog "Advanced mode: creating a new Ubuntu distro for Nymphs3D."
    }
    if ($SkipWslConfig.IsPresent) {
        Write-InstallLog "Advanced mode: skipping .wslconfig changes."
    } else {
        $recommended = Get-RecommendedWslConfig
        Write-WslConfig -Config $recommended
    }
    $ubuntuDistro = Ensure-WslAndUbuntu -RequestedDistroName $DistroName -CreateNew $createNewFlow

    if (-not (Test-WslDistroReady -DistroName $ubuntuDistro)) {
        Write-InstallLog ""
        Write-InstallLog "The target WSL distro exists but is not initialized yet."
        Write-InstallLog "Open that distro once, finish Linux user creation, then rerun this installer."
        exit 0
    }

    Write-InstallLog "Launching WSL-side install..."
    Invoke-WslInstall -DistroName $ubuntuDistro

    Write-InstallLog ""
    Write-InstallLog "One-click install finished."
    Write-InstallLog "Managed WSL installer checkout: ~/.nymphs3d-installer/Nymphs3D"
    Write-InstallLog "For a full API smoke test later, run: ~/.nymphs3d-installer/Nymphs3D/scripts/verify_install.sh --smoke-test 2mv"
    exit 0
} catch {
    Write-InstallLog ""
    Write-InstallLog "INSTALL FAILED: $($_.Exception.Message)"
    Write-InstallLog "See install log: $LogPath"
    exit 1
}
