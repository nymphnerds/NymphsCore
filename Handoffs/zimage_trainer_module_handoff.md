# Handoff: Manager-Launchable Local Training for Z-Image Turbo Runtime

## Goal

Enable local training for the existing **Z-Image Turbo runtime** managed by the **NymphsCore Manager**, with installation and launch handled from the Manager UI, while keeping the current serving/runtime environment stable.

---

## Executive Summary

The safest and most maintainable path is **not** to train directly inside the current Z-Image/Nunchaku runtime environment.

Instead:

1. Keep the existing **Z-Image / Nunchaku** runtime as a **serving/inference runtime**.
2. Add a separate **training sidecar module** managed by the Manager.
3. Install the sidecar in its **own repo + virtual environment**.
4. Train **LoRAs** locally.
5. Expose the trained LoRAs back to the runtime for inference-time loading.

This gives users a launchable local training workflow from the Manager without destabilizing the current runtime.

---

## Why this approach

### Current runtime is optimized for serving, not training

The existing Manager-managed Z-Image path is tightly pinned and structured around a dedicated runtime environment. That is good for reproducible inference, but risky for iterative training workloads.

Training directly inside the existing runtime would likely create:

- dependency drift
- CUDA / torch version conflicts
- harder repairs and upgrades
- fragile Runtime Tools checks
- support burden when the training stack changes independently

### Z-Image Turbo is not the ideal base for full finetuning

Z-Image Turbo is a distilled model, which makes it great for fast inference, but not the cleanest target for sustained finetuning.

That means the most practical near-term solution is:

- use a **Turbo-compatible LoRA training workflow** for short/local jobs
- keep expectations focused on style / concept / character LoRAs
- avoid presenting it as “full foundation-model finetuning”

---

## Recommended Architecture

## Option A — Recommended for v1

### Add a separate “Z-Image Trainer” module

Create a new optional Manager module:

- **Name:** `Z-Image Trainer`
- **Purpose:** local LoRA training for Z-Image family models, especially Turbo-compatible workflows
- **Install target:** WSL distro already managed by Manager
- **Runtime isolation:** separate venv and repo from the existing Nunchaku runtime

### Proposed filesystem layout

```text
/home/nymph/
  Nymphs2D2/                    # existing managed serving/runtime repo
    .venv-nunchaku/             # existing inference venv

  ZImage-Trainer/               # new trainer repo
    .venv-ztrain/               # new isolated training venv
    config/
    jobs/
    logs/

  data/
    zimage-datasets/
      <dataset_name>/

  models/
    zimage-loras/
      <lora_name>.safetensors
```

### Flow

1. User installs **Z-Image Trainer** from Manager.
2. Manager launches trainer UI or CLI job runner inside WSL.
3. User points the trainer at a local dataset.
4. Trainer outputs a LoRA into `/home/nymph/models/zimage-loras/`.
5. Manager refreshes available LoRAs.
6. Existing Z-Image runtime loads selected LoRA at inference time.

---

## Why this is better than in-place training

### Benefits

- preserves current runtime stability
- keeps upgrade path simple
- allows separate dependency management for training
- easier troubleshooting
- easier to add future support for full Z-Image training
- makes Runtime Tools simpler and more predictable

### Tradeoff

- slightly larger disk footprint
- one more optional module to maintain

This is a good trade for reliability.

---

## Recommended Training Backend

Use a training backend that already supports:

- Z-Image family workflows
- local LoRA training
- CLI and/or lightweight UI
- export to standard `.safetensors` LoRA outputs

### Practical v1 target

Use a backend that can support:

- `Tongyi-MAI/Z-Image-Turbo`
- short-run concept/style/character LoRA workflows
- config-based local jobs

This should be packaged as a **sidecar training tool**, not merged into the runtime venv.

---

## Product Scope Recommendation

## What v1 should support

Keep v1 intentionally narrow:

- installable from Manager
- launchable from Manager
- dataset folder selection
- trigger word / concept name
- output LoRA name
- basic training settings
  - steps
  - resolution
  - learning rate preset
- training log view
- refresh trained LoRAs
- attach selected LoRA to inference runtime

### Best use cases for v1

- style LoRAs
- character LoRAs
- small concept LoRAs
- short training runs

### What v1 should avoid claiming

- full robust base-model finetuning
- long-run Turbo training as a guaranteed stable workflow
- complex multi-adapter orchestration on day one

---

## Manager Integration Plan

## New install script

Add:

```text
Manager/scripts/install_zimage_trainer.sh
```

### Responsibilities

- install trainer system dependencies
- clone or update trainer repo
- create `.venv-ztrain`
- install trainer Python dependencies
- create standard directories:
  - datasets
  - outputs
  - logs
  - configs
- register helper commands/scripts

### Suggested helper commands

```bash
ztrain-start-ui
ztrain-stop-ui
ztrain-run-config <config>
ztrain-tail-log
ztrain-list-loras
```

---

## Runtime status integration

Extend Runtime Tools status with trainer checks.

### Add checks for

- trainer repo exists
- trainer venv exists
- trainer dependencies installed
- dataset root exists
- output LoRA directory exists
- number of discovered `.safetensors` LoRAs
- active trainer process status

### Suggested output fields

```text
ZIMAGE_TRAINER_INSTALLED=yes/no
ZIMAGE_TRAINER_VENV=yes/no
ZIMAGE_TRAINER_RUNNING=yes/no
ZIMAGE_TRAINER_LORAS_FOUND=<count>
ZIMAGE_TRAINER_DATASETS_FOUND=<count>
```

---

## Manager UI additions

### Suggested new buttons / actions

Under Runtime Tools or a new optional module section:

- **Install Z-Image Trainer**
- **Open Trainer UI**
- **Start Training Job**
- **Stop Training Job**
- **Show Training Logs**
- **Refresh LoRAs**
- **Attach LoRA to Z-Image Runtime**

### Suggested user-facing status labels

- Not Installed
- Installed
- Idle
- Training
- Error
- LoRA Ready

---

## C# integration touchpoints

Recommended integration points based on current Manager patterns:

- `apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`

### Responsibilities

#### `InstallerWorkflowService.cs`

- add optional module install flow for trainer
- run WSL install script
- support preflight dependency checks
- expose trainer install/repair state

#### `MainWindowViewModel.cs`

- add actions/commands for trainer lifecycle
- bind trainer status fields into UI
- expose buttons for launch/logs/refresh/attach

---

## Runtime loading strategy for trained LoRAs

The runtime should continue serving the base model as it does now.

Add a lightweight LoRA attach/load layer:

1. scan `/home/nymph/models/zimage-loras/`
2. let user choose a LoRA
3. pass that LoRA path into the runtime
4. load the LoRA before generation
5. optionally unload after generation or when deselected

### Recommended implementation shape

Create a small helper script, for example:

```text
Manager/scripts/zimage_list_loras.sh
Manager/scripts/zimage_attach_lora.sh
```

### Expected behavior

- return list of discovered LoRAs
- validate selected path
- expose selected adapter to runtime
- fail cleanly if file missing or incompatible

---

## Configuration Strategy

## Simple mode

Expose only a few settings in Manager:

- dataset path
- output name
- trigger word
- steps
- image resolution preset
- strength / learning-rate preset

This is the best UX for most users.

## Advanced mode

Allow power users to run config-driven jobs.

### Recommended layout

```text
/home/nymph/ZImage-Trainer/config/
/home/nymph/ZImage-Trainer/jobs/
```

Manager can optionally:

- generate a config from UI inputs
- save it into `jobs/`
- run trainer against that config

---

## Suggested Initial UX

## Install phase

User clicks:

- `Install Z-Image Trainer`

Manager performs:

- preflight check
- install script execution
- status refresh

## Launch phase

User clicks:

- `Open Trainer UI`

Manager performs:

- WSL launch command
- opens browser or embedded surface if supported

## Train phase

User provides:

- dataset folder
- output name
- trigger word
- simple training preset

Manager performs:

- config generation
- job launch
- log streaming or tail view

## Use phase

After training finishes:

- Manager scans LoRA outputs
- user selects one
- user clicks `Attach LoRA`
- runtime uses it for inference

---

## Minimal Viable Version

If implementation time is tight, v1 can be very small.

### MVP features

- install trainer sidecar
- launch trainer UI
- save outputs to managed LoRA folder
- refresh LoRA list
- attach selected LoRA for inference

### Defer until later

- full dataset import UI
- multi-job queueing
- scheduler/priorities
- advanced hyperparameter surface
- full Z-Image foundation-model training path
- remote or distributed training features

---

## Risks and Mitigations

## Risk 1 — Dependency conflicts

### Problem
Training stack and runtime stack may require different torch/CUDA/package versions.

### Mitigation
Keep them in separate repos and separate venvs.

---

## Risk 2 — Turbo training instability on long runs

### Problem
Turbo-oriented training can be less stable or less faithful over long or aggressive training runs.

### Mitigation
Set product expectations clearly:

- optimized for short LoRA jobs
- best for style/concept/character use cases
- recommend conservative presets in UI

---

## Risk 3 — UI complexity

### Problem
Training UIs can become overwhelming quickly.

### Mitigation
Provide:

- simple mode for most users
- advanced mode only as an escape hatch

---

## Risk 4 — Runtime compatibility of produced adapters

### Problem
A produced LoRA may not load cleanly in the current inference path.

### Mitigation
Add a validation step:

- verify file exists
- verify extension and metadata where possible
- offer a quick smoke test load

---

## Rollout Plan

## Phase 1 — Trainer sidecar install

Deliver:

- install script
- WSL trainer repo + venv
- status checks
- Manager install action

## Phase 2 — Launch + logs

Deliver:

- launch UI action
- stop action
- log tail action

## Phase 3 — LoRA management

Deliver:

- scan outputs
- attach/detach LoRA
- inference smoke test

## Phase 4 — UX polish

Deliver:

- simple presets
- config generator
- job history
- validation and recovery tools

## Phase 5 — Future expansion

Potential later work:

- support full Z-Image training workflows
- separate “Turbo trainer” vs “Foundation trainer” modes
- add dataset preparation helpers
- add metadata cards for trained LoRAs

---

## Concrete File Additions

### New scripts

```text
Manager/scripts/install_zimage_trainer.sh
Manager/scripts/ztrain_start_ui.sh
Manager/scripts/ztrain_stop_ui.sh
Manager/scripts/ztrain_run_config.sh
Manager/scripts/ztrain_tail_log.sh
Manager/scripts/zimage_list_loras.sh
Manager/scripts/zimage_attach_lora.sh
```

### C# touchpoints

```text
apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs
apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs
```

### Optional docs

```text
Manager/docs/zimage_trainer_module_handoff.md
Manager/docs/zimage_trainer_user_flow.md
```

---

## Suggested Acceptance Criteria

A v1 implementation should be considered successful if:

1. A user can install **Z-Image Trainer** from the Manager.
2. The trainer installs into its own isolated environment.
3. A user can launch the trainer locally from the Manager.
4. A user can complete a local LoRA training run.
5. The output LoRA is stored in a Manager-known folder.
6. The Manager can detect and list the new LoRA.
7. The existing Z-Image runtime can attach the LoRA for inference.
8. The existing serving runtime remains repairable and independent of the trainer.

---

## Recommendation

Build this as a **separate Manager-managed training sidecar**, not as an in-place modification of the current Z-Image runtime.

That gives you:

- a launchable local training workflow
- compatibility with the current Manager model
- safer upgrades
- clearer debugging
- a clean path to future full Z-Image training support

---

## Bottom Line

The most realistic solution is:

**Manager installs and launches a separate local Z-Image Trainer module in WSL, stores trained LoRAs in a managed folder, and the existing Z-Image/Nunchaku runtime loads those LoRAs at inference time.**

That is the best balance of feasibility, stability, and developer maintainability for this branch.
