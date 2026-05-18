# Z-Image Trainer Testing Handoff

Date: 2026-05-01

This handoff is for the current testing phase of the Z-Image Trainer after the move onto the AI Toolkit sidecar flow.

The goal is to help the next session focus on actual LoRA validation instead of re-digging through launcher and installer regressions.

## Current Position

The trainer flow is now centered on:

- Manager-managed AI Toolkit sidecar
- trainer root at `/home/nymph/ZImage-Trainer`
- AI Toolkit YAML job generation
- Brain-assisted caption drafting into `metadata.csv`
- LoRA outputs under `/home/nymph/ZImage-Trainer/loras`

The user should now be able to spend time on:

- dataset prep
- caption review
- training runs
- LoRA testing in Z-Image
- LoRA testing in Blender

rather than basic launcher recovery.

## What Is Working

### Trainer install and repair

The Manager install/repair path now uses:

- `Manager/scripts/install_zimage_trainer_aitk.sh`

This is the intended trainer backend path now.

### Trainer UIs

Both trainer UIs were recovered:

- `Nymphs UI`
- `Official UI`

Important final behavior:

- the official UI launcher is back on the fast working `npm run start` path
- Manager waits briefly for `localhost:8675` before opening the browser

That was the final practical fix for the official UI launch regression.

### Dataset selection

The name field is now an editable existing-dataset picker.

That means:

- users can still type a new dataset/LoRA name
- users can also choose an existing dataset folder from the dropdown

This was added because the previous flow relied too much on users knowing they could manually retype hidden folder names.

Known live datasets seen during testing:

- `my_first_lora`
- `ukiyo`

### Caption with Brain

The captioning path has moved forward significantly.

Current helper behavior:

- syncs helper files into the trainer sidecar
- launches through the trainer venv Python
- normalizes images before upload
- re-encodes to smaller JPEGs for captioning
- retries a second OpenAI-compatible image request shape on failure
- drafts one caption per image into `metadata.csv`

Important dependency fix:

- `Pillow` is now part of the trainer install/repair path
- the Manager sync path also self-heals it if needed

This fixed the earlier trap where the helper was importing `PIL` but the shell wrapper still launched plain `python3`.

### Live log

The trainer log is now:

- better at showing carriage-return progress updates
- slightly taller than before

This helps long runs feel less frozen.

## What Changed In Source

High-value files changed during this phase:

- `Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs`
- `Manager/apps/NymphsCoreManager/Services/ProcessRunner.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs`
- `Manager/apps/NymphsCoreManager/Views/MainWindow.xaml`
- `Manager/scripts/install_zimage_trainer_aitk.sh`
- `Manager/scripts/zimage_caption_brain.py`
- `Manager/scripts/zimage_caption_brain.sh`

Also mirrored in the packaged script copies under:

- `Manager/apps/NymphsCoreManager/publish/win-x64/scripts/`

## Current Presets

The active trainer presets are now:

- `Baseline`
- `Style`
- `Style High Noise`

Current adapter options:

- `v1 (Recommended)`
- `v2 (Experimental)`

Recommended first test path remains:

- preset: `Style` for painterly datasets
- adapter: `v1`

## Recommended Test Dataset

The best current user-led test direction is:

- around `10-16` images
- one coherent style
- content-focused captions

The `ukiyo` set is the current active style test candidate.

Brain drafting can help, but captions still need review.

## Known Remaining Quality Issue

`Caption with Brain` is now working mechanically, but style wording can still drift.

Observed example:

- it still mentioned `Japanese woodblock print` once

What was done:

- the style-caption prompt was tightened to explicitly discourage:
  - `woodblock`
  - `woodblock print`
  - `wood blockprint`
  - `wood block print`
  - `Japanese woodblock print`

Expectation:

- fewer of those slips
- but manual review is still expected

## End-To-End Alignment Status

### Manager to trainer

This is largely aligned now.

Manager responsibilities currently covered:

- install/repair
- status
- dataset prep
- caption drafting
- job creation
- training start/stop
- UI launch

### Trainer to output

This is aligned in directory shape:

- datasets under `/home/nymph/ZImage-Trainer/datasets`
- jobs under `/home/nymph/ZImage-Trainer/jobs`
- LoRAs under `/home/nymph/ZImage-Trainer/loras`

### Output to runtime

This is wired in source, but still needs practical testing with a real AI Toolkit-produced LoRA.

The Z-Image runtime already supports:

- `lora_path`
- `lora_scale`

and the Nunchaku fork already has the required Z-Image transformer LoRA methods.

### Output to Blender

The Blender addon is already aligned to the trainer output root and can discover `.safetensors` files under the trainer LoRA folder structure.

## Most Important Remaining Test

The biggest unresolved question is no longer install or launch.

It is this:

- does a LoRA produced by the AI Toolkit trainer load cleanly through the Nunchaku Z-Image runtime and actually influence output the way we expect in Blender

That is the next real milestone.

## Suggested Test Order

1. Use the dataset picker and select the intended set.
2. Open the pictures folder and confirm the image set is correct.
3. Run `Caption with Brain` if needed.
4. Open `metadata.csv` and remove any bad style wording.
5. Run a first training pass with:
   - preset: `Style`
   - adapter: `v1`
6. Wait for a real `.safetensors` LoRA under `/home/nymph/ZImage-Trainer/loras/...`
7. Test that LoRA in the Z-Image runtime.
8. Test that LoRA in Blender with the addon.
9. Compare without-LoRA and with-LoRA outputs using the same seed where possible.

## What To Watch For In The Next Session

Watch for:

- LoRA output file actually appearing
- training logs showing healthy progress instead of hanging or NaN loss
- runtime accepting the produced LoRA path
- visible output difference when LoRA is enabled
- style transfer showing up without collapsing prompt control

## If Something Fails Again

If `Caption with Brain` fails again:

- get the exact new log line first
- do not assume it is the same old `PIL` bug
- the Python wrapper path and `Pillow` install were already fixed

If the dataset dropdown looks wrong:

- rebuild the Manager first
- the list now comes from direct WSL-path filesystem enumeration, not a WSL shell listing

If `Official UI` regresses:

- remember the known-good live path was `npm run start`
- do not jump back to `build_and_start` on every click

## Related Files

Current trainer feature map:

- `/home/nymph/NymphsCore/docs/z-image-trainer-features.md`

Public beginner guide:

- `/home/nymph/NymphsCore/home/guides/training.html`

Changelog entry for this phase:

- `/home/nymph/NymphsCore/CHANGELOG.md`
