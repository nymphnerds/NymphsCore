# TRELLIS Backend Handoff

Date: `2026-04-09`  
Primary branch family: `exp/2mv-remake`

## Purpose

This note captures the current state of the official `TRELLIS.2` backend experiment and the partial Blender addon integration work completed in this session.

This is the correct checkpoint to resume from if another session needs to continue the TRELLIS lane without re-reading the whole branch history.

## Active Repos

- `/home/nymphs3d/TRELLIS.2`
- `/home/nymphs3d/Nymphs3D-Blender-Addon`
- `/home/nymphs3d/Nymphs3D`

Current branches:

- `/home/nymphs3d/TRELLIS.2`: `main`
- `/home/nymphs3d/Nymphs3D-Blender-Addon`: `exp/2mv-remake`
- `/home/nymphs3d/Nymphs3D`: `exp/2mv-remake`

## Goal Of This Session

The direction changed from "use the ComfyUI fork as the sandbox wrapper" to:

- keep using the official `TRELLIS.2` repo as the real implementation base
- expose it to the Blender addon through a thin local adapter server
- stop thinking of TRELLIS as a Hunyuan runtime toggle
- treat TRELLIS as a separate backend/service in the addon

The user explicitly preferred to move away from the ComfyUI wrapper as the primary implementation path.

## What Was Added

### 1. Official TRELLIS adapter server

New file:

- `/home/nymphs3d/TRELLIS.2/scripts/api_server_trellis.py`

This adapter exposes:

- `GET /server_info`
- `GET /active_task`
- `POST /generate`

Its purpose is to match the addon's backend contract closely enough that TRELLIS can be selected as another 3D backend.

Current supported adapter flows:

- single-image shape generation
- single-image shape + texture generation
- selected-mesh retexture request path exists, but is currently blocked by missing local model assets

### 2. Repo-path-aware TRELLIS helper config

Updated file:

- `/home/nymphs3d/TRELLIS.2/scripts/trellis_official_common.py`

Important changes:

- `REPO_ROOT` is now derived from the script path
- `resolve_local_model_root()` prefers:
  - `TRELLIS_MODEL_ROOT`
  - `REPO_ROOT/models/trellis2`
  - legacy `/home/nymphs3d/ComfyUI/models/trellis2`
- `resolve_output_dir()` prefers:
  - `TRELLIS_OUTPUT_DIR`
  - `REPO_ROOT/output`

This means the official repo now has a clean path to becoming self-contained, even though it still falls back to the legacy ComfyUI model location right now.

### 3. Blender addon TRELLIS backend wiring

Updated file:

- `/home/nymphs3d/Nymphs3D-Blender-Addon/Nymphs3D2.py`

TRELLIS was added as a separate backend/service, not as a Hunyuan runtime option.

Important addon changes already in place:

- new backend enum: `BACKEND_TRELLIS`
- new service key: `trellis`
- repo path field: `repo_trellis_path`
- python path field: `trellis_python_path`
- service port default: `8094`
- TRELLIS-specific shape payload builder
- TRELLIS-specific texture payload builder
- TRELLIS-specific shape panel branch
- TRELLIS-specific texture panel branch
- TRELLIS-specific capability interpretation
- TRELLIS launch and stop command handling

The addon treats TRELLIS as:

- single-image shape generation
- optional shape + texture from that single image
- no text guidance
- no multiview guidance
- retexture only when the backend truthfully advertises it

## What Was Verified

### Verified locally through the adapter

The adapter is currently running on:

- `http://127.0.0.1:8094`

The server is started with:

- `/home/nymphs3d/TRELLIS.2/.venv/bin/python`
- `/home/nymphs3d/TRELLIS.2/scripts/api_server_trellis.py`

Important local note:

- `/home/nymphs3d/TRELLIS.2/.venv` is a local symlink to the previously built TRELLIS environment
- this is only a convenience bridge for now, not a final env strategy

### Smoke tests that passed

1. `512` shape through the adapter

- response status: `200`
- content type: `model/gltf-binary`
- output: `/tmp/trellis_api_repo_shape_512.glb`

2. `512` shape + texture through the adapter

- response status: `200`
- content type: `model/gltf-binary`
- output: `/tmp/trellis_api_repo_textured_512.glb`

This means the official TRELLIS backend path is already viable for:

- shape only
- full image-to-3D shape + texture

using the adapter surface the addon expects.

### Validation that passed on the code side

`py_compile` passed for:

- `/home/nymphs3d/TRELLIS.2/scripts/api_server_trellis.py`
- `/home/nymphs3d/TRELLIS.2/scripts/trellis_official_common.py`
- `/home/nymphs3d/Nymphs3D-Blender-Addon/Nymphs3D2.py`

## Current Blocker

### Selected-mesh TRELLIS retexture is not currently available locally

The adapter now reports the real state in `/server_info`:

- `enable_tex=true`
- `mesh_retexture=false`

Current detail from the live server:

`Local TRELLIS mesh retexturing is unavailable: missing texturing_pipeline.json in the model bundle.`

This is intentional. The adapter was patched to fail early and honestly instead of failing deep in model loading with a Hugging Face traceback.

### Why this is blocked

The legacy local model bundle at:

- `/home/nymphs3d/ComfyUI/models/trellis2`

contains enough for:

- shape generation
- image-to-3D shape + texture generation

but it does not contain the full official selected-mesh texturing bundle.

Confirmed local gaps:

- no `texturing_pipeline.json`
- no local `shape_slat_encoder` / `shape_enc_next_dc_f16c32_fp16` checkpoint
- no `ckpts/slat_flow_imgshape2tex_dit_1_3B_1024_bf16.safetensors`

Because of that, the TRELLIS retexture lane should currently be treated as:

- structurally wired
- not yet operational

## What Was Fixed About This Failure

Before the last patch, retexture requests failed badly because:

- the adapter tried to load `texturing_pipeline.json`
- that file did not exist locally
- TRELLIS fell through to Hugging Face download logic using bad local-path assumptions

The adapter was changed so that:

- `/server_info` truthfully reports whether mesh retexture is actually available
- retexture requests fail immediately with a clear local-assets message when the official texturing bundle is incomplete
- the addon capability surface follows the backend truth instead of pretending TRELLIS retexture works

## Current Addon Behavior

The addon now has a meaningful TRELLIS service/backend path, but it is still only partially validated.

What is currently true:

- TRELLIS can be launched as a backend from the addon architecture
- the shape panel has a TRELLIS-specific single-image workflow branch
- the texture panel has a TRELLIS-specific branch
- the texture panel will disable the action when `caps["retexture"]` is false

This is the correct behavior for now.

## What Was Not Yet Validated

### 1. Blender runtime validation

This was not run.

Reason:

- `blender` was not found on `PATH` in this shell environment

Command used:

- `command -v blender || command -v blender-bin || true`

Result:

- no Blender executable found

So the addon changes are compile-validated, not Blender-runtime-validated.

### 2. Repo-local official model root sync

This also remains unfinished.

The long-term intended path is:

- `/home/nymphs3d/TRELLIS.2/models/trellis2`

But that directory does not exist yet.

The current server still resolves its model path to:

- `/home/nymphs3d/ComfyUI/models/trellis2`

via the fallback in `trellis_official_common.py`.

## Recommended Next Step

The next session should do this in order:

1. Create the repo-local TRELLIS model root:

- `/home/nymphs3d/TRELLIS.2/models/trellis2`

2. Copy the already working local files from:

- `/home/nymphs3d/ComfyUI/models/trellis2`

3. Download the missing official texturing assets from:

- `microsoft/TRELLIS.2-4B`

At minimum, expect to need:

- `texturing_pipeline.json`
- the official shape encoder checkpoint
- the missing `1024` texture flow weights

4. Set:

- `TRELLIS_MODEL_ROOT=/home/nymphs3d/TRELLIS.2/models/trellis2`

5. Restart the adapter and rerun:

- shape smoke test
- shape + texture smoke test
- selected-mesh retexture smoke test

6. Only after that, do Blender-side validation.

## Useful Pickup Checks

### Live server info

```bash
curl -sS http://127.0.0.1:8094/server_info
```

### Live backend progress

```bash
curl -sS http://127.0.0.1:8094/active_task
```

### Current important files

- `/home/nymphs3d/TRELLIS.2/scripts/api_server_trellis.py`
- `/home/nymphs3d/TRELLIS.2/scripts/trellis_official_common.py`
- `/home/nymphs3d/Nymphs3D-Blender-Addon/Nymphs3D2.py`

## Worktree State

At the end of this session:

- `/home/nymphs3d/TRELLIS.2` has untracked `scripts/` and `output/`
- `/home/nymphs3d/Nymphs3D-Blender-Addon` has modified:
  - `Nymphs3D2.py`
  - `ROADMAP.md`
- `/home/nymphs3d/Nymphs3D` has modified:
  - `docs/2mv_product_remake_plan_2026-04-08.md`

No commit was made in this session.

## Bottom Line

The TRELLIS backend experiment is already strong enough to justify continuing:

- official repo path works
- adapter path works
- addon-level backend structure is plausible
- `512` shape and `512` shape + texture are proven through the adapter

But selected-mesh retexture is not ready until the official texturing model bundle is synced locally and the adapter is pointed at that repo-local model root.
