# No-Tar Bootstrap Actions

This note turns the tar audit into an implementation checklist for a real `nymphscore-lite-no-tar` installer path.

Related notes:

- [NO_TAR_AUDIT.md](/home/nymph/NymphsCore/docs/NO_TAR_AUDIT.md:1)
- [NO_TAR_LIVE_AUDIT_CHECKLIST.md](/home/nymph/NymphsCore/docs/NO_TAR_LIVE_AUDIT_CHECKLIST.md:1)
- [TAR_OBSOLETE_FINDINGS.md](/home/nymph/NymphsCore/docs/TAR_OBSOLETE_FINDINGS.md:1)

## Goal

Replace the current `wsl --import ... NymphsCore.tar` flow with a bootstrap flow that:

- installs a plain Ubuntu WSL distro
- creates the managed `nymph` user
- seeds the minimum helper/runtime layout
- runs the existing finalize/setup scripts
- does not depend on a separately hosted tar artifact

## What Must Be Recreated

These are the pieces the tar currently provides that still matter.

### 1. A Fresh Ubuntu WSL Distro

The no-tar path still needs a base distro to exist before any finalize scripts run.

Current reference:

- [create_builder_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/create_builder_distro.ps1:64)
- [import_base_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/import_base_distro.ps1:158)

Needed behavior:

- install `Ubuntu-24.04` or another explicitly supported distro through `wsl --install`
- install it into the chosen Windows location
- ensure first boot can be automated from `root`

### 2. Managed Linux User Setup

The current import flow creates the `nymph` user, grants passwordless sudo, and writes `/etc/wsl.conf`.

Current reference:

- [import_base_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/import_base_distro.ps1:39)

Needed behavior:

- create `nymph` if missing
- add `nymph` to `sudo`
- create `/etc/sudoers.d/90-nymph-nymphscore`
- write `/etc/wsl.conf` with:
  - `[user] default=nymph`
  - `[boot] systemd=true`
- restart the distro after writing `wsl.conf`

### 3. Helper Repo At A Stable Path

Several scripts still assume the helper repo exists at `/opt/nymphs3d/NymphsCore` or `/opt/nymphs3d/Nymphs3D`.

Current reference:

- [create_builder_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/create_builder_distro.ps1:77)
- [common_paths.sh](/home/nymph/NymphsCore/Manager/scripts/common_paths.sh:3)
- [install_nymphs_brain.sh](/home/nymph/NymphsCore/Manager/scripts/install_nymphs_brain.sh:337)

Needed behavior:

- clone the helper repo during bootstrap, before finalize
- choose one canonical path and make the scripts agree on it
- keep `/opt/nymphs3d/Nymphs3D` compatibility only if some paths still hard-code it

Important note:

- this is one of the biggest remaining no-tar risks, because the tar currently freezes helper provenance

### 4. Runtime Path Normalization

The current tar/import flow standardizes runtime locations through `/etc/profile.d/nymphscore.sh`.

Current reference:

- [prepare_fresh_builder_distro.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_fresh_builder_distro.sh:65)
- [finalize_imported_distro.sh](/home/nymph/NymphsCore/Manager/scripts/finalize_imported_distro.sh:44)
- [run_finalize_in_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/run_finalize_in_distro.ps1:74)

Needed behavior:

- write `/etc/profile.d/nymphscore.sh`
- export:
  - `NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/NymphsCore` or the chosen compatibility path
  - `NYMPHS3D_RUNTIME_ROOT="$HOME"`
  - `NYMPHS3D_Z_IMAGE_DIR="$HOME/Z-Image"`
  - `NYMPHS3D_N2D2_DIR="$NYMPHS3D_Z_IMAGE_DIR"`
  - `NYMPHS3D_TRELLIS_DIR="$HOME/TRELLIS.2"`

### 5. Minimum Base Packages Before Finalize

Some scripts require basic packages before they can do their own work.

Current reference:

- [create_builder_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/create_builder_distro.ps1:72)
- [preflight_wsl.sh](/home/nymph/NymphsCore/Manager/scripts/preflight_wsl.sh:18)
- [install_system_deps.sh](/home/nymph/NymphsCore/Manager/scripts/install_system_deps.sh:4)
- [install_nymphs_brain.sh](/home/nymph/NymphsCore/Manager/scripts/install_nymphs_brain.sh:292)

Needed behavior:

- make sure the bootstrap distro already has:
  - `bash`
  - `sudo`
  - `git`
  - `curl`
  - `wget`
  - `ca-certificates`
  - `python3`
  - `python3-venv`
  - `python3-pip`
  - `tar`
- then let [install_system_deps.sh](/home/nymph/NymphsCore/Manager/scripts/install_system_deps.sh:1) install the fuller package set

Important note:

- `install_nymphs_brain.sh` explicitly says missing `curl`, `tar`, and a Python venv-capable interpreter "must be in the base distro"

### 6. Repo Bootstrap For Managed Backends

The tar currently pre-seeds backend source trees. Finalize later builds envs around them.

Current reference:

- [prepare_fresh_builder_distro.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_fresh_builder_distro.sh:43)
- [install_nymphs2d2.sh](/home/nymph/NymphsCore/Manager/scripts/install_nymphs2d2.sh:18)
- [install_trellis.sh](/home/nymph/NymphsCore/Manager/scripts/install_trellis.sh:121)

Needed behavior:

- either:
  - clone `Z-Image` and `TRELLIS.2` during bootstrap, matching current tar behavior
- or:
  - let finalize perform the first clone through `managed_repo_apply`

Recommended choice:

- let finalize own the repo checkout unless an earlier step truly needs the trees to exist

That keeps the bootstrap smaller and avoids freezing source state the way the tar does.

## What Can Be Dropped

These tar-carried pieces should not be reproduced in the lite no-tar path.

### 1. Hunyuan Trees And Vars

The tar still contains old Hunyuan history that this branch no longer wants.

Do not reproduce:

- `/opt/nymphs3d/runtime/Hunyuan3D-2`
- `/opt/nymphs3d/runtime/Hunyuan3D-Part`
- `NYMPHS3D_H2_DIR`
- `NYMPHS3D_PARTS_DIR`

Reference:

- [TAR_OBSOLETE_FINDINGS.md](/home/nymph/NymphsCore/docs/TAR_OBSOLETE_FINDINGS.md:1)

### 2. Full Frozen Git Snapshots

The tar contains full `.git` metadata and old remotes from older forks.

Do not reproduce:

- baked repo histories from `Babyjawz/*`
- stale helper repo identity under `Nymphs3D`
- obsolete branch heads just because the tar had them

Instead:

- use current scripted remotes under `nymphnerds/*`
- let managed repo scripts control checkout state

### 3. Cache And Build Artifacts

The tar builder already tries to exclude these, and the no-tar path should keep doing that.

Do not pre-seed:

- venvs
- Hugging Face caches
- pip caches
- CUDA build caches
- helper model downloads like `.u2net`

## Installer Changes Likely Needed

### 1. Add A No-Tar Fresh-Install Mode

Main target:

- [import_base_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/import_base_distro.ps1:1)

Needed change:

- add a fresh-bootstrap mode that uses `wsl --install --distribution Ubuntu-24.04 --location ... --no-launch`
- keep the existing tar import path for fallback until the new path is proven

### 2. Move Root Bootstrap Into A Shared Script

Main targets:

- [create_builder_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/create_builder_distro.ps1:67)
- [prepare_fresh_builder_distro.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_fresh_builder_distro.sh:1)

Needed change:

- extract the reusable root bootstrap steps from the builder flow
- reuse them for installer-time no-tar setup

This should become the heart of the zero-tar path.

### 3. Normalize Helper Path Naming

Main targets:

- [common_paths.sh](/home/nymph/NymphsCore/Manager/scripts/common_paths.sh:3)
- [run_finalize_in_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/run_finalize_in_distro.ps1:107)
- [install_nymphs_brain.sh](/home/nymph/NymphsCore/Manager/scripts/install_nymphs_brain.sh:337)

Needed change:

- decide whether the canonical helper path is:
  - `/opt/nymphs3d/NymphsCore`
  - or `/opt/nymphs3d/Nymphs3D`
- then add compatibility shims only where necessary

### 4. Update Manager UX And Validation

Main target:

- [InstallerWorkflowService.cs](/home/nymph/NymphsCore/Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs:846)

Needed change:

- stop hard-failing fresh installs on a missing tar when no-tar mode is selected
- change status text and readiness checks to describe bootstrap instead of tar import

## Recommended Implementation Order

1. Create a shared root bootstrap script that installs the minimum base packages, clones the helper repo, writes `nymphscore.sh`, and prepares `/opt/nymphs3d`.
2. Add a new no-tar branch to [import_base_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/import_base_distro.ps1:1) that uses plain Ubuntu install plus that bootstrap script.
3. Keep the tar path intact as a fallback while testing.
4. Normalize helper path naming so finalize, Brain, and common path logic stop disagreeing.
5. Test a full fresh install on `nymphscore-lite-no-tar`.
6. Only after that, remove tar-required messaging from Manager and docs.

## Current Bottom Line

The no-tar path is still feasible.

The main blockers are not model installs or backend setup. They are:

- bootstrapping the distro as `root`
- making helper-path assumptions consistent
- avoiding reproduction of the tar's stale frozen state

That is a manageable refactor, and this checklist is the safest place to start.
