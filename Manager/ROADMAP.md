# ROADMAP

This roadmap reflects the current state of `Nymphs3D` as a helper/setup repo for the local backend side of the workflow.

Installer-specific planning now lives here too:

- `docs/installer_roadmap_2026-04-06.md`
- `docs/windows_installer_app_v1_plan.md`

## Already Done

- `Nymphs3D` is now the public-facing helper repo name and local folder name
- the beginner install path is now centered on the WPF installer app under `apps/Nymphs3DInstaller`
- the install flow now:
  - checks Windows/WSL prerequisites
  - writes machine-specific `.wslconfig`
  - installs WSL dependencies
  - creates the locked Python environments
  - prefetches the main supported model weights
  - verifies the local install
- the repo now keeps the public-facing setup docs:
  - `absolute_beginner_local_install_guide.md`
  - `goblin_single_image_to_3d_example.md`
  - `install_disk_and_model_footprint.md`
- the standalone launcher exists as a Windows `.exe`
- the launcher can clear an in-use local port before startup
- the addon source has now moved out to a separate repo
- the backend install and repair flow is now substantially more reliable than before
- `Z-Image` is now the real local image-backend repo path instead of `~/Nymphs2D2`
- old local `ComfyUI-*` backend leftovers were removed from the active working tree
- TRELLIS now uses shared HF cache as its canonical model source instead of a repo-local `models/` bundle
- optional TRELLIS `flash-attn` optimization was proven on this machine with a targeted compute-capability build for `8.9`
- the Blender extension hotfix chain for the new image-preset system is live through:
  - `1.1.50`
  - `1.1.51`
  - `1.1.52`

## Current Repo Shape

This repo currently behaves as:

- a Windows + WSL backend setup helper
- a repair/update path for the forked Hunyuan repos
- a local runtime verification layer
- a home for backend helper files plus the standalone local launcher

The intended baseline is still:

- one Windows PC
- local WSL Ubuntu
- local Blender or another compatible client installed separately
- local server at `http://localhost:8080`

Important current direction change:

- `Hunyuan 2.1` is no longer part of the intended shipped stack
- `TRELLIS.2` is now the intended replacement experimental-to-product 3D lane
- `MVPaint` and `ComfyUI` should be treated as research harnesses only, not part of the shipped distro, launcher, or addon surface

The intended product boundary is:

- this repo = helper/setup/backend infrastructure
- separate product distribution = Blender addon/frontend
- this repo keeps the standalone launcher as helper/runtime tooling

The emerging product-family direction is:

- `Nymphs` = shared server/runtime layer
- `Nymphs3D2` = Hunyuan-focused 3D frontend
- `Nymphs2D2` = image-generation frontend/runtime layer
- optional advanced utilities can sit alongside those frontends when they serve
  a specific model family or workflow without becoming part of the default UX

That means the long-term server surface should be able to stand on its own
without being named like a 3D-only panel inside one specific addon.

## Current Direction

The current script-first installer is no longer the intended end-state user
experience.

The project is now heading toward:

- a simple Windows app as the main installer
- a small prebuilt WSL base distro as the install foundation
- runtime/model downloads after base install instead of baking everything into
  one huge first run
- a dedicated managed distro and user:
  - distro: `Nymphs3D2`
  - user: `nymphs3d`
- a managed distro that still mirrors the working home-based backend layout:
  - `~/Hunyuan3D-2`
  - `~/Z-Image`
  - `~/TRELLIS.2`

The existing batch files and PowerShell scripts remain important, but mainly as
the backend plumbing and test harness behind that future app.
The archived bootstrap installer now lives under `legacy/` and should not be
presented as the main path.

Important naming note:

- the managed distro name should be treated as installer/config state, not something baked permanently into the base tar import artifact
- renaming the managed distro later should primarily mean:
  - changing installer defaults
  - changing addon WSL-target defaults
  - updating docs and wording
- rebuilding the base tar should only be required if the actual contents of the base image need to change, not just because the imported distro name is being rebranded

## Immediate Priorities

These are the things still worth improving now.

### 1. Keep The Local Baseline Stable

- keep `2mv` startup stable from both the launcher and `Nymphs3D2`
- keep `TRELLIS.2` startup stable
- keep the shared-HF-cache TRELLIS model path stable
- keep optional `flash-attn` clearly non-mandatory unless a prebuilt-wheel story exists
- keep Blender import/handoff stable
- keep the Blender extension update path stable after the `1.1.50` to `1.1.52` preset hotfix chain
- keep texture generation stable
- keep status/progress useful without reintroducing heavy polling

### 2. Build The Base-Distro App Path

- keep the small base-distro workflow working on `D:`
- keep test imports separate from the real working `Ubuntu`
- finalize the post-import runtime setup flow
- keep the fresh-install runtime layout aligned with the working Ubuntu shape:
  - `~/Hunyuan3D-2`
  - `~/TRELLIS.2`
- keep the fresh default user flow stable:
  - `nymphs3d`
- document the pipeline clearly enough that it can be wrapped in a real app
- treat the current scripts as transitional tooling, not the final UX

### 3. Finish The Repo Split

- rewrite docs so this repo no longer reads like the paid addon product
- keep addon distribution out of this repo
- keep this repo focused on backend setup, repair, and verification

### 4. Write Real System Requirements

- write a thorough system requirements document before wider addon marketing
- frame it around the actual product shape:
  - paid Blender addon/frontend
  - local helper backend/runtime under WSL
- define supported and tested expectations clearly:
  - Windows version
  - WSL requirements
  - NVIDIA driver / CUDA expectations
  - GPU class and VRAM tiers
  - RAM
  - disk footprint
  - network/download expectations for first install
- separate:
  - minimum workable setup
  - recommended setup
  - currently tested reference machines
- make the support boundary explicit:
  - what is stable and intended for real users
  - what is still experimental or optimization-only
- make sure the addon docs stop implying that the frontend works in isolation when it actually depends on the helper backend

## Near-Term Improvements

These are reasonable next steps, but not emergency work.

### Texture Backend Evaluation

The remake has now proved:

- `Z-Image` is a viable image-generation lane
- `Hunyuan 2mv` is a viable shape lane
- `2mv` texture generation works, but is still the main slow bottleneck

So the next major backend investigation should be:

- evaluate a dedicated mesh-texture backend while keeping current `2mv` texture as the baseline fallback

Current direction:

- keep `2mv` texture as the real current baseline
- stop treating `MVPaint` as the likely successor
- keep `MVPaint` out of the shipped product path unless it later proves clearly better
- do not ship `ComfyUI` as part of the product architecture
- keep texture-backend research secondary to the now-working `Z-Image + 2mv + TRELLIS.2` stack

### TRELLIS Frontend Expansion

The official TRELLIS adapter is now working locally, and a frontend audit was
recorded here:

- `docs/trellis_frontend_audit_2026-04-10.md`

Current rule:

- do not flatten runtime controls, generation controls, and Blender cleanup
  controls into one crowded panel

Near-term TRELLIS UI additions worth exploring:

- add official `1024` TRELLIS shape mode alongside `512` and `1024_cascade`
- add TRELLIS runtime settings in the server panel:
  - attention backend
  - precision
  - VRAM / keep-loaded policy
- investigate whether TRELLIS remesh/export behavior should become a curated
  user-facing choice instead of staying mostly implicit
- investigate UV unwrap as a deliberate product decision:
  - backend-managed quick path
  - Blender-managed advanced path
  - or a mixed approach

Important product rule:

- mesh cleanup controls such as hole filling, component cleanup, and similar
  repair tools should probably grow as Blender-side finishing tools instead of
  being copied directly into the TRELLIS request panel

### Extension Release Hygiene

- keep addon release notes and extension feed versions aligned when hotfixes land quickly
- make sure Blender-facing error fixes are reflected in the extension feed immediately, not only in the addon source repo
- avoid shipping callback-backed Blender enums with invalid string defaults
- keep reload-safe registration behavior in place so extension metadata refreshes do not wedge the addon

### Remove `2.1` From The Product Surface

This is now an explicit product cleanup task, not just a future maybe.

Target end state:

- no `Hunyuan 2.1` in the addon UI
- no `Hunyuan 2.1` in launcher choices
- no `Hunyuan 2.1` in installer/runtime defaults
- no `Hunyuan 2.1` in the managed distro layout
- no `Hunyuan 2.1` in the shipped product docs except historical notes

Replacement direction:

- `TRELLIS.2` replaces `Hunyuan 2.1` as the secondary 3D lane
- `2mv` remains the practical current baseline

Important rule:

- remove `2.1` deliberately across addon, launcher, installer, distro, and docs as one coordinated cleanup
- do not leave half-removed `2.1` traces in product-facing places

### Helper Repo Quality

- cleaner helper repo positioning
- clearer separation from the paid addon/frontend
- less confusion around what is and is not included
- better wording around separately installed clients
- keep the launcher clearly framed as backend/runtime helper tooling
- shape the backend/server naming so it can support both `Nymphs3D2` and a
  future `Nymphs2D2` frontend cleanly

### Install And Distribution

- move toward one obvious Windows app entry point
- keep the current batch/PowerShell flow only as backend plumbing
- reduce beginner exposure to raw WSL concepts and terminal steps
- validate the exact first-run experience on another machine
- keep the launcher here but avoid mixing paid-addon distribution into this repo
- add GPU-tier-aware image runtime choices once the remake settles:
  - stronger cards can install fuller image runtimes
  - tighter cards should default to lighter runtime variants
  - `Z-Image` should not be treated as one fixed runtime path
  - current promising example: `Nunchaku` `INT4 r32` made `Z-Image-Turbo` practical on the target `4080 SUPER`
- if the managed distro name changes away from `Nymphs3D2`, do it as a coordinated installer/addon/docs rename instead of treating it as a base-image rebuild task
- once the image-runtime direction is finalized, re-evaluate whether the chosen Python environments should be baked into the exported base distro for smoother installs:
  - do not freeze experimental image envs into the tar too early
  - if the final runtime stack stops changing, prebuilt envs may be worth shipping to reduce user setup time and dependency pain
  - this should be decided after the `Z-Image` runtime choice settles, not before

### Optional Training Utilities

If `Z-Image` becomes a core image-model direction, add support for an optional
advanced training utility in the distro:

- candidate: `ostris/ai-toolkit`
- purpose: model training / fine-tuning utility for `Z-Image` and related
  image-model workflows
- launcher/addon role: provide a way to launch it from the managed runtime
- important rule: keep this out of the normal generation surface and treat it
  as an advanced tool, not the default user path

This should only be added in a way that preserves the clean split:

- `Nymphs2D2` = stable product-facing generation backend
- optional training utility = advanced, separate, model-specific workflow

### Managed Repo Update And Repair Path

The installer should become a safe updater for the backend/helper side of the
system, not only a fresh-install path.

Target behavior for an existing managed `Nymphs3D2` distro:

- detect every installer-owned repo
- check whether each repo is up to date
- fast-forward clean repos to the expected branch
- skip dirty, detached, or diverged repos without overwriting them
- rebuild only the backend pieces that actually changed
- run verification after the update
- log old commit, new commit, branch, and result for each managed repo

Repos the installer should own:

- the in-distro `Nymphs3D` helper repo, if that repo is part of the managed runtime
- `Hunyuan3D-2`
- `Nymphs2D2`
- `TRELLIS.2`

Repos the installer should not touch:

- `Nymphs3D-Blender-Addon`
- `Nymphs3D2-Extensions`

Recommended update policy:

- expected branch = `main`
- update method = fast-forward only
- if a repo has local changes, skip it and show a clear warning
- if a repo is detached or diverged, skip it and show a clear warning
- never silently overwrite user or debug changes inside the managed distro

Implementation shape:

- add a manifest for installer-owned repos with path, remote URL, branch, and
  repair/build responsibility
- add a `check_managed_repo_updates` step that fetches refs and reports repo
  state in a machine-readable way
- add an `apply_managed_repo_updates` step that clones missing repos and
  fast-forwards clean repos
- rerun backend install/build hooks only for repos whose commit changed
- surface update results in the installer UI and logs
- treat this as an explicit repair/update feature, not an accidental side
  effect of rerunning the installer

First practical milestone:

- implement repo status checks for the managed backend/helper repos
- update clean repos with fast-forward-only behavior
- rerun targeted backend repair/build after repo movement
- expose the result clearly in installer logs and verification output

## Future Backend Track: Hunyuan3D-Omni

`Hunyuan3D-Omni` looks interesting, but it is not a drop-in replacement for the current workflow.

What it appears to add:

- shape generation driven by structured control inputs such as:
  - point clouds
  - voxels
  - bounding boxes
  - poses

Why it matters:

- Blender is a good place to author or manipulate those control inputs
- this could become a new controlled-shape workflow later

Why it is not a near-term integration:

- it is a different input model from the current image/text-driven addon flow
- it would likely need its own API wrapper/server layer
- it does not look like a straightforward replacement for the current texture-oriented path

Practical future direction:

- treat `Omni` as a possible third backend family later
- keep current `Z-Image + 2mv + TRELLIS.2` flows as the production baseline
- only integrate `Omni` after the current local workflows are stable enough

## Longer-Term Ideas

These should stay clearly outside the default path until the baseline is boringly reliable.

### Optional Split Shape / Texture Passes

Still worth exploring later for lower-VRAM systems:

- shape-only run
- texture-only pass on an existing mesh

### Optional External Text-To-Image Fallback

Only if the local text path remains too weak:

- external text-to-image
- local image-to-3D
- local Blender workflow

### Eventual Simplification

Later, the product may converge toward:

- one paid Blender addon/frontend repo or package
- one helper/setup repo for backend onboarding and repair

## Priority Summary

1. Keep the current local Windows + WSL + Blender baseline stable.
2. Build the simple Windows app path on top of the base-distro workflow.
3. Finish separating helper-repo concerns from paid frontend-product concerns.
4. Treat `Hunyuan3D-Omni` as a future controlled-shape backend, not an immediate integration.
