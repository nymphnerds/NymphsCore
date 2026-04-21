# Absolute Beginner Local Backend Install Guide

This guide is for someone who wants the Nymphs backend running locally on one Windows PC.

You do not need to know Linux or WSL. The manager app handles the WSL distro, CUDA setup, Python environments, runtime repos, and model downloads.

The Blender addon is installed separately after the backend is ready:

- [Nymphs Blender Addon](https://github.com/nymphnerds/NymphsCore/tree/main/Blender/Addon)

## What You Are Installing

NymphsCore Manager installs a dedicated WSL distro named `NymphsCore`.

Inside it, the manager prepares:

- `TRELLIS.2` for single-image image-to-3D and texture/retexture workflows
- `Hunyuan 2mv` for multiview-guided shape and texture workflows
- `Z-Image` / Nunchaku for local image generation
- CUDA 13.0 inside WSL
- Python virtual environments for the supported runtimes
- model caches used by the local backend
- status checks and smoke tests

The local API normally runs at:

```text
http://localhost:8080
```

## Before You Start

You need:

- a Windows 10 or Windows 11 PC
- an NVIDIA GPU
- current NVIDIA drivers
- internet access
- enough time for large downloads
- about `120 GB` free before install

Safer recommendation:

- `150 GB` free before install

Why so much space:

- the ready-to-run runtime is currently about `92 GB`
- model prefetch can download about `72 GB`
- Python AI environments are large
- CUDA in WSL is large
- generated meshes, textures, logs, and future updates need room

More detail:

- [Install Disk And Model Footprint](install_disk_and_model_footprint.md)

## Download The Manager

Download the manager zip:

- [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/main/Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip)

Download the base distro tar separately:

- [NymphsCore.tar](https://drive.google.com/file/d/1PIE9LJCcb06MCQ9G4T5ywrBJ8DWeqR5a/view?usp=drive_link)

Then:

1. Extract `NymphsCoreManager-win-x64.zip` to a normal Windows folder.
2. Put `NymphsCore.tar` in that extracted folder.
3. Confirm `NymphsCore.tar` is next to `NymphsCoreManager.exe`.
4. Run `NymphsCoreManager.exe`.

The folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  NymphsCore.tar
  scripts/
    ...
```

Do not run the manager from inside the zip.

The zip intentionally does not include `NymphsCore.tar`. Keeping the tar separate keeps the GitHub download small and makes it clearer which file is the app and which file is the base WSL distro.

This build is currently unsigned. If Windows says `Windows protected your PC`, click `More info`, then `Run anyway`. If Windows says `Unknown publisher`, that is expected for this build.

## Step 1: Welcome

The welcome screen explains that the manager will create and maintain the local runtime.

Use the side buttons if you need:

- `README`
- `Footprint`
- `Addon Guide`
- `Show Logs`

## Step 2: System Check

The manager checks:

- administrator access
- whether `NymphsCore.tar` is in the correct folder
- available install drives
- WSL availability
- existing WSL distros
- NVIDIA visibility

If a check fails, read the message on screen before continuing.

Most common fixes:

- move `NymphsCore.tar` beside `NymphsCoreManager.exe`
- extract the zip instead of running from inside it
- free more disk space
- update NVIDIA drivers
- enable or repair WSL

If you already have a `NymphsCore` WSL distro, the manager will treat this as an existing install and can repair or refresh it.

## Step 3: Install Location

Choose where the managed WSL distro should live.

Pick a drive with enough space. The manager uses the fixed distro name:

```text
NymphsCore
```

The Linux user inside the distro is:

```text
nymph
```

## Step 4: WSL Resources And Models

This page lets you choose WSL resource settings, model download timing, and optional experimental modules.

For most beginners:

- use the recommended WSL resource settings
- leave model prefetch on
- skip experimental Nymphs-Brain unless you specifically want to test the local LLM stack

### Model Prefetch

Model prefetch decides whether the manager downloads the large model files during setup.

Recommended beginner choice:

- leave model prefetch on

With prefetch on:

- the first install takes longer
- the manager downloads about `72 GB` of model and helper files
- first use from Blender is smoother

With prefetch off:

- setup finishes sooner
- large model downloads happen later
- the first backend launch from the manager or addon can feel slow

The optional Hugging Face token box is only for downloads that need your Hugging Face account access. The manager uses it for the current run and does not write it permanently into the distro.

### Experimental Nymphs-Brain

`Nymphs-Brain` is an optional experimental local LLM stack. It is not required for the Blender backend and can be skipped safely.

If selected, it installs inside WSL at:

```text
/home/nymph/Nymphs-Brain
```

It keeps its Python environment, npm tools, model cache, and wrappers under that folder where possible. LM Studio CLI itself may still use its normal home-managed location.

If you enable it, the manager also prepares:

- local LLM start/stop/model helper commands
- a local MCP gateway for tool access
- Open WebUI on:

```text
http://localhost:8081
```

This is optional. If you only want Blender backend workflows, leave it off.

## Step 5: Installation Progress

During install, the manager:

- imports the `NymphsCore` WSL distro from `NymphsCore.tar`
- prepares Linux system packages
- installs CUDA 13.0 inside WSL
- clones or refreshes runtime repos
- creates Python environments
- installs backend dependencies
- downloads model files if prefetch is on
- verifies the install

This can take a long time. On a normal home connection, the download-heavy stage can take 1 to 2 hours or more.

Logs are written to:

```text
%LOCALAPPDATA%\NymphsCore\
```

Use `Show Logs` if the app appears stuck. If the log is still changing, the install is still working.

## Step 6: Finish

The finish screen summarizes the install.

If setup completed, open `Runtime Tools`.

If setup failed:

1. click `Show Logs`
2. look for the newest `installer-run-*.log`
3. rerun the manager after fixing the issue

Rerunning the latest manager is the intended repair path for interrupted installs.

## Step 7: Runtime Tools

Runtime Tools can:

- check whether `Hunyuan 2mv`, `Z-Image`, and `TRELLIS.2` are ready
- fetch missing models into an existing install
- run backend smoke tests
- confirm whether the optional Brain module was installed

Status checks are quick.

Smoke tests are slower because they start a backend and wait for the local API to answer.

Use `Fetch Models Now` if you skipped prefetch or if a model download was interrupted.

If `Nymphs-Brain` was installed, use the dedicated `Brain` page to:

- start or stop the Brain stack
- start or stop Open WebUI
- check Brain `LLM`, `MCP`, and model status
- open the role-aware `Manage Models` flow for `Act` / `Plan` model assignment
- inspect the Brain activity log

If you installed the optional Brain stack and want the full local LLM, WebUI, MCP, and Cline guide after install, use:

```text
Manager/docs/nymphs_brain_guide.md
```

## Which Backend Should I Use?

Use `TRELLIS.2` for:

- single-image image-to-3D
- single-image texture/retexture workflows
- the simplest first real test

Use `Hunyuan 2mv` for:

- multiview guidance
- front/left/right/back reference workflows
- multiview texture guidance

Use `Z-Image` for:

- generating concept or reference images locally
- creating images that can later feed the 3D backends

## After The Backend Works

Install the Blender addon:

- [Nymphs Blender Addon](https://github.com/nymphnerds/NymphsCore/tree/main/Blender/Addon)

Then:

1. open Blender
2. enable the addon
3. use the managed local runtime defaults
4. start a backend from the addon or from Runtime Tools
5. run one simple image-to-3D test

Most local users should not need to change the API URL.

## Simple First Test

The simplest useful proof is:

1. finish the manager install
2. open `Runtime Tools`
3. check backend status
4. run a `TRELLIS.2` smoke test
5. install and enable the Blender addon
6. run one simple single-image 3D job

If model prefetch was off, expect the first real run to spend time downloading missing models.

## If Something Goes Wrong

The most common issues are:

- `NymphsCore.tar` is not next to `NymphsCoreManager.exe`
- the manager was run from inside the zip
- disk space is too low
- WSL is disabled or unhealthy
- NVIDIA is not visible inside WSL
- a model download was interrupted
- the first model download is still running

Send these when asking for help:

- the newest `installer-run-*.log`
- a screenshot of the manager window
- whether model prefetch was on or off
- how much free disk space is left on the install drive

## What This Guide Does Not Cover

This guide does not cover:

- Blender basics
- Linux basics
- paid addon licensing or distribution
- remote server deployment
- manual backend development
