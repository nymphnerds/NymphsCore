# Install Disk And Model Footprint

This document explains the current local disk footprint for the NymphsCore Manager install.

The current public runtime stack is:

- `TRELLIS.2` for single-image image-to-3D and texture/retexture workflows
- `Hunyuan 2mv` for multiview-guided workflows
- `Z-Image` / Nunchaku for local image generation

Older experimental lanes and local development experiments are not part of this baseline.

## Headline Numbers

Plan for:

- about `92 GB` installed for a ready-to-run local backend
- about `72 GB` of required model/helper downloads during model prefetch
- about `120 GB` free before install as a practical minimum
- about `150 GB` free before install for comfortable headroom

The install can grow over time as you create meshes, textures, logs, model cache files, and future runtime updates.

## Why The Recommendation Is Larger Than The Install

The free-space recommendation is higher than the final runtime footprint because installation needs room for:

- downloaded model snapshots
- Python wheels and package caches
- CUDA packages
- temporary install files
- WSL filesystem growth
- generated meshes and textures
- logs and future updates

Do not try to install with exactly the advertised final size available. Leave headroom.

## What The Manager Installs

The manager imports a dedicated WSL distro named:

```text
NymphsCore
```

Inside that distro, it prepares:

- CUDA 13.0 inside WSL
- system packages needed by the backend scripts
- the `Hunyuan3D-2` runtime family used by the `Hunyuan 2mv` lane
- the `Z-Image` / Nunchaku runtime used by local image generation
- the official `TRELLIS.2` runtime
- Python virtual environments for the supported backends
- model caches for the supported local workflows

The managed Linux user is:

```text
nymph
```

## Model Prefetch

Model prefetch downloads the large model files during setup.

With prefetch on:

- expect about `72 GB` of model/helper downloads
- the first install is slower
- first use from Blender is smoother

With prefetch off:

- setup skips those large downloads for now
- the manager still prepares the runtime stack
- missing models download later from `Runtime Tools` or first real addon use

Turning prefetch off is useful when you want to get the base runtime installed first, but it does not remove the need for those models.

## Current Runtime Breakdown

The current product baseline should be read like this:

- `TRELLIS.2` owns the single-image 3D lane
- `Hunyuan 2mv` is kept for multiview-guided workflows
- `Z-Image` / Nunchaku is kept for local image generation

Some folders and model caches still use upstream names such as `Hunyuan3D-2` because the multiview and texture paths depend on that runtime family. Seeing that name on disk does not mean every older Hunyuan workflow is part of the public product surface.

The manager UI currently lists these major download groups:

- `tencent/Hunyuan3D-2`: about `28 GB`
- `tencent/Hunyuan3D-2mv`: about `19 GB`
- `u2net` helper model: about `168 MB`
- `Tongyi-MAI/Z-Image-Turbo`: about `31 GB`

Runtime and environment pieces include:

- Hunyuan 2mv runtime repo folder: about `6.5 GB`
- Hunyuan 2mv Python `.venv`: about `6.1 GB`
- Z-Image Turbo via Nunchaku Python `.venv`: about `5.4 GB`
- CUDA 13.0 in WSL: about `4.9 GB`

These numbers are approximate. Upstream repositories and model snapshots can change size.

## Generated Output Growth

Generated files are not part of the required install footprint.

They can still become large:

- mesh exports
- texture maps
- preview images
- intermediate backend outputs
- repeated test generations

If the install drive starts filling up after successful setup, generated outputs are usually the first place to check.

## What Is Not Included

The headline footprint does not include:

- Blender itself
- the Blender addon package
- personal project files
- generated meshes and textures
- old local experiments
- archived legacy installer work
- optional developer checkouts outside the managed distro

It also does not include older removed model experiments that may exist on a development machine from previous testing.

## Practical Advice

For normal users:

- choose a drive with at least `120 GB` free
- prefer `150 GB` free
- leave model prefetch on if you want the smoothest first Blender session
- use `Runtime Tools` later if a model download was skipped or interrupted

For support:

- ask for the newest `installer-run-*.log`
- ask whether model prefetch was on
- ask how much free space remains
- ask whether the issue happened during import, CUDA setup, Python setup, model prefetch, or a smoke test
