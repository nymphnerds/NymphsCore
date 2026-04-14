# Installer Issue Changelog 2026-04-05

This document records the real installer failures found during a first clean-machine Windows + WSL install test and the fixes applied during the same session.

Test context:

- fresh-ish Windows user path
- Ubuntu installed through the one-click flow
- Blender user perspective, not a Linux-first developer flow
- final result: successful full install and verification

Final observed success state:

- post-install verification passed
- `Hunyuan3D-2` venv uses Python `3.10`
- `Hunyuan3D-2.1` venv uses Python `3.11`
- core repos, imports, and prefetched model snapshots verified

## Issue Log

### 1. Packaged easy-install archive was stale

Symptom:

- the beginner-facing `assets/Install_Nymphs3D.7z` still contained older bootstrap files and caused confusion about whether fixes were live

Fix:

- refreshed the packaged archive

Commit:

- `b33c380` Refresh packaged installer archive

### 2. Ubuntu distro detection was too brittle

Symptom:

- installer kept saying Ubuntu was not installed
- `wsl --install -d Ubuntu` replied that the distro already existed

Fix:

- broadened distro detection and logged visible distros

Commits:

- `d6aa4e6` Fix Ubuntu distro detection in one-click installer
- `66fd11c` Harden WSL distro detection in installer
- `59402f4` Log Ubuntu readiness probe in installer

### 3. Generated `.wslconfig` was invalid

Symptom:

- WSL printed errors like:
  - `Invalid memory string 'GB'`
  - `Invalid integer value '' for key 'wsl2.processors'`

Fix:

- corrected generated `.wslconfig` values

Commit:

- `f6f15b8` Fix generated .wslconfig values

### 4. WSL-side install was blocked by interactive `sudo`

Symptom:

- Windows-side wrapper reached `Launching WSL-side install...`
- install then failed without surfacing the real Ubuntu prompt behavior

Fix:

- allowed the WSL-side install to run interactively so Ubuntu password prompts could work normally

Commit:

- `6c87208` Allow interactive sudo during WSL install

### 5. Installer logging crashed on empty lines

Symptom:

- PowerShell failed with:
  - `Write-InstallLog : Cannot bind argument to parameter 'Message' because it is an empty string.`

Fix:

- allowed empty installer log lines

Commit:

- `6e16f0c` Allow empty installer log lines

### 6. Embedded bash was being mangled by PowerShell

Symptom:

- PowerShell tried to interpret bash expressions like `$(date +%Y%m%d-%H%M%S)`
- install failed with parameter binding errors about `Date`

Fix:

- stopped PowerShell from interpolating the embedded WSL bash script

Commits:

- `4d7577a` Escape bash date substitution in installer
- `8d6dcca` Stop PowerShell interpolation in WSL install command

### 7. Ubuntu 24.04 Python package detection was wrong

Symptom:

- installer tried and failed to install:
  - `python3.10`
  - `python3.10-venv`
  - `python3.10-dev`

Fix:

- corrected the logic that decides when extra package sources are needed for Python `3.10` on Ubuntu `24.04`

Commit:

- `6c87da0` Fix Python 3.10 package detection on Ubuntu 24.04

### 8. Lock install tried to fetch repo-local extension packages from pip

Symptom:

- failures like:
  - `No matching distribution found for custom_rasterizer==0.1`

Fix:

- excluded local extension packages from raw lockfile pip install and let the installer build them from the repo afterward

Commit:

- `ab2229e` Skip local extension packages in Hunyuan lock installs

### 9. Shell entry scripts were missing executable bits

Symptom:

- direct Ubuntu-side run hit `Permission denied`

Fix:

- marked WSL install entry scripts executable in git

Commit:

- `030f400` Mark WSL install entry scripts executable

### 10. Hunyuan 2.1 could not share the old Python 3.10 assumption

Symptom:

- 2.1 dependency install hit unavailable wheels and compatibility problems

Fix:

- kept `Hunyuan3D-2` on Python `3.10`
- moved `Hunyuan3D-2.1` to Python `3.11`

Commit:

- `85c5a69` Use Python 3.11 for Hunyuan 2.1 install

### 11. `bpy==4.0.0` pin was stale

Symptom:

- pip could not install `bpy==4.0.0`
- available wheels started at later versions

Fix:

- filtered the stale lockfile pin and installed an available `bpy` `4.2.x` build afterward

Commit:

- `b80d802` Install available bpy for Hunyuan 2.1

### 12. PyTorch CUDA wheels were being installed from the wrong index

Symptom:

- pip could not resolve:
  - `torch==2.5.1+cu124`
  - `torchvision==0.20.1+cu124`
  - `torchaudio==2.5.1+cu124`

Fix:

- removed those packages from the plain lockfile install
- installed them separately from the PyTorch CUDA `12.4` wheel index

Commit:

- `2f4f803` Install PyTorch CUDA wheels separately for Hunyuan 2.1

### 13. `custom_rasterizer` build isolation hid the already-installed torch package

Symptom:

- local editable extension build failed with:
  - `ModuleNotFoundError: No module named 'torch'`

Fix:

- disabled build isolation for that local extension install

Commit:

- `9f61bd1` Disable build isolation for 2.1 rasterizer

## Smoke Test Notes

Important runtime distinction:

- normal `2.1` API launcher uses port `8080`
- smoke test for `2.1` uses port `8091`
- smoke test for `2mv` uses port `8090`

So seeing `8091` during smoke testing is expected and does not mean the normal runtime port changed.

Relevant scripts:

- `scripts/run_hunyuan_2_1_api.sh`
- `scripts/run_hunyuan_2_mv_api.sh`
- `scripts/smoke_test_server.sh`
- `scripts/verify_install.sh`

## Outcome

By the end of the session, the tested machine reached:

- successful one-click install
- successful post-install verification
- successful creation of both backend repos and venvs
- correct split:
  - `Hunyuan3D-2` on Python `3.10`
  - `Hunyuan3D-2.1` on Python `3.11`

This means the installer path is materially more robust than the start-of-session state, especially for a non-technical Windows user path.

## Addendum: Base-Distro App Rewrite 2026-04-06

After the original one-click installer hardening pass, the project direction
changed in a significant way.

The new installer target is now:

- one simple Windows app

## Addendum: Fresh `Nymphs3D2` Reinstall Fixes 2026-04-06

Later testing in the WPF installer flow found a second class of issue:

- a fresh `Nymphs3D2` install could finish, but still open as `root`
- fresh shells could still inherit stale runtime exports pointing at:
  - `/opt/nymphs3d/runtime/...`
- this no longer matched the actual home-based runtime layout that was behaving
  correctly in the working Ubuntu install

Fixes applied:

- `scripts/import_base_distro.ps1`
  - now normalizes `/etc/profile.d/nymphs3d.sh`
  - now restarts the distro after writing `/etc/wsl.conf`
- `scripts/finalize_imported_distro.sh`
  - now rewrites the same runtime shell profile during finalize
- `scripts/prepare_fresh_builder_distro.sh`
  - now writes home-based runtime exports instead of stale `/opt` runtime exports

Observed corrected fresh-install state:

- `wsl -d Nymphs3D2 -- whoami`
  - returns `nymphs3d`
- `NYMPHS3D_RUNTIME_ROOT`
  - no longer forces `/opt/nymphs3d/runtime`
- runtime repos and venvs now align with the working home-based layout

Observed no-model install footprint after the fix:

- `D:\WSL\Nymphs3D2`
  - `39.4 GB`

This moved the main remaining problem away from installer layout correctness and
toward first-run launcher/addon model-pull behavior and progress visibility.
- a small prebuilt `Nymphs3D2` WSL base distro
- required runtime setup after import
- model prefetch optional during install
- later model downloads allowed from the launcher or Blender addon on first real use

### New Working Pieces

The following pieces now exist and have been tested at least once locally:

- fresh builder distro on `D:`
- export of a small base tar instead of cloning the full daily-use Ubuntu
- import of a dedicated `Nymphs3D2` distro
- WPF installer app scaffold and multi-step install flow
- dedicated managed Linux user:
  - `nymphs3d`
- local launcher updates:
  - explicit API vs Gradio modes
  - explicit `2.1 Texture-Only API` mode
  - WSL distro picker
  - automatic use of the selected distro's default Linux user

### New Verification Helper

A fresh-install checker was added to make post-install validation less manual:

- `dev/5_CHECK_FRESH_INSTALL.bat`
- `scripts/check_fresh_install.ps1`

This checker verifies:

- managed distro exists
- default Linux user
- runtime folders
- expected Python versions
- CUDA path
- whether models were prefetched or skipped

### Current Known Blocker

The main release blocker found during the new app-based install flow is:

- a fresh `Nymphs3D2` install can currently end with repos present under
  `/opt/nymphs3d/runtime`, but the backend venvs not found there afterward

Current diagnosis:

- the base image expects `/opt/nymphs3d/runtime`
- parts of the runtime install flow still fall back to `${HOME}` unless the
  runtime root is made explicit everywhere

Current status:

- a fix is in progress in `scripts/common_paths.sh`
- the new installer app should not be treated as release-ready until this is
  re-tested cleanly
