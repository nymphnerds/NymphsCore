# No-Tar Audit Notes

This note captures what the original `NymphsCore.tar` flow appeared to provide, what is intentionally added later by finalize/setup, and what had to be audited before replacing the tar-required installer path.

Branch context:

- base branch: `no-hunyuan-2mv`
- experiment branch: `no-hunyuan-2mv-no-tar`

## Current Tar Role

The current installer is explicitly built around a pre-exported WSL base image:

- older Manager system checks required `NymphsCore.tar` to exist for a fresh install
- older fresh installs used `wsl --import ... <tar>`
- current lite no-tar work keeps a prebuilt tar as an optional faster path, but can bootstrap a fresh Ubuntu base locally when no tar is present

Relevant files:

- [Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs](/home/nymph/NymphsCore/Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs:846)
- [Manager/scripts/import_base_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/import_base_distro.ps1:198)
- [Manager/apps/Nymphs3DInstaller/Views/MainWindow.xaml](/home/nymph/NymphsCore/Manager/apps/Nymphs3DInstaller/Views/MainWindow.xaml:291)

## What The Builder Appears To Bake Into The Tar

The builder flow is:

1. create a fresh Ubuntu WSL distro
2. install a few bootstrap packages as root
3. clone the helper repo into `/opt/nymphs3d/Nymphs3D`
4. clone backend source repos into `/opt/nymphs3d/runtime`
5. clean caches, venvs, and temporary state
6. export that distro as `NymphsCore.tar`

Relevant files:

- [Manager/scripts/create_builder_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/create_builder_distro.ps1:67)
- [Manager/scripts/prepare_fresh_builder_distro.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_fresh_builder_distro.sh:17)
- [Manager/scripts/prepare_base_distro_export.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_base_distro_export.sh:63)

The exported base image appears to intentionally include:

- helper repo snapshot at `/opt/nymphs3d/Nymphs3D`
- `Z-Image` repo checkout
- `TRELLIS.2` repo checkout
- shell profile wiring for `NYMPHS3D_HELPER_ROOT`, `NYMPHS3D_RUNTIME_ROOT`, `NYMPHS3D_Z_IMAGE_DIR`, `NYMPHS3D_N2D2_DIR`, and `NYMPHS3D_TRELLIS_DIR`
- a generally cleaned Ubuntu base

## What The Tar Intentionally Does Not Contain

The scripts strongly suggest the tar is meant to stay lean and defer heavier pieces until after import.

Explicitly removed or excluded:

- Python virtual environments
- Hugging Face model caches
- helper model downloads like `u2net`
- transient outputs and package-manager caches

Relevant files:

- [Manager/scripts/prepare_fresh_builder_distro.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_fresh_builder_distro.sh:53)
- [Manager/scripts/prepare_base_distro_export.sh](/home/nymph/NymphsCore/Manager/scripts/prepare_base_distro_export.sh:79)
- [Manager/scripts/finalize_imported_distro.sh](/home/nymph/NymphsCore/Manager/scripts/finalize_imported_distro.sh:16)

## What Finalize Adds After Import

After the tar is imported, finalize/setup handles the heavier runtime work:

- Linux package install/update
- CUDA installation
- backend environment creation
- model prefetch
- install verification

Relevant files:

- [Manager/scripts/finalize_imported_distro.sh](/home/nymph/NymphsCore/Manager/scripts/finalize_imported_distro.sh:72)
- [Manager/scripts/run_finalize_in_distro.ps1](/home/nymph/NymphsCore/Manager/scripts/run_finalize_in_distro.ps1:143)

Important note:

- `run_finalize_in_distro.ps1` can already use the packaged Windows-side scripts instead of relying entirely on helper scripts inside the distro. That is a strong signal that a no-tar bootstrap path is realistic.

## Main Risk For A No-Tar Installer

The tar may still carry frozen custom state that is not fully reproduced by public scripts.

Likely risk categories:

- helper repo branch or fork differences at export time
- backend repo branch/fork differences at export time
- Linux package assumptions not captured in the finalize/bootstrap scripts
- extra config files under `/etc`, `/opt`, `/usr/local`, `/home/nymph`, or `/root`
- permissions/ownership details that happen to be correct in the tar but are not currently created by script

This matters because the current builder does not just export stock Ubuntu. It exports a curated snapshot after bootstrap.

## Audit Checklist Before Removing Tar

Compare a working tar-based install against what the scripts guarantee.

Audit these areas first:

- `/opt/nymphs3d/Nymphs3D`
  - branch, remote, local modifications, untracked files
- `/home/nymph/Z-Image` or `/opt/nymphs3d/runtime/Z-Image`
  - branch, remote, local modifications, untracked files
- `/home/nymph/TRELLIS.2` or `/opt/nymphs3d/runtime/TRELLIS.2`
  - branch, remote, local modifications, untracked files
- `/etc/profile.d/nymphscore.sh`
- `/etc/wsl.conf`
- `/etc/sudoers.d/90-*-nymphscore`
- package set differences from a plain Ubuntu-24.04 WSL install
- any files under `/usr/local` that are not recreated by scripts
- any files under `/home/nymph` that are required before finalize runs

Questions to answer:

- Does the tar contain only script-reproducible state?
- Are any local forks or edited repos silently frozen into the base image?
- Does the current Brain installer rely on anything "already in the base distro" that is not recreated elsewhere?
- Can a plain Ubuntu bootstrap plus packaged scripts create the same end state?

## Best Validation Path

Safest validation order:

1. Inspect one known-good tar-based install.
2. Record repo remotes, branches, and local diffs for helper/backend folders.
3. Record non-obvious config files and package assumptions.
4. Prototype a fresh-Ubuntu bootstrap distro without removing the tar path yet.
5. Compare the resulting installed runtime against the tar-based runtime.
6. Only remove tar requirements after those two paths converge.

## Current Conclusion

Replacing the tar path looks feasible.

But the tar should be treated as a potentially customized snapshot until a live tar-based install is audited. The core risk is not WSL import mechanics; it is hidden state that may exist in the exported image but not yet in reproducible scripts.
