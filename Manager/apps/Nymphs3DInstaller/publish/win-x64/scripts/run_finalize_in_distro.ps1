param(
    [Parameter(Mandatory = $true)] [string] $DistroName,
    [string] $FinalizeScriptPath = "/opt/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh",
    [string] $LinuxUser,
    [switch] $CheckUpdatesOnly,
    [switch] $SystemOnly,
    [switch] $SkipCuda,
    [switch] $SkipBackendEnvs,
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

function ConvertTo-BashSingleQuoted {
    param(
        [AllowNull()]
        [string] $Value
    )

    if ($null -eq $Value) {
        return "''"
    }

    return "'" + ($Value -replace "'", "'\''") + "'"
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

function Build-LinuxSessionPrefix {
    param(
        [string] $UserName,
        [string] $TokenExportPrefix
    )

    if ([string]::IsNullOrWhiteSpace($UserName)) {
        return "set -euo pipefail; " + $TokenExportPrefix
    }

    $linuxHome = "/home/$UserName"
    return (
        "set -euo pipefail; " +
        "export HOME=" + (ConvertTo-BashSingleQuoted $linuxHome) + "; " +
        "export USER=" + (ConvertTo-BashSingleQuoted $UserName) + "; " +
        "export LOGNAME=" + (ConvertTo-BashSingleQuoted $UserName) + "; " +
        'export NYMPHS3D_RUNTIME_ROOT="$HOME"; ' +
        'export NYMPHS3D_H2_DIR="$HOME/Hunyuan3D-2"; ' +
        'export NYMPHS3D_Z_IMAGE_DIR="$HOME/Z-Image"; ' +
        'export NYMPHS3D_N2D2_DIR="$NYMPHS3D_Z_IMAGE_DIR"; ' +
        'export NYMPHS3D_TRELLIS_DIR="$HOME/TRELLIS.2"; ' +
        $TokenExportPrefix
    )
}

$existingDistros = @(Get-WslDistroNames)
if ($existingDistros -notcontains $DistroName) {
    throw "WSL distro '$DistroName' was not found."
}

$finalizeArgs = @()
if ($SkipCuda.IsPresent) {
    $finalizeArgs += "--skip-cuda"
}
if ($SkipBackendEnvs.IsPresent) {
    $finalizeArgs += "--skip-backend-envs"
}
if ($SkipModels.IsPresent) {
    $finalizeArgs += "--skip-models"
}
if ($SkipVerify.IsPresent) {
    $finalizeArgs += "--skip-verify"
}

Write-Host "Running finalize step inside distro '$DistroName'..."
if ($CheckUpdatesOnly.IsPresent) {
    Write-Host "Mode: update-check-only"
}
elseif ($SystemOnly.IsPresent) {
    Write-Host "Mode: system-only"
} else {
    Write-Host "Finalize options: $($finalizeArgs -join ' ')"
}

$originalLocation = Get-Location
try {
    # Avoid inheriting a \\wsl.localhost or mapped-drive working directory into wsl.exe.
    Set-Location $env:SystemRoot
    $wslUserArgs = @(Build-WslUserArgs -UserName $LinuxUser)
    $hfToken = $env:NYMPHS3D_HF_TOKEN
    $packagedScriptsDir = ConvertTo-WslPath -WindowsPath $PSScriptRoot
    $originalWslEnv = $env:WSLENV
    $tokenExportPrefix = ""
    $effectiveScriptsDir = "/opt/nymphs3d/Nymphs3D/scripts"
    $effectiveFinalizeScriptPath = $FinalizeScriptPath

    if (-not [string]::IsNullOrWhiteSpace($hfToken)) {
        $hfToken = $hfToken.Trim()
        $env:NYMPHS3D_HF_TOKEN = $hfToken
        $wslEnvEntries = @()
        if (-not [string]::IsNullOrWhiteSpace($originalWslEnv)) {
            $wslEnvEntries += $originalWslEnv.Split(':', [System.StringSplitOptions]::RemoveEmptyEntries)
        }
        $wslEnvEntries = @($wslEnvEntries | Where-Object { $_ -notmatch '^NYMPHS3D_HF_TOKEN(?:/.*)?$' })
        $wslEnvEntries += "NYMPHS3D_HF_TOKEN/u"
        $env:WSLENV = $wslEnvEntries -join ":"
        Write-Host "Hugging Face token detected for installer-time downloads ($($hfToken.Length) chars after trimming)."
        $tokenExportPrefix = "export NYMPHS3D_HF_TOKEN=" + (ConvertTo-BashSingleQuoted $hfToken) + "; "
    }

    $sessionPrefix = Build-LinuxSessionPrefix -UserName $LinuxUser -TokenExportPrefix $tokenExportPrefix

    if (-not [string]::IsNullOrWhiteSpace($packagedScriptsDir)) {
        $effectiveScriptsDir = $packagedScriptsDir
        $effectiveFinalizeScriptPath = "$packagedScriptsDir/finalize_imported_distro.sh"
        Write-Host "Using packaged helper scripts from '$effectiveScriptsDir'."
    }
    else {
        Write-Host "Using in-distro helper scripts from '/opt/nymphs3d/Nymphs3D/scripts'."
    }

    if (-not $SystemOnly.IsPresent) {
        Write-Host "Effective finalize script: $effectiveFinalizeScriptPath"
    }

    if ($CheckUpdatesOnly.IsPresent) {
        $checkUpdatesCommand = @(
            "-d", $DistroName
        ) + $wslUserArgs + @(
            "--",
            "/bin/bash", "-lc",
            $sessionPrefix + "bash " + (ConvertTo-BashSingleQuoted "$effectiveScriptsDir/check_managed_repo_updates.sh")
        )
        $checkOutput = & wsl @checkUpdatesCommand 2>&1
        foreach ($line in @($checkOutput)) {
            $text = ("$line" -replace [char]0, "").TrimEnd()
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                Write-Host $text
            }
        }
        if ($LASTEXITCODE -ne 0) {
            throw "Managed repo update check failed in distro '$DistroName'."
        }
        Write-Host "Managed repo update check completed."
        return
    }

    if ($SystemOnly.IsPresent) {
        $systemOnlyCommand = @(
            "-d", $DistroName
        ) + $wslUserArgs + @(
            "--",
            "/bin/bash", "-lc",
            $sessionPrefix + "bash " + (ConvertTo-BashSingleQuoted "$effectiveScriptsDir/preflight_wsl.sh") + "; bash " + (ConvertTo-BashSingleQuoted "$effectiveScriptsDir/install_system_deps.sh")
        )
        & wsl @systemOnlyCommand
        if ($LASTEXITCODE -ne 0) {
            throw "System-only finalize step failed in distro '$DistroName'."
        }
        Write-Host "System-only finalize step completed."
        return
    }

    $probeCommand = @(
        "-d", $DistroName
    ) + $wslUserArgs + @(
        "--",
        "/bin/bash", "-lc",
        $sessionPrefix + "test -f " + (ConvertTo-BashSingleQuoted $effectiveFinalizeScriptPath)
    )
    & wsl @probeCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Finalize script not found or not executable inside distro '$DistroName': $effectiveFinalizeScriptPath"
    }

    $tokenProbeCommand = @(
        "-d", $DistroName
    ) + $wslUserArgs + @(
        "--",
        "/bin/bash", "-lc",
        $sessionPrefix + 'if [ -n "${NYMPHS3D_HF_TOKEN:-}" ]; then echo Installer-time-Hugging-Face-token-is-visible-inside-WSL.; else echo Installer-time-Hugging-Face-token-is-not-present-inside-WSL.; fi'
    )
    & wsl @tokenProbeCommand
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Installer-time Hugging Face token visibility probe could not be completed."
    }

    $invokeShell = $sessionPrefix + "bash " + (ConvertTo-BashSingleQuoted $effectiveFinalizeScriptPath)
    if ($finalizeArgs.Count -gt 0) {
        $invokeShell += " " + (($finalizeArgs | ForEach-Object { ConvertTo-BashSingleQuoted $_ }) -join " ")
    }
    $invokeCommand = @(
        "-d", $DistroName
    ) + $wslUserArgs + @(
        "--",
        "/bin/bash", "-lc",
        $invokeShell
    )

    & wsl @invokeCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Finalize step failed in distro '$DistroName'."
    }
}
finally {
    if ($null -eq $originalWslEnv) {
        Remove-Item Env:WSLENV -ErrorAction SilentlyContinue
    }
    else {
        $env:WSLENV = $originalWslEnv
    }
    Set-Location $originalLocation
}

Write-Host "Finalize step completed."
