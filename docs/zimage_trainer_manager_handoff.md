# Z-Image Trainer Manager Handoff

Date: 2026-04-30

## WSL Context

This handoff is written for the managed `NymphsCore` testing WSL runtime used by the Windows Manager.

It is not a statement about whatever separate developer WSL checkout happens to contain the source tree.

Important:

```text
- Paths such as /home/nymph/ZImage-Trainer and /home/nymph/Z-Image refer to the live managed trainer/runtime inside the NymphsCore testing distro
- a missing trainer sidecar in a separate dev WSL does not mean the testing WSL is broken
- when validating this handoff, run the checks inside the same WSL distro the Manager is targeting
```

## 2026-04-30 Backend Handoff For Tomorrow

This section is the real restart point. It is backend-only on purpose.

### Current backend systems

```text
Manager app:
  /home/nymph/NymphsCore/Manager/apps/NymphsCoreManager

Managed trainer root:
  /home/nymph/ZImage-Trainer

Trainer virtualenv:
  /home/nymph/ZImage-Trainer/.venv-ztrain

AI Toolkit repo:
  /home/nymph/ZImage-Trainer/ai-toolkit

Trainer logs:
  /home/nymph/ZImage-Trainer/logs

Datasets:
  /home/nymph/ZImage-Trainer/datasets/<lora_name>/

Jobs/configs:
  /home/nymph/ZImage-Trainer/jobs/

LoRA outputs:
  /home/nymph/ZImage-Trainer/loras/

Inference runtime:
  /home/nymph/Z-Image

Nunchaku venv:
  /home/nymph/Z-Image/.venv-nunchaku
```

### Verified state tonight

```text
- AI Toolkit trainer stack is installed under /home/nymph/ZImage-Trainer
- Gradio package is installed in .venv-ztrain
- official AI Toolkit UI stack is installed
- trainer configs are generated as AI Toolkit YAML jobs
- manual metadata.csv caption editing is still valid and should remain first-class
- Nymphs Brain captioning is optional only
```

### Gradio status

Live Gradio is the important backend fix that must not be lost.

Verified:

```text
localhost:7861 is listening from the managed trainer install
```

The live launcher that now works is:

```text
/home/nymph/ZImage-Trainer/bin/ztrain-start-gradio-ui
```

Important:

```text
This launcher had been stale/broken before.
It was repaired in the live distro and also synced into:

  /home/nymph/NymphsCore/Manager/scripts/install_zimage_trainer_aitk.sh
  /home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/win-x64/scripts/install_zimage_trainer_aitk.sh
```

What the fixed launcher now does:

```text
- starts AI Toolkit Gradio on port 7861
- uses a socket/listener check instead of the earlier fake-running check
- launches flux_train_ui via demo.launch(...)
```

### Why Gradio failed before

The main failure was not “Gradio missing”.

It was:

```text
- AI Toolkit's flux_train_ui.py still had old Gradio arguments
  show_share_button
  show_download_button

- Gradio 6 rejects those arguments
```

That was patched in the live trainer repo so direct launch works again.

### Direct known-good Gradio launch

If tomorrow needs a backend sanity check, this is the clean direct launch path:

```bash
cd /home/nymph/ZImage-Trainer/ai-toolkit
source /home/nymph/ZImage-Trainer/.venv-ztrain/bin/activate
python -c 'import flux_train_ui as f; f.demo.launch(server_name="127.0.0.1", server_port=7861, share=False, show_error=True, inbrowser=False)'
```

If that runs, the backend is healthy enough and any remaining breakage is outside the core Gradio stack.

### Trainer logic state

Manager-side trainer logic is now aimed at AI Toolkit, not DiffSynth.

Important current logic:

```text
- metadata.csv remains the editable manual caption file
- per-image .txt caption mirrors are generated before training
- training jobs are AI Toolkit YAML configs in /jobs
- presets now feed AI Toolkit config generation
- manual captions must remain a valid path with zero Brain dependency
```

Critical correction made tonight:

```text
Caption Brain is optional only.
It must never be required for the training flow.
```

### Nunchaku / inference status

This is still separate from trainer install health.

Known state:

```text
- Manager/runtime plumbing for the Nunchaku fork exists
- native Z-Image LoRA loading experiments on Nunchaku are not yet trustworthy
- earlier outputs still collapsed into static / block-noise
- do not assume LoRA-on-Nunchaku quality is solved
```

So tomorrow should treat these as separate tracks:

```text
1. trainer/backend health
2. inference/runtime health
3. Nunchaku LoRA correctness
```

### Exact files touched that matter

Backend-important files:

```text
/home/nymph/NymphsCore/Manager/scripts/install_zimage_trainer_aitk.sh
/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/win-x64/scripts/install_zimage_trainer_aitk.sh
/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
/home/nymph/ZImage-Trainer/bin/ztrain-start-gradio-ui
/home/nymph/ZImage-Trainer/ai-toolkit/flux_train_ui.py
```

### Known-good backend checks for tomorrow

Run these before touching more code:

1. Gradio install check

```bash
source /home/nymph/ZImage-Trainer/.venv-ztrain/bin/activate
python -c 'import gradio; print(gradio.__version__)'
```

Expected tonight:

```text
6.13.0
```

2. Gradio listener check

```bash
ss -ltn | grep :7861 || true
```

3. Recent trainer log check

```bash
tail -n 80 /home/nymph/ZImage-Trainer/logs/aitk-gradio.log
```

4. Live launcher check

```bash
/home/nymph/ZImage-Trainer/bin/ztrain-start-gradio-ui
```

### Backend-only next steps for tomorrow

Do these in order:

1. confirm `ztrain-start-gradio-ui` in the live distro still matches the fixed script
2. confirm `flux_train_ui.py` in the live trainer repo still lacks the bad Gradio 6 args
3. keep manual caption workflow valid:
   - images
   - metadata.csv
   - optional Brain captioning
4. confirm AI Toolkit YAML job generation still works from Manager-side config creation
5. only after trainer stack is confirmed healthy, resume the separate Nunchaku LoRA reliability work

### Do not lose these conclusions

```text
- Gradio was a launcher/script issue, not a missing package issue
- manual captions must remain first-class
- Brain is optional only
- AI Toolkit is now the intended trainer backend
- Nunchaku LoRA correctness is still unresolved and should not be conflated with trainer install health
```

## 2026-04-30 Verdict

Current verdict after the first full train -> Blender -> Nunchaku test:

```text
- Manager trainer plumbing works
- Caption Brain plumbing works
- Blender addon LoRA selection flow works
- Z-Image backend now receives lora_path / lora_scale correctly
- Nunchaku native Z-Image LoRA loading is still not producing trustworthy images
```

Observed result:

```text
The generated image completes but collapses into static / block-noise even at tiny LoRA strength values.
```

Current conclusion:

```text
The weakest link is no longer the addon UI or request wiring.
The current mixed stack is:

  training: DiffSynth sidecar
  adapter: ostris/zimage_turbo_training_adapter
  inference: Z-Image on Nunchaku
  LoRA application: custom Nunchaku Z-Image mapper

This is not the official AI Toolkit path the adapter was made for.
```

Recommended direction:

```text
Do not treat the current DiffSynth -> custom Nunchaku Z-Image LoRA route as production-ready.
The better next architecture step is to move the Manager training backend toward AI Toolkit,
which is the official open-source stack the adapter was built for.
```

## 2026-04-30 AI Toolkit Pivot Status

Manager-side pivot now started:

```text
- Trainer install path now targets AI Toolkit
- Trainer status now checks an AI Toolkit sidecar under /home/nymph/ZImage-Trainer
- Trainer configs are now intended to be AI Toolkit YAML files under jobs/
- metadata.csv remains the editable user file
- per-image .txt captions are mirrored automatically before training
```

Not yet validated:

```text
- first real AI Toolkit training run from Manager
- resulting LoRA quality in Blender
```

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

This still matters historically, but as of 2026-04-30 it should be treated as:

```text
working prototype path
not final recommended long-term backend
```

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

Important official alignment note:

```text
The adapter's official model card says it was made for use with AI Toolkit.
That does not mean DiffSynth use is impossible.
It does mean AI Toolkit is the cleaner reference backend when trying to match the intended method.
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

This is now known drift from the official AI Toolkit / Ostris framing:

```text
- Manager presets are repeat + epochs driven
- AI Toolkit is step-driven
- AI Toolkit exposes content/style/balanced directly
- the current Manager preset system is therefore only approximate
```

## Official Source Notes

Primary public sources:

```text
AI Toolkit:
  https://github.com/ostris/ai-toolkit

Training adapter:
  https://huggingface.co/ostris/zimage_turbo_training_adapter

DiffSynth Z-Image docs:
  https://github.com/modelscope/DiffSynth-Studio/blob/main/docs/en/Model_Details/Z-Image.md

Nunchaku:
  https://github.com/nunchaku-tech/nunchaku
```

Key takeaways from those sources:

```text
- the adapter is explicitly described as an AI Toolkit training adapter
- AI Toolkit is public and MIT licensed
- AI Toolkit is step-based and has assistant_lora_path
- AI Toolkit exposes content_or_style = balanced/style/content
- DiffSynth docs warn against Z-Image Turbo quantization for image quality
- Nunchaku has a mature official LoRA path for FLUX, but Z-Image LoRA support is not a known official path
```

## AI Toolkit Transition Direction

The current recommendation is:

```text
keep the existing Trainer page UX
change the backend over time from DiffSynth sidecar to AI Toolkit
keep the Blender addon LoRA-use flow
keep Z-Image inference on Nunchaku as a separate concern
```

What should stay:

```text
- LoRA name
- Open Pictures Folder
- Open Captions File
- Brain caption draft flow
- Training focus
- Training amount
- Start Training
- Open Finished LoRAs
```

What should change behind the scenes:

```text
- replace DiffSynth job generation with AI Toolkit config generation
- replace repeat/epoch framing with step-based presets
- expose assistant_lora_path via AI Toolkit config
- align style/content/balanced to AI Toolkit's native semantics
```

Practical note:

```text
The local AI Toolkit clone inspected during this handoff had the needed primitives
(assistant_lora_path, steps, content_or_style, save/sample cadence),
but did not include a ready-made Z-Image example config in config/examples.
Expect to author a NymphsCore-owned Z-Image AI Toolkit config template.
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

Current next frontier: make trained LoRAs work on the fast Nunchaku Z-Image runtime.

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
The trainer now produces real epoch-*.safetensors files.
The addon now has LoRA run/checkpoint selection and strength controls.
The Z-Image backend now accepts lora_path / lora_scale.
The remaining blocker is native LoRA support inside the Nunchaku Z-Image transformer.
```

Nunchaku fork:

```text
https://github.com/nymphnerds/nunchaku
```

Manager is now pinned to the LoRA-capable fork commit:

```text
2e7d391f8fb4c0a2aa04d9788dcc465cc281465a
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

## Transcript Alignment Note

Current `Stylized Look` is directionally inspired by the Ostris Z-Image Turbo training walkthrough, but it is **not** a 1:1 match yet.

Current truth:

```text
What matches:
  - Turbo training adapter path
  - conservative 1e-4 / 8e-5 style learning-rate direction
  - content-first captioning philosophy for style work
  - high-noise emphasis as an important tool for broad style/composition changes

What does not match 1:1:
  - the Manager starts Stylized Look with a high-noise-biased timestep range immediately
  - Ostris demonstrated starting more balanced, then switching to high-noise later
  - the Manager presets are expressed as repeat/epochs presets, not a direct "3000 steps" style control
  - the Manager does not currently expose Differential Guidance as an advanced option
```

If the goal is transcript-faithful behavior, the next refinement should move closer to this:

```text
Stylized Look preferred future flow:
  1. start style runs in a balanced timestep mode
  2. allow switching to high-noise emphasis later when the goal is stronger composition/style takeover
  3. optionally expose Differential Guidance in an advanced section
  4. consider a clearer user-facing "target steps" framing or at least a visible step estimate
```

Important product note:

```text
Earlier discussion wanted the style path to match the transcript more closely at the start.
That has not been completed yet.
Do not describe the current preset as a 1:1 Ostris clone.
Describe it as an approximation or inspired-by preset until the above alignment work is done.
```

Possible future inference feature:

```text
Z-Image-Turbo-Fun-Controlnet-Union-2.1 may be worth exploring later for the generation side.

This is not part of the current LoRA training path and should not distract from:
  - simplifying the trainer flow
  - matching Ostris more closely
  - adding clean addon LoRA-use support

Treat it as a separate future generation/control feature for things like:
  - pose guidance
  - canny/depth/hed/scribble control
  - inpaint/control workflows
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
