# NymphsCore

The central hub for the NymphNerds game development backend. This repo contains the core runtime, Manager, and Blender addon source that power the local pipeline.

---

## Install NymphsCore

NymphsCore Manager is the Windows setup and repair app for the local backend runtime used by the Nymphs Blender addon.

Use it when you want the Nymphs backend on your own Windows PC, without manually building WSL, CUDA, Python environments, or model caches.

### What It Installs

The manager imports and maintains a dedicated WSL distro named `NymphsCore`.

Inside that distro, it prepares the supported local backend stack:

- `TRELLIS.2` for single-image image-to-3D and texture/retexture workflows
- `Hunyuan 2mv` for multiview-guided 3D workflows
- `Z-Image` / Nunchaku for local image generation
- CUDA 13.0, Python environments, helper scripts, and runtime checks

The managed Linux user inside the distro is:

```text
nymph
```

The usual local API address is:

```text
http://localhost:8080
```

### Download

To install the local backend on Windows:

1. Download the manager app:
   [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/main/Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip)
2. Download the base distro package:
   [NymphsCore.tar](https://drive.google.com/file/d/1PIE9LJCcb06MCQ9G4T5ywrBJ8DWeqR5a/view?usp=drive_link)
3. Extract the zip to a normal Windows folder.
4. Put `NymphsCore.tar` beside `NymphsCoreManager.exe`.
5. Run `NymphsCoreManager.exe`.

Your extracted folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  NymphsCore.tar
  scripts/
    ...
```

Important:

- Do not run the manager from inside the zip.
- Extract it first.
- `NymphsCore.tar` must sit next to `NymphsCoreManager.exe`.

The release zip is intentionally a no-tar archive. It contains the manager exe and helper scripts only; `NymphsCore.tar` stays separate so the GitHub download remains small.

### Quick Start

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract it to a normal folder on Windows.
3. Download `NymphsCore.tar`.
4. Put `NymphsCore.tar` in the extracted manager folder.
5. Run `NymphsCoreManager.exe`.
6. Approve the Windows administrator prompt.
7. Leave model prefetch turned on unless you need a shorter first install.
8. Use `Runtime Tools` after install to check backend readiness or run smoke tests.

The manager build is currently unsigned. If Windows SmartScreen appears, choose `More info`, then `Run anyway`.

### Requirements

Recommended baseline:

- Windows 10 or Windows 11
- NVIDIA GPU with current drivers
- WSL available on the machine
- reliable internet connection
- about `120 GB` free before install
- `150 GB` free if you want comfortable headroom

The ready-to-run backend footprint is currently about `92 GB` installed. The model prefetch stage can download about `72 GB` of required model and helper files.

For the detailed disk story, read:

- [Install Disk And Model Footprint](docs/FOOTPRINT.md)

### Manager Flow

The manager walks through these steps:

- `Welcome`: explains the local runtime and links to docs
- `System Check`: checks administrator access, WSL, NVIDIA visibility, install files, and existing distros
- `Install Location`: chooses the Windows drive/folder for the managed distro
- `WSL Resources And Models`: chooses WSL resource settings, model prefetch, and optional experimental modules
- `Installation Progress`: imports the distro and prepares runtime environments
- `Finish`: summarizes the install
- `Runtime Tools`: checks backend status, fetches missing models, and runs smoke tests

Model prefetch is recommended for non-technical users. Turning it off only skips the large model downloads; the manager still prepares the runtime stack. Missing models can be fetched later from `Runtime Tools` or during first real use from the addon.

The installer can also offer an experimental optional `Nymphs-Brain` local LLM stack. It installs under `/home/nymph/Nymphs-Brain` inside WSL when selected, is not required for the Blender backend, and can be skipped safely.

If selected, `Nymphs-Brain` now includes:

- an LM Studio-backed local LLM runtime
- Open WebUI on `http://localhost:8081`
- a local MCP gateway for tool access from Cline/Open WebUI
- helper commands under `/home/nymph/Nymphs-Brain/bin`

The installer and runtime wrappers use LM Studio's normal CLI flow for model fetch and server start. No separate manual daemon bootstrap step should be needed.

For connecting Cline to the local Brain model and MCP tools, see:

```text
docs/CLINE_WITH_NYMPHS_BRAIN_QUICKSTART.md
```

Useful Brain commands:

```text
/home/nymph/Nymphs-Brain/bin/lms-start
/home/nymph/Nymphs-Brain/bin/lms-model
/home/nymph/Nymphs-Brain/bin/lms-get-profile
/home/nymph/Nymphs-Brain/bin/lms-set-profile
/home/nymph/Nymphs-Brain/bin/lms-stop
/home/nymph/Nymphs-Brain/bin/mcp-start
/home/nymph/Nymphs-Brain/bin/open-webui-start
/home/nymph/Nymphs-Brain/bin/brain-status
```

The current Brain stack supports:

- one `Act` model
- one optional `Plan` model
- loading only `Act` or both `Act` + `Plan` from the Linux-side Brain config

### Runtime Tools

Use `Runtime Tools` to:

- check whether `Hunyuan 2mv`, `Z-Image`, and `TRELLIS.2` are ready
- fetch missing model files into an existing install
- run backend smoke tests
- confirm the local API can start
- check whether the optional Brain module is installed

Smoke tests are slower than normal status checks because they actually start a backend and wait for a response.

### Brain

Use the dedicated `Brain` page to:

- check Brain `LLM`, `MCP`, `Open WebUI`, and model status
- start or stop the Brain stack
- start or stop Open WebUI
- open the role-aware `Manage Models` terminal flow
- update the Linux-side Brain stack components
- inspect the Brain activity log

### Logs And Troubleshooting

Logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

If something fails, send the newest `installer-run-*.log` and a screenshot of the manager window.

Common causes:

- `NymphsCore.tar` is missing or not beside `NymphsCoreManager.exe`
- the manager was launched from inside the zip
- not enough free disk space
- WSL is disabled or unhealthy
- NVIDIA is not visible inside WSL
- the model download is still running or was interrupted

Rerunning the latest manager is the intended repair path for interrupted installs, missing packages, missing models, or refreshed runtime scripts. The optional Nymphs-Brain install should not require a separate LM Studio initialization step outside the manager.

### After Install

After the backend is installed, the Blender addon source lives here:

- [Blender Addon](Blender/Addon)

Useful docs:

- [Absolute Beginner Local Backend Install Guide](docs/ABSOLUTE_BEGINNER_INSTALL_GUIDE.md)
- [Install Disk And Model Footprint](docs/FOOTPRINT.md)
- [Blender Addon User Guide](docs/BLENDER_ADDON_USER_GUIDE.md)
- [Retexturing Guide](docs/BLENDER_RETEXTURING_GUIDE.md)
- [Cline With Nymphs-Brain Quickstart](docs/CLINE_WITH_NYMPHS_BRAIN_QUICKSTART.md)

---

## Structure

```
NymphsCore/
├── Manager/        — WSL backend, C# installer, and setup scripts
└── Blender/
    └── Addon/      — Blender addon source and extension build tooling
```

---

## Why This Structure?

Previously the codebase was split across several repos with confusing names. NymphsCore now keeps the local runtime, Manager, and Blender addon source together under `nymphnerds`.

The active managed backend defaults should point at `nymphnerds`. Legacy helper-path naming still exists in some installer/bootstrap internals and should be cleaned up separately from repo ownership.

### Why is the Blender Extension a separate repo?
Blender Extensions (Blender 4.2+) must have `blender_manifest.toml` at the **root** of a repo to support direct Git URL installs from inside Blender. So each Blender Extension needs its own repo — see [`NymphsExt`](https://github.com/nymphnerds/NymphsExt).

The classic **Blender Addon** (`Blender/Addon/`) is different — it's installed by the Manager app automatically, so it doesn't need to be Blender-accessible directly and lives fine as a subfolder here.

The current addon workflow includes guided image part extraction: generate or choose a master image, plan extractable character parts, select the parts to keep, and generate separate references for the anatomy base, clothing, hair, props, and optional eyeball assets. The source addon lives in `Blender/Addon/`, while the public extension feed is mirrored through `NymphsExt`.

---

## Adding Things in Future

**New Blender Extension** — create a new repo on `nymphnerds` with `blender_manifest.toml` at the root. Same pattern as `NymphsExt`.

**New classic Blender Addon** — add a folder under `Blender/` and update the Manager scripts to copy it into place.

---

## Contributing

```bash
git clone https://github.com/nymphnerds/NymphsCore.git
```

No submodules. Push normally.

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md) for the full NymphsCore change history across the Manager, Blender addon, and extension publishing flow.

---

## Related Repos

| Repo | Purpose |
|---|---|
| [NymphsExt](https://raw.githubusercontent.com/nymphnerds/NymphsExt/main/index.json) | Blender Extensions (install via repository URL) |
| [NymphsCore](https://github.com/nymphnerds/NymphsCore) | Current Manager, runtime, and addon monorepo |
| [Nymphs2D2](https://github.com/nymphnerds/Nymphs2D2) | 2D backend repo used for the `Z-Image` runtime |
| [Hunyuan3D-2](https://github.com/nymphnerds/Hunyuan3D-2) | Managed multiview/texture backend fork |
