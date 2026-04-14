# Nymphs Parts P3-SAM Guide

Date: 2026-04-11

This guide describes the current Nymphs Parts integration in the Blender addon.

## Current Behavior

Nymphs Parts does not run as a persistent backend server yet.

You do not start it from the Runtimes panel. The current P3-SAM path runs as a one-shot background job from the Nymphs Parts panel:

```text
selected mesh -> prepared temp mesh -> P3-SAM segmentation -> segmented GLB imported into Blender
```

The practical test lane is:

```text
Backend: P3-SAM
Mode: Segment Parts
Source: Selected Mesh
Format: GLB
```

X-Part and Decompose Parts are still exploratory and should not be treated as working user flows yet.

## X-Part Defaults

The addon default X-Part settings now follow the upstream example more closely:

```text
Steps: 50
Octree: 512
Max Boxes: 0
Precision: Float32
```

Why these defaults:

- the official X-Part demo uses `torch.float32`
- the official X-Part demo uses `octree_resolution=512`
- the demo does not cap the number of boxes
- the pipeline default diffusion step count is `50`

Practical note:

- the earlier `8 / 256 / 1` lane was only a crash-isolation lane
- it is useful for proving the runtime still works
- it is not a quality preset

If a full-quality run is too heavy on your machine, step back in this order:

- lower Steps to `20`
- lower Max Boxes to `3` or `1`
- lower Octree to `256`
- keep Precision on `Float32`

## Quick Test

1. Select a mesh object in Blender.
2. Open the Nymphs sidebar.
3. Open Nymphs Parts.
4. Set Source to Selected Mesh.
5. Leave Format as GLB.
6. Click Prepare Source.
7. Wait for a Prepared path to appear.
8. Click Run P3-SAM.
9. Wait for the background job to finish.
10. Confirm that the segmented GLB imports back into Blender.

The first run may take longer because P3-SAM and Sonata weights may load or download into the cache.

## Why Run P3-SAM Is Greyed Out

The Run P3-SAM button only enables when both of these are true:

- a prepared source mesh exists
- the P3-SAM demo entrypoint is detected in the configured repo

Selecting a mesh is not enough by itself. Click Prepare Source first.

If Run P3-SAM is still disabled after Prepare Source, expand Experimental Backend and check the setup status.

Expected setup:

```text
Repo Path:
~/Hunyuan3D-Part

Python Path:
~/Hunyuan3D-Part/.venv-official/bin/python
```

Expected status:

```text
P3-SAM: Found
Python: Found
```

If either item is missing, the addon cannot launch the P3-SAM job.

## Recommended Low-VRAM Settings

The known working RTX 4080 SUPER test lane is:

```text
Points: 30000
Prompts: 96
Prompt Batch: 4
```

If the job crashes, runs out of VRAM, or stalls during inference, try:

```text
Points: 20000
Prompts: 64
Prompt Batch: 2
```

The controls mean:

- Points: how many sampled surface points P3-SAM uses. Higher can preserve more detail but uses more VRAM.
- Prompts: how many region prompts P3-SAM tries. Higher may find more regions but is slower and heavier.
- Prompt Batch: how many prompts run at once. Lower is safer for VRAM.

## Source Options

Selected Mesh uses the active selected mesh object, or the first selected mesh if the active object is not a mesh.

Latest Result copies the latest generated shape result if one exists. If no latest result exists, the panel may fall back to the selected mesh for display, but the current Prepare Source path requires a real latest result for that mode.

For early testing, use Selected Mesh.

## Output Behavior

Prepare Source exports or copies the mesh into the addon temp folder:

```text
<system temp>/nymphs3d2_parts_sources
```

The P3-SAM job writes result files under:

```text
~/.cache/hunyuan3d-part/outputs
```

The addon expects this main output:

```text
p3sam_segmented.glb
```

Other useful files may also be written by the wrapper:

```text
p3sam_segmented.ply
p3sam_segmented_aabb.glb
p3sam_segmented_face_ids.npy
p3sam_aabb.json
summary.json
```

When the job succeeds, the addon imports `p3sam_segmented.glb` into Blender automatically.

## Import Options

Keep Original Mesh controls whether the source mesh remains visible after the segmented result imports.

If Keep Original Mesh is off, the addon hides the source mesh after importing the result.

Send To New Collection is intended to place parts output into a dedicated collection, but collection handling still needs a full Blender-side pass. Treat this as an area to verify carefully during testing.

## Current Limitations

- Nymphs Parts is not shown in Runtimes because it is not a persistent server.
- The Experimental Backend wording is confusing because P3-SAM is wired as a one-shot job, not a startable runtime.
- X-Part is visible but intentionally not a working production path yet.
- Decompose Parts is not the current practical P3-SAM flow.
- Output currently uses the WSL cache/output path, not a polished user-selected project folder.
- First-run model load can take a while and may look idle while weights initialize.

## Troubleshooting

If Prepare Source is greyed out:

- select a mesh object
- set Source to Selected Mesh
- make sure the object type is Mesh

If Run P3-SAM is greyed out:

- click Prepare Source first
- expand Experimental Backend
- confirm P3-SAM: Found
- confirm Python: Found

If the job fails:

- lower Points, Prompts, and Prompt Batch
- confirm the Python Path points to the Hunyuan3D-Part venv
- check that the source mesh exported as GLB
- open the temp folder and confirm the prepared source file exists

If the result imports but the source is hidden:

- enable Keep Original Mesh before running

If no result imports:

- check whether `p3sam_segmented.glb` exists under the latest output folder in `~/.cache/hunyuan3d-part/outputs`
- check the panel status text for the last reported P3-SAM progress or error

## UI Cleanup Notes

The current panel should be cleaned up so the workflow is clearer:

- rename Experimental Backend to P3-SAM Setup
- say explicitly that no runtime start is needed
- replace stale "backend not wired" wording
- disable or hide X-Part and Decompose Parts unless an experimental mode is explicitly enabled
- make the Prepare Source -> Run P3-SAM dependency clearer in the panel
