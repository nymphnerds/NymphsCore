# Handoff: X-Part Export Follow-Up

Date: 2026-04-12

Purpose:

- capture the current `X-Part` runtime state after the latest addon-launched runs
- record what is now proven to work in the real wrapper path
- record the current export-stage blocker and the latest patch applied

## Latest Real Addon-Launched Run

Latest inspected output:

- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-180307-X-Part-world/nymphs_parts_run.log`

What this run proves:

- the real addon-launched wrapper is using:
  - `python=/home/nymphs3d/Hunyuan3D-Part/.venv/bin/python`
  - `torch_file=/home/nymphs3d/Hunyuan3D-2/.venv/lib/python3.10/site-packages/torch/__init__.py`
- CUDA is live in the real wrapper process
- startup CUDA smoke passed
- pre-model-transfer CUDA smoke passed
- pre-generation CUDA smoke passed
- conditioning completed
- diffusion sampling completed all 8/8 steps

## Current Blocker

The run no longer dies in startup, model transfer, conditioning, or diffusion step 0.

It now fails during mesh extraction/export:

- `VAE Trained with Occupancy, inference with marching cubes level -0.001953125.`
- `Failed to export part 0 with error CUDA driver error: device not ready`

The wrapper then hit a secondary failure because it tried to export an empty
`trimesh.Scene`:

- `ValueError: Can't export empty scenes!`

## Important Comparison

An earlier controlled shell run succeeded with the same small lane:

- `/tmp/hunyuan3d-part-xpart-real2-small.log`

That older successful path still had the old bf16 mismatch warning, but it did
finish diffusion and export meshes.

This matters because the latest failure pattern looks like an export-stage
regression rather than a fundamental `X-Part` inability to produce parts on this
machine.

## Latest Local Interpretation

One regression in the recent staging changes was concrete:

- on `pipeline.to(device="cuda")`, the VAE was no longer moved to CUDA up front
- it stayed on CPU until right before mesh extraction
- the export path then performed a late CPU->CUDA VAE move after diffusion

That late VAE move did not exist in the last known-good export path.

## Latest Patch Applied

Patched files:

- `/home/nymphs3d/Hunyuan3D-Part/XPart/partgen/partformer_pipeline.py`
- `/home/nymphs3d/Hunyuan3D-Part/scripts/run_xpart_generate.py`

Changes:

- restored VAE move to the target CUDA device during `pipeline.to(...)`
- kept the newer conditioner staging and dtype/autocast fixes
- preserved the export-stage helper, but with the VAE already on CUDA it should
  now be a no-op on the normal path
- added a clean wrapper failure if `X-Part` returns an empty scene, instead of
  crashing later with a generic trimesh export error

## Regression Found After That Patch

Two newer addon-launched runs showed that restoring the VAE to CUDA during
`pipeline.to(...)` regressed the pipeline earlier than before:

- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-183146-X-Part-world/nymphs_parts_run.log`
- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-183319-X-Part-world/nymphs_parts_run.log`

What changed:

- pre-generation free VRAM dropped from about `1223 MiB` to about `373 MiB`
- the run no longer reached diffusion
- it failed during Sonata seg-feature extraction with:
  - `RuntimeError: CUDA driver error: device not ready`

Interpretation:

- keeping the VAE on GPU before conditioning starved the hybrid conditioning path
- that was a real regression relative to the earlier export-stage failure

Current local state after confirming this:

- the early VAE-on-GPU change has been reverted
- the clean empty-scene wrapper error remains
- the next rerun should confirm whether the pipeline returns to the later
  export-stage failure instead of the earlier conditioning crash

## Current Next Step

Run one more addon-launched `X-Part` test with the same small settings and check
whether the failure clears:

- `max_aabb=1`
- `num_inference_steps=8`
- `octree_resolution=256`
- `dtype=float32`

Most important log questions for the next run:

- does diffusion still complete?
- does mesh extraction/export now finish?
- if it still fails, does the failure remain in `latent2mesh_2` / marching cubes,
  or move somewhere else?

## Clean Official Env Result

The addon-launched run on the clean official env:

- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-193045-X-Part-world/nymphs_parts_run.log`

proved that the mixed old venv was not the whole story.

What this run showed:

- python came from `Hunyuan3D-Part/.venv-official`
- torch came from the same env: `2.4.0+cu124`
- startup CUDA smoke: ok
- pre-model-transfer CUDA smoke: ok
- pre-generation CUDA smoke: ok
- diffusion completed `8/8`
- export still failed later with:
  - `Failed to export part 0 with error CUDA driver error: device not ready`

Critical export diagnostics from the same run:

- export-stage-start free VRAM: `841 MiB`
- after offloading model/conditioner: `14471 MiB`
- after moving VAE to CUDA: `13251 MiB`
- after VAE decode: `13191 MiB`
- at export exception: `mem_free_mib=0`

Interpretation:

- startup/runtime env cleanliness helped and removed one major confounder
- but the live blocker survives on the clean official stack
- the current failure is specifically inside export-time `latent2mesh_2` /
  implicit-function marching-cubes work, after VAE decode

## Latest Patch After Clean Env Test

Patched files:

- `/home/nymphs3d/Hunyuan3D-Part/XPart/partgen/partformer_pipeline.py`
- `/home/nymphs3d/Hunyuan3D-Part/scripts/run_xpart_generate.py`

Changes:

- reduced the effective export chunk default from `400000` to `20000`
- added explicit wrapper arg `--export_num_chunks` and recorded it in metadata
- added a wrapper log line that prints:
  - dtype
  - octree resolution
  - export chunk size
  - staged-conditioner enabled/disabled
- changed staged conditioner from a hardwired flag to an env-controlled toggle:
  - `NYMPHS_XPART_STAGED_CONDITIONER=0` disables it
  - default remains enabled for now

Reasoning:

- `PartFormerPipeline.__call__` was using `num_chunks=400000`
- `_export()` and the lower-level VAE helpers use much smaller defaults
  (`10000-20000`)
- the clean-env failure occurred immediately on the first implicit-function
  chunk, which strongly suggests export query chunk pressure rather than an
  early startup/runtime problem

## Validation Note

A direct Codex-launched shell reproduction is still not trustworthy for CUDA on
this machine:

- `nvidia-smi` worked
- but direct torch startup in that shell reported `torch.cuda.is_available() =
  False`

That mismatch is why the next meaningful validation must come from the real
addon-launched wrapper log again, not from the Codex shell.

## First Successful Addon-Launched Export

The next addon-launched run on the clean official env succeeded:

- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-194338-X-Part-world/nymphs_parts_run.log`

Artifacts written:

- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-194338-X-Part-world/xpart_parts.glb`
- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-194338-X-Part-world/xpart_parts_bbox.glb`
- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-194338-X-Part-world/xpart_input_bbox.glb`
- `/home/nymphs3d/.cache/hunyuan3d-part/outputs/20260412-194338-X-Part-world/xpart_explode.glb`

What this run proved:

- addon launcher used `Hunyuan3D-Part/.venv-official`
- torch came from the same env: `2.4.0+cu124`
- the wrapper logged:
  - `X-Part settings: dtype=float32 octree_resolution=256 export_num_chunks=20000 staged_conditioner=1`
- diffusion completed `8/8`
- export completed successfully
- `[X-PartExportDiag] phase=export-after-latent2mesh` was reached
- final wrapper lines were:
  - `Pipeline returned. Exporting part meshes...`
  - `Export complete.`

Important export diagnostics from the successful run:

- export-stage-start free VRAM: `1000 MiB`
- after offloading model/conditioner: `14471 MiB`
- after moving VAE to CUDA: `13251 MiB`
- after VAE decode: `13191 MiB`
- after `latent2mesh_2`: `12139 MiB`

Interpretation:

- the export-stage `device not ready` failure is currently cleared
- lowering export chunking from `400000` to `20000` was the decisive fix for
  the surviving export crash
- the clean official env was still necessary, but it was not by itself
  sufficient

## Quality Note

The first successful export used intentionally conservative survival settings:

- `max_aabb=1`
- `num_inference_steps=8`
- `octree_resolution=256`
- `dtype=float32`

Those settings are useful for stability validation, not quality. The produced
geometry was reportedly "hilariously unusable", which is consistent with the
settings rather than with an export/runtime failure.

Suggested next quality lane:

- keep `dtype=float32`
- keep `export_num_chunks=20000`
- raise `num_inference_steps` to `20`, then `30` if stable
- raise `octree_resolution` to `512`
- raise `max_aabb` above `1`, or use `0` to allow all Stage-1 boxes

## Updated Overall Conclusion

The shared old venv was a real and important source of earlier instability:

- mixed python / torch import path
- non-isolated CUDA extension stack
- unsupported runtime shape relative to repo docs

But the final blocker was not only the shared venv.

More accurate summary:

- clean official env removed a major confounder
- several earlier code changes remain valid:
  - runtime diagnostics
  - export diagnostics
  - empty-result wrapper failure
  - removal of hardcoded bf16 autocast
- the export crash that survived on the clean env was then fixed by reducing
  export chunk pressure

The repo is now in a working baseline state for addon-launched X-Part small-lane
generation.
