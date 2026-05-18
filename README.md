<p align="center">
  <img src="Graphics/NymphsCoreLogo.png" alt="NymphsCore" width="960">
</p>

# NymphsCore

## Download

[Download NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/main/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)

Extract the zip, run `NymphsCoreManager.exe`, install `Base Runtime`, then add the modules you want from the Manager home screen.

The Manager is currently unsigned. If Windows SmartScreen appears, choose `More info`, then `Run anyway`.

## What It Is

NymphsCore is a Windows Manager for local creative AI tools. It creates a dedicated `NymphsCore` WSL runtime, then installs optional modules one at a time from the official registry.

The Manager handles WSL setup, runtime scripts, module install actions, logs, and module controls so users do not have to manually build Python, CUDA, model, or service environments.

## Quick Start

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract it to a normal Windows folder.
3. Run `NymphsCoreManager.exe`.
4. Open `Base Runtime`.
5. Install or repair the Base Runtime.
6. Return Home and install modules from their cards.

Do not run the Manager from inside the zip. Extract it first.

## Modules

Current official modules:

- `Brain`: local assistant, Open WebUI, llama-server, and MCP tooling.
- `Z-Image Turbo`: local image generation backend.
- `LoRA`: Z-Image Turbo LoRA training workspace.
- `TRELLIS.2`: local image-to-3D generation backend.
- `WORBI`: local-first worldbuilding workspace.

Heavy modules may require separate model downloads after install.

## Requirements

- Windows 10 or Windows 11
- WSL available on the machine
- NVIDIA GPU with current drivers for GPU-heavy modules
- reliable internet connection
- enough free disk space for the modules and model weights you choose

See [Install Disk And Model Footprint](docs/FOOTPRINT.md) before installing large model modules.

## Blender Addon

Install the Blender addon through Blender's Extensions remote repository system:

```text
https://raw.githubusercontent.com/nymphnerds/NymphsExt/main/index.json
```

Guide: [Blender Addon User Guide](docs/BLENDER_ADDON_USER_GUIDE.md)

## Docs

- [Getting Started](docs/GETTING_STARTED.md)
- [Absolute Beginner Install Guide](docs/ABSOLUTE_BEGINNER_INSTALL_GUIDE.md)
- [Blender Addon User Guide](docs/BLENDER_ADDON_USER_GUIDE.md)
- [Features](docs/FEATURES.md)
- [Roadmap](docs/ROADMAP.md)
- [Nymphs Module Making Guide](docs/NYMPHS_MODULE_MAKING_GUIDE.md)

## Troubleshooting

Logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

If something fails, open the Manager `Logs` page and check the newest `installer-run-*.log`.
