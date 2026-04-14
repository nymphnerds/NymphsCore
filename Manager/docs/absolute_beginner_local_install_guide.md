# Absolute Beginner Local Backend Install Guide

This guide is for a user who:

- has never used WSL
- has never used Linux
- wants the system on one Windows PC
- wants a local backend server running on the same machine as Blender or another compatible client

This guide covers the backend/helper repo only.

The Blender addon/frontend is part of the NymphsCore monorepo. Install it separately after the backend is working.

Addon link:

- [Nymphs3D Blender Addon](https://github.com/nymphnerds/NymphsCore/tree/main/Blender/Addon)

## What You Are Installing

This helper flow installs the managed local runtime used by the addon, including:

- `Hunyuan3D-2`
  - used for `Hunyuan 2mv`
  - multiview and image-guided shape generation
  - multiview texture guidance
- `Z-Image`
  - used for image generation
  - can later supply images to the 3D backends
- `TRELLIS.2`
  - used for single-image shape generation
  - used for single-image texture / retexture workflows
- optional experimental `Hunyuan3D-Part`
  - only if you enable the Parts option during install or a later rerun

You are also installing:

- WSL Ubuntu
- Python environments
- CUDA toolkit inside WSL
- the main model files used by the supported local backend flows

This guide assumes a local baseline:

- Windows PC
- `NymphsCore` WSL runtime on that PC
- local backend runtimes on that PC
- Blender addon or another compatible client on that PC

## Before You Start

You need:

- a Windows PC
- an NVIDIA GPU
- internet access
- a `.7z` extractor such as `7-Zip`
- at least `120G` free space

Safer recommendation:

- `150G` free space

Why so much space:

- AI Python environments are very large
- model downloads are very large
- CUDA in WSL takes space
- temporary install files and generated assets also take space

More detail:

- [install_disk_and_model_footprint.md](install_disk_and_model_footprint.md)

## Install This First

Use the Windows manager app.

Recommended download:

- [Download NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/main/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip)
- [Download NymphsCore.tar](https://drive.google.com/file/d/1PIE9LJCcb06MCQ9G4T5ywrBJ8DWeqR5a/view?usp=drive_link)

Beginner path:

1. download the manager `.zip`
2. extract it to a normal Windows folder
3. download `NymphsCore.tar`
4. place `NymphsCore.tar` in that extracted manager folder next to `NymphsCoreManager.exe`
5. run `NymphsCoreManager.exe`

This app is the main beginner installer/manager for the backend/helper side.

What it does:

- checks that Windows, WSL, and NVIDIA access look usable
- imports the dedicated `NymphsCore` runtime
- lets you choose the install drive/location for that managed runtime
- installs the supported backend environments
- can optionally install experimental Parts support
- can optionally prefetch models now instead of on first use
- verifies the local backend install

## What Happens During Install

The manager will try to do all of this for you:

- check basic Windows requirements
- check whether the base distro tar is present next to the manager
- import the managed `NymphsCore` WSL distro
- ask which Windows drive/location to use for that managed runtime
- write a machine-specific `.wslconfig`
- install system packages in WSL
- install CUDA 13.0 in WSL
- clone the backend repos
- create the Python virtual environments
- install the locked Python packages
- predownload the main model weights
- verify that the install is usable

What to expect on screen:

- Windows will ask for Administrator approval
- the manager writes logs under `%LOCALAPPDATA%\NymphsCore\`
- the installer keeps a managed repair/update checkout inside WSL at `~/.nymphs3d-installer/Nymphs3D`

## If You Rerun The Manager Later

Rerunning the latest manager is the intended repair path for:

- interrupted installs
- missing Python packages
- missing compiled extensions
- missing model downloads
- an out-of-date managed installer checkout in WSL
- enabling optional experimental Parts later

Rerunning is not a guaranteed fix for:

- broken Windows GPU drivers
- a damaged WSL installation outside this setup flow
- Blender not being installed yet
- any separately distributed addon/frontend package

## What To Do After Install

If the manager finishes successfully:

1. install your separately distributed Blender addon/frontend or other compatible client
2. follow that product's own install instructions
3. point the client at the local backend if needed
4. start the backend you want from the addon or use the manager runtime tools / smoke tests
5. if you skipped model prefetch during install, let the manager or addon download the missing models now
6. run one simple generation

Important:

- if models were not prefetched during install, the first real manager/addon model download can take a long time
- this is normal for the first real use

Addon link:

- [Nymphs3D Blender Addon](https://github.com/nymphnerds/NymphsCore/tree/main/Blender/Addon)

If you want a deeper proof that the local API can really boot, open Ubuntu and run:

```text
~/.nymphs3d-installer/Nymphs3D/scripts/verify_install.sh --smoke-test trellis
```

You can also use:

```text
~/.nymphs3d-installer/Nymphs3D/scripts/verify_install.sh --smoke-test 2mv
```

Those checks are slower than the default verification because they actually start a local backend and wait for `/server_info`.

## Which Backend To Start

Use `TRELLIS.2` if:

- you want the current default single-image shape path
- you want the current default single-image texture / retexture path

Use `Hunyuan 2mv` if:

- you want the multiview workflow
- you want image-guided multiview shape generation
- you want multiview texture guidance

Use `Z-Image` if:

- you want to generate a concept/reference image first
- you want to feed that image into the 3D workflows later

## Which Options To Use As A Beginner

If you are not sure, use these defaults:

### For `TRELLIS.2`

- backend: `TRELLIS.2`
- use a single clean source image
- start with shape first
- add texture if you want a textured result

### For `Hunyuan 2mv`

- backend: `Hunyuan 2mv`
- use multiview guidance if you have front/left/right/back views
- use image-guided shape if you only have one image
- add texture if you want a textured result
- `Turbo model`: on
- `Enable FlashVDM`: on

## Client Connection Notes

For the normal local setup:

- let the addon use its managed-runtime defaults
- the addon is already set up to target the managed `NymphsCore` runtime
- most local setups should not need manual API URL changes

Only advanced or remote setups should need a different API URL.

## If You Want The Simplest First Test

Do this:

1. finish the backend install in `NymphsCore Manager`
2. install your separate addon/frontend package
3. let it target the managed `NymphsCore` runtime
4. start `TRELLIS.2` or `Hunyuan 2mv` from the addon
5. let it finish any first-time model downloads if needed
6. choose one simple image-to-3D task
7. turn on texture only if you want a textured result
8. run one simple generation

This is the most important real-world test because it proves the backend and client can actually talk to each other.

## If Something Goes Wrong

The most common issues are:

- `NymphsCore.tar` was not placed next to the extracted manager
- not enough disk space
- NVIDIA GPU / WSL CUDA is not healthy
- first-time downloads are still happening
- the manager log in `%LOCALAPPDATA%\NymphsCore\` points at the exact failing step

## What This Guide Is Not Trying To Cover

This guide does not try to explain:

- Linux basics
- WSL internals
- how to install the paid addon/frontend package
- remote API setups in full detail
- advanced experimental texture-helper paths

## Optional: Basic Remote Server Setup

This is not the main beginner path.

Only use this if:

- one PC is running the local backend server
- a different PC is running Blender or another client

In that setup:

- PC A = WSL + NymphsCore runtimes
- PC B = Blender + separate addon/frontend

### Basic Idea

Instead of the client talking to:

- `http://localhost:8080`

it talks to the LAN address of the server machine:

- `http://SERVER-PC-IP:8080`

### Basic Steps

1. Install the backend system normally on the server PC.
2. Start the backend on the server PC.
3. Find the server PC local IP address.
4. Make sure Windows Firewall allows port `8080`.
5. On the client PC, install the separately distributed addon/frontend.
6. In that client, change the API URL from `http://localhost:8080` to:

```text
http://SERVER-PC-IP:8080
```

7. Test connection from the client.

### Example

If the server PC address is:

```text
192.168.1.50
```

then the client API URL becomes:

```text
http://192.168.1.50:8080
```

### Important Notes

- both PCs must be on the same network unless you set up more advanced routing
- the backend/server machine must stay on while the client uses it
- if the firewall blocks the port, the client will not connect
- this is an advanced setup compared with the normal local workflow
