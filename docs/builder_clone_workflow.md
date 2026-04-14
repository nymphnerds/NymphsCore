# Builder Workflow

This workflow creates a fresh small Ubuntu builder distro on `D:`, bootstraps it
with the Nymphs3D helper repo plus backend source repos, and then exports a
distributable `Nymphs3D2.tar`.

The goal is to avoid touching the real working distro and to avoid exporting the
entire huge day-to-day Ubuntu install.

## Overview

1. create a fresh Ubuntu builder distro on `D:`
2. bootstrap it with only the Nymphs3D helper repo and backend source repos
3. export that prepared builder distro
4. optionally unregister the builder distro

## Helper Scripts

Create the builder distro:

- `scripts/create_builder_distro.ps1`

Prepare the builder inside Linux:

- `scripts/prepare_fresh_builder_distro.sh`

Export the builder distro:

- `scripts/export_builder_distro.ps1`

## Suggested First Builder Run

Assumptions:

- fresh builder distro name: `Ubuntu-24.04`
- builder install location: `D:\WSL\Nymphs3D2-Builder`
- final distributable tar: `D:\WSL\Nymphs3D2.tar`

Create and bootstrap the fresh builder:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\create_builder_distro.ps1 `
  -BuilderInstallLocation D:\WSL\Nymphs3D2-Builder `
  -Force
```

That will:

- install a fresh Ubuntu builder distro on `D:`
- bootstrap `/opt/nymphs3d/Nymphs3D`
- clone backend source repos into `/opt/nymphs3d/runtime`
- keep venvs and model caches out of the base image

Export the builder:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\export_builder_distro.ps1 `
  -BuilderDistroName Ubuntu-24.04 `
  -OutputTarPath D:\WSL\Nymphs3D2.tar `
  -Force `
  -UnregisterBuilderAfterExport
```

## What The Builder Contains

The builder bootstrap is intended to leave you with:

- a fresh Ubuntu base
- `/opt/nymphs3d/Nymphs3D`
- `/opt/nymphs3d/runtime/Hunyuan3D-2`
- `/opt/nymphs3d/runtime/Hunyuan3D-2.1`
- profile exports that point runtime scripts at `/opt/nymphs3d`

The builder intentionally does not include:

- Hugging Face model caches
- Python virtual environments
- helper model downloads such as `u2net`
- baked user-specific home-directory junk

## Important Safety Rule

Do not point this workflow at your real working distro.

This fresh-builder method is specifically meant to avoid cloning/exporting the
huge daily-use `Ubuntu` install.
