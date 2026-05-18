# Z-Image Trainer Handoff And Roadmap

Date: 2026-05-01

## Executive Summary

The Z-Image Trainer work is partially integrated in `NymphsCore Manager`, but the live trainer sidecar is not currently present on this machine.

What exists today:

- Manager-side installer and launch wiring for an AI Toolkit-based trainer
- status scripts and helper scripts for datasets, jobs, logs, and LoRA outputs
- a live `Z-Image` inference runtime with ongoing Nunchaku work
- prior handoff notes documenting the AI Toolkit pivot

What does not exist today:

- `/home/nymph/ZImage-Trainer`
- trainer repo checkout under `ZImage-Trainer/ai-toolkit`
- trainer venv at `ZImage-Trainer/.venv-ztrain`
- trainer UI build, trainer DB, datasets, or LoRA outputs

Bottom line:

- the product direction is now clear: keep training as a separate sidecar
- the immediate blocker is not architecture, it is bringing the trainer install back to a known-good live state and validating one real end-to-end training run

## Current Machine State

Verified on 2026-05-01:

```text
Trainer status:
  ZIMAGE_TRAINER_INSTALLED=missing
  ZIMAGE_TRAINER_REPO=no
  ZIMAGE_TRAINER_VENV=no
  ZIMAGE_TRAINER_DATASET_ROOT=no
  ZIMAGE_TRAINER_OUTPUT_ROOT=no
  ZIMAGE_TRAINER_RUNNING=no
  ZIMAGE_TRAINER_LORAS_FOUND=0
  ZIMAGE_TRAINER_DATASETS_FOUND=0
  ZIMAGE_TRAINER_UI_DIR=no
  ZIMAGE_TRAINER_UI_NODE=no
  ZIMAGE_TRAINER_UI_BUILD=no
  ZIMAGE_TRAINER_UI_DB=no
  ZIMAGE_TRAINER_UI_RUNNING=no
  ZIMAGE_TRAINER_GRADIO_RUNNING=no
```

Verified paths:

- inference runtime repo exists at `/home/nymph/Z-Image`
- expected trainer root `/home/nymph/ZImage-Trainer` is missing

Repo state worth noting:

- `Z-Image` has local uncommitted changes in `api_server.py`, `model_manager.py`, and `schemas.py`
- `NymphsCore` has ongoing local work for the trainer install, Manager UI, status scripts, and docs

## What The Codebase Already Tells Us

### 1. Product direction has already changed from DiffSynth to AI Toolkit

Manager scripts and docs now point to:

- `ostris/ai-toolkit`
- a dedicated trainer sidecar under `/home/nymph/ZImage-Trainer`
- AI Toolkit YAML job generation
- the Z-Image Turbo training adapter

### 2. The intended install layout is consistent

Expected layout from scripts and docs:

```text
/home/nymph/ZImage-Trainer/
  ai-toolkit/
  .venv-ztrain/
  datasets/
  loras/
  config/
  jobs/
  logs/
  bin/
  .node20/
  aitk_db.db
  models/Tongyi-MAI/Z-Image-Turbo/
  adapters/zimage_turbo_training_adapter/
```

### 3. Manager integration is fairly far along

Relevant files already exist in `NymphsCore`:

- `Manager/scripts/install_zimage_trainer_aitk.sh`
- `Manager/scripts/zimage_trainer_status.sh`
- `Manager/scripts/ztrain_run_config.sh`
- `Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs`
- `Manager/apps/NymphsCoreManager/Views/MainWindow.xaml`

This means the system is past the “should we do this?” stage and into the “make install and training actually reliable” stage.

### 4. Inference and training are still separate concerns

Current inference runtime:

- repo: `/home/nymph/Z-Image`
- experimental fast path: Nunchaku
- LoRA support work is still active

Important implication:

- even after the trainer sidecar is restored, we still need to prove that AI Toolkit-trained LoRAs behave correctly on the intended inference runtime

## Main Risks

### Live-state drift

Previous handoff notes describe a working trainer install and Gradio listener, but that live install is not present now. The biggest immediate risk is treating older notes as current truth.

### End-to-end validation gap

The code strongly suggests the install flow is implemented, but there is no verified first training run captured in the current live state.

### LoRA runtime uncertainty

Nunchaku-side LoRA loading for Z-Image has been under active experimentation. Trainer success does not automatically mean inference success.

### UI duplication risk

There is pressure to make the trainer feel native to Nymphs, but rebuilding too much of AI Toolkit in Manager would create a maintenance trap.

## Recommended Handoff Position

If someone else picks this up next, they should assume:

1. The architecture choice is settled enough for now.
2. The trainer should remain a separate sidecar, not merged into `Z-Image`.
3. The highest-value next work is reinstall plus validation, not redesign.
4. Nunchaku LoRA correctness should stay a separate track from trainer install health.

## Roadmap

### Phase 0: Restore A Known-Good Trainer Install

Goal:

- make `/home/nymph/ZImage-Trainer` real again and get status from `missing` to `installed` or at least `partial`

Work:

- run the AI Toolkit trainer installer path from Manager scripts
- confirm repo clone, venv creation, Node runtime, UI build, DB bootstrap, model prefetch, and adapter prefetch
- verify helper launchers are generated under `ZImage-Trainer/bin`
- rerun `zimage_trainer_status.sh` and record the result

Exit condition:

- trainer root exists and status script no longer reports `missing`

### Phase 1: Validate Backend Health Without Changing UX

Goal:

- prove the sidecar can launch cleanly before touching more Manager-facing polish

Work:

- launch Gradio UI and verify listener on port `7861`
- launch official UI and verify listener on port `8675`
- inspect logs under `ZImage-Trainer/logs`
- confirm AI Toolkit config generation still matches current Manager expectations

Exit condition:

- one UI path launches reliably and logs are readable enough to support real usage

### Phase 2: Run One Real Training Job

Goal:

- verify that the trainer is not just installable, but usable

Work:

- create one tiny known-good dataset
- confirm `metadata.csv` remains the first-class manual caption source
- generate `.txt` caption mirrors if the current pipeline requires them
- write one Manager-owned AI Toolkit YAML job into `jobs/`
- run a short training job and capture output artifacts

Exit condition:

- at least one LoRA checkpoint is produced under `ZImage-Trainer/loras`

### Phase 3: Prove Inference Compatibility

Goal:

- determine whether AI Toolkit output is actually useful in the intended runtime

Work:

- load the trained LoRA through the current `Z-Image` runtime path
- test with fixed prompts and seeds
- compare base model vs LoRA-enabled output
- compare Nunchaku behavior vs any slower fallback path if needed

Exit condition:

- one trained LoRA produces a meaningful, non-broken change in output on the target runtime

### Phase 4: Manager UX Tightening

Goal:

- make the working backend feel stable and understandable

Work:

- normalize training log rendering
- tighten install-state messaging in Manager
- keep caption workflow clear:
  - pictures
  - `metadata.csv`
  - optional Brain captioning
- keep job presets in step-based language instead of exposing backend churn

Exit condition:

- a first-time user can install, caption, train, and test a LoRA without needing shell-level troubleshooting

## Recommended Immediate Next Steps

In order:

1. Restore `/home/nymph/ZImage-Trainer` using the AI Toolkit installer path.
2. Save a fresh status snapshot after install.
3. Launch one UI path and verify logs.
4. Run one tiny training job.
5. Test the resulting LoRA in `Z-Image`.
6. Only then spend more time on frontend polish or deeper product wording.

## Open Questions

These are still unresolved and should be answered with testing, not assumptions:

- Does the current installer still complete successfully on this machine?
- Is the Gradio compatibility patch still enough for the current AI Toolkit revision?
- Which UI should be treated as the primary user-facing path: official UI, Gradio UI, or a Nymphs wrapper?
- Do AI Toolkit-trained Z-Image LoRAs behave correctly on the Nunchaku runtime?
- If Nunchaku remains unreliable for LoRAs, what fallback runtime should Manager expose for testing?

## Suggested Ownership Split

Track A: Trainer sidecar health

- installer
- repo/venv/UI build
- job generation
- logs
- first training run

Track B: Inference compatibility

- LoRA loading
- runtime correctness
- Nunchaku behavior
- smoke tests

Track C: Product polish

- Manager wording
- install-state visibility
- caption workflow clarity
- training preset tuning

## Key Files For The Next Person

Primary docs:

- `/home/nymph/NymphsCore/docs/zimage_trainer_manager_handoff.md`
- `/home/nymph/NymphsCore/docs/zimage_ai_toolkit_backend_handoff.md`
- `/home/nymph/NymphsCore/docs/ROADMAP.md`

Primary scripts:

- `/home/nymph/NymphsCore/Manager/scripts/install_zimage_trainer_aitk.sh`
- `/home/nymph/NymphsCore/Manager/scripts/zimage_trainer_status.sh`
- `/home/nymph/NymphsCore/Manager/scripts/ztrain_run_config.sh`

Primary runtime repo:

- `/home/nymph/Z-Image`

## Final Recommendation

Do not restart the design process.

Treat this as an execution and validation problem:

- reinstall the trainer sidecar
- verify one training run
- verify one inference run with the resulting LoRA
- then polish the Manager experience around the path that actually works
