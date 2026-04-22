# No-Hunyuan-2mv Edition Plan

This document turns the backend audit into a concrete implementation plan for a NymphsCore edition that does not ship `Hunyuan 2mv`.

## Current Lite Branch Status

The permanent Lite branch is now `nymphscore-lite`.

Current product shape:

- WSL distro name: `NymphsCore_Lite`
- no `Hunyuan 2mv` backend
- no prewarmed tar requirement
- local tarless Ubuntu bootstrap is the normal install/repair path
- optional prebuilt `NymphsCore.tar` remains only as a maintainer shortcut
- Manager `Runtime Tools` checks `Z-Image` and `TRELLIS.2`
- System Check no longer treats the tar/base package as a visible requirement
- optional `Nymphs-Brain` is separate from Blender backend runtime health

Recent Brain follow-up:

- Brain is plan-first for local model profiles
- a local `Plan` model can be used while `Act` remains external
- `Update Stack` refreshes installed Brain wrappers before updating LM Studio/Open WebUI
- `Stop Brain` is shown whenever any Brain service is running

## Recommendation

Treat this as a product-surface branch, not a runtime toggle.

Why:

- `Hunyuan 2mv` is part of the current baseline installer flow, Manager UI, packaged payload, addon runtime surface, and docs.
- The repo already assumes three local backend families in several places.
- A branch-level cleanup is simpler and safer than trying to make every `2mv` path conditional.

Recommended target shape:

- keep `TRELLIS.2` for shape and single-image texture work
- keep `Z-Image` for image generation and 4-view image creation
- remove `Hunyuan 2mv` install, launch, status, smoke-test, and docs surface
- remove or explicitly disable multiview-to-3D and multiview-guided retexture features until there is a replacement

## User-Facing Product Changes

If `Hunyuan 2mv` is removed, these features disappear unless they are rebuilt on another backend:

- multiview image-to-3D shape generation
- multiview-guided mesh retexture
- the dedicated `Hunyuan 2mv` runtime card in the Blender addon
- the dedicated `Hunyuan 2mv` runtime card in Manager `Runtime Tools`
- `2mv` smoke tests and status checks
- `Hunyuan3D-2` repo/model download footprint

Features that can stay:

- `TRELLIS.2` single-image shape generation
- `TRELLIS.2` single-image texturing
- `Z-Image` local image generation
- 4-view image generation as a raw image-prep tool, if you still want those outputs for future workflows

## Feasibility Summary

Overall feasibility: good.

Expected effort: medium.

Why it is feasible:

- `NymphsCore` does not contain the core `2mv` model implementation; it launches a separate managed repo.
- most coupling is at the integration layer: installer scripts, addon backend selection, Manager status/test UI, and docs

Why it is not tiny:

- the installer baseline currently assumes `Hunyuan3D-2` is a required core runtime
- the Manager UI and packaged payload mirror the source scripts
- the Blender addon still exposes explicit `2mv` runtime and texturing controls

## Recommended Strategy

Do this in phases.

### Phase 1: Remove Installer And Manager Dependency

Goal:

- make the managed runtime install and verify cleanly with only `Z-Image` and `TRELLIS.2`

Files to change:

- `Manager/scripts/install_all.sh`
- `Manager/scripts/finalize_imported_distro.sh`
- `Manager/scripts/common_paths.sh`
- `Manager/scripts/prefetch_models.sh`
- `Manager/scripts/runtime_tools_status.sh`
- `Manager/scripts/verify_install.sh`
- `Manager/scripts/smoke_test_server.sh`
- `Manager/scripts/check_managed_repo_updates.sh`
- `Manager/scripts/audit_working_install.sh`
- `Manager/scripts/check_fresh_install.ps1`
- `Manager/scripts/prepare_fresh_builder_distro.sh`
- `Manager/scripts/create_builder_distro.ps1`
- `Manager/scripts/import_base_distro.ps1`
- `Manager/scripts/run_finalize_in_distro.ps1`

Packaged payload that must match source:

- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/*`

Concrete changes:

- stop calling `install_hunyuan_2.sh` from the baseline install flow
- remove `Hunyuan3D-2` repo path exports if they are no longer needed
- remove `tencent/Hunyuan3D-2` and `tencent/Hunyuan3D-2mv` model prefetch checks
- remove `2mv` from runtime status output
- remove `2mv` from smoke-test CLI options
- remove `verify_hunyuan2`
- remove fresh-install expectations that require `~/Hunyuan3D-2`
- remove managed-repo update reporting for `Hunyuan3D-2`
- recalculate audit/footprint messaging to match the new baseline

Risk:

- low to medium

Main risk:

- forgetting to update the packaged `publish/win-x64/scripts` copy after changing source scripts

### Phase 2: Remove Manager UI Surface

Goal:

- make the Windows Manager present only the backends that actually ship

Files to change:

- `Manager/apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `Manager/apps/Nymphs3DInstaller/Views/MainWindow.xaml`
- `Manager/apps/Nymphs3DInstaller/README.md`

Concrete changes:

- remove Hunyuan model/runtime size text from installer footprint copy
- remove `HunyuanRuntimeStatus`, `TestHunyuanCommand`, and related button labels
- stop expecting `"2mv"` in runtime status parsing
- remove the Hunyuan runtime card from the `Runtime Tools` screen
- update summaries like `Core backends look ready for smoke testing` so they mean `Z-Image` plus `TRELLIS.2`
- remove `2mv` from backend label helpers

Risk:

- low

Main risk:

- leaving stale strings that imply a missing backend is broken instead of intentionally removed

### Phase 3: Remove Blender Addon Runtime Surface

Goal:

- make the addon behave like a two-runtime product instead of a three-runtime product

Files to change:

- `Blender/Addon/Nymphs.py`
- `Blender/Addon/README.md`
- `Blender/Addon/docs/USER_GUIDE.md`
- `docs/BLENDER_ADDON_USER_GUIDE.md`

Concrete changes:

- remove `2mv` from `SERVICE_LABELS`, `SERVICE_PROP_PREFIXES`, and `SERVICE_ORDER`
- remove `service_2mv_*` properties
- remove `repo_2mv_path` and `python_2mv_path`
- remove `launch_backend = BACKEND_2MV`
- remove `texture_backend = 2MV`
- remove `2mv` launch and stop shell composition
- remove `2mv` capability fallback handling
- remove the `Hunyuan 2mv` runtime configuration card
- remove Hunyuan-only texture options such as `texture_resolution_2mv`
- remove selected-mesh retexture path that requires multiview guidance

Important behavioral choice:

- either remove `MULTIVIEW` from `shape_workflow`
- or keep the UI visible but disable it with a clear `not available in this edition` message

Recommended choice:

- remove `MULTIVIEW` shape mode from the public surface for the no-2mv edition

Why:

- keeping a dead-end workflow will confuse users
- current multiview shape payload construction is tightly associated with the `2mv` family

Risk:

- medium

Main risks:

- state migration inside Blender if saved scenes still contain old enum values
- stale capability assumptions in fallback code paths

### Phase 4: Decide What To Do With 4-View MV Image Generation

This is a product decision, not a mechanical cleanup.

Current situation:

- 4-view image generation is produced by the image backend and stored in the addon MV slots
- those slots are then used heavily by the `2mv` multiview shape and retexture paths

Options:

- keep 4-view image generation as an image-only utility
- remove it from the public UI to avoid suggesting a missing downstream workflow

Recommended default:

- keep it only if you have a near-term follow-up plan for those images
- otherwise remove it from the main UI in the no-2mv edition

Reason:

- without multiview shape or multiview retexture, the feature becomes much less central

Risk:

- product confusion, not technical risk

### Phase 5: Rewrite Product Docs And Footprint Numbers

Files to change:

- `README.md`
- `Manager/README.md`
- `docs/FOOTPRINT.md`
- `docs/ARCHITECTURE.md`
- `docs/FEATURES.md`
- `docs/GETTING_STARTED.md`
- `docs/ABSOLUTE_BEGINNER_INSTALL_GUIDE.md`
- `Blender/Addon/README.md`
- `Blender/Addon/docs/USER_GUIDE.md`
- `docs/BLENDER_ADDON_USER_GUIDE.md`

Concrete changes:

- replace the `3 local AI services` story with a `2 local AI services` story
- remove `Hunyuan 2mv` feature claims and how-to flows
- update footprint estimates to remove:
  - `tencent/Hunyuan3D-2`
  - `tencent/Hunyuan3D-2mv`
  - `Hunyuan3D-2` repo checkout
  - `Hunyuan3D-2` venv
- rewrite install guidance so `Runtime Tools` only checks `Z-Image` and `TRELLIS.2`

Risk:

- low

Main risk:

- inconsistent docs making support harder

## Specific Integration Points

### Blender Addon

Key `2mv` integration points in `Blender/Addon/Nymphs.py`:

- service registry and labels around `SERVICE_LABELS` and `SERVICE_ORDER`
- WSL launch command composition for `api_server_mv.py`
- stop command composition for the `2mv` process
- `launch_backend` enum
- `texture_backend` enum
- `service_2mv_*` state properties
- `repo_2mv_path` and `python_2mv_path`
- shape payload path for `MULTIVIEW`
- texture payload path that requires `mv_image_front`
- Hunyuan-only texture options UI

This is the highest-risk code area because the file is large and stateful.

### Installer And Runtime Scripts

Key `2mv` integration points:

- `install_hunyuan_2.sh`
- `run_hunyuan_2_mv_api.sh`
- `run_hunyuan_2_gradio.sh`
- `prefetch_models.sh`
- `runtime_tools_status.sh`
- `verify_install.sh`
- `smoke_test_server.sh`

Important note:

- even if you stop calling these scripts, the repo will still look inconsistent until you remove the surrounding references

### Manager App

Key `2mv` integration points:

- installer size copy in `MainWindowViewModel.cs`
- runtime status parsing and summary logic
- smoke test command binding
- Hunyuan runtime card in `MainWindow.xaml`
- WSL environment exports in `InstallerWorkflowService.cs`

### Packaged Payload

The published installer bundles a script mirror under:

- `Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts`

If source changes are made, rebuild the release payload so the packaged scripts match.

## Suggested Implementation Order

1. Make the source installer scripts pass without `2mv`.
2. Remove Hunyuan from Manager runtime status and UI.
3. Rebuild the packaged `publish/win-x64` payload.
4. Remove the addon `2mv` runtime surface.
5. Decide whether `4-View MV` remains in the no-2mv edition.
6. Rewrite docs and footprint values last, once the product shape is stable.

## Validation Checklist

After the branch is cut, verify:

- source scripts no longer mention `install_hunyuan_2.sh`, `api_server_mv.py`, or `--backend 2mv`
- packaged `publish/win-x64/scripts` no longer mention those paths either
- Manager `Runtime Tools` shows only `Z-Image` and `TRELLIS.2`
- installer text no longer includes Hunyuan storage/download numbers
- Blender addon no longer exposes `Hunyuan 2mv` launch, shape, or texture controls
- Blender addon still performs `TRELLIS.2` shape generation
- Blender addon still performs `TRELLIS.2` single-image texture guidance
- `Z-Image` image generation still works
- all docs describe the same supported backend set

## Suggested Branch Name

Example:

- `no-hunyuan-2mv`

## Bottom Line

This is feasible and should be much easier than replacing `2mv` with a new backend.

The cleanest version is:

- installer baseline becomes `Z-Image + TRELLIS.2`
- Manager stops presenting Hunyuan as part of the product
- addon removes `2mv` and multiview-guided retexture
- docs and footprint numbers are rewritten to match

The hardest part is not backend code. The hardest part is making every integration layer agree on the new product shape.
