# Installer Roadmap 2026-04-06

This roadmap defines the current installer direction for `Nymphs3D2`.

The important product decision is now settled:

- the final user-facing installer should be one simple Windows app
- that app should install a dedicated `Nymphs3D2` WSL distro on a chosen drive
- it should not touch the user's normal `Ubuntu` distro by default
- it should hide WSL, Linux shells, builder distros, and repair commands from the normal user path

## Current State

These parts are now real and working at least once in local testing:

- small base-distro build workflow on `D:`
- exported base tar around `2G`, not a giant clone of the whole working Ubuntu
- test import of the base distro into a separate `Nymphs3D2` WSL distro
- Windows WPF installer app scaffold
- installer app screen flow:
  - Welcome
  - System Check
  - Install Location
  - Model Prefetch
  - Progress
  - Finish
- dedicated managed distro name:
  - `Nymphs3D2`
- dedicated managed Linux user:
  - `nymphs3d`
- standalone launcher updates:
  - explicit backend choice
  - explicit API vs Gradio choice
  - explicit `2.1 Texture-Only API` choice
  - WSL distro chooser
  - automatic use of the selected distro's default Linux user

## Main Remaining Blocker

The earlier fresh-install runtime-root mismatch has now been fixed.

The current main blocker is different:

- first-run model pulling on a clean no-model install still needs the launcher
  and addon experience to feel boringly trustworthy

The fresh install itself is now behaving correctly:

- default user:
  - `nymphs3d`
- runtime layout:
  - `~/Hunyuan3D-2`
  - `~/Hunyuan3D-2.1`

The remaining work is mainly around:

- first-run model download expectations
- launcher/addon handoff clarity
- deciding whether model prefetch should remain optional or become the
  recommended default normal path

## Phase 1: Foundation

Status:

- mostly done

Goals:

- keep the small base-distro export/import path stable
- keep the managed distro separate from the user's normal Ubuntu
- keep model prefetch optional
- keep the app shell as the main future entry point

Exit condition:

- base distro import is boringly reliable

## Phase 2: Runtime Stabilization

Status:

- active

Goals:

- keep the fresh-install home-based runtime layout stable:
  - `~/Hunyuan3D-2`
  - `~/Hunyuan3D-2.1`
- confirm:
  - `Hunyuan3D-2` uses Python `3.10`
  - `Hunyuan3D-2.1` also currently uses Python `3.10` in the tested managed path
- confirm CUDA path is consistent:
  - `/usr/local/cuda-13.0`
- confirm first launcher boot can download models later if install skipped prefetch

Exit condition:

- fresh install passes a repeatable health check without manual Linux repair

## Phase 3: App UX Polish

Status:

- active, but second priority behind runtime stabilization

Goals:

- keep the app wording explicit for non-technical Windows users
- keep admin and WSL expectations obvious
- keep progress stable and readable
- make per-run logs easy to find
- keep model-download behavior explained clearly:
  - if install skips model prefetch, the launcher or Blender addon can pull models later on first real use

Desired result:

- a Blender artist can get through the installer without understanding Linux

## Phase 4: Launcher And Addon Alignment

Status:

- started

Goals:

- launcher should target the correct WSL distro cleanly
- launcher should not force the wrong Linux user
- launcher should expose only honest launch modes
- addon should gain matching WSL-target awareness

Planned launcher surface:

- `2mv API`
- `2mv Gradio`
- `2.1 API`
- `2.1 Gradio`
- `2.1 Texture-Only API`

Important rule:

- texture-only is not a Gradio mode

## Phase 5: Packaging

Status:

- not ready yet

Goals:

- publish a real Windows installer `.exe`
- keep Debug and Release behavior split cleanly
- keep admin elevation automatic in Release
- keep the app detached from dev PowerShell windows

Release gate:

- do not ship the installer app until fresh no-model installs and first launcher/addon model pulls both feel reliable enough for a non-technical user

## Phase 6: Repair And Update

After the main install is stable, the app should grow into:

- reinstall from base distro
- repair runtime setup
- reprefetch models
- launcher/addon handoff help
- simple diagnostics export

## Short Priority Order

1. Confirm launcher and addon both behave correctly against the fixed fresh `Nymphs3D2` install.
2. Decide whether model prefetch becomes the recommended default path.
3. Polish remaining first-run model-pull feedback.
4. Keep app wording and layout tidy.
5. Build and test a proper installer app `.exe`.
