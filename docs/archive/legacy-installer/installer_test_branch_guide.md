# Installer Test Branch Guide

Legacy note:

- this document describes the older bootstrap-based branch test flow
- the main install path is now the WPF app under `apps/Nymphs3DInstaller`
- the old bootstrap files now live under `legacy/`

This document describes the installer test branch for validating the new
multi-distro beginner flow without touching an existing working WSL setup.

## Branch

- branch name: `installer_test_drive_choice`

Use a simple branch name without `/` characters when testing through the
bootstrap downloader.

## Goal

This branch exists to validate a safer beginner install path on a Windows PC
that already has an existing working WSL distro.

Main scenario:

- there is already a working WSL Ubuntu on `C:`
- that existing distro must remain untouched
- the installer should let the user create a new Ubuntu for Nymphs3D on a
  different Windows drive

## What Changed

The Windows installer now supports:

- choosing `Use an existing WSL distro`
- choosing `Create a new Ubuntu for Nymphs3D on another drive`
- choosing the target Windows drive for the new distro
- handing off to one visible PowerShell installer window instead of leaving a
  separate batch window open
- explicitly targeting a named distro so the installer does not silently pick
  the existing Ubuntu on `C:`
- skipping `.wslconfig` changes with `-SkipWslConfig`
- overriding the bootstrap source branch with `-RepoBranch`

Relevant files:

- `legacy/Install_Nymphs3D.bat`
- `legacy/Install_Nymphs3D.ps1`
- `scripts/install_one_click_windows.ps1`

## Important Safety Notes

The new distro flow is designed to avoid touching an existing working WSL
distro, but some Windows-level state is still global.

Things that remain separate:

- the selected WSL distro
- the Linux home directory inside that distro
- backend repos cloned into that distro
- Python environments inside that distro

Things that are still global on Windows:

- `%USERPROFILE%\.wslconfig`
- `%LOCALAPPDATA%\Nymphs3D\install.log`
- `%USERPROFILE%\Nymphs3DInstaller`
- Windows-side ports such as `8080`

If you want to avoid `.wslconfig` changes during testing, use:

```text
-SkipWslConfig
```

## How Branch Testing Works

The public-facing bootstrap normally redownloads the installer from GitHub
`main`, so a branch-only test needed an explicit branch override.

This guide was originally written for the temporary `installer_test_drive_choice`
branch.

That branch has since been folded into `main`, so any old branch-specific
download or `-RepoBranch installer_test_drive_choice` instruction should now be
read as `main`.

Historically, that test branch added:

```text
-RepoBranch main
```

That makes both stages pull from this branch:

- the Windows-side zip download
- the managed WSL-side repo refresh/clone

## Recommended Test Command

For a non-technical tester, the intended path is:

1. download the repo zip from GitHub
2. extract it
3. double-click `legacy/DOUBLE_CLICK_THIS_TO_TEST_INSTALLER.bat`

Current repo zip URL:

```text
https://github.com/Babyjawz/Nymphs3D/archive/refs/heads/main.zip
```

The dedicated test launcher hardcodes:

```text
-RepoBranch main -SkipWslConfig
```

So the tester does not need to type any arguments.

If you want to run it manually from a Windows shell after extraction:

```text
legacy\Install_Nymphs3D.bat -RepoBranch main -SkipWslConfig
```

Then choose:

1. `Create a new Ubuntu for Nymphs3D on another drive`
2. the target drive, for example `D:`

Advanced non-interactive example:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File .\legacy\Install_Nymphs3D.ps1 -RepoBranch main -CreateNewDistro -InstallDrive D: -SkipWslConfig
```

## Expected Result

For the create-new flow, the installer should:

- detect that WSL already exists
- offer the existing-vs-new choice
- ask for the Windows drive when `Create new` is selected
- create a separate Ubuntu distro on that drive using WSL `--location`
- stop and ask the user to open that distro once if first-run Linux user setup
  is still required
- continue the Nymphs3D backend install against that new distro only

## Current Limitations

- the create-new path depends on a WSL version that supports `wsl --install`
  with `--location`
- the create-new path currently auto-picks the first unused official Ubuntu WSL
  distro name available on the machine
- if all supported official Ubuntu distro names are already installed, the
  script will stop and tell the user
- this branch has not been end-to-end validated from a real Windows PowerShell
  session yet

## What To Watch During Testing

- whether the existing `C:` Ubuntu remains untouched
- whether the drive-choice prompt is clear for a beginner
- whether the selected drive gets the new distro folder
- whether the installer still rewrites `.wslconfig` when `-SkipWslConfig` is
  omitted
- whether first-run Ubuntu user creation messaging is clear
- whether reruns keep targeting the new distro instead of falling back to the
  old one

Record beginner-friction feedback in:

- `total_noob_test_notes.md`

## Rollback

If this test branch fails, the branch can be deleted.

That does not affect `main` unless these changes are merged.

If the branch was pushed:

```text
git push origin --delete installer_test_drive_choice
```

If the branch only exists locally:

```text
git branch -D installer_test_drive_choice
```

Do not delete the branch until any wanted changes have been copied or merged.
