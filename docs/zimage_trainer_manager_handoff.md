# Z-Image Trainer Manager Handoff

Date: 2026-04-28

## Current State

The Manager now has a `Z-Image Trainer` page and optional install support for a separate local trainer sidecar.

The sidecar is deliberately separate from the existing Z-Image/Nunchaku inference runtime.

```text
Runtime:
  /home/nymph/Z-Image/.venv-nunchaku

Trainer:
  /home/nymph/ZImage-Trainer/.venv-ztrain
```

The trainer installs DiffSynth-Studio and uses Z-Image Turbo Differential LoRA training with the Turbo training adapter.

## User-Facing Design Direction

Keep the page simple. The user should not need to understand:

```text
dataset
job
script
trigger word
```

The beginner flow should stay:

```text
LoRA name
Open Pictures Folder
Open Captions File
Training focus: Character/Object or Stylized Look
Training amount: Quick Test / Normal / Strong
Start Training
Open Finished LoRAs
Live log
```

`LoRA name` is used internally as:

```text
folder name
output name
prompt keyword
```

## Installed Paths

Trainer root:

```text
/home/nymph/ZImage-Trainer/
```

DiffSynth repo:

```text
/home/nymph/ZImage-Trainer/DiffSynth-Studio
```

Trainer venv:

```text
/home/nymph/ZImage-Trainer/.venv-ztrain
```

Training pictures:

```text
/home/nymph/ZImage-Trainer/datasets/<lora_name>/
```

Finished LoRAs:

```text
/home/nymph/ZImage-Trainer/loras/
```

Generated jobs:

```text
/home/nymph/ZImage-Trainer/jobs/<lora_name>.sh
```

## Current UI

The current page shows:

```text
Z-Image Trainer

Make a Custom LoRA
  LoRA name
  Pictures -> Open Pictures Folder / Open Captions File
  Caption help -> Use Caption Brain drafts / Draft Captions
  Training focus -> Character/Object / Stylized Look
  Training amount -> Quick Test / Normal / Strong
  Start Training

Finished LoRAs
  Open Finished LoRAs

Status
  Repair Trainer
  Refresh

Live log
  Open Logs
```

This is the intended direction. Avoid drifting back toward a developer-facing dashboard.

## Current Implementation Notes

`Open Pictures Folder` creates and opens:

```text
/home/nymph/ZImage-Trainer/datasets/<lora_name>/
```

`Start Training` currently:

1. Creates the needed training files behind the scenes.
2. Refreshes `metadata.csv` so image filenames stay in sync with the folder.
3. Refuses to start if captions are still blank.
4. Generates a DiffSynth training job.
5. Runs the generated job.

The training method is:

```text
Z-Image Turbo Differential LoRA
```

with:

```text
ostris/zimage_turbo_training_adapter
```

The backend still uses DiffSynth's Z-Image training script:

```text
examples/z_image/model_training/train.py
```

Manager now routes training through a managed wrapper entrypoint so it can pass timestep-boundary controls for the Z-Image path.

Current internal bias rules:

```text
Character/Object:
  balanced full-range timestep sampling

Stylized Look:
  early/high-noise timestep emphasis
  intended for broad look/composition shifts like kids-drawing style
```

## Files Touched

```text
Manager/apps/NymphsCoreManager/Models/InstallSettings.cs
Manager/apps/NymphsCoreManager/Models/ZImageTrainerStatus.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml.cs
Manager/scripts/install_zimage_trainer.sh
Manager/scripts/zimage_caption_brain.sh
Manager/scripts/zimage_caption_brain.py
Manager/scripts/zimage_trainer_status.sh
Manager/scripts/ztrain_list_loras.sh
Manager/scripts/ztrain_run_config.sh
```

## Validation So Far

Script syntax checks passed for the new shell scripts.

XAML parses as XML.

Manager build can be run from WSL by calling Windows `dotnet.exe` directly against the WSL UNC path. This works even if Linux `.NET` is not installed inside WSL.

Working command shape:

```text
dotnet.exe build '\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\NymphsCoreManager.csproj' -c Debug
```

Notes:

```text
- The compiler is the Windows .NET SDK, not a Linux SDK inside WSL.
- Use the explicit \\wsl.localhost\... project path if wslpath-style conversion gives a bad path.
- This was confirmed to build successfully from the dev WSL.
```

The trainer sidecar install was tested through the Manager Add Modules route and reported:

```text
ZIMAGE_TRAINER_INSTALLED=installed
ZIMAGE_TRAINER_REPO=yes
ZIMAGE_TRAINER_VENV=yes
ZIMAGE_TRAINER_DATASET_ROOT=yes
ZIMAGE_TRAINER_OUTPUT_ROOT=yes
ZIMAGE_TRAINER_RUNNING=no
ZIMAGE_TRAINER_LORAS_FOUND=0
ZIMAGE_TRAINER_DATASETS_FOUND=0
```

Caption Brain draft pass was also tested successfully with a tiny 3-image woodblock-print dataset.

Confirmed working path:

```text
1. Put 3 images in /home/nymph/ZImage-Trainer/datasets/my_first_lora
2. Enable "Use Nymphs Brain to caption"
3. Click "Draft Captions"
4. Brain temporarily starts the local Qwen2.5-VL-7B GGUF through llama-server
5. metadata.csv is written with one caption row per image
6. Manager refreshes metadata.csv and reports 0 blank captions left
```

Example successful log shape:

```text
Caption Brain will use model: lmstudio-community/Qwen2.5-VL-7B-Instruct-GGUF
llama-server is ready on port 8000
Drafted caption for <image>.jpg: ...
Caption Brain wrote metadata.csv with 3 drafted caption(s), 0 skipped row(s), and 0 blank row(s) left.
```

This proves:

```text
- trainer page wiring works
- temporary Brain model switch/start works
- multimodal GGUF + mmproj path works
- local llama-server API path works
- metadata.csv writing works
```

## Next Test Steps

1. Build the Manager on Windows.
2. Open the `Z-Image Trainer` page.
3. Enter a simple LoRA name, for example:

   ```text
   test_lora
   ```

4. Click `Open Pictures Folder`.
5. Add a very small image set.
6. If using Caption Brain, click `Draft Captions`.
7. Open `metadata.csv` and lightly review the drafted text.
8. For stylized test art, switch to:

   ```text
   Training focus: Stylized Look
   Training amount: Quick Test
   ```

9. Click `Start Training`.
10. Watch the live log.

Likely failures to look for:

```text
DiffSynth dependency install gaps
missing model download/auth issues
metadata/caption format issues
output file naming/path issues
CUDA/VRAM errors
```

Recommended first real training test:

```text
Use a tiny consistent set, for example 3 to 5 images.

Goal:
  prove end-to-end training works
  produce a first .safetensors LoRA
  judge whether the look transfers at all

Do not optimize heavily yet.
First prove:
  captioning works
  training completes
  LoRA output file appears
```

## Likely Next UX Fixes

Show image count for the current LoRA folder, not just dataset folder count.

Add clearer empty-state text near Pictures:

```text
Put your training pictures in this folder, then click Start Training.
```

Manager should help with `metadata.csv` by filling in image filenames, but it should not invent caption text for the user.

Keep captions user-controlled. They are part of the training quality, especially for heavily stylized work.

Character/object training and heavily stylized training should both be first-class options in the UI, not hidden behind vague presets.

Add `Stop Training`.

Later: attach/load trained LoRAs into the Nunchaku Z-Image runtime.

Addon follow-up handoff now exists here:

```text
docs/zimage_addon_lora_handoff.md
```

That document covers the exact current gap in:

```text
/home/nymph/NymphsAddon/Nymphs.py
/home/nymph/Z-Image/
```

Key current truth:

```text
The trainer now produces real epoch-*.safetensors files,
but the addon still has no LoRA controls and the Z-Image backend still has no lora_path / lora_scale request contract.
```

If Brain caption-help is added later, account for GPU memory pressure:

```text
A local image-aware caption helper may hold several GB of VRAM.
Example: a Qwen2.5-VL-7B Q4_K_M helper can be around 6 GB.

Expected flow may need to be:
  caption first
  then stop/unload Brain helper
  then begin LoRA training
```

Do not assume Brain captioning and trainer execution should stay live together on one GPU.

Preferred product shape if this is added:

```text
Do not add a heavyweight Manager install setting for a dedicated caption model.

Instead:
  Brain page can download compatible models in general
  training page can expose a simple Caption Brain toggle or action
  training page can detect a compatible downloaded model
  training page can start it on demand
  training page can stop/unload it before LoRA training starts
```

Concrete intended behavior:

```text
Caption Brain should draft metadata.csv, not silently finalize it.

Recommended first modes:
  Fill Blank Only
  Overwrite All Drafts
  Rewrite Selected Caption

Default:
  Fill Blank Only
```

Current caption quality note from the first successful run:

```text
The first-pass captions were valid and usable, but one caption drifted a bit too generic/style-labeled:
  "traditional Japanese art style"

Next quality improvement should be prompt tuning so style-mode captions:
  describe scene/content clearly
  avoid vague generic style labels
  avoid repetitive "art style" phrasing
  stay natural and concise
```

Suggested future caption prompt behavior:

```text
Character / Object:
  emphasize subject identity and visible traits

Stylized Look:
  emphasize scene/content/composition
  let the images teach the rendering style
  avoid keyword soup and broad generic style tags
```

The training page should treat Caption Brain as a temporary helper session:

```text
1. detect a compatible downloaded local vision model
2. verify mmproj/projector support is present when required
3. temporarily switch Brain to that model
4. start Brain through lms-start / llama-server
5. draft one caption per image into metadata.csv
6. let the user review/edit captions
7. stop Brain before LoRA training begins
```

The user should not have to manually keep the vision model selected as their normal Brain chat model if we can avoid it.

Caption Brain success criteria:

```text
one valid CSV row per image
short natural-language draft captions
user can still edit everything
training never starts while Brain caption help is still consuming VRAM
```

Model-manager requirement:

```text
Downloaded vision models should be treated as vision-ready only when the GGUF and matching mmproj are both present.

If a likely VLM model is downloaded and mmproj is missing:
  auto-fetch it if possible
  otherwise show a clear warning
```

## Product Rule

Keep this feature beginner-first:

```text
Name it.
Add pictures.
Write captions.
Choose what you are training.
Choose training amount.
Start training.
Use finished LoRA.
```

Do not expose job scripts or trigger words in the default UI.

It is okay to expose captions in a beginner-friendly way because caption quality directly affects the result and the user should stay in control of them.
