# Install Disk And Model Footprint

This note records the current disk footprint of the current local backend stack.

It replaces the older "all Hunyuan lanes" framing. The intended product shape
is now:

- `Z-Image` via `Nunchaku` for image generation
- official `TRELLIS.2` for the single-image 3D lane
- `Hunyuan 2mv` only for the multiview workflow where that is still useful

That means the old non-MV Hunyuan single-image path should be treated as legacy,
not as part of the current product surface.

## Headline Numbers

Current measured local size for the core kept stack on this machine:

- about `104.0G` for the backend stack itself
- about `108.9G` including `/usr/local/cuda-13.0`

If optional experimental Parts is also installed on this machine:

- add about `8.8G` more for the current local `Hunyuan3D-Part` repo/env
- treat local Parts outputs as user-generated content, not required baseline footprint

Practical planning guidance:

- minimum comfortable free space: about `130G`
- safer recommendation: about `150G`

If you plan to enable experimental Parts too, still leave extra room above the
core stack because experimental runs can generate large local outputs.

Why the free-space target is higher than the measured install:

- temporary installer/download overhead
- generated meshes, textures, and logs
- future model/runtime updates
- WSL filesystem growth overhead

## Scope

This estimate covers the current kept stack on the working machine:

- `Z-Image` repo/runtime
- `Tongyi-MAI/Z-Image-Turbo` base model files used by the current `Nunchaku`
  runtime
- `Nunchaku` `Z-Image` cache
- `TRELLIS.2`
- `Hunyuan 2mv`
- `Hunyuan3D-2` texture-side assets used by `2mv`
- the local repo and Python environment weight needed to run those lanes

Optional experimental add-on footprint on this machine:

- `Hunyuan3D-Part`
- required Parts weights and caches only

It does not treat the removed Hunyuan text-bridge cache as part of the intended
product.

## Current Product Reading

Current backend ownership should be read like this:

- `Z-Image` = image-generation frontend and prompt-to-image lane
- `TRELLIS.2` = single-image image-to-3D lane
- `Hunyuan 2mv` = multiview-only lane for the cases where multiview guidance is
  important

Important clarification:

- the current `Z-Image` runtime uses `Nunchaku` plus the base
  `Tongyi-MAI/Z-Image-Turbo` model snapshot
- so `Nunchaku` does not fully replace the base `Z-Image-Turbo` weights in the
  current code path
- `Hunyuan 2mv` is still backed by the `Hunyuan3D-2` repo/runtime family
- `Hunyuan 2mv` also still uses `tencent/Hunyuan3D-2` as the texture-side model
  in the current code path
- so seeing `Hunyuan3D-2` on disk does not automatically mean the unwanted
  non-MV product lane is still being kept intentionally
- the thing that should be removed from the product story is the non-MV Hunyuan
  workflow, not necessarily every file path that still carries the upstream
  `Hunyuan3D-2` name

## Measured Local Sizes

These are rough working-machine notes for the current local install.

### Core Kept Stack

- `Z-Image-Turbo` base cache: about `31G`
- `Nunchaku` `Z-Image` cache: about `7.1G`
- `TRELLIS.2` model cache: about `16G`
- `Hunyuan3D-2mv cache`: about `19G`
- `Hunyuan3D-2 cache`: about `19G`
- `Hunyuan3D-2` runtime repo/env used for the `2mv` lane: about `6.3G`
- `Z-Image` repo/env: about `5.4G`
- `TRELLIS.2` repo/env: about `2.3G`

### Optional Experimental Parts

- `Hunyuan3D-Part` repo/env: about `8.8G`

This note does **not** count user-generated Parts outputs under:

- `~/.cache/hunyuan3d-part/outputs`

This kept stack does not include the older Hunyuan text-to-image bridge cache.

### Removed Or Legacy Extras

- `Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled` should be treated as a
  removed legacy text-bridge cache, not part of the kept addon/backend stack

Important distinction:

- the non-MV Hunyuan single-image workflow is no longer part of the intended
  product surface
- but the `Hunyuan3D-2` cache is still part of the kept stack because the
  current `2mv` textured path uses it
- likewise, the old stock non-`Nunchaku` `Z-Image` runtime is not the intended
  lane, but the base `Z-Image-Turbo` snapshot is still needed by the current
  `Nunchaku` runtime path

## What Is Actually Needed Now

If the goal is the kept product stack, the disk story should be read like this:

- keep `Z-Image`
- keep the base `Z-Image-Turbo` model files required by the current
  `Nunchaku` path
- keep the `Nunchaku` `Z-Image` cache
- keep `TRELLIS.2`
- keep `Hunyuan 2mv`
- keep the `Hunyuan3D-2` texture-side cache used by `2mv`
- do not describe `Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled` as
  required

In other words:

- `TRELLIS.2` takes over the single-image 3D lane
- `Hunyuan 2mv` stays only as the multiview-specific lane
- the old Hunyuan text bridge is removed from the kept baseline
- the current `Nunchaku` and `2mv` implementations still depend on upstream base
  model assets that carry older repo/model names

## Practical Footprint Reading

For the current intended core stack, the important weight is approximately:

- `31G` for the base `Z-Image-Turbo` snapshot on this working machine
- `7.1G` for `Nunchaku` `Z-Image` cache
- `16G` for the `TRELLIS.2` model cache
- `19G` for `Hunyuan 2mv` cache
- `19G` for the `Hunyuan3D-2` texture-side cache used by `2mv`
- `6.3G` for the `Hunyuan3D-2` runtime used by `2mv`
- `5.4G` for `Z-Image`
- `2.3G` for `TRELLIS.2` repo/env

That is about `104.0G` before counting:

- CUDA
- apt/package-manager overhead
- temporary install files
- generated meshes, textures, and logs
- future updates

Optional experimental Parts currently adds about:

- `8.8G` for `Hunyuan3D-Part`

This does **not** include personal generated results or cached output folders
under `~/.cache/hunyuan3d-part/outputs`.

Important note:

- that `31G` figure reflects the current local `Z-Image-Turbo` cache on this
  machine
- the real minimum may be lower if the kept `Z-Image` base snapshot is trimmed
  to the filtered files the current runtime actually uses
- this note should treat `31G` as the current measured local weight, not a
  finalized optimized minimum

So the practical real-world recommendation should leave generous headroom above
the raw kept-stack total.

## Important Caveat

The upstream repos can still expose experimental or older code paths that may
download more weights than the kept product surface actually needs.

That should be documented as:

- extra cache weight may exist locally from experiments
- not every downloaded model should be treated as part of the shipped baseline
- user-generated outputs should not be counted as required install footprint
- the removed Hunyuan text bridge should not be counted as part of the current
  kept stack
- the kept baseline is `Z-Image via Nunchaku + TRELLIS.2 + Hunyuan 2mv`
- optional Parts should be counted separately from the core baseline

## Use In README

The future README should summarize this as:

- the kept stack is `Z-Image via Nunchaku + TRELLIS.2 + Hunyuan 2mv`
- optional `Hunyuan3D-Part` repo/env adds extra disk use and should be treated separately
- user-generated outputs should not be counted as required install footprint
- `TRELLIS.2` owns the single-image 3D lane
- `Hunyuan 2mv` is retained only for the multiview workflow
- `Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled` should not be presented
  as required baseline weight
- the current code still relies on base model assets behind `Nunchaku` and
  `2mv`, so those underlying caches should be counted until the runtime surface
  is slimmed further
