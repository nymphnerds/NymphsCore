# Nymphs Parts Implementation Plan

Date: 2026-04-11

This note captures the current best plan for `Hunyuan3D-Part` integration after
checking:

- the official `Hunyuan3D-Part` repo and README
- the public Hugging Face model layout
- the local `P3-SAM` bring-up work
- the local addon implementation
- the current real Blender-launched `P3-SAM` output

## Bottom Line

The high-level direction is correct:

- `Hunyuan3D-Part` should be treated as a two-stage pipeline
- `P3-SAM` is stage 1
- `X-Part` is stage 2
- the addon should stop presenting these as equal peer backends

But the implementation plan needs to be more precise than:

- "just keep improving P3-SAM first"
- or
- "persist every artifact and hope X-Part uses it"

The first working product shape should be:

1. `Analyze Mesh` using `P3-SAM`
2. save a stable stage-1 run folder
3. `Generate Parts` using `X-Part`
4. feed `X-Part` the saved stage-1 mesh and `aabb`

## What Is Already Proven

Local machine status:

- `P3-SAM` has a real Blender-launched path
- the WSL path handoff from Blender works
- the local wrapper works
- the addon can launch the wrapper as a one-shot background job

Important local result:

- the current successful Blender-launched run reported `part_count=2`
- the same run log also mentions more connected mesh regions later in
  post-process
- the saved `p3sam_aabb.json` currently contains `2` boxes

This means:

- stage 1 is runnable
- stage-1 output quality is still an open question
- stage 2 must be tested against real stage-1 output before the UI is treated as
  architecturally settled

## What The Official/Public Sources Actually Support

Official repo reading:

- the public repo explicitly describes a two-stage pipeline:
  - holistic mesh -> `P3-SAM`
  - `P3-SAM` outputs semantic features, segmentations, and part bounding boxes
  - `X-Part` generates the complete parts

Public release reading:

- the public release is still described as a light version of `X-Part`
- however, the public Hugging Face tree exposes the expected `X-Part` model
  layout

Local code reading:

- `X-Part` is written to load from `PartFormerPipeline.from_pretrained(...)`
- `PartFormerPipeline.__call__` can accept:
  - `mesh_path`
  - `aabb`
  - optional `part_surface_inbbox`

Key consequence:

- the first real `X-Part` integration does not need persisted stage-1 feature
  blobs
- the first useful contract is:
  - canonical stage-1 mesh
  - stage-1 `aabb`

## Corrections To The Earlier Plan

### 1. Do not model Parts as a backend toggle

Current local addon behavior still uses:

- `Backend: P3-SAM / X-Part`
- `Parts Mode: Segment / Decompose`

That is useful for bring-up, but it is the wrong product model.

The correct product model is:

- `Stage 1: Analyze Mesh`
- `Stage 2: Generate Parts`

`P3-SAM` and `X-Part` are implementation details of those stages, not the main
user-facing choice.

### 2. Do not make stage 2 depend on persisted "features" first

The current public/local `X-Part` path does not clearly expose a clean "load
saved features from stage 1" surface for the first integration.

For the first real implementation, the correct minimal contract is:

- canonical stage-1 mesh
- `p3sam_aabb.json`

That is enough to attempt real `X-Part` generation without inventing extra file
formats prematurely.

### 3. Do not default stage 2 to the original input mesh

`P3-SAM` may clean or otherwise alter the mesh before returning final labels and
boxes.

So the stage-2 wrapper should use:

- the canonical mesh emitted by stage 1

not:

- the original prepared-source mesh by default

### 4. Do not over-read the current successful P3-SAM run

The first real run proves that the plumbing works.

It does not yet prove that the current stage-1 output is decomposition-quality.

The key unresolved issue is:

- if stage 1 only yields `2` boxes on a test object where more meaningful parts
  are expected, stage 2 may technically run but still not be useful

So stage-1 quality remains a real gating issue.

## Recommended Stage-1 Output Contract

Each analysis run should produce a stable folder containing:

- `source_mesh_original.*`
- `source_mesh_stage1.glb`
- `p3sam_segmented.glb`
- `p3sam_segmented.ply`
- `p3sam_segmented_face_ids.npy`
- `p3sam_aabb.json`
- `summary.json`
- `nymphs_parts_run.log`

Recommended summary fields:

- `mesh_path_original`
- `mesh_path_stage1`
- `point_num`
- `prompt_num`
- `prompt_bs`
- `semantic_label_count`
- `aabb_count`
- `face_count`
- `vertex_count`

Optional later fields:

- `connected_region_count`
- `kept_region_count`

Those should only be added if extracted reliably from the real stage-1 result
rather than guessed from logs.

## Recommended Stage-2 Contract

First `X-Part` wrapper should be minimal.

Inputs:

- `--mesh_path`
- `--aabb_json`
- `--output_dir`
- optional seed and runtime flags

Behavior:

- load `PartFormerPipeline.from_pretrained(...)`
- load `aabb` from stage 1
- call the pipeline with:
  - `mesh_path`
  - `aabb`
- let `X-Part` sample `part_surface_inbbox` from the mesh itself

Important rule:

- do not use `X-Part`'s internal bbox predictor on the first integration path

Reason:

- the current `X-Part` bbox-estimator code still contains the same kind of
  hard-reset behavior that previously made `P3-SAM` too heavy
- bypassing that path with saved stage-1 `aabb` is the safest first strategy

## Recommended Addon Refactor

### Short-term

Keep the current `P3-SAM` bring-up code, but rename the workflow conceptually.

Current real user flow:

- select mesh
- prepare source
- run `P3-SAM`
- inspect analysis result

This should be reframed in the UI as:

- `Analyze Mesh`

not:

- one side of a backend switch

### Medium-term

Refactor the `Nymphs Parts` panel to:

- `Analyze Mesh`
- `Generate Parts`
- `Source`
- `Output`
- advanced setup foldout

Where:

- `Analyze Mesh` runs stage 1
- `Generate Parts` requires a valid stage-1 output folder

The advanced foldout can still contain:

- repo path
- python path
- low-memory knobs
- experimental/runtime notes

### What should be removed from the main product surface

Eventually remove from the main visible workflow:

- visible `P3-SAM` vs `X-Part` backend toggle
- visible `Segment` vs `Decompose` pairing as if they are independent modes

Those are development-era controls, not the clearest user-facing model.

## Recommended Order Of Work

1. stabilize the stage-1 output contract
2. add canonical stage-1 mesh export
3. improve summary fields so stage-1 usefulness is measurable
4. write a minimal `run_xpart_generate.py`
5. prove `X-Part` locally from:
   - sample mesh + saved `aabb`
   - real Blender-produced stage-1 output + saved `aabb`
6. only then refactor the addon panel from backend-toggle to pipeline
7. after that:
   - improve progress UX
   - improve import/collection behavior
   - evaluate `flash-attn` for the parts venv
   - later explore editable/retry-per-part UX

## What Not To Do Next

Do not prioritize these before stage 2 is proven:

- more cosmetic `P3-SAM` panel polish
- deep `flash-attn` work for the parts venv
- runtime-card integration for Parts
- part-aware texture workflows
- PBR/material design work tied to parts output

Those are downstream concerns. The current critical path is:

- stage-1 output quality
- stage-2 viability using saved `aabb`

## Current Best Product Reading

For now, the honest product stance should be:

- `P3-SAM` is the first real public integration lane
- `X-Part` remains the intended second stage, not a discarded idea
- `Nymphs Parts` should evolve into a pipeline-first workflow
- the next engineering milestone is not "more P3-SAM", it is:
  - make stage-1 outputs canonical
  - prove stage 2 on those outputs
