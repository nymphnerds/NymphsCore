# Nymphs3D Platform Changelog

This changelog lives in the main repo because `Nymphs3D` now owns the Windows
installer, launcher, WSL distro/bootstrap flow, and runtime setup story.

This file covers the platform-side project history from the earliest local WSL
helper bundle through the current installer-app and packaged-launcher state. It
does not replace the Blender addon changelog; it tracks the installer, launcher,
and runtime-support side of the work.

## High-Level Project Arc

Across the documented history in this repo, the platform work moved through
seven phases:

1. initial WSL install-and-run bundle for a forked local Hunyuan setup
2. launcher prototype work and the first one-click install flow
3. lock-based runtime setup, repo renaming, and cleaner helper-repo boundaries
4. real clean-machine installer hardening from fresh Windows testing
5. base-distro import strategy and the dedicated `Nymphs3D2` distro direction
6. Windows installer app work, packaged release flow, and beginner-facing docs
7. repair/resume hardening, packaged-script sync, launcher cleanup, and final
   archive-format validation on `main`

## Detailed Timeline

Newest entries first.

### 2026-04-13 flash-attn survivability pass and manager repair-flow cleanup
Source: fresh Windows installer testing, TRELLIS flash-attn failure analysis, and a broad manager UX/debugging pass
Context: the managed install flow had reached the point where `flash-attn` source builds, repo-update messaging, and runtime-tool logging were the remaining trust-breakers. The goal of this pass was to make reruns safer, update behavior more honest, and long-running backend setup steps easier to reason about on a real Windows machine.

Documented changes:

- hardened the managed `TRELLIS.2` install path around optional `flash-attn`
- added explicit pre-build diagnostics to the TRELLIS installer path, including:
  - Python and pip versions
  - torch and CUDA visibility
  - `CUDA_HOME`
  - `TORCH_CUDA_ARCH_LIST`
  - `nvidia-smi`
  - `nvcc --version`
  - RAM and swap snapshots
  - `ulimit`
- changed `flash-attn` build handling so the installer now preserves the full build log under the TRELLIS repo instead of losing the useful failure context
- added safer `flash-attn` parallelism selection:
  - explicit override support through `NYMPHS3D_TRELLIS_FLASH_ATTN_MAX_JOBS`
  - low-memory mode through `NYMPHS3D_TRELLIS_FLASH_ATTN_LOW_MEMORY`
  - automatic fallback to `MAX_JOBS=1` on tighter-memory or no-swap systems
  - otherwise capped builds at `MAX_JOBS=2` instead of unconstrained compile fan-out
- improved failure reporting so the manager/installer now prints the preserved log path and a useful tail when `flash-attn` fails
- cleaned up manager wording so rerunning the latest manager is now the intended repair/update story instead of a misleading separate `Update` concept
- changed existing-install action language toward `Repair / Refresh`
- removed UI wording that implied a lightweight updater path distinct from the normal repair/install workflow
- kept `Experimental Parts Tools` as an install-time optional path and clarified that users can add it later by rerunning the latest manager with that option enabled
- extended managed update-check summaries so they now include:
  - the helper repo
  - `Hunyuan3D-Part`
  - the existing core runtime repos
- improved process log readability inside the manager by labeling stderr output instead of making every stderr line look like a hard app failure
- improved `Z-Image` model-prefetch feedback so long downloads now emit manager-friendly heartbeat lines rather than looking frozen
- rebuilt and repackaged the manager zip without bundling `NymphsCore.tar`

Why it matters:

- slow `flash-attn` builds now look like slow builds instead of sudden opaque deaths
- Windows users get much better evidence when TRELLIS optional acceleration really does fail
- the manager UI now matches what the installer actually does: rerun to repair/refresh the managed stack
- `Hunyuan3D-Part` is treated more honestly as an optional runtime add-on instead of a confusing half-updater concept
- runtime download/setup logs are less likely to read as random broken behavior during long installs

### 2026-04-10
Source: kept-stack cleanup, installer updater cleanup pass, and remake-branch packaging refresh
Context: after narrowing the intended shipped stack to `Z-Image + TRELLIS.2 + Hunyuan 2mv`, the helper repo still had stale docs, stale public download links, and updater behavior that was too sensitive to generated runtime folders.

Documented changes:

- rewrote footprint and strategy docs around the kept stack:
  - `Z-Image` via `Nunchaku`
  - official `TRELLIS.2`
  - `Hunyuan 2mv`
- removed the old Hunyuan text-bridge requirement from the platform-side story:
  - `HunyuanDiT` cache no longer counted as required
  - `Hunyuan3D-2/api_server.py` now rejects text-only requests instead of loading the old bridge model
- added legacy-runtime cleanup support for older managed installs:
  - new `scripts/prune_legacy_runtime.sh`
  - finalize/install flows now prune obsolete `Hunyuan3D-2.1` and old runtime leftovers before proceeding
- corrected the remake-branch README so installer and launcher download links point at `exp/2mv-remake` artifacts instead of accidentally sending testers to `main`
- extended existing-install updater reporting so it now includes `TRELLIS.2`
- changed updater dirty-check behavior so generated or user-output folders no longer block safe update checks:
  - `Prompts/`
  - `output/`
  - `outputs/`
  - `gradio_cache/`
  - `gradio_cache_tex/`
  - `__pycache__/`
- taught the updater to treat repo-local TRELLIS runtime layout as normal managed state:
  - `models/`
  - local adapter `scripts/`
  - `output/`
- rebuilt and pushed refreshed `Nymphs3DInstaller-win-x64.zip` archives after those updater-cleanup changes

Why it matters:

- the public remake-branch installer download now points at the correct branch artifact
- older managed installs can be cleaned up more deliberately instead of carrying dead `2.1` baggage forever
- normal generated content stopped looking like a code modification during updater checks
- the updater can now report the real three-backend managed stack instead of the older two-backend summary
- the platform docs now match the current intended shipped stack instead of the older mixed-backend story

### 2026-04-10 later
Source: local runtime naming cleanup, TRELLIS cache migration, export cleanup correction, and kept-stack pruning
Context: after the stack was narrowed to `Z-Image + TRELLIS.2 + Hunyuan 2mv`, the local repo layout and model-storage story still mixed legacy names and inconsistent backend conventions.

Documented changes:

- renamed the local image backend repo path from `~/Nymphs2D2` to `~/Z-Image`
- updated helper/runtime defaults so active paths now prefer `~/Z-Image`
- kept legacy `NYMPHS2D2_*` env names as compatibility fallbacks instead of breaking the current launcher/addon flow in one step
- updated runtime-facing `Z-Image` backend labels so the API no longer identifies itself as `Nymphs2D2`
- removed leftover local `ComfyUI-*` repos that were no longer part of the intended shipped stack:
  - `ComfyUI-Env-Manager`
  - `ComfyUI-GeometryPack`
  - `ComfyUI-Pulse-MeshAudit`
  - `ComfyUI-Trellis2`
- corrected base-distro export cleanup behavior so generated backend outputs are stripped from the builder distro without deleting the output directory paths themselves
- updated the public helper README to clarify:
  - managed runtime layout now uses `~/Z-Image`
  - backend venvs are not normally baked into the base distro tar
  - `flash-attn` is optional
  - `sdpa` is the stable TRELLIS fallback
- added a roadmap requirement to write a real system requirements document before wider addon marketing
- changed TRELLIS platform-side tooling so the canonical model source is now the shared Hugging Face cache for `microsoft/TRELLIS.2-4B`
- completed the TRELLIS cache migration locally:
  - full TRELLIS model bundle downloaded into shared HF cache
  - old repo-local TRELLIS model bundle removed from `~/TRELLIS.2/models`
  - TRELLIS smoke test passed after the repo-local model copy was removed

Why it matters:

- local repo names now match what the backends actually are instead of leaking older product placeholders
- the kept-stack machine layout is cleaner and easier to explain to users
- TRELLIS now follows the same shared-cache model-storage story as the other Hugging Face-backed components
- the TRELLIS repo stopped carrying a duplicate `15G` local model bundle and dropped to a much cleaner code/runtime footprint
- export behavior is safer for future distro rebuilds because generated outputs are no longer supposed to leak into the tar

### 2026-04-08
Source: installer archive format regression and final branch promotion cleanup
Context: after a packaging refresh, the tracked installer download turned out to
be a tar archive with a `.zip` name, and the former `base_distro_v2` line was
in the process of becoming the real mainline.

Documented changes:

- verified that the tracked `Nymphs3DInstaller-win-x64.zip` was actually a
  POSIX tar archive
- changed `apps/Nymphs3DInstaller/build-release.ps1` to build the installer
  archive through `.NET` `ZipFile`
- added a `PK` header validation guard before moving the built archive into
  place
- rebuilt the tracked installer artifact and verified it as a real zip
- promoted the former `base_distro_v2` work to `main` and removed the duplicate
  branch label

Why it matters:

- the public Windows installer download now behaves like a normal zip instead
  of a misleading tarball
- the repo no longer splits the active installer history across two branch
  names for the same code

### 2026-04-08 later
Source: managed backend updater flow and noob-first update UX pass
Context: once existing-install repair was working, the next gap was giving the
installer a safe way to check backend repo updates, apply clean fast-forward
updates, and explain the result in normal-user language instead of raw git
state.

Documented changes:

- added managed repo inspection and safe fast-forward logic for:
  - `Hunyuan3D-2`
  - `Hunyuan3D-2.1`
- kept packaged `Nymphs3D` helper scripts as the authoritative installer logic
  instead of trying to self-update helper scripts in place during a repair run
- added a dedicated `Check for Updates` path in the installer app for existing
  `Nymphs3D2` installs
- moved the update check to a direct `wsl.exe` call instead of tunneling it
  through the heavier finalize wrapper path
- changed update-check output from raw `repo=...|state=...` diagnostics into a
  plain-English summary for normal users
- made the primary action labels stateful for existing installs so available
  updates surface as `Update`
- changed finish-state messaging so the app can now distinguish:
  - `Install Complete`
  - `Update Complete`
  - `Already Up To Date`
- added clearer progress wording around the long `custom_rasterizer` build
  pause in the `Hunyuan3D-2.1` setup path

Related backend repo fix:

- pushed `f47a33c` to `Babyjawz/Hunyuan3D-2.1`
- added `gradio_cache_tex/` to `.gitignore` so generated texture output no
  longer creates a false dirty state during installer update checks

Why it matters:

- the installer can now tell users whether backend updates exist without
  forcing them to understand git internals
- clean existing installs can be updated more safely through the installer
- generated texture-cache output stopped looking like a meaningful local code
  modification in `Hunyuan3D-2.1`

### 2026-04-07 late follow-up +0100
Source: installer repair/resume and runtime handoff hardening
Context: real installer runs showed stale in-distro helper scripts, confusing
model-prefetch feedback, and rough recovery behavior when a managed distro
already existed.

Documented changes:

- changed the installer app to repair or continue an existing `Nymphs3D2`
  install instead of always unregistering it
- added an optional Hugging Face token field and clearer model-download
  messaging
- synced packaged helper scripts into the distro before finalize so the live
  run uses the same logic as the current installer package
- changed model-prefetch logging from tqdm-style output to installer-friendly
  heartbeat lines
- hardened launcher backend shutdown so stale local WSL servers are cleaned up
  more aggressively

Why it matters:

- rerunning the installer stopped feeling like a destructive reset
- finalize behavior became more trustworthy because packaged scripts and active
  in-distro scripts were aligned
- launcher stop/start behavior became more predictable during long local backend
  sessions

### 2026-04-07 packaging and guided bootstrap pass +0100
Source: installer follow-up handoff and packaging refresh
Context: once the WPF installer app existed, the next gap was making the
package and first-run story understandable for beginners.

Documented changes:

- made missing tar handling clearer and surfaced a direct `Nymphs3D2.tar`
  download hint
- kept `Show Logs` visible and improved failed system-check presentation
- rewrote the root README and installer README for normal users instead of
  source-build users
- added the first guided WSL bootstrap flow through
  `wsl --install --no-distribution`
- kept the dedicated managed-distro direction centered on `Nymphs3D2`

Why it matters:

- the project moved from "developer tooling with docs" toward a real
  beginner-facing install flow
- the repo's public entry points started matching the intended product story

### 2026-04-06 to 2026-04-07
Source: `base_distro_v2` strategy docs and early installer-app work
Context: the earlier all-live install approach was too long, too technical, and
too fragile for the intended Windows user path.

Documented changes:

- adopted `wsl --import` around a prepared base distro instead of building the
  full runtime live from scratch on the user's machine
- added builder, export, import, and finalize helper scripts for a disposable
  builder-distro workflow
- scaffolded a real Windows installer app shell in `apps/Nymphs3DInstaller`
- introduced dedicated-distro expectations:
  - distro name `Nymphs3D2`
  - Linux user `nymphs3d`
  - repo layout under the managed home directory
- added logging, progress, system checks, install-location handling, and an
  optional model-prefetch flow to the installer-app line

Why it matters:

- this is the architectural pivot that made the current installer possible
- separating "import a prepared base" from "finish runtime setup" made the
  whole flow easier to explain, test, and recover

### 2026-04-05
Source: first fresh Windows + WSL install test and the installer issue changelog
Context: a real beginner-style machine test exposed the difference between a
script that works on the dev box and a setup flow that survives a clean user
machine.

Documented changes:

- refreshed stale packaged installer artifacts
- hardened Ubuntu detection and logged visible WSL distros
- fixed broken generated `.wslconfig` values
- allowed interactive `sudo` inside the WSL-side install
- fixed logging crashes on empty lines and PowerShell interpolation damage in
  embedded bash
- corrected Python selection so `Hunyuan3D-2` used `3.10` and `Hunyuan3D-2.1`
  moved to `3.11`
- excluded repo-local packages from raw lockfile pip installs and fixed local
  extension build isolation
- fixed PyTorch CUDA wheel installation so it uses the right wheel index

Why it matters:

- this is the first point where the installer path became materially believable
  on a non-dev Windows machine
- a large chunk of the later base-distro work depended on lessons from this
  failure-heavy but useful test cycle

### Early helper-repo boundary and launcher packaging phase
Commits: `d6d9f4c` through `4aa2574`
Context: the project needed a cleaner separation between Blender-side addon work
and Windows/backend-side helper work.

Documented changes:

- split the addon out into a separate repo and cleaned helper-repo README
  boundaries
- kept launcher binaries and direct download links visible while the repo was
  still the main way to hand files to testers
- refined beginner docs so launcher and addon roles were less mixed together
- renamed installer entry points and docs around the `Nymphs3D` naming line

Why it matters:

- this is where the current three-repo shape started to make sense
- it stopped the main repo from trying to be both the addon product and the
  backend/setup tooling at the same time

### Early launcher and one-click install phase
Commits: `c3b097c` through `5afadd1`
Context: once the first local WSL helper bundle existed, the next problem was
turning that into something repeatable enough to launch and reinstall without
living entirely in the terminal.

Documented changes:

- added the first launcher prototype
- added one-click install flow updates and working-environment lock installs
- introduced `Nymphs3D2` naming and harder install-flow assumptions
- iterated on roadmap and install docs around text-mode and multiview support

Why it matters:

- this is where the repo stopped being just a pile of helper scripts and became
  a usable local setup tool
- the launcher became a real bridge between Windows-side actions and WSL-side
  backend control

### Project start
Commit: `8df172f`
Title: `Add initial WSL install and run bundle for forked Hunyuan setup`

Documented changes:

- introduced the first bundled WSL install and run flow for the forked Hunyuan
  backend setup
- established the repo as the place to collect Windows-facing setup steps,
  scripts, and docs

Why it matters:

- this is the root of every later installer and launcher iteration in this repo
- without this first bundle, there would be no later one-click flow, launcher,
  or base-distro installer line

## Current State

The active role of this repo is now:

- Windows installer app
- packaged launcher
- WSL base-distro import and finalize workflow
- runtime helper scripts and verification tooling

The Blender addon product surface now lives separately in:

- `Nymphs3D-Blender-Addon`
