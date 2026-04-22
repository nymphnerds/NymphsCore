# Windows Manager Build And No-Tar Test

This checklist is for testing the `nymphscore-lite-no-tar` manager on Windows.

Run these commands from an Administrator PowerShell unless noted otherwise.

## Build The Manager

Recommended path from Windows PowerShell:

```powershell
Set-Location "\\wsl.localhost\NymphsCore\home\nymph\NymphsCore\Manager\apps\Nymphs3DInstaller"
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

Why:

- the Manager is a Windows WPF app
- the local WSL distro may not have a Linux `dotnet` SDK installed
- building through Windows PowerShell lets `dotnet publish` use the Windows SDK/tooling while reading the repo through the WSL UNC path
- this is the path that successfully rebuilt the Lite Manager exe and zip on 2026-04-22

Equivalent command if you are already in a Windows checkout of the repo:

```powershell
cd .\Manager\apps\Nymphs3DInstaller
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

Avoid relying on a plain Linux-side `dotnet publish` from inside WSL unless you have intentionally installed and tested the required Windows-targeting .NET SDK there.

Expected outputs:

- `Manager\apps\Nymphs3DInstaller\publish\win-x64\NymphsCoreManager.exe`
- `Manager\apps\Nymphs3DInstaller\publish\NymphsCoreManager-win-x64.zip`

## Confirm The No-Tar Payload

The lite no-tar test should work without placing `NymphsCore.tar` next to the exe.

Check:

```powershell
Test-Path .\publish\win-x64\NymphsCore.tar
Test-Path .\publish\win-x64\scripts\bootstrap_fresh_distro_root.sh
Test-Path .\publish\win-x64\scripts\import_base_distro.ps1
```

Expected:

- `NymphsCore.tar` can be `False`
- both script paths should be `True`

## First Launcher Test

1. Run `publish\win-x64\NymphsCoreManager.exe`.
2. System Check should warn that no prebuilt tar was found, not fail on it.
3. Continue through install.
4. The install log should say:
   - `Base tar not found. Bootstrapping a fresh Ubuntu base locally.`
   - `No base tar was found. Bootstrapping a fresh Ubuntu base locally...`
   - `Temporary bootstrap distro: NymphsCore_Lite-bootstrap`

## WSL Version Risk

The no-tar path uses:

```powershell
wsl --install --distribution Ubuntu-24.04 --name NymphsCore_Lite-bootstrap --location D:\WSL\NymphsCore_Lite-bootstrap --no-launch
```

If that fails, update WSL first:

```powershell
wsl --update
wsl --version
```

Then rerun the manager.

## Cleanup After A Failed Test

If a test stops midway, inspect distros:

```powershell
wsl -l -v
```

Possible cleanup commands:

```powershell
wsl --unregister NymphsCore_Lite-bootstrap
wsl --unregister NymphsCore_Lite
```

Only unregister `NymphsCore_Lite` if you are intentionally discarding that test install.

## What Success Looks Like

After the bootstrap step, the manager should continue into the existing finalize flow:

- create the `nymph` Linux user
- normalize `/etc/profile.d/nymphscore.sh`
- install Linux system dependencies
- install CUDA 13.0
- install `Z-Image` and `TRELLIS.2` environments
- optionally prefetch models
- run verification

This still needs a real Windows/WSL test. The Linux dev shell cannot validate `wsl --install`, PowerShell parsing, or the WPF build.
