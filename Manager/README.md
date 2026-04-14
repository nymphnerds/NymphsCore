# Nymphs3D

Nymphs3D sets up the local Nymphs backend runtime on a Windows PC with WSL.

If you are a normal user, start with `NymphsCore Manager`. Do not build it from source unless you are working on the installer itself.

## What This Repo Is

This repo is the Windows-first setup layer for running the local `NymphsCore` backend runtime used by the `Nymphs` Blender addon.

It is mainly for:

- installing the local backend runtime on Windows with WSL
- packaging the Windows manager app
- keeping the helper scripts and managed runtime tooling together

It is not the Blender addon itself.

The Blender addon is separate:

- [Nymphs3D Blender Addon](https://github.com/nymphnerds/NymphsCore/tree/main/Blender/Addon)

The backend model/runtime repos used by this setup are:

- `Babyjawz/Hunyuan3D-2`
- `Babyjawz/Z-Image` for the `Z-Image` backend
- `microsoft/TRELLIS.2`

Published GitHub artifact links now use `main`.
Old `exp/2mv-remake` download URLs are obsolete.

## Quick Start

Download these two files:

- [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/main/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip)
- [NymphsCore.tar](https://drive.google.com/file/d/1PIE9LJCcb06MCQ9G4T5ywrBJ8DWeqR5a/view?usp=drive_link)

## What To Do

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract it to a normal folder on Windows.
3. Download `NymphsCore.tar` from the Google Drive link above.
4. Put `NymphsCore.tar` in the same extracted folder as `NymphsCoreManager.exe`.
5. Run `NymphsCoreManager.exe`.
6. If Windows asks for administrator permission, click `Yes`.
7. To repair, refresh, or add optional Parts later, rerun the latest manager package again.

Your folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  NymphsCore.tar
  scripts/
    ...
```

## Important Notes

- Do not run the manager from inside the zip.
- Extract it first.
- This manager build is unsigned, so Windows may warn about it.
- If Windows shows `Windows protected your PC`, click `More info`, then `Run anyway`.
- If Windows shows `Unknown publisher`, that is expected for this test build.

## What The Manager Does

The manager:

- checks your Windows machine
- checks WSL and NVIDIA visibility
- warns if an existing WSL distro is already present
- imports a dedicated `NymphsCore` WSL distro
- prepares the backend runtime
- can optionally include experimental `Hunyuan Parts`
- optionally prefetches models now instead of on first real use
- can be rerun later to repair or refresh the existing `NymphsCore` install

The managed Linux user inside the new distro is:

- `nymph`

The runtime layout inside the distro uses:

- `~/Hunyuan3D-2`
- `~/Z-Image`
- `~/TRELLIS.2`
- `~/Hunyuan3D-Part` when experimental Parts is enabled

## Runtime And Export Notes

- The exported base distro tar is not intended to ship backend Python virtual environments.
- That means machine-specific compiled extension builds are normally created after install on the target machine, not baked into `NymphsCore.tar`.
- TRELLIS `flash-attn` is currently treated as an optional optimization, not a hard requirement.
- The stable TRELLIS fallback is `sdpa`.
- If `flash-attn` is enabled during install, it may compile locally on the user machine.
- Those local compiled artifacts are not meant to be part of the base distro export unless prebuilt venvs are deliberately shipped later.
- User-generated backend outputs should also not be baked into the export tar; the builder cleanup path removes output contents before export.

## If Something Fails

- Use the `Show Logs` button in the manager.
- Logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

- If you need help, send:
  - the newest `installer-run-*.log`
  - a screenshot of the manager window

## After Install

After the backend install completes:

1. install the Blender addon separately
2. point the addon at the managed `NymphsCore` runtime
3. if install skipped model prefetch, let the manager or addon download missing models on first real use
4. rerun the latest manager later if you want to repair/refresh the install or enable optional Parts

Addon link:

- [Nymphs3D Blender Addon](https://github.com/nymphnerds/NymphsCore/tree/main/Blender/Addon)

Default local API URL:

- `http://localhost:8080`

## Help Docs

- [Absolute Beginner Local Backend Install Guide](docs/absolute_beginner_local_install_guide.md)
- [Install Disk And Model Footprint](docs/install_disk_and_model_footprint.md)
- [Goblin Single Image to 3D Example](docs/goblin_single_image_to_3d_example.md)

## Developer Note

If you are trying to work on the manager itself, the source lives under:

- `apps/Nymphs3DInstaller`

The old bootstrap installer path is archived under:

- `legacy/`
