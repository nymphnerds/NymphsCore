# Handoff: NymphsCore Manager Progress

Date: `2026-04-13`

## Purpose

This note captures the current state of the `NymphsCore Manager` work after:

- renaming the WSL/runtime product surface from `Nymphs3D2` to `NymphsCore`
- renaming the Windows app surface to `NymphsCore Manager`
- building a new lean base tar
- wiring experimental `Hunyuan Parts` as an optional install path
- fixing a real import failure found during live test
- rebuilding and publishing the updated manager archive
- adding post-install model fetch and backend smoke-test actions in the manager UI

This is the right restart point for the next manager-focused session.

## Current Product Naming

Confirmed naming direction:

- addon/product brand: `Nymphs`
- managed WSL distro: `NymphsCore`
- managed Linux user: `nymph`
- Windows app: `NymphsCore Manager`

## What Is Working Now

### 1. Lean tar flow is working

A new lean base tar was built successfully.

Current local tar:

- `D:\WSL\NymphsCore.tar`

Observed size during this session:

- about `1.8G`

Current design intent:

- include helper repo + backend source layout
- exclude model caches
- exclude backend venvs
- exclude machine-specific generated output

### 2. Manager rebrand is in source

The installer app source now presents as `NymphsCore Manager` rather than the
older `Nymphs3D2` installer naming.

Main visible changes:

- app/window title says `NymphsCore Manager`
- distro default is `NymphsCore`
- Linux user default is `nymph`
- manager release artifact now uses `NymphsCoreManager`
- user-facing README/docs were updated to the new naming

### 3. WSL import defaults were corrected

The import flow now targets:

- distro name: `NymphsCore`
- user: `nymph`

And writes:

- `/etc/wsl.conf` with:
  - `[user] default=nymph`
  - `[boot] systemd=true`

### 4. Optional experimental Parts lane exists

The manager source now supports an install-time toggle for:

- `Experimental Parts Tools`

Current intent:

- core runtime remains:
  - `Hunyuan3D-2`
  - `Z-Image`
  - `TRELLIS.2`
- experimental optional runtime:
  - `Hunyuan3D-Part`

Parts is currently treated as:

- addon-facing
- experimental
- managed through its own dedicated env
- not a persistent server runtime

### 5. Real test failure was found and fixed

During a live manager test, the base distro import itself succeeded, but the
post-import path failed while writing `/etc/profile.d/nymphscore.sh`.

Observed failure:

- heredoc termination broke during `Normalize-DistroShellPaths`
- manager log showed:
  - `here-document ... wanted 'EOF'`
  - `unexpected EOF while looking for matching '"'`

Root cause:

- `scripts/import_base_distro.ps1` used a PowerShell double-quoted here-string
- PowerShell expanded `$HOME` before bash saw it
- that corrupted the bash heredoc payload

Fix:

- switched the relevant shell payload blocks to PowerShell single-quoted here-strings
- explicitly substituted only the Linux user placeholder afterward

Important:

- this was a manager/package script bug
- not a tar-content failure
- the tar import itself completed successfully

### 6. Post-install actions now exist in the manager

The manager source now includes post-install and existing-install actions for:

- `Fetch Models Now`
- `Test Hunyuan 2mv`
- `Test Z-Image`
- `Test TRELLIS.2`

Current behavior:

- the finish page shows these as the next step after a successful install
- the system-check page exposes `Fetch Models Now` for an existing managed install
- smoke tests run through the existing shell-based backend verifier path
- the finish page also shows a live log panel for these actions

## Current Published State

Source commits pushed during this work:

- `7c0da56` `Rename installer to NymphsCore Manager`
- `8048d29` `Add NymphsCore Manager archive`
- `cc17d76` `Update NymphsCore Manager docs`
- `ceac9f6` `Fix NymphsCore import script quoting`
- `1216104` `Refresh NymphsCore Manager archive`

Current published manager archive path in repo:

- `apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip`

Important packaging note:

- the manager zip does **not** include `NymphsCore.tar`
- the tar is still distributed separately

## What Is Not Done Yet

### 1. Full launcher merge is not implemented yet

The current manager now has smoke-test actions, but it is still primarily:

- installer
- updater
- runtime setup tool
- smoke-test frontend

It does **not** yet include the planned launcher replacement features such as:

- backend start/stop page
- live runtime control tab
- health probe controls for backend combinations
- integrated runtime log streaming UI

### 2. True repair mode is not implemented yet

The direction is agreed, but the full repair feature still needs work.

Still missing:

- backend-by-backend health inspection
- venv validation and selective rebuild
- cache/model integrity repair
- repo reuse logic beyond the current installer/update path
- explicit repair UI flow

### 3. Runtime control UI is only partially implemented

Now present:

- `Fetch Models Now`
- `Hunyuan 2mv` smoke-test button
- `Z-Image` smoke-test button
- `TRELLIS.2` smoke-test button
- finish-page live log reuse

Still missing:

- persistent start/stop controls
- combination test presets
- richer readiness summary UI
- launcher-style runtime session management

## Current Test Priorities

Next useful manual checks:

1. Confirm the rebuilt manager no longer fails during base import.
2. Confirm a fresh `NymphsCore` install reaches finalize successfully.
3. Confirm the manager behaves correctly with:
   - Parts enabled
   - Parts disabled
4. Confirm `Fetch Models Now` works against an existing install.
5. Confirm the finish-page smoke tests work for:
   - `Hunyuan 2mv`
   - `Z-Image`
   - `TRELLIS.2`
6. Confirm `nymph` is the default user after install.
7. Confirm `/etc/wsl.conf` contains `systemd=true`.
8. Confirm the extracted manager archive uses the fixed packaged `import_base_distro.ps1`.

## Best Next Implementation Pass

After basic retest passes, the next manager-specific build should focus on:

1. extend the smoke-test finish page into a fuller `Runtimes` / `Server Test` page
2. reuse launcher logging/process-watch patterns more directly
3. add persistent start/stop controls for:
   - `Hunyuan3D-2`
   - `Z-Image`
   - `TRELLIS.2`
4. keep `Hunyuan Parts` visible as experimental/install status only
5. add real repair inspection before broader UI churn

## Main Files Touched For This Progress Pass

Manager app:

- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/Nymphs3DInstaller.csproj`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/build-release.ps1`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/App.xaml.cs`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/Views/MainWindow.xaml`

Core scripts:

- `/home/nymphs3d/Nymphs3D/scripts/import_base_distro.ps1`
- `/home/nymphs3d/Nymphs3D/scripts/run_finalize_in_distro.ps1`
- `/home/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh`
- `/home/nymphs3d/Nymphs3D/scripts/create_builder_distro.ps1`
- `/home/nymphs3d/Nymphs3D/scripts/prepare_fresh_builder_distro.sh`
- `/home/nymphs3d/Nymphs3D/scripts/prepare_base_distro_export.sh`
- `/home/nymphs3d/Nymphs3D/scripts/install_hunyuan_parts.sh`
- `/home/nymphs3d/Nymphs3D/scripts/smoke_test_server.sh`
- `/home/nymphs3d/Nymphs3D/scripts/verify_install.sh`

Docs:

- `/home/nymphs3d/Nymphs3D/README.md`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/README.md`
- `/home/nymphs3d/Nymphs3D/docs/base_distro_v2_strategy.md`
- `/home/nymphs3d/Nymphs3D/docs/windows_installer_app_v1_plan.md`

## Practical Recommendation

Do not start the next session by redesigning the manager UI again.

The most valuable next information is:

- whether the fixed manager now installs cleanly
- whether the packaged import script is stable across reruns
- whether the optional Parts lane behaves predictably
- which launcher/runtime controls should be brought over first
