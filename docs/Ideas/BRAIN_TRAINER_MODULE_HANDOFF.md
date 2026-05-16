# brain-train Module Handoff

Date: 2026-05-16
Branch context: `modular`

Use this handoff to design and build the future NymphsCore module for training
local Brain coding/game-dev adapters.

Working module name:

```text
brain-train
```

Suggested repo id:

```text
brain-train
```

Suggested install root:

```text
/home/nymph/brain-train
```

## Goal

Build a beginner-friendly local LLM adapter trainer for Brain.

The product should help a user train a local coding/game-engine assistant from
their own codebases, docs, project conventions, and engine-specific knowledge.

The intended end-to-end flow is:

```text
pick Brain/base model
-> add source folders and docs
-> build reviewed instruction dataset
-> add trainer job
-> start/stop/delete/poll job
-> save finished LoRA/QLoRA adapter
-> install/link adapter into Brain
-> optionally pair it with a project RAG/index
```

This should become a sibling training module, not an expansion of the existing
image LoRA module.

## Read These First

Core standards:

```text
/home/nymph/NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md
/home/nymph/NymphsCore/docs/NYMPH_MODULE_UI_STANDARD.md
```

Reference modules:

```text
/home/nymph/NymphsModules/lora
/home/nymph/NymphsModules/lora/docs/EASY_LORA_MODULE_HANDOFF.md
/home/nymph/NymphsModules/brain
/home/nymph/NymphsModules/brain/docs/BRAIN_MODULE_MIGRATION_NOTES.md
```

Reference runtime folders:

```text
Brain runtime:      /home/nymph/Nymphs-Brain
LoRA runtime:       /home/nymph/LoRA
Future trainer:     /home/nymph/brain-train
Managed WSL distro: NymphsCore
```

When validating from the development WSL, use the real managed runtime distro:

```bash
wsl.exe -d NymphsCore --user nymph -- ...
```

Do not confuse the development/source WSL with the runtime WSL.

## Important Product Rule

Do not promise "train on my whole repo and magic happens."

For code and game engines, training should teach behavior, style, conventions,
and recurring patterns. It should not be treated as a replacement for retrieval
or exact source lookup.

Target architecture:

```text
RAG/index -> exact current code/docs/project facts
LoRA/QLoRA adapter -> local coding behavior, project style, engine habits
```

The first useful product should be:

```text
Build a clean dataset from selected code/docs, review it, train a small adapter,
and connect the result to Brain.
```

## Current Source Shape

### Brain

Brain is currently a service module at:

```text
/home/nymph/NymphsModules/brain
```

It owns:

```text
llama-server API: http://127.0.0.1:8000
Open WebUI:       http://127.0.0.1:8081
MCP gateway:      http://127.0.0.1:8100
mcpo bridge:      http://127.0.0.1:8099
models:           /home/nymph/Nymphs-Brain/models
logs:             /home/nymph/Nymphs-Brain/logs
secrets:          /home/nymph/Nymphs-Brain/secrets
```

Useful source files:

```text
scripts/install_brain.sh
scripts/brain_status.sh
scripts/brain_start.sh
scripts/brain_stop.sh
scripts/brain_webui.sh
scripts/brain_manage_models.sh
scripts/brain_logs.sh
scripts/_brain_common.sh
```

Brain status already exposes selected local and remote model fields:

```text
local_model=...
remote_model=...
llm_running=...
model_configured=...
```

brain-train should read Brain state through module actions/status files or
stable Brain runtime config. It should not hardcode Manager internals.

### LoRA

LoRA is currently a training module at:

```text
/home/nymph/NymphsModules/lora
```

It proves the most important workflow shape for brain-train:

```text
form values
-> normalize name
-> prepare dataset metadata
-> generate backend config
-> register/add job
-> start queue/job
-> stop/delete safely
-> poll status/log/progress in-place
-> preserve user data and outputs
```

Useful source files:

```text
nymph.json
ui/manager.html
scripts/install_lora.sh
scripts/lora_status.sh
scripts/lora_dataset.py
scripts/lora_job.py
scripts/lora_job_control.py
scripts/lora_logs.sh
scripts/_lora_common.sh
```

brain-train should copy the pattern, not the image-specific behavior.

## Proposed Runtime Layout

```text
/home/nymph/brain-train
  .nymph-module-version
  bin/
  venv/
  trainer/
    axolotl/
  sources/
    my_project.sources.json
  datasets/
    my_project_coder/
      raw_manifest.json
      examples.jsonl
      review.jsonl
      dataset_report.json
  jobs/
    my_project_coder.yaml
    my_project_coder.json
  adapters/
    my_project_coder/
      adapter_model.safetensors
      adapter_config.json
      nymphs_brain_adapter.json
  indexes/
    my_project_coder/
  config/
    selected_backend.json
    brain_integration.json
  logs/
    brain-train.log
```

Do not commit runtime folders, datasets, generated examples, adapters, indexes,
model weights, venvs, logs, secrets, or downloaded backend repos.

## Proposed Repo Layout

```text
NymphsModules/brain-train/
  nymph.json
  README.md
  CHANGELOG.md
  docs/
    BRAIN_TRAINER_MODULE_HANDOFF.md
    DATASET_GUIDE.md
    BRAIN_INTEGRATION_GUIDE.md
  packs/
    unreal.json
    unity.json
    godot.json
    blender.json
    nymphscore.json
  scripts/
    _brain_trainer_common.sh
    install_brain_trainer.sh
    brain_trainer_update.sh
    brain_trainer_status.sh
    brain_trainer_logs.sh
    brain_trainer_open_sources.sh
    brain_trainer_open_datasets.sh
    brain_trainer_open_adapters.sh
    brain_trainer_build_dataset.sh
    brain_trainer_review_dataset.sh
    brain_trainer_create_job.sh
    brain_trainer_start_job.sh
    brain_trainer_stop_job.sh
    brain_trainer_delete_job.sh
    brain_trainer_job_status.sh
    brain_trainer_install_into_brain.sh
    brain_trainer_uninstall.sh
    brain_dataset.py
    brain_job.py
    brain_job_control.py
  ui/
    manager.html
```

## Manifest Sketch

Start with a normal installable module manifest:

```json
{
  "manifest_version": 1,
  "id": "brain-train",
  "name": "brain-train",
  "short_name": "BT",
  "version": "0.1.0",
  "description": "Local LoRA/QLoRA trainer for Brain coding and game-development adapters.",
  "category": "training",
  "packaging": "repo",
  "install": {
    "root": "$HOME/brain-train",
    "entrypoint": "scripts/install_brain_trainer.sh",
    "version_marker": "$HOME/brain-train/.nymph-module-version",
    "installed_markers": [
      "$HOME/brain-train/.nymph-module-version"
    ]
  },
  "artifacts": {
    "install_root": "$HOME/brain-train",
    "sources_root": "$HOME/brain-train/sources",
    "datasets_root": "$HOME/brain-train/datasets",
    "jobs_root": "$HOME/brain-train/jobs",
    "adapters_root": "$HOME/brain-train/adapters",
    "indexes_root": "$HOME/brain-train/indexes",
    "logs_root": "$HOME/brain-train/logs"
  },
  "entrypoints": {
    "install": "scripts/install_brain_trainer.sh",
    "update": "scripts/brain_trainer_update.sh",
    "status": "scripts/brain_trainer_status.sh",
    "open_sources": "scripts/brain_trainer_open_sources.sh",
    "open_datasets": "scripts/brain_trainer_open_datasets.sh",
    "open_adapters": "scripts/brain_trainer_open_adapters.sh",
    "build_dataset": "scripts/brain_trainer_build_dataset.sh",
    "create_job": "scripts/brain_trainer_create_job.sh",
    "start_job": "scripts/brain_trainer_start_job.sh",
    "stop_job": "scripts/brain_trainer_stop_job.sh",
    "delete_job": "scripts/brain_trainer_delete_job.sh",
    "job_status": "scripts/brain_trainer_job_status.sh",
    "install_into_brain": "scripts/brain_trainer_install_into_brain.sh",
    "logs": "scripts/brain_trainer_logs.sh",
    "uninstall": "scripts/brain_trainer_uninstall.sh"
  },
  "ui": {
    "sort_order": 35,
    "standard_lifecycle_rail": true,
    "manager_ui": {
      "type": "local_html",
      "title": "brain-train",
      "entrypoint": "ui/manager.html"
    },
    "manager_actions": [
      {
        "id": "open_trainer",
        "label": "brain-train",
        "entrypoint": "open_trainer",
        "result": "open_module_ui"
      },
      {
        "id": "open_datasets",
        "label": "Open Datasets",
        "entrypoint": "open_datasets",
        "result": "open_directory"
      },
      {
        "id": "open_adapters",
        "label": "Open Adapters",
        "entrypoint": "open_adapters",
        "result": "open_directory"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "open_notepad"
      }
    ]
  }
}
```

The exact action ids can change, but keep them module-owned and manifest
declared. Do not add brain-train-specific UI code to the Manager.

## Backend Recommendation

Start with Axolotl as the first backend.

Why:

- config-driven YAML jobs fit the existing Nymph module pattern
- LoRA/QLoRA support is mature enough for first local experiments
- it keeps training behavior behind module-owned scripts
- it is easier to inspect, save, rerun, and debug than a one-off trainer script

Possible later backends:

```text
Unsloth -> low-VRAM consumer GPU mode
TRL     -> custom advanced workflows
```

Do not support multiple backends in the first milestone. Pick one, prove the
end-to-end product, then add choices later.

## Beginner UI Flow

Use Easy LoRA's corrected mental model:

```text
Name adapter
Choose Brain/base model
Choose source folders/docs
Choose engine pack
Build Dataset
Review Dataset
Add Job
Start Job
Stop Job
Delete Job
Refresh
Install Into Brain
```

Recommended primary-action states:

```text
No dataset         -> Build Dataset primary
Dataset built      -> Review Dataset primary
Reviewed dataset   -> Add Job primary
Job saved          -> Start Job primary
Queued/running     -> Stop Job primary
Finished adapter   -> Install Into Brain primary
Installed adapter  -> Test in Brain primary
```

Keep `Add Job` and `Start Job` separate. The LoRA validation showed that this
is much clearer once the UI makes the state obvious.

Beginner labels:

```text
Small Test
Project Style
Code Helper
Engine Expert
Low VRAM
Build Dataset
Review Examples
Add Job
Start Job
Install Into Brain
```

Hide advanced terms initially:

```text
sequence length
LoRA rank
learning rate
epochs/max steps
batch size
gradient accumulation
packing
QLoRA
checkpoint frequency
```

Add hover help from the start.

## Dataset Builder

This is the first real milestone. Build it before training.

Goal:

```text
source folders/docs -> clean reviewed JSONL instruction dataset
```

Inputs:

```text
project source folders
engine docs
plugin docs
markdown notes
module docs
code snippets
```

Ignore by default:

```text
.git
.svn
.hg
node_modules
bin
obj
Build
Binaries
DerivedDataCache
Intermediate
Library
Temp
.vs
.vscode
venv
.venv
__pycache__
dist
out
coverage
*.png
*.jpg
*.jpeg
*.webp
*.gif
*.bmp
*.uasset
*.umap
*.dll
*.exe
*.pdb
*.lib
*.so
*.dylib
```

Include candidates:

```text
.cs
.cpp
.c
.h
.hpp
.py
.gd
.ts
.js
.jsx
.tsx
.json
.yaml
.yml
.toml
.ini
.md
.txt
.uproject
.uplugin
.csproj
.sln
```

The builder should create:

```text
raw_manifest.json
examples.jsonl
dataset_report.json
```

Every example should carry source metadata:

```json
{
  "messages": [
    {
      "role": "system",
      "content": "You are a local game-development coding assistant."
    },
    {
      "role": "user",
      "content": "Explain this Unreal component."
    },
    {
      "role": "assistant",
      "content": "..."
    }
  ],
  "source": {
    "path": "Source/MyGame/FooComponent.cpp",
    "engine": "unreal",
    "language": "cpp",
    "pack": "unreal"
  }
}
```

Do not start by generating thousands of low-quality examples. Prefer a small,
auditable dataset and a visible report:

```text
files scanned
files skipped
examples generated
estimated tokens
largest files
ignored binary/cache paths
warnings
```

## Dataset Review

The review phase is mandatory for the noob-safe product.

First version can be simple:

```text
open review.jsonl / examples.jsonl in a text editor
show dataset counts in Manager UI
allow rebuild
```

Better version:

```text
local HTML review list
accept/reject example
filter by pack/language/path
regenerate selected examples
export reviewed dataset
```

Bad training data is worse than no training data. Keep this visible.

## Engine Packs

Create simple JSON packs early:

```text
packs/unreal.json
packs/unity.json
packs/godot.json
packs/blender.json
packs/nymphscore.json
```

Each pack should define:

```text
id
label
file includes
ignore patterns
language mapping
system prompt
example templates
evaluation prompts
recommended defaults
```

Example pack intent:

```text
unreal     -> C++, headers, .uproject, .uplugin, engine/plugin conventions
unity      -> C#, asmdef/csproj metadata, editor tooling patterns
godot      -> GDScript/C#, scene/script conventions
blender    -> Python addon/operator/panel patterns
nymphscore -> module manifests, shell scripts, Manager docs, local module rules
```

## Job Flow

Preserve the proven LoRA module structure:

```text
form values
-> normalize adapter/dataset/job name
-> build/review dataset
-> generate backend YAML
-> save local job metadata JSON
-> add/register job in module-owned state
-> start job through module-owned runner
-> stop/delete only job state/processes
-> poll logs/progress
-> detect final adapter
-> install/link into Brain
```

Unlike image LoRA, Axolotl may not provide the same UI/API queue as AI Toolkit.
That is okay. brain-train can own a local job registry and process runner, as
long as the user-facing flow remains:

```text
Add Job -> Start Job -> Stop Job -> Delete Job -> Refresh
```

Do not use `Delete Job` to delete:

```text
source folders
datasets
reviewed examples
finished adapters
Brain models
Brain config
```

Delete Job should delete only the trainer job config/registration and any
active queued process state.

## Status Contract

`brain_trainer_status.sh` should be lightweight and marker-first.

Suggested key/value fields:

```text
id=brain-train
name=brain-train
installed=true|false
runtime_present=true|false
data_present=true|false
version=0.1.0
state=available|installed|needs_backend|needs_brain|running|needs_attention
health=ok|degraded|missing
backend=axolotl
backend_ready=true|false
brain_installed=true|false
brain_model_configured=true|false
brain_local_model=...
dataset_count=0
job_count=0
adapter_count=0
active_state=idle|queued|running|stopping|finished|failed
active_job=...
install_root=/home/nymph/brain-train
datasets=/home/nymph/brain-train/datasets
adapters=/home/nymph/brain-train/adapters
logs_dir=/home/nymph/brain-train/logs
detail=...
```

`brain_trainer_job_status.sh` should be the in-place UI polling endpoint and
can be heavier.

Suggested fields:

```text
normalized_name=...
dataset_exists=true|false
reviewed_dataset_exists=true|false
job_exists=true|false
job_status=idle|queued|running|stopped|failed|finished
progress_current=...
progress_total=...
progress_percent=...
progress_text=...
adapter_exists=true|false
adapter_path=...
adapter_size=...
adapter_modified=...
installed_into_brain=true|false
log_available=true|false
log_tail_json="..."
```

The UI should not blindly reuse old completed progress for a new unsaved job.
Easy LoRA already hit this bug; keep workflow state and finished-output summary
separate.

## Logs Contract

Follow the current module standard:

```json
{
  "id": "logs",
  "label": "Logs",
  "entrypoint": "logs",
  "result": "open_notepad"
}
```

The script must create and print a real log file:

```text
last_log=/home/nymph/brain-train/logs/brain-train.log
logs_dir=/home/nymph/brain-train/logs
```

Do not route module `Logs` to the Manager left-sidebar Logs page.

## Brain Integration

First version should not rewrite Brain internals.

Recommended first integration:

```text
brain-train writes adapter metadata under its own adapters folder.
Install Into Brain writes a small Brain-readable adapter registry file.
Brain later learns how to list/use that registry.
```

Possible target:

```text
/home/nymph/Nymphs-Brain/config/adapters.json
```

Example adapter metadata:

```json
{
  "id": "my_project_coder",
  "name": "My Project Coder",
  "base_model": "selected Brain local model",
  "adapter_path": "/home/nymph/brain-train/adapters/my_project_coder",
  "dataset_path": "/home/nymph/brain-train/datasets/my_project_coder/review.jsonl",
  "index_path": "/home/nymph/brain-train/indexes/my_project_coder",
  "created_at": "2026-05-16T00:00:00Z",
  "engine_pack": "unreal"
}
```

Treat adapter loading as a separate Brain integration task. Do not make Brain
Trainer silently edit `lms-start` until the Brain runtime has a stable adapter
loading contract.

## RAG/Index Pairing

Plan for this early, even if it is not in v0.1.

brain-train should eventually create:

```text
adapter -> style/conventions/patterns
index   -> exact code/docs/project facts
```

Possible future artifacts:

```text
/home/nymph/brain-train/indexes/<name>/chunks.jsonl
/home/nymph/brain-train/indexes/<name>/vectors.sqlite
/home/nymph/brain-train/indexes/<name>/index_manifest.json
```

This can become a Brain MCP tool or Open WebUI/RAG integration later.

## Milestone Plan

### Milestone 0: Decide and Scaffold

- Create `NymphsModules/brain-train`.
- Add `nymph.json`, README, changelog, docs, empty scripts.
- Add registry entry only after local install/status works.
- Do not add Manager-specific code.

### Milestone 1: Install/Status/Logs Shell

- Install creates `/home/nymph/brain-train` layout.
- Write `.nymph-module-version`.
- Add uninstall with preserve-by-default data behavior.
- Add `logs` with `open_notepad` contract.
- Status reports marker-installed truth quickly.
- Status reads Brain presence/model state where cheap.

### Milestone 2: Source and Dataset Builder

- Add source folder definitions.
- Add pack-based scanner and ignore rules.
- Generate `raw_manifest.json`, `examples.jsonl`, `dataset_report.json`.
- Add Open Sources/Open Datasets actions.
- Manager UI can build dataset and show report counts.

### Milestone 3: Review Workflow

- Add reviewed dataset file.
- First version may open JSONL/report in editor.
- Better version adds local HTML accept/reject review.
- Job creation must require reviewed examples or explicit confirmation.

### Milestone 4: Backend Install

- Install Axolotl in isolated trainer venv.
- Keep model caches under shared/preserved cache paths where sensible.
- Do not download giant base models during base install.
- Detect compatible Brain-selected base model if possible.

### Milestone 5: Add Job / Start Job

- Generate Axolotl YAML from form values and dataset path.
- Save job metadata JSON.
- Add Job only registers/saves the job.
- Start Job starts a tracked local process.
- Logs stream to module log.
- Status polls progress from log/process state.

### Milestone 6: Stop/Delete/Completion

- Stop Job terminates only the active trainer process.
- Delete Job removes job registration/config only.
- Detect final adapter and write `nymphs_brain_adapter.json`.
- Do not delete datasets, sources, reviewed examples, adapters, or Brain files.

### Milestone 7: Install Into Brain

- Write a Brain adapter registry descriptor.
- Show installed/link state in brain-train.
- Add a safe test prompt flow if Brain is running.
- Keep Brain changes small and documented.

### Milestone 8: Tiny Real Validation

Validate with a tiny source folder and tiny model first:

```text
build dataset
review examples
add job
start tiny training
stop if needed
finish adapter
install/link into Brain
confirm Brain can see or report the adapter metadata
delete job without deleting dataset/adapter
```

Only after this should the module be advertised in the registry.

## No-Go List

Do not:

- put brain-train UI into Manager source
- mutate Brain runtime startup scripts without a stable Brain adapter contract
- train directly against random huge repo dumps without review
- delete user code/source folders
- delete Brain models or shared caches from brain-train Delete Job
- download base models during base install
- require OpenRouter/cloud services for the local training path
- hide logs/progress during long dataset/training actions
- collapse `Add Job` and `Start Job` into one confusing button

## Open Questions

- Which base local model should be the first supported target?
- Should v0.1 require GGUF base models, Hugging Face transformer models, or a
  separate trainer-format base model?
- What is the safest adapter loading path for the current Brain llama-server
  runtime?
- Should the first training proof use Axolotl directly, or should Unsloth be
  the first backend for lower VRAM?
- Should RAG/index be part of v0.1, or documented as v0.2 after adapter proof?
- Should dataset examples be generated locally by Brain, remotely through the
  Brain llm-wrapper, or initially template-only?

## Suggested First Task

Do not start with training.

Start with the dataset proof:

```text
Create brain-train module skeleton
-> install/status/logs work
-> pick a tiny source folder
-> generate examples.jsonl and dataset_report.json
-> expose Build Dataset and Refresh in local Manager UI
```

Once the dataset builder is real and reviewable, training becomes a much less
mysterious problem.
