# Handoff 2026-04-08 Installer Backend Updater

This handoff captures the tested state after the managed backend updater pass,
the existing-install update UX work, and the `Hunyuan3D-2.1` generated-cache
ignore fix.

## Repo State

- repo: `Babyjawz/Nymphs3D`
- branch: `main`
- latest pushed commit:
  - `e29797c`
  - `Polish installer backend update UX`

Related backend fork:

- repo: `Babyjawz/Hunyuan3D-2.1`
- latest pushed commit for this session's updater fix:
  - `f47a33c`
  - `Ignore generated texture cache output`

## What Was Implemented

### Managed Backend Repo Updater

The installer-side runtime flow now supports managed repo inspection and safe
fast-forward updates for:

- `Hunyuan3D-2`
- `Hunyuan3D-2.1`

Key behavior:

- clean repos on `main` can be fast-forwarded
- dirty, detached, diverged, or mismatched repos are skipped instead of being
  overwritten
- the installer still treats packaged helper scripts as authoritative
- the in-distro helper repo is informational only during update checks

Files involved:

- `scripts/managed_repo_utils.sh`
- `scripts/check_managed_repo_updates.sh`
- `scripts/finalize_imported_distro.sh`
- `scripts/run_finalize_in_distro.ps1`
- `scripts/install_hunyuan_2_1.sh`

### Existing-Install Update UX

The WPF installer app now has an existing-install update-check path that is
much more product-like than the first raw-script version.

Implemented behavior:

- `System Check` shows an `Existing install detected` section when the managed
  `Nymphs3D2` distro already exists
- that section exposes a `Check for Updates` button
- the check runs through a direct `wsl.exe` command instead of the heavier
  finalize wrapper
- the user-facing result is translated into plain-English summary text
- when updates are available, the primary action label becomes `Update`
- finish state can now distinguish:
  - `Install Complete`
  - `Update Complete`
  - `Already Up To Date`

Files involved:

- `apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `apps/Nymphs3DInstaller/Views/MainWindow.xaml`

### `Hunyuan3D-2.1` False Dirty-State Fix

The earlier update-check testing surfaced that generated texture output under
`gradio_cache_tex/` was still appearing as an untracked repo change.

This was fixed in the backend fork by adding:

- `gradio_cache_tex/`

to:

- `Hunyuan3D-2.1/.gitignore`

This prevents generated texture-cache output from blocking installer updates.

## What Was Tested

Tested locally through the Windows installer UI:

- existing install detection
- visible `Check for Updates` entry point
- update summary phrasing for noob users
- clean-but-behind repo state showing a real available update
- successful end-to-end backend update through the installer

Additional validation:

- `Hunyuan3D-2.1` was manually placed one commit behind `origin/main`
- installer update check correctly surfaced an available update
- installer update flow brought it back to `origin/main`

## Important Current Limitation

The source changes are pushed, but the tracked packaged installer zip in git was
not refreshed as part of the final source push for `e29797c`.

That means:

- GitHub source is current
- but if you want the tracked distribution artifact itself to match the latest
  installer source commit, rebuild and refresh:
  - `apps/Nymphs3DInstaller/publish/win-x64/Nymphs3DInstaller-win-x64.zip`

This is the main remaining release-side follow-up.

## Recommended Next Steps

1. Rebuild the installer package from Windows PowerShell against `main`.
2. If you want GitHub to carry the refreshed package artifact too, commit the
   rebuilt `Nymphs3DInstaller-win-x64.zip`.
3. Do one final UI sanity pass on the refreshed build:
   - fresh install path
   - existing install with no updates
   - existing install with updates available
   - final success screen wording
4. If that looks good, treat this installer update flow as the new baseline.

## Safe Mental Model Going Forward

- installer package updates installer logic
- installer `Update` flow updates backend repos
- managed backend repo set for this updater is:
  - `Hunyuan3D-2`
  - `Hunyuan3D-2.1`
- addon repos are not part of this installer updater path
