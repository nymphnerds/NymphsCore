<p align="center">
  <img src="Graphics/NymphsCoreLogo.png" alt="NymphsCore" width="960">
</p>

NymphsCore is the local runtime and Manager shell for NymphNerds game-development AI pipelines.

This repository contains the Windows Manager, WSL runtime scripts, Blender addon source, and standardization notes for turning the old hardcoded tool stack into a registry-driven module system.

---

## Current Branch State

Branch: `modular`

Manager build: `v0.9.3`

This branch is an active plugin/module standardization checkpoint.

### Branch Promotion Notes

`modular` is intended to become `main` once the registry-driven Manager is stable.

Keep current `main` for now. It is still the old-manager UI/workflow reference.

Before promoting `modular` to `main`, change:

| Place | Current | Promote to |
| --- | --- | --- |
| README download link | `raw/modular/.../NymphsCoreManager-win-x64.zip` | `raw/main/...` |
| `GuideUrl` | `blob/modular/docs/GETTING_STARTED.md` | `blob/main/docs/GETTING_STARTED.md` |
| docs branch labels | `modular` | `main` or remove label |

Manager runtime wrappers are bundled in the EXE zip under `scripts/`:

- `Manager/scripts/install_nymph_module_from_registry.sh`
- `Manager/scripts/uninstall_nymph_module.sh`

The Manager stages those bundled files into the managed `NymphsCore` distro at action time. It should not fetch generic lifecycle wrappers from this repo over the network during normal EXE use.

The Manager is no longer meant to install every tool through one hardcoded install flow. The new shape is:

```text
Install Base Runtime -> load module cards from registry -> install modules one at a time
```

What works now:

- Manager shell loads official module cards from `nymphs-registry`.
- Base Runtime is a first-class system card for creating or unregistering the managed `NymphsCore` WSL distro.
- The old hardcoded module surfaces have been removed from the active shell.
- Module status is parsed through generic `key=value` snapshots.
- WORBI is the first live proof module for the new lifecycle contract.
- The packaged Manager release is rebuilt under:

```text
Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.exe
Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
```

Still in proof phase:

- Brain, Z-Image, LoRA, and TRELLIS still need full install/status/start/stop/open/logs/uninstall validation under the new module contract.
- Module-owned UI surfaces are not finished yet.
- `Delete Module + Data` remains conservative until each module declares safe purge scopes.

Cleaned repo layout:

- `Manager/` is the active Windows Manager and packaged release.
- `Graphics/` holds shared logo/source assets.
- `docs/` holds public docs plus the single active standardization handoff.
- Old prototype roots (`ManagerFEUI/`, `Monitor/`, `WORBI-installer/`, and the static `home/` site) were removed from this branch.

The live handoff for this work is:

[Nymph Plugin Standardization Handoff](docs/RautysIdeas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md)

---

## Module Contract Rule

The current standard is:

```text
registry -> nymph.json manifest -> entrypoints -> status key/value snapshot -> Manager UI truth
```

Installed state must be based on the version marker:

```text
installed runtime == .nymph-module-version exists
installed runtime != install folder exists
installed runtime != preserved data exists
```

Installers should:

- install into a temp/staging folder first
- install dependencies inside staging
- swap into the real install root only after success
- write `.nymph-module-version` last
- leave a clean not-installed state if interrupted
- avoid random backup folders in `/home/nymph`
- preserve only manifest-declared user data
- expose bounded, fast status checks
- declare log paths so the Manager can find module logs

---

## Quick Start For This Branch

1. Download or build the Manager release from this branch.
2. Extract the zip to a normal Windows folder.
3. Run `NymphsCoreManager.exe`.
4. Open `Base Runtime`.
5. Confirm Windows WSL is ready.
6. Install Base Runtime.
7. Return Home and install modules from their cards.

The managed WSL distro is named:

```text
NymphsCore
```

The managed Linux user is:

```text
nymph
```

Important WSL boundary:

```text
NymphsCore_Lite = dev/source WSL
NymphsCore      = managed runtime WSL
```

Runtime setup must not make the managed `NymphsCore` distro execute scripts from `NymphsCore_Lite` paths.

---

## Download

Current `modular` build:

[NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/modular/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)

After downloading:

1. Extract the zip.
2. Run `NymphsCoreManager.exe`.
3. If Windows SmartScreen appears, choose `More info`, then `Run anyway`.

The Manager is currently unsigned.

---

## Requirements

Recommended baseline:

- Windows 10 or Windows 11
- WSL available on the machine
- NVIDIA GPU with current drivers for GPU-heavy modules
- reliable internet connection
- enough free disk space for the base runtime plus module-specific models/assets

Disk usage now depends on which modules you install. Base Runtime is intentionally separate from optional modules.

Model and artifact path standardization is still part of the next module-contract pass.

---

## Manager Screens

Current shell:

- `Home`: system overview and registry-provided module cards
- `Base Runtime`: Windows WSL readiness, managed runtime install, progress, current state, and unregister
- `Logs`: selectable Manager log stream
- `Guide`: lightweight user guidance
- compact monitor mode: sidebar-only runtime monitor with optional always-on-top behavior

Module cards open a detail page first. Install is a deliberate action from the detail page.

---

## Official Modules

Registry cards currently cover:

- Brain
- Z-Image Turbo
- LoRA
- TRELLIS.2
- WORBI

Proof order:

```text
WORBI -> Z-Image -> LoRA -> Brain -> TRELLIS
```

WORBI is currently the most standardized live module. Its installer has been hardened to stage installs, write the version marker last, and avoid backup clutter.

---

## Build Manager

From Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

From the WSL dev shell, the project lives at:

```text
/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager
```

The release build produces:

```text
Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.exe
Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
```

---

## Logs

Windows Manager logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

Module logs should be standardized through module manifests and status output. This is part of the community module contract work.

---

## Important Docs

- [Nymph Plugin Standardization Handoff](docs/RautysIdeas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md)
- [Plugin Manager Implementation Plan](docs/RautysIdeas/NYMPH_PLUGIN_MANAGER_IMPLEMENTATION_PLAN.md)
- [Install Disk And Model Footprint](docs/FOOTPRINT.md)

---

## Status

This branch is useful for testing the new Manager shell and module lifecycle contract.

It should not be treated as the final stable public installer until the official modules have each passed the same install/status/start/stop/open/logs/uninstall loop from registry cards.
