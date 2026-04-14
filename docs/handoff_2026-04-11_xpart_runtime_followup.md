# Handoff: X-Part Runtime Follow-Up

Date: 2026-04-11

Purpose:

- capture the current `Hunyuan3D-Part` / `Nymphs Parts` runtime state after the
  first real `X-Part` bring-up attempts
- record the exact blockers already cleared
- record the current remaining blocker
- make restart/resume safe without relaunching duplicate heavy GPU jobs

## Update: Stage-2 Proof After Initial Handoff

After the first handoff was written, a clean single-process retry succeeded.

Successful minimal `X-Part` lane:

```text
mesh_path=/mnt/c/Users/babyj/AppData/Local/Temp/nymphs3d2_parts_sources/20260411-161200-selected-mesh.glb
aabb_json=/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260411-161212-P3-SAM-geometry_0.001/p3sam_aabb.json
output_dir=/tmp/hunyuan3d-part-xpart-real2-small
num_inference_steps=8
octree_resolution=256
dtype=float32
max_aabb=1
```

Confirmed outputs:

```text
/tmp/hunyuan3d-part-xpart-real2-small/summary.json
/tmp/hunyuan3d-part-xpart-real2-small/xpart_parts.glb
/tmp/hunyuan3d-part-xpart-real2-small/xpart_parts_bbox.glb
/tmp/hunyuan3d-part-xpart-real2-small/xpart_input_bbox.glb
/tmp/hunyuan3d-part-xpart-real2-small/xpart_explode.glb
```

Meaning:

- `P3-SAM -> saved AABB -> X-Part` is proven locally
- `X-Part` can consume saved stage-1 `aabb` instead of rerunning its internal
  bbox predictor
- the first working lane is intentionally tiny:
  - one box
  - 8 diffusion steps
  - octree 256
  - float32
- next addon work can now target the real two-stage pipeline instead of
  treating `X-Part` as only theoretical

## Machine State At Handoff

As of the last check before writing this note:

- no `run_xpart_generate.py` processes are alive
- GPU is clear
- `nvidia-smi` showed:
  - `386 MiB / 16376 MiB`
  - no running GPU processes

This matters because earlier in the session multiple `X-Part` test runs were
alive at once and drove the machine nearly unusable.

## Important Operational Rule

Do not launch another `X-Part` test unless this is clean first:

```bash
ps -eo pid,ppid,etimes,%cpu,%mem,cmd | rg 'run_xpart_generate.py'
```

If that prints anything real, stop and kill the old job first.

## What Changed Locally

### In `/home/nymphs3d/Hunyuan3D-Part`

Local repo status during this handoff:

- modified:
  - `P3-SAM/demo/auto_mask.py`
  - `P3-SAM/model.py`
  - `XPart/partgen/models/sonata/model.py`
- untracked:
  - `.venv/`
  - `scripts/`

Important local scripts now present:

- `scripts/run_p3sam_segment.py`
- `scripts/run_xpart_generate.py`

### `run_p3sam_segment.py`

This wrapper was improved to export a canonical stage-1 mesh and richer summary
data.

Current behavior:

- exports `source_mesh_stage1.glb`
- keeps:
  - `p3sam_segmented.glb`
  - `p3sam_segmented.ply`
  - `p3sam_aabb.json`
  - `summary.json`
- summary now includes:
  - `mesh_path_original`
  - `mesh_path_stage1`
  - `semantic_label_count`
  - `aabb_count`
- still keeps `part_count` for current compatibility

### `run_xpart_generate.py`

Current wrapper behavior:

- loads `X-Part` from the local/cached public `tencent/Hunyuan3D-Part` model
- consumes:
  - `--mesh_path`
  - `--aabb_json`
  - `--output_dir`
- does **not** use the internal bbox-predictor on the intended happy path
- added local options:
  - `--dtype`
  - `--max_aabb`
  - `--num_inference_steps`
  - `--octree_resolution`

Path fixes already added:

- `XPart` root added to `sys.path`
- `P3-SAM` root added to `sys.path`
- `P3-SAM/demo` added to `sys.path`

Reason:

- `X-Part` bbox-estimator code imports `model` as a top-level module and fails
  unless those paths exist

## Runtime / Dependency Progress

The following blockers were already cleared:

1. public `X-Part` model snapshot was downloaded successfully into:

```text
/home/nymphs3d/.cache/hunyuan3d-part/models/tencent/Hunyuan3D-Part
```

2. `flash-attn` is now installed in:

```text
/home/nymphs3d/Hunyuan3D-Part/.venv
```

Details:

- reused the already-built local wheel from the TRELLIS-compatible torch stack
- confirmed import worked in the parts venv

3. `torch-cluster` is now installed in:

```text
/home/nymphs3d/Hunyuan3D-Part/.venv
```

Installed wheel:

- `torch-cluster-1.6.3+pt211cu130`

4. `X-Part` now gets past:

- missing model download
- missing `flash_attn`
- missing top-level `model` import
- missing `torch_cluster`
- full Sonata / `X-Part` model initialization

## Important Findings

### `bfloat16` is not viable right now

One low-memory `X-Part` run on the real Blender mesh failed on:

```text
KeyError: torch.bfloat16
```

in `spconv`.

Meaning:

- this current path cannot use `--dtype bfloat16`
- use `float32` for now

### Current stage-2 blocker is deeper runtime stability

The best controlled real-world test attempted was:

- real Blender mesh:
  - `/mnt/c/Users/babyj/AppData/Local/Temp/nymphs3d2_parts_sources/20260411-161200-selected-mesh.glb`
- real stage-1 `aabb`:
  - `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260411-161212-P3-SAM-geometry_0.001/p3sam_aabb.json`
- reduced settings:
  - `--max_aabb 1`
  - `--num_inference_steps 8`
  - `--octree_resolution 128`
  - `--dtype float32`

This still did not produce output.

Observed result:

- output dir stayed empty:

```text
/tmp/hunyuan3d-part-xpart-real2-small
```

- log only captured startup:

```text
/tmp/hunyuan3d-part-xpart-real2-small.log
```

This strongly suggests the process is being killed hard in a deeper CUDA/runtime
phase before normal Python error handling or export.

Current best interpretation:

- no longer an installation problem
- no longer a missing-dependency problem
- likely memory/runtime instability under load

## Earlier Duplicate-Run Failure

At one point there were multiple live `run_xpart_generate.py` jobs at once:

- duplicate full-size runs on `xpart-real2`
- one smaller diagnostic run

That consumed almost all VRAM and made the PC nearly unusable.

This was corrected by killing the jobs and rechecking `nvidia-smi`.

Do not resume from any old live PTY sessions assuming they are safe.

## Best Resume Point

When resuming after restart, start from a clean machine and assume:

- the local model cache is already present
- `flash-attn` is already installed in the parts venv
- `torch-cluster` is already installed in the parts venv
- the remaining work is addon integration and scaling tests, not basic setup

## Best Next Steps

1. keep the current architecture direction:
   - stage 1 = `P3-SAM`
   - stage 2 = `X-Part`
2. refactor the addon panel from backend-toggle to two-stage workflow:
   - `Analyze Mesh`
   - `Generate Parts`
3. wire `Generate Parts` to the proven `run_xpart_generate.py` contract
4. keep the first addon-visible `X-Part` lane conservative:
   - one saved analysis folder
   - one selected/saved AABB or capped AABB count
   - 8 steps
   - octree 256
   - float32
5. add better progress/log capture around:
   - conditioning
   - diffusion
   - mesh export
6. later explore scaling:
   - more boxes
   - more steps
   - full two-box run
   - decimated mesh lane if needed
7. keep single-process discipline:
   - one `X-Part` job only
   - never overlap retries

## Useful Paths

Model cache:

```text
/home/nymphs3d/.cache/hunyuan3d-part/models/tencent/Hunyuan3D-Part
```

Real stage-1 run folder:

```text
/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260411-161212-P3-SAM-geometry_0.001
```

Real stage-2 small diagnostic log:

```text
/tmp/hunyuan3d-part-xpart-real2-small.log
```

Real stage-2 small diagnostic output dir:

```text
/tmp/hunyuan3d-part-xpart-real2-small
```

## Session Anchor

Codex session shown in `/status` during this work:

```text
019d77fb-eca7-76c1-b1fa-11ede0dca3da
```

Related planning doc already written and pushed in `Nymphs3D`:

- `docs/nymphs_parts_implementation_plan_2026-04-11.md`
