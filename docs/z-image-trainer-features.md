# Z-Image Trainer Features

Last updated: 2026-05-01

This document describes the current Z-Image Trainer feature set in NymphsCore Manager as it exists in local source.

## WSL Context

Unless noted otherwise, the trainer paths and runtime expectations in this document refer to the managed `NymphsCore` testing WSL runtime that the Windows Manager targets.

They do not guarantee that the same sidecar is present in a separate developer WSL environment that only contains the source checkout.

The trainer now uses an isolated AI Toolkit sidecar under:

- `/home/nymph/ZImage-Trainer`

It is designed to cover the whole flow from dataset prep to LoRA training and then hand the resulting LoRA over to the Z-Image runtime and Blender addon.

## What The Trainer Is

The Z-Image Trainer is a managed AI Toolkit sidecar for Z-Image Turbo LoRA training.

Its purpose is to give the Manager a local, repeatable workflow for:

- installing the trainer stack
- preparing datasets
- drafting captions
- generating AI Toolkit YAML jobs
- running training jobs
- launching both trainer UIs
- writing LoRAs into a known output folder

## Install And Repair

The primary Manager button acts as:

- `Install Trainer` when the sidecar is missing
- `Repair Trainer` when it is already installed

Install and repair use the AI Toolkit installer script:

- `Manager/scripts/install_zimage_trainer_aitk.sh`

The trainer install currently prepares:

- AI Toolkit repo checkout
- isolated Python venv
- Torch and Python dependencies
- `Pillow` for caption image normalization
- local Node runtime for the official UI
- AI Toolkit UI database
- built official UI
- Z-Image Turbo training model bundle
- Turbo training adapter files
- generated launcher scripts

## Trainer Root Layout

The sidecar keeps its main assets together under:

- `/home/nymph/ZImage-Trainer/datasets`
- `/home/nymph/ZImage-Trainer/jobs`
- `/home/nymph/ZImage-Trainer/loras`
- `/home/nymph/ZImage-Trainer/logs`
- `/home/nymph/ZImage-Trainer/models`
- `/home/nymph/ZImage-Trainer/adapters`
- `/home/nymph/ZImage-Trainer/bin`
- `/home/nymph/ZImage-Trainer/config`

This is the current source of truth for trainer datasets and outputs.

## Manager Toolbar Features

The trainer page currently exposes these top-level controls:

- `Nymphs UI`
- `Official UI`
- `Install Trainer` or `Repair Trainer`
- `Refresh`

What they do:

- `Nymphs UI` starts the simple Gradio trainer UI
- `Official UI` starts the official AI Toolkit web UI
- `Install Trainer` or `Repair Trainer` lays down or refreshes the sidecar
- `Refresh` re-runs trainer status detection

## Dataset And File Features

After you choose a LoRA name, the Manager uses that name to anchor the local dataset and output flow.

The current file-related controls are:

- `LoRA name`
- `Open Pictures Folder`
- `Open LoRAs`
- `Open Captions File`

What they do:

- `LoRA name` determines the normalized dataset/job/output naming
- `Open Pictures Folder` opens the dataset folder for that run name
- `Open LoRAs` opens the trainer LoRA output folder
- `Open Captions File` opens `metadata.csv` for the current dataset

When the pictures folder is prepared, the Manager also ensures:

- the dataset folder exists
- `metadata.csv` exists
- image rows are synchronized into metadata
- per-image `.txt` caption mirrors can be generated from metadata for training use

## Caption Features

The trainer currently supports two caption paths:

- manual caption editing in `metadata.csv`
- `Caption with Brain`

### Caption With Brain

`Caption with Brain` uses a local Brain vision model to draft one caption per image.

Current behavior:

- prepares or refreshes `metadata.csv`
- scans supported image files in the dataset folder
- can fill only blank rows or overwrite all draft rows
- writes one training caption per image
- leaves the final review to the user

Supported image input types currently include:

- `.png`
- `.jpg`
- `.jpeg`
- `.webp`
- `.bmp`

The caption helper now also:

- normalizes images before sending them to the Brain endpoint
- re-encodes them as smaller JPEGs for captioning
- auto-installs `Pillow` into the trainer venv when needed
- retries with a second OpenAI-compatible `image_url` request shape if the first one is rejected

### Caption Fill Modes

The current caption fill options are:

- `Fill Blank Only`
- `Overwrite All Drafts`

Use cases:

- `Fill Blank Only` is safer for incremental work
- `Overwrite All Drafts` is useful when you want a fresh Brain pass

## Presets

The trainer currently exposes three presets:

- `Baseline`
- `Style`
- `Style High Noise`

### Baseline

Intended use:

- safest first proof of the full trainer pipeline
- subject or general LoRA tests

Current defaults:

- steps: `3000`
- learning rate: `1e-4`
- rank: `16`
- resolution: `1024`
- `content_or_style`: `content`
- sample steps: `8`
- guidance scale: `1`

### Style

Intended use:

- painterly or illustrative style datasets
- first style attempt before stronger style push

Current defaults:

- steps: `3000`
- learning rate: `1e-4`
- rank: `16`
- resolution: `1024`
- `content_or_style`: `balanced`
- sample steps: `8`
- guidance scale: `1`

### Style High Noise

Intended use:

- stronger style push
- style runs that need more composition shift

Current defaults:

- steps: `3000`
- learning rate: `1e-4`
- rank: `16`
- resolution: `1024`
- `content_or_style`: `style`
- sample steps: `8`
- guidance scale: `1`

## Adapter Selection

The trainer currently exposes a training adapter selector:

- `v1 (Recommended)`
- `v2 (Experimental)`

Current intention:

- `v1` is the baseline and safest first test path
- `v2` is available for comparison and experimentation

The selected adapter version is passed through into AI Toolkit job creation instead of being silently hidden in backend logic.

## Training Controls

The trainer page currently exposes these adjustable training controls:

- `Steps`
- `Learning rate`
- `LoRA rank`
- `Low VRAM mode`

Current value ranges:

- steps slider: `1000` to `5000`
- learning rate options: `5e-5`, `1e-4`, `2e-4`, `4e-4`
- rank options: `4`, `8`, `16`, `32`

## Training Actions

The current training actions are:

- `Start Training`
- `Stop Training`

### Start Training

When training starts, the Manager currently:

- refreshes metadata
- blocks the run if there are no images
- blocks the run if captions are still blank
- generates an AI Toolkit YAML job
- resolves the selected adapter path
- runs the generated trainer job

### Stop Training

`Stop Training` sends a stop request for the active AI Toolkit training process in WSL.

The intended use is:

- stop a run that is clearly unhealthy
- stop a long run after enough evidence has been collected
- recover from a stalled or mistaken test run

## Logging And Progress Features

The trainer page has a live log panel.

Current improvements in source include:

- a slightly taller live log area
- better streamed process output handling
- carriage-return progress updates are treated as meaningful log boundaries

This is especially useful for long AI Toolkit runs that otherwise look frozen when terminal-style progress updates do not end in a newline.

## Status Features

The trainer page currently reports status information such as:

- install state
- LoRA count
- dataset count
- activity summary
- whether Official UI is running
- whether Nymphs UI is running

The install/repair flow also now ends with a clearer success line rather than only raw status keys.

## UIs

The trainer currently supports two separate web interfaces.

### Nymphs UI

This is the simpler Gradio path.

Current purpose:

- quick trainer access
- lighter interaction path
- easier direct trainer testing

### Official UI

This is the official AI Toolkit UI.

Current purpose:

- closer view of the upstream AI Toolkit workflow
- official sidecar web interface

The official UI launch path was recovered to a simpler working pattern:

- use `npm run start`
- wait briefly for `localhost:8675`
- then open the browser

## Metadata And Caption Ownership

The current design intentionally keeps captions user-owned.

That means:

- captions live in `metadata.csv`
- Brain drafts are editable
- the Manager does not treat generated captions as sacred
- you are expected to review them before training

This is an important part of the current trainer philosophy.

## Output Behavior

Training outputs are written under the trainer LoRA root.

The Blender addon is already aligned to search this location recursively for:

- LoRA run folders
- `.safetensors` checkpoints

That means the addon is already aimed at the AI Toolkit output layout used by the new trainer flow.

## Runtime Handoff

The intended end-to-end flow is:

1. create dataset
2. review or draft captions
3. train a LoRA through AI Toolkit
4. output a `.safetensors` LoRA
5. use that LoRA in Z-Image via the Nunchaku runtime
6. select and test it from Blender

The runtime and addon wiring are already largely in place, but the final proof still depends on testing a real AI Toolkit-produced LoRA in the live Z-Image Nunchaku path.

## Current Strengths

The current trainer feature set is strongest at:

- managed local install
- dataset folder preparation
- metadata-first caption workflow
- Brain-assisted draft captions
- AI Toolkit YAML job creation
- sidecar UI launch
- LoRA output in a known shared location

## Current Cautions

Things that still deserve continued validation:

- exact Brain vision request compatibility on all local model variants
- final AI Toolkit LoRA compatibility in the Nunchaku inference fork
- LoRA quality and strength behavior in Blender
- whether `v2` adapter runs are consistently better or just different

## Suggested Test Order

For the most practical end-to-end validation, use this order:

1. install or repair the trainer
2. confirm both UIs launch
3. add a small clean dataset
4. prepare `metadata.csv`
5. test `Caption with Brain`
6. review captions manually
7. run `Baseline` or `Style` with adapter `v1`
8. confirm `.safetensors` output appears under `/home/nymph/ZImage-Trainer/loras`
9. test that LoRA in Z-Image and Blender

## Related Docs

Public beginner guide:

- `home/guides/training.html`

Official AI Toolkit page in the site:

- `home/aitoolkit.html`
