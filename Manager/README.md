# NymphsCore Manager

NymphsCore Manager is the Windows shell for creating the local `NymphsCore` WSL runtime and managing optional Nymph modules from the registry.

Use it when you want Nymphs tools on your own Windows PC without manually building WSL, CUDA, Python environments, module repos, or model caches.

## What It Installs

The manager creates and maintains a dedicated WSL distro named `NymphsCore`.

Base Runtime prepares the managed Linux environment. Optional modules are installed later from registry cards:

- `Brain`
- `Z-Image Turbo`
- `LoRA`
- `TRELLIS.2`
- `WORBI`

Modules own their install/start/stop/status/log actions through their manifests and entrypoint scripts. Installed modules may also expose a custom Manager UI through `ui.manager_ui`.

The managed Linux user inside the distro is:

```text
nymph
```

The usual local API address is:

```text
http://localhost:8080
```

## Download

Download the manager zip from the repo:

- [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/modular/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)

The manager can bootstrap its own fresh Ubuntu WSL base locally.

If you already have a compatible `NymphsCore.tar`, you can put it next to `NymphsCoreManager.exe` after extracting the zip. That optional maintainer shortcut is faster, but it is no longer required and is not treated as a system-check requirement on this branch.

Your folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  scripts/
    ...
```

Do not run the manager from inside the zip. Extract it first.

## Quick Start

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract it to a normal folder on Windows.
3. Run `NymphsCoreManager.exe`.
4. Approve the Windows administrator prompt.
5. Open `Base Runtime` and install or repair the managed runtime.
6. Return Home and install modules from their cards.
7. Open an installed module page for lifecycle actions, logs, and module-owned UI when the module provides it.

The manager build is currently unsigned. If Windows SmartScreen appears, choose `More info`, then `Run anyway`.

## Requirements

Recommended baseline:

- Windows 10 or Windows 11
- NVIDIA GPU with current drivers
- WSL available on the machine
- reliable internet connection
- enough free disk space for the base runtime plus the modules and model weights you choose

Disk usage is now module-dependent. Heavy AI modules and their model caches can still consume many tens of gigabytes.

For the detailed disk story, read:

- [Install Disk And Model Footprint](../docs/FOOTPRINT.md)

## Manager Flow

The current modular Manager surfaces are:

- `Home`: system overview and registry-provided module cards
- `Base Runtime`: WSL readiness, managed runtime install/repair, progress, current state, and unregister
- module detail pages: lifecycle actions, status, logs, source facts, and custom module UI entry when available
- `Logs`: selectable Manager log stream
- `Guide`: lightweight guidance linked to repo docs
- compact monitor mode: sidebar-only runtime monitor with optional always-on-top behavior

Module cards open detail pages first. Installing, updating, uninstalling, starting, stopping, opening logs, and opening module UI should all happen through the module page.

## Module UI

Installed modules may expose a local Manager UI from their installed `nymph.json`:

```json
{
  "ui": {
    "manager_ui": {
      "type": "local_html",
      "entrypoint": "ui/manager.html",
      "title": "Module Controls"
    }
  }
}
```

Current support is installed `local_html` hosted by WebView2. The Manager owns the shell and Back bar; the module owns the content inside the hosted surface.

Long-running module UI actions, such as model downloads, should switch to the
standard Logs page and stream stdout/stderr there. This keeps progress visible
and avoids hiding downloads inside a cramped module panel.

Model-download pages may request a Hugging Face token. The current Manager saves
the shared token under `%LOCALAPPDATA%\NymphsCore\shared-secrets.json`, hydrates
the field when the page opens again, and passes it to download actions without
printing the secret to logs.

For UI rules and the Z-Image fast-load lessons, read:

```text
../docs/NYMPH_MODULE_UI_STANDARD.md
```

## Official Modules

Registry cards currently cover:

- Brain
- Z-Image Turbo
- LoRA
- TRELLIS.2
- WORBI

WORBI is the cleanest lifecycle-contract proof. Z-Image is the current heavy-runtime and custom-UI proof. Brain, LoRA, and TRELLIS still need the same full validation pass.

## Logs And Troubleshooting

Logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

If something fails, send the newest `installer-run-*.log` and a screenshot of the manager window.

Common causes:

- WSL is too old for local no-tar bootstrap
- the manager was launched from inside the zip
- not enough free disk space
- WSL is disabled or unhealthy
- NVIDIA is not visible inside WSL
- a module download/install is still running or was interrupted
- a module status script failed even though the install marker exists

Rerunning the latest manager is the intended repair path for Base Runtime problems. Module problems should be handled from the module page first.

## After Install

Install the Blender addon separately:

- [Blender Addon User Guide](../docs/BLENDER_ADDON_USER_GUIDE.md)
- Blender addon: available on Superhive (temporary URL)

Most local installs should not need a custom API URL. The addon is designed to target the managed local runtime.

Useful docs:

- [Absolute Beginner Local Backend Install Guide](../docs/ABSOLUTE_BEGINNER_INSTALL_GUIDE.md)
- [Install Disk And Model Footprint](../docs/FOOTPRINT.md)
- [Blender Addon User Guide](../docs/BLENDER_ADDON_USER_GUIDE.md)
- [Nymph Module UI Standard](../docs/NYMPH_MODULE_UI_STANDARD.md)
- [Nymph Module Making Guide](../docs/NYMPHS_MODULE_MAKING_GUIDE.md)

## Developer Notes

The current manager app source lives under:

- `apps/NymphsCoreManager`

The legacy batch/PowerShell installer is archived under:

- `legacy/`
