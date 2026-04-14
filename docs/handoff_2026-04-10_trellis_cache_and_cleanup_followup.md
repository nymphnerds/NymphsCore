# Handoff 2026-04-10 TRELLIS Cache And Cleanup Follow-Up

Date: `2026-04-10`  
Primary branch family: `main`

## Purpose

This note captures the current local state after:

- renaming the image backend repo from `~/Nymphs2D2` to `~/Z-Image`
- removing leftover `ComfyUI-*` repos
- shifting TRELLIS toward shared Hugging Face cache usage
- tightening export behavior so generated outputs do not get baked into a base distro tar

It is the right restart point if another session needs to finish the TRELLIS
cache cleanup aftermath, verify the optional TRELLIS optimization path, or keep
cleaning the helper repo toward a clearer marketed addon + helper-backend split.

## What Changed

### 1. `Z-Image` is now the real local repo path

Local repo move:

- old: `/home/nymphs3d/Nymphs2D2`
- new: `/home/nymphs3d/Z-Image`

What was updated:

- runtime-facing backend labels now say `Z-Image`
- helper scripts now prefer `~/Z-Image`
- addon default path now prefers `~/Z-Image`
- config now accepts preferred `Z_IMAGE_*` env vars while still accepting legacy `NYMPHS2D2_*` names as fallbacks

Important compatibility note:

- old env names were not hard-removed
- this was done to avoid snapping the launcher/addon/helper flow in one shot

Main touched files:

- `/home/nymphs3d/Z-Image/api_server.py`
- `/home/nymphs3d/Z-Image/config.py`
- `/home/nymphs3d/Z-Image/model_manager.py`
- `/home/nymphs3d/Z-Image/README.md`
- `/home/nymphs3d/Nymphs3D/scripts/common_paths.sh`
- `/home/nymphs3d/Nymphs3D/scripts/install_all.sh`
- `/home/nymphs3d/Nymphs3D/scripts/finalize_imported_distro.sh`
- `/home/nymphs3d/Nymphs3D/scripts/check_fresh_install.ps1`
- `/home/nymphs3d/Nymphs3D-Blender-Addon/Nymphs3D2.py`

Published installer script copies were also synced under:

- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/publish/win-x64/scripts`

### 2. Old `ComfyUI-*` repos were removed

Removed local leftovers:

- `/home/nymphs3d/ComfyUI-Env-Manager`
- `/home/nymphs3d/ComfyUI-GeometryPack`
- `/home/nymphs3d/ComfyUI-Pulse-MeshAudit`
- `/home/nymphs3d/ComfyUI-Trellis2`

They were checked first against the active stack and had no live references in:

- `Nymphs3D`
- `Z-Image`
- `TRELLIS.2`
- `Hunyuan3D-2`
- `Nymphs3D-Blender-Addon`

Rough reclaimed space:

- about `1.2G`

### 3. Export cleanup was corrected

Problem:

- output directories such as `TRELLIS.2/output` and `Z-Image/outputs` would
  have ended up in the exported distro tar unless manually cleaned
- the first fix removed the whole directories, which was too blunt if runtime
  code expected the path to exist

Final fix:

- export prep now empties output/cache directories instead of deleting the dirs
  themselves
- transient junk such as `__pycache__` is still removed outright

Touched files:

- `/home/nymphs3d/Nymphs3D/scripts/prepare_base_distro_export.sh`
- `/home/nymphs3d/Nymphs3D/apps/Nymphs3DInstaller/publish/win-x64/scripts/prepare_base_distro_export.sh`

Current intended export behavior:

- backend venvs are removed by default before export
- generated outputs are not supposed to be baked into the tar
- runtime output directories should still exist after install

### 4. README and roadmap were updated

README changes:

- runtime layout now says `~/Z-Image`
- added explicit runtime/export notes:
  - base tar does not normally ship backend venvs
  - `flash-attn` is optional
  - `sdpa` is the stable TRELLIS fallback
  - generated outputs should not be baked into the tar

Touched file:

- `/home/nymphs3d/Nymphs3D/README.md`

ROADMAP changes:

- corrected managed runtime path to `~/Z-Image`
- added a dedicated future task to write a thorough system requirements doc for
  addon marketing

Touched file:

- `/home/nymphs3d/Nymphs3D/ROADMAP.md`

### 5. TRELLIS now uses shared Hugging Face cache as the canonical model source

Intent:

- keep model storage consistent with the rest of the HF-backed stack
- stop treating `~/TRELLIS.2/models/trellis2` as the long-term canonical store
- make `microsoft/TRELLIS.2-4B` the source of truth

What changed:

- TRELLIS wrapper code now treats `microsoft/TRELLIS.2-4B` as the canonical model id
- prefetch/verify/smoke/install scripts were updated to target the shared HF cache model path conceptually
- `server_info` now reports the canonical TRELLIS model id rather than the repo-local model bundle path

Main touched files:

- `/home/nymphs3d/TRELLIS.2/scripts/trellis_official_common.py`
- `/home/nymphs3d/TRELLIS.2/scripts/api_server_trellis.py`
- `/home/nymphs3d/TRELLIS.2/scripts/run_official_image_to_3d.py`
- `/home/nymphs3d/TRELLIS.2/scripts/run_official_shape_only.py`
- `/home/nymphs3d/Nymphs3D/scripts/prefetch_models.sh`
- `/home/nymphs3d/Nymphs3D/scripts/verify_install.sh`
- `/home/nymphs3d/Nymphs3D/scripts/smoke_test_server.sh`
- `/home/nymphs3d/Nymphs3D/scripts/install_trellis.sh`

Published installer copies were also updated.

Final state:

- canonical model id resolves to `microsoft/TRELLIS.2-4B`
- shared HF cache contains the full TRELLIS weight bundle
- repo-local TRELLIS models were removed:
  - `/home/nymphs3d/TRELLIS.2/models`
- runtime now resolves against the shared HF cache, not the old repo-local bundle

Observed resolver behavior after cleanup:

- `resolve_model_reference()` -> `microsoft/TRELLIS.2-4B`
- `resolve_runtime_model_reference()` -> `microsoft/TRELLIS.2-4B`
- `resolve_local_model_root(local_files_only=True)` -> shared HF snapshot path under:
  - `/home/nymphs3d/.cache/huggingface/hub/models--microsoft--TRELLIS.2-4B`

### 6. TRELLIS smoke validation passed after the final cache migration

Verified locally:

- changed TRELLIS Python files compile
- changed shell scripts pass `bash -n`
- `./scripts/smoke_test_server.sh --backend trellis --timeout 120` passed after
  the final cache-backed cleanup

Additional verified state:

- shared TRELLIS HF cache size ended around `16G`
- `/home/nymphs3d/TRELLIS.2` dropped to roughly `1.4G` after removing the old local model bundle
- changelog was updated after the TRELLIS cache cleanup

## Follow-Up Results

### TRELLIS HF-cache migration is complete

Completed steps:

- full `microsoft/TRELLIS.2-4B` cache fetch finished successfully
- runtime resolution was verified against the shared HF cache
- `/home/nymphs3d/TRELLIS.2/models` was removed
- TRELLIS smoke test passed after that removal
- no additional duplicate TRELLIS model/cache location remained to delete

### `flash-attn` optimization work completed successfully

What happened:

- the original broad multi-arch build was stopped because it was compiling for far more GPU targets than this machine needs
- installer logic was updated to auto-detect local compute capability when `NYMPHS3D_TRELLIS_CUDA_ARCH_LIST` is unset
- this machine resolved to compute capability `8.9` for the RTX 4080 SUPER
- a targeted rebuild was started for `TORCH_CUDA_ARCH_LIST=8.9`
- that targeted build finished successfully

Final installed result:

- package: `flash-attn 2.8.3`
- built wheel:
  - `flash_attn-2.8.3-cp310-cp310-linux_x86_64.whl`
- wheel size: roughly `242M`
- installed inside:
  - `/home/nymphs3d/TRELLIS.2/.venv`

Relevant local session history:

- old broad build session: `36533`
- targeted build session: `32697`

- current base-distro export behavior removes backend venvs by default
- so this compiled `flash-attn` wheel is not supposed to end up in the exported
  base tar unless the product later chooses to ship prebuilt backend venvs
- in the current install model, users would only compile `flash-attn` locally if
  the TRELLIS install path enables it on their machine

Current product stance:

- `flash-attn` is optional
- `sdpa` is the working stable fallback
- TRELLIS currently works without waiting for this build to finish

Recommended next decision after current cleanup work:

- if the goal is product reliability, do not make `flash-attn` mandatory
- if optimization work continues beyond the current targeted source build, move
  toward a known-good prebuilt wheel strategy for the exact Python / torch /
  CUDA stack

Product relevance:

- README now states that `flash-attn` is optional
- `sdpa` remains the stable TRELLIS fallback
- if a later product decision keeps `flash-attn`, a targeted per-GPU-arch build
  or prebuilt wheel strategy would be cleaner than the current broad source build

## Current Local Layout

Active repos still intended in the working tree:

- `/home/nymphs3d/Z-Image`
- `/home/nymphs3d/TRELLIS.2`
- `/home/nymphs3d/Hunyuan3D-2`
- `/home/nymphs3d/Nymphs3D`

Observed rough sizes after the cache cleanup:

- `Z-Image`: about `5.4G`
- `TRELLIS.2`: about `1.4G`
- `Hunyuan3D-2`: about `6.3G`
- `Nymphs3D`: about `288M`

Shared cache note:

- `/home/nymphs3d/.cache/huggingface/hub/models--microsoft--TRELLIS.2-4B`: about `16G`

TRELLIS size breakdown after final cache cleanup:

- `/home/nymphs3d/TRELLIS.2/.git`: `774M`
- `/home/nymphs3d/TRELLIS.2/.venv`: `255M`
- `/home/nymphs3d/TRELLIS.2/output`: `138M`

## What Still Needs To Be Done

### 1. Keep TRELLIS cache-backed and avoid reintroducing repo-local model bundles

Current desired state:

- canonical TRELLIS model source stays in shared HF cache
- `/home/nymphs3d/TRELLIS.2` stays code/runtime focused rather than becoming a second model store

### 2. Decide whether `flash-attn` stays optional source-built optimization or moves to a prebuilt-wheel strategy

Current state is acceptable for local power users, but the product-grade choice
still needs to be made explicitly.

### 3. Keep Blender extension release notes current with the hotfix chain

Important same-day releases now exist in the addon feed:

- `1.1.50`: prompt preset registration safety
- `1.1.51`: reload-safe class registration
- `1.1.52`: enum callback default fix

These were all part of stabilizing the image-panel preset rollout.

## Validation Already Performed

- `Z-Image` Python files compile from the new repo path
- changed TRELLIS Python files compile
- changed helper shell scripts pass `bash -n`
- addon Python file compiles after the `~/Z-Image` default-path update
- TRELLIS smoke test passed after the final cache-backed model cleanup
- targeted `flash-attn` build completed and installed in the TRELLIS venv
- Blender addon hotfixes were published through extension versions `1.1.50`, `1.1.51`, and `1.1.52`
- README, roadmap, and changelog updates are already written to disk

## Summary

This session got the local helper/backend tree much closer to the intended
clean product story:

- `Z-Image` is now the real local image-backend repo path
- old `ComfyUI-*` runtime clutter is gone
- export cleanup behavior is safer and cleaner
- README and roadmap better match the actual marketed addon + helper-backend model
- TRELLIS is now actually cache-backed with the old local model bundle removed
- targeted `flash-attn` optimization is installed successfully without the earlier broad multi-arch waste
- the Blender extension hotfix chain is published through `1.1.52`

The main remaining work is product polish, documentation clarity, and deciding
how far optional TRELLIS optimization should go for real users.
