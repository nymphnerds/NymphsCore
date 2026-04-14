# NymphsCore Base Distro Strategy

This document resets the installer direction around a prebuilt WSL distro
instead of assembling the full runtime live on the user's machine.

The reference pattern is the local CHIM package provided for comparison:

- one obvious installer entry file
- one prebuilt distro tar
- optional components installed after the distro import

That is a better fit for `NymphsCore` than the current script-heavy first-run path.

## Core Decision

NymphsCore should:

1. import a prepared WSL distro to a user-chosen folder
2. keep the imported base image relatively small
3. defer large model downloads until after import
4. defer optional components until after import
5. use a real Windows installer UI later, but keep the import/finalize flow
   simple first

The immediate technical target is a `wsl --import` based workflow.

Later, this can evolve into a packaged `.wsl` distribution if that becomes
useful.

## Product Goal

The target user-facing product is a simple Windows app.

That app should:

- present one obvious installer entry point
- hide the builder/import/finalize mechanics from the user
- show clear progress and human-readable status
- let the user choose install location without learning WSL details
- install the small base distro first
- pull runtime pieces, models, and optional components after base install

The batch files and PowerShell helpers in this repo are transitional tooling for
building and testing the pipeline behind that app. They are not the intended
final beginner experience.

## Why The Current Approach Feels Wrong

The current installer is trying to do too much live:

- install or create WSL
- choose a distro
- install system packages
- install CUDA
- create Python environments
- download massive model caches
- repair and verify everything

That works poorly for a beginner because:

- the process is long
- the logs are technical
- silent steps look hung
- partial failures are hard to interpret
- the beginner has no mental model of what is happening

## What The Base Image Should Contain

The base distro should contain:

- a Linux base OS prepared specifically for `NymphsCore`
- the Nymphs3D helper repo
- backend repo clones without giant model caches
- basic shell tooling and package-manager configuration
- optional first-run helper scripts

The base distro should not contain:

- Hugging Face model caches
- optional helper model downloads
- user-specific logs and histories
- temporary files
- machine-specific caches

For the first pass, the safest default is also:

- do not bake Python virtual environments into the exported distro
- do not bake CUDA into the exported distro unless a separate NVIDIA-ready
  image is intentionally produced

## Size Guidance From The Working Machine

Measured on the current working machine:

- `~/Hunyuan3D-2` without `.venv`: about `833M`
- `~/Hunyuan3D-2.1` without `.venv`: about `797M`
- `~/Hunyuan3D-2/.venv`: about `6.1G`
- `~/Hunyuan3D-2.1/.venv`: about `9.2G`
- `/usr/local/cuda-13.0`: about `4.9G`

Model/cache weight that should stay out of the base image:

- `tencent/Hunyuan3D-2`: about `28G`
- `tencent/Hunyuan3D-2mv`: about `19G`
- `tencent/Hunyuan3D-2.1`: about `6.5G`
- `facebook/dinov2-giant`: about `4.3G`

This suggests three image profiles:

1. Lean base image
Contains OS + helper repo + backend source repos only.
Best for download size.

2. NVIDIA-ready base image
Lean base image + CUDA.
Bigger download, less post-import setup.

3. Prewarmed runtime image
NVIDIA-ready base image + Python environments.
Likely too large for general beginner distribution.

For now, the recommended target is the Lean base image.

## Recommended V2 Flow

1. Windows installer starts
2. user chooses install location, for example `D:\WSL\NymphsCore`
3. installer imports `NymphsCore.tar` with `wsl --import`
4. installer launches a post-import finalizer inside that distro
5. finalizer installs system/runtime pieces not baked into the image
6. installer offers model/component choices after base install succeeds
7. models download later, or on first use

## What Happens After Import

After importing the base distro, the finalizer should handle:

- system package sanity checks
- Python env creation
- optional CUDA installation
- backend package installation
- optional model downloads
- verification

This repo now includes a first-pass script for that:

- `scripts/finalize_imported_distro.sh`

## Builder Workflow

Do not export the day-to-day working distro directly.

Instead:

1. create a fresh small Ubuntu builder distro on the target drive
2. bootstrap it with only the helper repo and backend source repos
3. export that prepared builder distro as the distributable base image
4. unregister the disposable builder distro if desired

This avoids both:

- risking the real daily-use environment
- exporting a huge model-filled personal distro just to throw most of it away

## Next Practical Steps

1. capture a clean manifest from the working install
2. decide the exact base profile to target first
3. create a fresh disposable builder distro on `D:`
4. bootstrap only the base repos/layout into that builder distro
5. export `NymphsCore.tar`
6. import-test it to a separate location on another drive
7. wire the import flow into a Windows installer app

## Helper Scripts Added In This Repo

Working-install audit:

- `scripts/audit_working_install.sh`

Post-import finalize:

- `scripts/finalize_imported_distro.sh`

Windows-side import helper:

- `scripts/import_base_distro.ps1`

Windows-side builder helpers:

- `scripts/create_builder_distro.ps1`
- `scripts/export_builder_distro.ps1`

Fresh-builder bootstrap:

- `scripts/prepare_fresh_builder_distro.sh`

Example future flow:

1. run `scripts/audit_working_install.sh` to confirm the target footprint
2. create a fresh builder distro on `D:` with `scripts/create_builder_distro.ps1`
3. let the bootstrap clone only the helper repo and backend source repos
4. export that prepared builder distro as `NymphsCore.tar`
5. import that tar on a test machine with `scripts/import_base_distro.ps1`
6. run the post-import finalizer with or without model downloads

## Decision For This Repo

The current script installer should be treated as transitional tooling.

The mainline user experience should move toward:

- one simple Windows app
- base distro import behind that app
- guided post-import finalize behind that app
- optional component/model installs after the base image is ready
