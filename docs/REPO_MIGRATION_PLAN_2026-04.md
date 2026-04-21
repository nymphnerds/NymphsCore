# Repo Migration Plan

Date: 2026-04-21

## Status

This migration is complete for the active repos and installer defaults.

Completed outcomes:

- `NymphsCore`, `Hunyuan3D-2`, and `Nymphs2D2` are now under `nymphnerds`
- Manager source scripts and bundled publish scripts point at `nymphnerds`
- local runtime repos were repointed to `nymphnerds`
- the helper checkout at `/opt/nymphs3d/Nymphs3D` was repointed to `nymphnerds/NymphsCore`
- repair/install validation succeeded without active `Babyjawz` pulls

Validation checkpoints:

- [installer-run-20260421-101315.log](/home/nymph/logs/installer-run-20260421-101315.log): exposed original remote mismatches
- [installer-run-20260421-103334.log](/home/nymph/logs/installer-run-20260421-103334.log): backend repos fixed; helper repo still had permission issues
- [installer-run-20260421-104532.log](/home/nymph/logs/installer-run-20260421-104532.log): helper repo fetch issue resolved; only a temporary dirty file mode remained, later cleaned

Open follow-up:

- legacy `Nymphs3D` naming in paths, env vars, and helper checkout location still exists and can be cleaned up later as a separate refactor

## Goal

Consolidate the active backend repos under `nymphnerds`, then clean up naming without breaking the Manager install/update flow. The owner-consolidation part is now done; the remaining work is optional naming cleanup.

Working owner assumption for this plan:

- `Hunyuan3D-2` should live under `nymphnerds`
- `Nymphs2D2` should also live under `nymphnerds`
- `NymphsCore` remains the main monorepo
- `NymphsExt` remains the Blender extension feed repo

## What NymphsCore Actively Relies On

The monorepo itself has no git submodules, but the Manager still installs and verifies external backend repos.

Hard runtime dependencies today:

- `Hunyuan3D-2`
- `Z-Image` backend, currently sourced from the `Nymphs2D2` repo
- `TRELLIS.2`

Optional subsystem:

- `Nymphs-Brain`

Not a runtime dependency:

- `NymphsExt` is the Blender extension distribution repo
- `Nymphs3D-Blender-Addon` appears to be historical/archive context, not a live dependency

## Current Repo/Owner State

Current live defaults inside `NymphsCore` should point at:

- `https://github.com/nymphnerds/NymphsCore.git`
- `https://github.com/nymphnerds/Hunyuan3D-2.git`
- `https://github.com/nymphnerds/Nymphs2D2.git`
- `https://github.com/microsoft/TRELLIS.2.git`

Current local checkouts in this workspace:

- `/home/nymph/NymphsCore` -> `nymphnerds/NymphsCore`
- `/home/nymph/Hunyuan3D-2` -> `nymphnerds/Hunyuan3D-2`
- `/home/nymph/Z-Image` -> `nymphnerds/Nymphs2D2`
- `/home/nymph/TRELLIS.2` -> `microsoft/TRELLIS.2`

## Recommended Target Repo Map

### Keep as-is

- `nymphnerds/NymphsCore`
- `nymphnerds/NymphsExt`
- upstream `microsoft/TRELLIS.2`

### Move now

- `Hunyuan3D-2` under `nymphnerds` (done)
- `Nymphs2D2` under `nymphnerds` (done)

### Replace or retire

- legacy `Nymphs3D` helper repo

Recommendation:

- do not treat `Nymphs3D` as the long-term primary repo anymore
- either make the bootstrap/install flow clone `NymphsCore` directly
- or create a deliberately named thin helper repo such as `nymphnerds/NymphsCore-Helper`

### Archive only

- legacy `Nymphs3D-Blender-Addon` repo

Recommendation:

- keep it as an archive/backup
- do not make it part of the active product surface again unless you specifically want to preserve it under the new account

## Naming Recommendation

Use a two-stage rename strategy.

### Stage 1: owner move with minimal repo renames

Do this first:

- `nymphnerds/Hunyuan3D-2`
- `nymphnerds/Nymphs2D2`

Why:

- `Hunyuan3D-2` is tightly coupled to upstream model family naming, local folder names, and code expectations
- `Nymphs2D2` is still the repo identity embedded in scripts and env var names even though the product/backend label is now `Z-Image`

### Stage 2: product-surface cleanup after the move works

Consider later:

- `nymphnerds/Nymphs2D2` -> `nymphnerds/Z-Image`
- eliminate `Nymphs3D` helper naming from scripts, paths, project names, and installer payloads

Why stage this:

- changing owner is easy to verify
- changing owner and repo name and WSL path conventions at the same time creates a much larger blast radius

## Safest Migration Order

1. Transfer `Hunyuan3D-2` to `nymphnerds/Hunyuan3D-2` without renaming the repo. Done.
2. Transfer `Nymphs2D2` to `nymphnerds/Nymphs2D2` without renaming the repo. Done.
3. Patch `NymphsCore` so all default URLs stop pointing at `nymphnerds` directly instead of relying on legacy redirects. Done.
4. Rebuild or refresh the checked-in published Manager payload so the bundled scripts match the source scripts. Done.
5. Test fresh install, repair/update, and managed-repo update check against the new owner URLs. Done.
6. After that is stable, decide whether to:
   - replace the `Nymphs3D` helper repo bootstrap with `NymphsCore`
   - rename `Nymphs2D2` to `Z-Image`
   - perform broader internal `Nymphs3D` -> `NymphsCore` naming cleanup

## Consolidated Owner Notes

This consolidated-owner layout is feasible.

- `nymphnerds/Hunyuan3D-2` fits the main product family better because `NymphsCore` and `NymphsExt` already live there
- `nymphnerds/Nymphs2D2` keeps the active backend repos together with `NymphsCore` and `NymphsExt`
- there is no technical requirement that all managed repos share the same GitHub owner, but a single-owner layout is simpler here

For the Stage 1 patch, the intended defaults would become:

- `NYMPHS3D_H2_REPO_URL=https://github.com/nymphnerds/Hunyuan3D-2.git`
- `NYMPHS3D_N2D2_REPO_URL=https://github.com/nymphnerds/Nymphs2D2.git`

## Why `Nymphs3D` Should Not Be the First Rename

The old helper repo name is still baked into multiple installer/bootstrap paths:

- WSL bootstrap clones `Nymphs3D`
- `/opt/nymphs3d/Nymphs3D` is still a real path
- several scripts use `NYMPHS3D_*` env vars
- builder-distro creation still assumes the helper repo name

That means `Nymphs3D` is the most coupled rename and should be handled after the repo-owner move is already working.

## Files That Must Change For Stage 1

These are the primary source-of-truth files that were changed because they pointed at legacy owner URLs or old repo assumptions.

Core docs and metadata:

- `README.md`
- `Blender/Addon/blender_manifest.toml`

Manager source scripts:

- `Manager/scripts/common_paths.sh`
- `Manager/scripts/install_nymphs2d2.sh`
- `Manager/scripts/check_managed_repo_updates.sh`
- `Manager/scripts/install_one_click_windows.ps1`
- `Manager/scripts/create_builder_distro.ps1`
- `Manager/scripts/prepare_fresh_builder_distro.sh`
- `Manager/legacy/Install_Nymphs3D.ps1`

Bundled/published script copies that must match the source scripts:

- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/common_paths.sh`
- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/install_nymphs2d2.sh`
- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/check_managed_repo_updates.sh`
- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/install_one_click_windows.ps1`
- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/create_builder_distro.ps1`
- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/prepare_fresh_builder_distro.sh`

## Files To Review For Stage 2 Naming Cleanup

These are not required for the first owner move, but they are where deeper `Nymphs3D` naming still survives.

Installer app/project naming:

- `Manager/apps/Nymphs3DInstaller/`
- `Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs` still carries `Nymphs3D2.tar` fallback candidates

WSL helper path assumptions:

- `Manager/scripts/import_base_distro.ps1`
- `Manager/scripts/run_finalize_in_distro.ps1`
- `Manager/scripts/install_nymphs_brain.sh`
- `Manager/scripts/prepare_fresh_builder_distro.sh`
- `Manager/scripts/create_builder_distro.ps1`

Legacy naming that may or may not be worth changing:

- `/opt/nymphs3d/Nymphs3D`
- `NYMPHS3D_*` env vars
- `Nymphs3DInstaller` project/namespace names
- old `Nymphs3D2.tar` fallback paths

Recommendation:

- leave env vars and internal path names alone during Stage 1
- only rename those after fresh install/update tests pass against the new owner URLs

## Decision Table

### `Hunyuan3D-2`

Recommendation:

- move to `nymphnerds`
- keep repo name

Reason:

- lowest risk
- aligns the main 3D backend fork with the main `NymphsCore` product org
- stays aligned with upstream model family naming

### `Nymphs2D2`

Recommendation:

- move owner now
- keep repo name for Stage 1
- optionally rename to `Z-Image` in Stage 2

Reason:

- product surface says `Z-Image`
- code and env vars still say `N2D2`

### `Nymphs3D`

Recommendation:

- do not lead with a rename
- replace it as a bootstrap dependency after owner migration succeeds

Reason:

- it is the most coupled legacy name in installer paths and helper scripts

### `Nymphs3D-Blender-Addon`

Recommendation:

- archive only

Reason:

- not part of the live runtime dependency graph anymore

## Verification Checklist After Stage 1

Run these checks after patching `NymphsCore`:

1. Managed repo update check resolves the new `nymphnerds` URLs.
2. A clean install path clones `nymphnerds/Hunyuan3D-2`.
3. A clean install path clones `nymphnerds/Nymphs2D2`.
4. Existing installs can run repair/update without trying to fetch from legacy-owner URLs.
5. Published Manager payload scripts match the source scripts.
6. Docs no longer tell users to preserve legacy repos as active dependencies.

## Practical Next Step

The next implementation pass in `NymphsCore` should do only this:

- update all default legacy URLs to `nymphnerds`
- update docs so legacy repos are described as historical only
- leave repo names, env var names, and WSL helper paths untouched for now

That gives a clean owner migration with the smallest possible failure surface.
