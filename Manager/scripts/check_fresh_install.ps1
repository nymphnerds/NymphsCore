[CmdletBinding()]
param(
    [string]$DistroName = "NymphsCore",
    [switch]$BuildManager
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Run-WslCapture {
    param(
        [string]$Distro,
        [string]$Command
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "wsl.exe"
    $psi.Arguments = "-d $Distro -- bash -lc ""$Command"""
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()

    [pscustomobject]@{
        ExitCode = $proc.ExitCode
        StdOut   = $stdout.TrimEnd()
        StdErr   = $stderr.TrimEnd()
    }
}

function Require-Success {
    param(
        [string]$Label,
        [object]$Result
    )

    if ($Result.ExitCode -ne 0) {
        throw "$Label failed.`n$($Result.StdErr)`n$($Result.StdOut)"
    }
}

Write-Step "WSL distros"
$distros = (& wsl.exe -l -v) | ForEach-Object { ($_ -replace [char]0, "").TrimEnd() }
$distros | Out-Host

$distroNames = (& wsl.exe -l -q) | ForEach-Object { ($_ -replace [char]0, "").Trim() } | Where-Object { $_ }
if (-not ($distroNames -contains $DistroName)) {
    throw "Expected distro '$DistroName' was not found."
}

Write-Step "Default user"
$whoamiResult = Run-WslCapture -Distro $DistroName -Command "whoami"
Require-Success -Label "whoami" -Result $whoamiResult
$currentUser = $whoamiResult.StdOut.Trim()
Write-Host "Current default user: $currentUser"
if ($currentUser -ne "nymph") {
    throw "Expected default user 'nymph', got '$currentUser'."
}

Write-Step "Core folders"
$foldersResult = Run-WslCapture -Distro $DistroName -Command "ls ~ && printf '\n---\n' && ls ~/Hunyuan3D-2 ~/Z-Image ~/TRELLIS.2"
Require-Success -Label "folder check" -Result $foldersResult
$foldersResult.StdOut | Out-Host

Write-Step "Python versions"
$pythonResult = Run-WslCapture -Distro $DistroName -Command "~/Hunyuan3D-2/.venv/bin/python --version && ~/Z-Image/.venv-nunchaku/bin/python --version && ~/TRELLIS.2/.venv/bin/python --version"
Require-Success -Label "python version check" -Result $pythonResult
$pythonResult.StdOut | Out-Host
if ($pythonResult.StdOut -notmatch "Python 3\.10") {
    throw "Hunyuan3D-2 venv is not using Python 3.10."
}
if (($pythonResult.StdOut -split "`n").Count -lt 3) {
    throw "Expected all core backend Python versions to be reported."
}
if ($pythonResult.StdOut -notmatch "Python 3\.11") {
    throw "Z-Image Turbo via Nunchaku venv is not using Python 3.11."
}

Write-Step "CUDA path"
$cudaResult = Run-WslCapture -Distro $DistroName -Command "test -d /usr/local/cuda-13.0 && echo CUDA_OK || echo CUDA_MISSING"
Require-Success -Label "CUDA check" -Result $cudaResult
$cudaResult.StdOut | Out-Host
if ($cudaResult.StdOut.Trim() -ne "CUDA_OK") {
    throw "CUDA 13.0 path is missing in '$DistroName'."
}

Write-Step "Model prefetch state"
$modelResult = Run-WslCapture -Distro $DistroName -Command "for p in ~/.cache/huggingface/hub /home/nymph/.cache/huggingface/hub; do if [ -d \"$p\" ]; then echo MODEL_CACHE_PRESENT:$p; fi; done"
Require-Success -Label "model cache check" -Result $modelResult
if ([string]::IsNullOrWhiteSpace($modelResult.StdOut)) {
    Write-Host "No prefetched model cache found. This is expected if you installed without models."
} else {
    $modelResult.StdOut | Out-Host
}

if ($BuildManager) {
    Write-Step "Build local manager"
    $managerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
    $managerProject = Join-Path $managerRoot "apps\Nymphs3DInstaller\Nymphs3DInstaller.csproj"
    if (-not (Test-Path $managerProject)) {
        throw "Manager project not found at $managerProject"
    }
    Push-Location (Split-Path -Parent $managerProject)
    try {
        & dotnet build $managerProject
    } finally {
        Pop-Location
    }
}

Write-Step "Fresh install check passed"
Write-Host "Distro '$DistroName' looks healthy."
Write-Host "Next step: open the Nymphs addon against '$DistroName' or continue testing through NymphsCore Manager."
