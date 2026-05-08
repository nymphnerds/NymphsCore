<p align="center">
  <img src="Graphics/NymphsCoreLogo.png" alt="NymphsCore" width="960">
</p>

The central hub for the NymphNerds game development backend. This repo contains the core runtime, Manager, and Blender addon source that power the local pipeline.

---

## Install NymphsCore

NymphsCore Manager is the Windows setup and repair app for the local backend runtime used by the Nymphs Blender addon.

Use it when you want the Nymphs backend on your own Windows PC, without manually building WSL, CUDA, Python environments, or model caches.

### What It Installs

The manager imports and maintains a dedicated WSL distro named `NymphsCore`.

Inside that distro, it prepares the supported local backend stack:

- `TRELLIS.2` for single-image image-to-3D and texture/retexture workflows
- `Z-Image` / Nunchaku for local text-to-image and image-to-image generation
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
   [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/refs/heads/rauty/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)
2. Extract the zip to a normal Windows folder.
3. Run `NymphsCoreManager.exe`.
4. Let the manager bootstrap its own fresh Ubuntu WSL base locally.

Optional faster path:

- If you already have a compatible `NymphsCore.tar`, place it beside `NymphsCoreManager.exe`.
- If no tar is present, the manager creates the base distro locally instead.

Your extracted folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  scripts/
    ...
```

Important:

- Do not run the manager from inside the zip.
- Extract it first.
- `NymphsCore.tar` is optional; the manager can bootstrap a fresh Ubuntu base locally.
- A prebuilt tar is only a faster fallback if you already have one.

### Quick Start

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract it to a normal folder on Windows.
3. Run `NymphsCoreManager.exe`.
4. Approve the Windows administrator prompt.
5. Leave model prefetch turned on unless you need a shorter first install.
6. Use `Runtime Tools` after install to check backend readiness or run smoke tests.

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
- `System Check`: checks administrator access, WSL, NVIDIA visibility, optional prebuilt distro package, and existing distros
- `Install Location`: chooses the Windows drive/folder for the managed distro
- `WSL Resources And Models`: chooses WSL resource settings, model prefetch, and optional experimental modules
- `Installation Progress`: bootstraps or imports the distro and prepares runtime environments
- `Finish`: summarizes the install
- `Runtime Tools`: checks backend status, fetches missing models, and runs smoke tests

Model prefetch is recommended for non-technical users. Turning it off only skips the large model downloads; the manager still prepares the runtime stack. Missing models can be fetched later from `Runtime Tools` or during first real use from the addon.

The installer can also offer an experimental optional `Nymphs-Brain` local LLM stack. It installs under `/home/nymph/Nymphs-Brain` inside WSL when selected, is not required for the Blender backend, and can be skipped safely.

If selected, `Nymphs-Brain` now includes:

- LM Studio CLI model management
- a CUDA-accelerated `llama-server` local LLM runtime on `http://localhost:8000/v1`
- Open WebUI on `http://localhost:8081`
- a local MCP gateway for tool access from Cline/Open WebUI
- optional OpenRouter-backed `llm-wrapper` delegation with local prompt caching
- helper commands under `/home/nymph/Nymphs-Brain/bin`
- a bundled `remote_llm_mcp` runtime under `Manager/scripts/remote_llm_mcp`

The installer and runtime wrappers use LM Studio's normal CLI flow for model fetch and management, then serve the selected GGUF model through `llama-server`. No separate manual daemon bootstrap step should be needed.

For the full optional Brain stack guide, see:

```text
docs/NYMPHS_BRAIN_GUIDE.md
```

### Runtime Tools

Use `Runtime Tools` to:

- check whether `Z-Image` and `TRELLIS.2` are ready
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
- enter an optional OpenRouter key for `llm-wrapper`
- open `Manage Models` for the local GGUF model, context length, and optional remote wrapper model
- update the Linux-side Brain stack components
- inspect the Brain activity log

The intended Manager-first flow is:

1. install Brain from the Manager
2. optionally enter an OpenRouter key on the Brain page and click `Apply Key`
3. use `Manage Models` to choose the local GGUF model, context length, and optional remote `llm-wrapper` model
4. start Brain or run `Update Stack`

If no OpenRouter key is present, Brain skips `llm-wrapper` and still starts the rest of the stack normally.

You can verify the wrapper directly from WSL with:

```bash
curl -s http://127.0.0.1:8099/llm-wrapper/llm_call \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"Reply with exactly DIRECT_WRAPPER_TEST_OK and nothing else."}'
```

### Logs And Troubleshooting

Logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

If something fails, send the newest `installer-run-*.log` and a screenshot of the manager window.

Common causes:

- WSL is too old for local no-tar bootstrap, or an optional prebuilt tar is incompatible
- the manager was launched from inside the zip
- not enough free disk space
- WSL is disabled or unhealthy
- NVIDIA is not visible inside WSL
- the model download is still running or was interrupted

Rerunning the latest manager is the intended repair path for interrupted installs, missing packages, missing models, or refreshed runtime scripts. The optional Nymphs-Brain install should not require a separate LM Studio initialization step outside the manager.

### After Install

After the backend is installed, use the Blender addon through the published user guide:

- [Blender Addon User Guide](docs/BLENDER_ADDON_USER_GUIDE.md)
- Blender addon: available on Superhive (temporary URL)

Useful docs:

- [Absolute Beginner Local Backend Install Guide](docs/ABSOLUTE_BEGINNER_INSTALL_GUIDE.md)
- [Install Disk And Model Footprint](docs/FOOTPRINT.md)
- [Blender Addon User Guide](docs/BLENDER_ADDON_USER_GUIDE.md)
- [Nymphs-Brain Guide](docs/NYMPHS_BRAIN_GUIDE.md)

---

## Structure

```
NymphsCore/
├── Manager/        — WSL backend, C# installer, and setup scripts
└── docs/           — install guides, backend docs, and addon user guide
```

### Why is the Blender Addon separate now?
The live Blender addon source now lives outside this repo so the Manager/runtime monorepo can stay public-facing without carrying the addon implementation.

Public install and usage docs remain here in `docs/BLENDER_ADDON_USER_GUIDE.md`, while distribution is temporarily described as available on Superhive.

---

## Adding Things in Future

**New Blender distribution change** — publish package updates through the private distribution path and public Superhive-facing listing.

**New Blender addon source work** — make changes in the separate private addon source repo.

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
| Blender addon | Available on Superhive (temporary URL) |
| [NymphsCore](https://github.com/nymphnerds/NymphsCore) | Current Manager, installer, runtime helpers, and public docs |
| [Nymphs2D2](https://github.com/nymphnerds/Nymphs2D2) | 2D backend repo used for the `Z-Image` runtime |
