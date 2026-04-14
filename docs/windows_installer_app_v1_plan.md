# NymphsCore Manager V1 Plan

This document defines the first real user-facing installer app for the
`NymphsCore` base-distro workflow.

It is written from one clear assumption:

- the final audience is not comfortable with WSL, terminals, Linux shells, or
  manual repair steps

So the app must hide those mechanics and present one obvious Windows-first flow.

## Primary Goal

Ship one simple Windows app that:

1. installs a small prebuilt `NymphsCore` WSL base distro to a user-chosen drive
2. runs guided post-import setup without exposing Linux shell commands
3. downloads optional runtime pieces after the base install
4. keeps the user's existing `Ubuntu` distro untouched

## Product Boundary

The app is not the Blender addon.

The app is:

- backend installer
- backend updater
- backend repair tool
- backend runtime setup UI

The Blender addon remains separate.

The app should describe the relationship clearly, for example:

- `This app installs the local runtime systems required by the Nymphs Blender addon.`

## Why This App Exists

The current script pipeline now works technically:

- build fresh builder distro
- export small `NymphsCore.tar`
- import test distro
- run system-only finalize from Windows

But that pipeline is still not acceptable as the end user experience because it
still exposes:

- admin PowerShell
- multiple batch files
- WSL distro names
- temporary builder cleanup
- Linux-root weirdness

V1 of the app should absorb all of that.

## Recommended Tech Direction

For V1, use:

- Windows-only desktop app
- .NET 8
- WPF
- single-file published executable
- application manifest for admin elevation

Why:

- Windows-only is fine for this installer
- WPF matches the CHIM installer pattern that already felt right
- .NET gives a straightforward way to run PowerShell and `wsl.exe`, stream
  output, log progress, and show real status updates

This app does not need to replace the underlying scripts immediately. It should
orchestrate them.

## V1 User Flow

The default V1 flow should be:

1. Welcome
2. System Check
3. Install Location
4. Base Distro Install
5. Model Prefetch Choice
6. Runtime Setup Progress
7. Finish

No raw Linux shell should be exposed in the normal path.

## Screen-by-Screen Plan

### 1. Welcome

Purpose:

- confirm the user opened the right installer
- explain what this app does in plain English
- set expectations for admin approval and long downloads

Copy direction:

- `This app installs the local runtime systems required by the Nymphs Blender addon.`
- `Your existing Ubuntu setup will not be touched.`
- `Windows will ask for administrator permission. Click Yes to continue.`

Buttons:

- `Install`
- `Cancel`

### 2. System Check

Purpose:

- check whether the machine is capable of the base install
- fail early with plain-English messages

Checks:

- WSL available
- WSL version usable
- enough free space on selected/default drive
- NVIDIA GPU visible from Windows
- internet available

Output style:

- green/yellow/red checklist
- no raw stack traces

If WSL is missing:

- app offers to install/update WSL directly

### 3. Install Location

Purpose:

- let the user choose where the `NymphsCore` distro will live

V1 default:

- show available drives with free space
- user picks one drive
- app chooses the full install path automatically, for example:
  - `D:\WSL\NymphsCore`

V1 simplification:

- do not ask the user to choose a distro name
- use fixed distro name: `NymphsCore`

Reason:

- fewer decisions
- less WSL confusion
- no generic `Ubuntu-24.04` naming leak in the user flow

### 4. Base Distro Install

Purpose:

- import the prebuilt `NymphsCore.tar`

App action:

- call `wsl --import NymphsCore <InstallLocation> <TarPath>`

UI behavior:

- visible progress stage
- plain-English messages such as:
  - `Installing the NymphsCore base environment`
  - `This can take a few minutes`
  - `Please keep this window open`

No mention of builder distros here. Builder distros are internal build-time
concepts only.

### 5. Model Prefetch Choice

Purpose:

- keep the user-facing choice minimal while the app still installs the required
  runtime automatically

V1 behavior:

- base distro import always happens
- required runtime environment setup always happens
- the only optional choice is whether to prefetch required models now

V1 choice:

1. `Download required models during install`
   - if on, the app prefetches the required model set
   - if off, models can be downloaded later when the server starts

Reason:

- enough to make the backend usable by default
- avoids overwhelming the user with too many install modes
- keeps model prefetch optional because it is the heavier discretionary step

### 6. Runtime Setup Progress

Purpose:

- run the chosen finalize steps
- stream progress in a readable way

App action:

- call Windows wrappers / PowerShell entrypoints that drive Linux scripts
- capture output and present it as:
  - current stage
  - rolling log panel
  - error summary when something fails

Stages should be explicit:

- `Checking the imported distro`
- `Installing Ubuntu system packages`
- `Checking CUDA support`
- `Creating backend environments`
- `Downloading models`
- `Verifying install`

### 7. Finish

Purpose:

- clearly state success
- explain what the user can do next

Show:

- installed distro name: `NymphsCore`
- install location
- what was completed
- what still needs to happen, if anything

Buttons:

- `Open logs`
- `Open backend folder`
- `Finish`

Optional text:

- `You can now install or use the Nymphs Blender addon frontend.`

## What The App Owns

The app should directly own:

- elevation prompt
- screen flow
- user messaging
- progress UI
- drive selection
- download of installer assets
- import of base distro
- choice of finalize depth
- readable failure messages
- repair/retry entry point

The app should also own log collection and present one obvious log location.

## What The Scripts Still Own

For V1, the existing scripts remain the execution engine for:

- base distro import helpers
- system package installation inside WSL
- CUDA install logic
- backend environment creation
- model prefetch
- verification

The app should call those layers, not rewrite them all immediately.

This keeps V1 achievable.

## V1 Simplifications

To keep the app small and actually shippable, V1 should deliberately avoid:

- custom distro naming UI
- exposing `Use existing Ubuntu` as the default path
- multiple backend family choices on the first screen
- model-library management UI
- advanced per-component toggles everywhere
- interactive Linux shell repair

Those can come later.

## V1 Opinionated Defaults

The simplest V1 policy should be:

- always install a dedicated distro named `NymphsCore`
- always install to a user-chosen drive under a fixed path
- never touch the user's generic `Ubuntu` distro by default
- offer repair/update against `NymphsCore` only

This is a better beginner experience than asking users to reason about existing
distros.

## Failure Handling

The app should not stop at:

- `exit code 1`

Instead it should show:

- stage that failed
- short human-readable reason
- expandable technical details
- path to the full log
- suggested next action

Examples:

- `CUDA was not found yet. You can finish the base install now and add CUDA later.`
- `The model download failed because the disk is full on D:. Free space and retry.`
- `WSL needs an update before the NymphsCore base distro can be installed.`

## Repair Mode

V1 should already support a basic repair mode.

If `NymphsCore` already exists, the app should offer:

- `Repair current install`
- `Reinstall from base distro`
- `Cancel`

Repair mode should re-run the Windows-driven finalize steps, not rebuild the
base distro from scratch unless the user explicitly chooses reinstall.

## Build-Time Pipeline Behind The App

The app depends on a separate internal build pipeline:

1. create fresh builder distro
2. bootstrap helper repo + backend source repos
3. export `NymphsCore.tar`
4. package app + tar together for distribution

That build pipeline remains internal tooling and should not leak into the app UI.

## Packaging Direction

V1 distribution should likely be:

- one installer app executable
- one bundled `NymphsCore.tar`
- minimal supporting files

Not:

- a GitHub repo zip
- a source tree
- multiple batch files visible to end users

## Immediate Implementation Plan

1. keep the current base-distro pipeline stable
2. add Windows-side wrappers for the remaining finalize modes
3. define one app-hostable PowerShell entry per stage
4. scaffold the Windows app shell
5. wire screens to those stage commands
6. stream logs and stage progress into the app
7. test the app on a machine that already has a normal `Ubuntu` distro

## Success Criteria

V1 is good enough when a non-technical Windows user can:

1. download one obvious app
2. click install
3. choose a drive
4. wait through readable progress
5. finish with a dedicated `NymphsCore` backend install

without needing to understand:

- WSL distro names
- Linux root shells
- PowerShell commands
- builder distros
- manual cleanup steps
