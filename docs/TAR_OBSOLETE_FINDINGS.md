# Tar Obsolete Findings

This note summarizes why the current `NymphsCore.tar` looks increasingly obsolete for the active `nymphscore-lite` direction.

Context:

- branch: `nymphscore-lite-no-tar`
- tar inspected: `/home/nymph/NymphsCore.tar`
- tar size observed locally: about `3.1 GB`

## Executive Summary

Yes, this report is useful.

The tar is not just a neutral Ubuntu bootstrap artifact. It contains a frozen snapshot of older helper/runtime state, including legacy repo names, old fork remotes, and removed product lanes. That means:

- it is valuable evidence for no-tar planning
- it should not be treated as the current product truth
- it is now likely preserving obsolete history as much as useful bootstrap state

## What The Tar Contains

The tar includes:

- `/opt/nymphs3d/Nymphs3D`
- `/opt/nymphs3d/runtime/Z-Image`
- `/opt/nymphs3d/runtime/TRELLIS.2`
- `/opt/nymphs3d/runtime/Hunyuan3D-2`
- `/opt/nymphs3d/runtime/Hunyuan3D-Part`
- `/etc/profile.d/nymphscore.sh`
- `/etc/wsl.conf`

This confirms it is a curated exported image, not just a blank distro.

## Frozen Repo Remotes Inside The Tar

Extracted `.git/config` files show:

- helper repo in tar:
  - path: `/opt/nymphs3d/Nymphs3D`
  - origin: `https://github.com/Babyjawz/Nymphs3D.git`
  - branch: `main`
- Z-Image repo in tar:
  - path: `/opt/nymphs3d/runtime/Z-Image`
  - origin: `https://github.com/Babyjawz/Nymphs2D2.git`
  - branch: `main`
- TRELLIS.2 repo in tar:
  - path: `/opt/nymphs3d/runtime/TRELLIS.2`
  - origin: `https://github.com/microsoft/TRELLIS.2.git`
  - branch: `main`
- Hunyuan repo in tar:
  - path: `/opt/nymphs3d/runtime/Hunyuan3D-2`
  - origin: `https://github.com/Babyjawz/Hunyuan3D-2.git`
  - branch: `main`

## Frozen Commit Heads Inside The Tar

Main branch heads extracted from the tar:

- helper repo:
  - `67c7864413f1d1f316f8425a99c9eab2c77aae97`
- Z-Image:
  - `251d1f27c6aa9a5f1b6a9e46c1a28d58aead47fe`
- TRELLIS.2:
  - `5565d240c4a494caaf9ece7a554542b76ffa36d3`
- Hunyuan3D-2:
  - `3d16dc10450dbc1e2acd6bbfe7f6fa7170c6f5fe`

These commits may still be useful for archaeology, but they also prove the tar is freezing source state at export time.

## Why It Looks Obsolete

### 1. Helper Repo Name Is Old

The tar still points at:

- `Babyjawz/Nymphs3D`

Current repo/scripts point at:

- `nymphnerds/NymphsCore`

That means the tar still carries pre-rename helper identity.

### 2. Z-Image Remote Is Old

The tar points at:

- `Babyjawz/Nymphs2D2`

Current scripts expect:

- `nymphnerds/Nymphs2D2`

So the tar is frozen on an older fork location.

### 3. Hunyuan Lanes Are Still Embedded

The tar contains:

- `/opt/nymphs3d/runtime/Hunyuan3D-2`
- `/opt/nymphs3d/runtime/Hunyuan3D-Part`

And the in-tar shell profile still exports:

- `NYMPHS3D_H2_DIR`
- `NYMPHS3D_PARTS_DIR`

For `nymphscore-lite`, this is now dead product history rather than active product surface.

### 4. The Tar Is Carrying Full Source Trees

The tar includes large helper/runtime trees, docs, dev files, and git metadata. That means it is acting as a frozen project snapshot, not just a small bootstrap base.

## What Still Looks Relevant

Not everything in the tar is obsolete.

Still useful concepts:

- `/etc/wsl.conf` with `systemd=true`
- helper-root/runtime-root path conventions
- the idea of a pre-seeded helper repo under `/opt/nymphs3d`
- a reproducible Linux baseline before model downloads

But those are all things that can be reproduced with scripts. They do not require a tar artifact in principle.

## Best Current Interpretation

The tar appears to preserve three kinds of state:

1. useful bootstrap state
2. frozen fork provenance
3. obsolete product history

For the current lite direction, categories `2` and `3` are now the bigger story.

## Practical Conclusion

This report is worth keeping because it supports two decisions:

- the tar should not be treated as the authoritative definition of the current product
- a no-tar installer is safer now that we can clearly see which parts of the tar are old, renamed, or removed

The remaining real question is not "can we live without the tar?"

It is:

- which useful bootstrap pieces from the tar still need to be recreated explicitly by scripts before the tar can be retired
