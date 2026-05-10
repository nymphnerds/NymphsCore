# Current Nymph Module Repo Deep Dive

**Generated**: 2026-05-07  
**Branch Context**: `modular`  
**Purpose**: Map the current installed/planned Manager backends into the future `nymph.json` + registry shape.

---

## Ground Rules

- Do not switch branches to inspect `main`.
- Use `git show main:path/to/file` when checking old installer structure.
- Develop this conversion on `modular`.
- Keep live runtime/install state out of `nymph.json`.
- Treat `nymph.json` as static module definition.
- Treat runtime detection as Manager/service state.

---

## Current Working Module Set

The current shell knows these Nymphs:

| Module | ID | Kind | Category | Expected install root |
|---|---:|---|---|---|
| Brain | `brain` | `repo` | `service` | `~/Nymphs-Brain` |
| Z-Image Turbo | `zimage` | `repo` | `runtime` | `~/Z-Image` |
| AI Toolkit | `ai-toolkit` | `repo` | `trainer` | `~/ZImage-Trainer` |
| TRELLIS.2 | `trellis` | `repo` | `runtime` | `~/TRELLIS.2` |
| WORBI | `worbi` | `archive` | `tool` | `~/worbi` |

## Preferred Repo Rule

Use **one clean repo per Nymph module**.

That means each module should have its own obvious home:

```text
github.com/nymphnerds/zimage
github.com/nymphnerds/trellis
github.com/nymphnerds/brain
github.com/nymphnerds/ai-toolkit
github.com/nymphnerds/worbi
```

Each module repo owns:

```text
nymph.json
README.md
scripts/
  install.sh
  status.sh
  update.sh
  start.sh
  stop.sh
  open.sh
  logs.sh
```

The module repo can still clone or depend on public upstream repos internally, but the Manager only has to understand **one repo per module**.

Example:

```text
ai-toolkit repo
  -> Manager sees this repo
  -> its install script clones/updates upstream ostris/ai-toolkit inside ~/ZImage-Trainer/ai-toolkit
```

This keeps the Manager simple:

```text
registry -> module repo -> nymph.json -> stable wrapper scripts
```

It also keeps ownership clean:

- public upstream repos can change independently
- Nymphs wrapper behavior stays under our control
- friends can maintain one module without touching NymphsCore
- Manager does not need special-case logic for every backend

In this current dev shell WSL root, detected installed folders are:

```text
~/Z-Image
~/TRELLIS.2
~/Nymphs-Brain
```

Not currently detected in this current dev shell WSL root:

```text
~/ZImage-Trainer
~/worbi
```

Important correction:

- the live test WSL distro named `NymphsCore` **does** have AI Toolkit installed and working under `~/ZImage-Trainer`
- use that live distro as the source of truth for AI Toolkit migration details
- do not copy user datasets, LoRAs, jobs, logs, venvs, node runtimes, or model caches from it into the future repo

---

## Existing Repo Reality

### Z-Image Turbo

Current live folder:

```text
/home/nymph/Z-Image
```

Git state:

```text
origin: git@github.com:nymphnerds/Nymphs2D2.git
branch: main
HEAD: 2109c9d34584793ad42df3aed7df129d48a7fcfe
latest commit: Stabilize Z-Image Nunchaku LoRA runtime path
worktree: clean
```

Installer source:

```text
Manager/scripts/install_nymphs2d2.sh
Manager/scripts/zimage_backend_overlay/
Manager/scripts/runtime_tools_status.sh
Manager/scripts/smoke_test_server.sh
```

Current default source in scripts:

```text
https://github.com/nymphnerds/Nymphs2D2.git
```

Current lock file pin:

```text
runtime-deps.lock.json -> Nymphs2D2 pinned 655112d1d344914c0b7289fbc97558d123d3ecf5
```

Note: live `~/Z-Image` is ahead/different from the current lock pin. That may be intentional while developing, but the module conversion should make the update policy explicit.

Recommended future module repo:

```text
github.com/nymphnerds/zimage
```

Recommended package kind:

```json
"kind": "repo"
```

Recommended approach:

- make this the clean Z-Image Nymph module repo
- it may fork or vendor the current `Nymphs2D2` source internally
- add `nymph.json` at the repo root
- move Manager-facing scripts into stable wrapper names
- keep heavy model downloads out of git
- keep Nunchaku/diffusers pins in the module manifest or module lock file

---

### TRELLIS.2

Current live folder:

```text
/home/nymph/TRELLIS.2
```

Git state:

```text
origin: git@github.com:microsoft/TRELLIS.2.git
branch: main
HEAD: 5565d240c4a494caaf9ece7a554542b76ffa36d3
latest commit: Release Training Code
worktree: clean
```

Installer source:

```text
Manager/scripts/install_trellis.sh
Manager/scripts/trellis_adapter/
Manager/scripts/runtime_tools_status.sh
Manager/scripts/smoke_test_server.sh
```

Extra runtime repos currently pulled by installer:

```text
https://github.com/Aero-Ex/ComfyUI-Trellis2-GGUF.git
https://github.com/city96/ComfyUI-GGUF.git
https://github.com/EasternJournalist/utils3d.git
https://github.com/JeffreyXiang/CuMesh.git
https://github.com/JeffreyXiang/FlexGEMM.git
https://github.com/NVlabs/nvdiffrast.git
```

Current lock file pin:

```text
runtime-deps.lock.json -> TRELLIS.2 pinned 5565d240c4a494caaf9ece7a554542b76ffa36d3
```

Recommended future module repo:

```text
github.com/nymphnerds/trellis
```

Recommended package kind:

```json
"kind": "hybrid"
```

Why hybrid:

- Microsoft TRELLIS.2 is the upstream source
- Nymphs adds GGUF adapter scripts and Manager lifecycle wrappers
- several pinned support repos are involved

Recommended approach:

- make this the clean TRELLIS Nymph module repo
- add `nymph.json` there
- keep adapter scripts in the module repo
- treat Microsoft TRELLIS.2 as upstream/source dependency
- keep all GGUF/support pins in one module-owned lock file

---

### Brain

Current live folder:

```text
/home/nymph/Nymphs-Brain
```

Git state:

```text
not a git repo
```

Observed shape:

```text
bin/
config/
local-tools/
lmstudio/
mcp/
mcp-venv/
models/
npm-global/
open-webui-data/
open-webui-venv/
scripts/
secrets/
venv/
```

Installer source:

```text
Manager/scripts/install_nymphs_brain.sh
Manager/scripts/monitor_query.sh
Manager/scripts/remote_llm_mcp/
```

Important external pulls:

```text
https://github.com/ggml-org/llama.cpp.git
https://lmstudio.ai/install.sh
Open WebUI packages
MCP npm/python packages
```

Recommended future module repo:

```text
github.com/nymphnerds/brain
```

Recommended package kind:

```json
"kind": "repo"
```

But the repo should not contain the whole installed runtime.

The repo should contain:

```text
nymph.json
README.md
scripts/
  install_brain.sh
  brain_status.sh
  brain_start.sh
  brain_stop.sh
  brain_open.sh
  brain_logs.sh
  brain_update.sh
templates/
  config files
  wrapper scripts
```

The repo should not contain:

```text
venv/
mcp-venv/
open-webui-venv/
models/
open-webui-data/
secrets/
logs/
```

Recommended approach:

- split the current giant installer into stable wrapper scripts
- make `~/Nymphs-Brain` an install output, not the source repo itself
- put user data and models under ignored runtime folders
- have the Manager call wrappers through manifest entrypoints

---

### AI Toolkit

Live test WSL folder:

```text
/home/nymph/ZImage-Trainer
```

Live test WSL state:

```text
installed in distro `NymphsCore`
```

Observed live install shape:

```text
/home/nymph/ZImage-Trainer/
  ai-toolkit/
  DiffSynth-Studio/
  .node20/
  .venv-ztrain/
  adapters/zimage_turbo_training_adapter/
  bin/
  config/
  datasets/
  jobs/
  logs/
  loras/
  models/Tongyi-MAI/
  run/
```

Observed upstream AI Toolkit checkout:

```text
path:   /home/nymph/ZImage-Trainer/ai-toolkit
origin: https://github.com/ostris/ai-toolkit.git
branch: main
HEAD:   0d91fce Allow edit of captioning job
state:  local edits exist (`flux_train_ui.py`) plus `.gradio/`
```

Installer source:

```text
Manager/scripts/install_zimage_trainer_aitk.sh
Manager/scripts/zimage_trainer_status.sh
Manager/scripts/ztrain_run_config.sh
Manager/scripts/zimage_caption_brain.sh
Manager/scripts/zimage_caption_brain.py
Manager/scripts/compare_zimage_loras.py
```

Current default source in scripts:

```text
https://github.com/ostris/ai-toolkit.git
```

Expected install shape:

```text
~/ZImage-Trainer/
  ai-toolkit/
  datasets/
  loras/
  jobs/
  logs/
  config/
  bin/
  models/Tongyi-MAI/Z-Image-Turbo/
  adapters/zimage_turbo_training_adapter/
```

Migration warning:

The live AI Toolkit install contains real user/training material:

```text
datasets/
loras/
jobs/
logs/
models/
ai-toolkit/venv/
.node20/
.venv-ztrain/
ai-toolkit/ui/node_modules/
ai-toolkit/aitk_db.db
```

Do **not** copy those into the future module repo.

Only copy/port:

- Manager wrapper scripts
- training adapter files/templates
- config templates
- status/start/stop/open/log wrappers
- caption helper scripts
- documented install assumptions

Recommended future module repo:

```text
github.com/nymphnerds/ai-toolkit
```

Recommended package kind:

```json
"kind": "hybrid"
```

Why hybrid:

- upstream AI Toolkit is a public repo
- Nymphs adds Z-Image-specific training defaults, wrappers, DB path fixes, UI launch/stop scripts, caption helpers, and Brain integration

Recommended approach:

- create one clean AI Toolkit Nymph repo
- keep upstream AI Toolkit as the cloned payload inside `~/ZImage-Trainer/ai-toolkit`
- later decide whether that repo forks upstream directly or continues to clone upstream during install
- add `nymph.json` to the wrapper repo
- keep datasets, LoRAs, jobs, DBs, models, node runtime, and venvs out of git

---

### WORBI

Expected live folder:

```text
/home/nymph/worbi
```

Current WSL state:

```text
not detected
```

Current standard reference:

```text
docs/Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md
```

Recommended future module repo:

```text
github.com/nymphnerds/worbi
```

Recommended package kind for first test:

```json
"kind": "archive"
```

Recommended approach:

- friend hosts WORBI repo with `nymph.json`
- repo includes stable Manager-facing scripts
- repo may contain an archive package for first test
- registry points to the WORBI manifest URL

---

## Registry Shape For Our End

Recommended repo:

```text
github.com/nymphnerds/nymphs-registry
```

Recommended first file:

```text
nymphs.json
```

Example:

```json
{
  "registry_version": 1,
  "modules": [
    {
      "id": "zimage",
      "name": "Z-Image Turbo",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/zimage/main/nymph.json"
    },
    {
      "id": "trellis",
      "name": "TRELLIS.2",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/trellis/main/nymph.json"
    },
    {
      "id": "brain",
      "name": "Brain",
      "channel": "experimental",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/brain/main/nymph.json"
    },
    {
      "id": "ai-toolkit",
      "name": "AI Toolkit",
      "channel": "experimental",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/ai-toolkit/main/nymph.json"
    },
    {
      "id": "worbi",
      "name": "WORBI",
      "channel": "test",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/worbi/main/nymph.json"
    }
  ]
}
```

For development, bundle a local copy in this repo first:

```text
Manager/registry/nymphs.json
```

Then later add online refresh from GitHub.

---

## What The Manager Needs Next

### Phase 1: Bundled local registry

Add:

```text
Manager/registry/nymphs.json
Manager/registry/manifests/*.nymph.json
```

Use this before online fetching. It lets the Manager prove the architecture without network instability.

### Phase 2: Manifest models

Add C# models for:

```text
NymphRegistry
NymphRegistryEntry
NymphManifest
NymphSource
NymphEntrypoints
NymphRuntimeState
```

Important:

- manifest data is static
- install/running/update state is detected separately

### Phase 2.5: Preserve module-specific pages

Do not replace existing real module pages with the generic shell page.

The generic page currently showing module facts and manager contract capabilities is only a fallback.

Each Nymph needs its own content surface:

```text
Brain       -> local LLM, MCP, WebUI, model manager, OpenRouter, monitor, logs
Z-Image     -> image runtime readiness, model fetch, smoke test, LoRA/runtime support
TRELLIS     -> 3D runtime readiness, GGUF quant, model fetch, adapter repair, smoke test
AI Toolkit  -> datasets, captions, LoRAs, jobs, queue, training UI, progress, logs
WORBI       -> app health, launch/stop, project/data folder, logs, version/config
```

The `main` branch Manager already contains serious page work for Brain, Z-Image Trainer, and Runtime Tools. Carry that content forward into the new modular shell rather than rebuilding those pages as generic manifest inspectors.

### Phase 3: Registry loader service

Add a service that:

- reads bundled registry
- loads bundled manifests
- later can fetch remote registry
- validates minimum fields
- ignores malformed modules instead of crashing the shell

### Phase 4: Convert hardcoded module roster

Current hardcoded source:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
```

Replace the hardcoded `_allModules` list with manifest-loaded module definitions, but preserve the current install detection paths while migrating.

### Phase 5: Entrypoint execution

After definitions load from manifests, map generic UI actions to entrypoints:

```text
install
update
status
start
stop
open
logs
configure
```

The Manager should call trusted wrapper scripts, not arbitrary unknown internet commands.

---

## Security / Trust Rules

The Manager should not search all of GitHub.

It should:

- fetch only the trusted registry
- read manifests linked by that registry
- require known `id` format
- show source repo clearly
- run only approved wrapper entrypoints
- keep online discovery separate from script execution

First implementation should be conservative:

```text
bundled registry -> show available modules -> local install detection
```

Then:

```text
online registry refresh -> compare remote manifests -> show newly available modules
```

---

## Short Version

The current installed backends are close to the future model, but not equally ready:

- `Z-Image Turbo` is already basically a module repo.
- `TRELLIS.2` should become a wrapper/fork/hybrid Nymph because the Nymphs value is the adapter/runtime glue.
- `Brain` needs the biggest split: source repo vs installed runtime folder.
- `AI Toolkit` should start as a wrapper Nymph around upstream `ostris/ai-toolkit`.
- `WORBI` is the cleanest first external test because it can be hosted by a friend with a small manifest and scripts.

The next Manager-side move is a bundled local registry and manifest loader, not full online install execution yet.
