# Install Disk And Model Footprint

This document explains the current local disk footprint for the NymphsCore Manager install.

The current public runtime stack is:

- `TRELLIS.2` for single-image image-to-3D and texture/retexture workflows
- `Z-Image` / Nunchaku for local image generation

Older experimental lanes and local development experiments are not part of this baseline.

`Nymphs-Brain` is now available as an optional experimental module, but it is not part of the baseline footprint numbers below unless you explicitly choose it.

## Headline Numbers

Plan for a large multi-GB install with comfortable headroom. The exact total depends on whether model prefetch is enabled, how much shared model cache is already present, and which optional modules you install.

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

## Optional Nymphs-Brain Footprint

`Nymphs-Brain` is optional. If you do not enable it, you do not need to budget for its extra Python/npm/helper files.

If enabled, it installs under:

```text
/home/nymph/Nymphs-Brain
```

That optional module can add:

- a Brain Python venv
- an MCP Python venv
- an Open WebUI Python venv
- local Node/npm tools
- LM Studio model cache growth
- Open WebUI data, logs, and Hugging Face helper cache

Important:

- the big Brain disk usage comes mostly from the selected local LLM model
- Open WebUI can also pull supporting embedding/helper assets on first startup
- these optional Brain downloads are separate from the core 3D backend model-prefetch numbers above

Practical advice:

- if you only want the Blender 3D workflows, leave Brain off and use the baseline numbers
- if you enable Brain, leave extra headroom beyond the normal `120-150 GB` recommendation
- if you select a larger coding model, expect additional multi-GB growth under the optional Brain install

## Current Runtime Breakdown

The current product baseline should be read like this:

- `TRELLIS.2` owns the built-in 3D lane
- `Z-Image` / Nunchaku is kept for local image generation

The manager UI currently lists these major download groups:

- `u2net` helper model: about `168 MB`
- `Tongyi-MAI/Z-Image-Turbo`: about `31 GB`
- `TRELLIS.2` model bundle: a large shared-cache download whose exact size depends on upstream snapshot changes

Runtime and environment pieces include:

- TRELLIS.2 runtime repo and Python environment
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
- optional `Nymphs-Brain` model growth unless you explicitly install that module

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
