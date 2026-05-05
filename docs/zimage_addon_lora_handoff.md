# Z-Image Addon LoRA Handoff

Date: 2026-04-29

## Purpose

The `Z-Image Trainer` now produces real LoRA files under:

```text
/home/<user>/ZImage-Trainer/loras/<lora_name>/epoch-*.safetensors
```

The missing piece is the Blender addon flow for actually using those LoRAs during `Nymphs Image` generation.

This handoff covers what needs to be added to:

```text
/home/nymph/NymphsAddon/Nymphs.py
```

and what backend contract changes are still needed in:

```text
/home/nymph/Z-Image/
```

This is not just a UI gap. Right now the addon has no LoRA controls, and the local Z-Image backend has no LoRA request fields either.

## Current Source Findings

### Addon UI currently has no LoRA surface

In `NymphsAddon/Nymphs.py`:

```text
6030-6198  state properties for Z-Image:
           guide image
           img2img strength
           prompt
           presets
           width / height
           steps
           guidance scale
           seed
           variants
```

There are no properties for:

```text
use_lora
lora_path
lora_strength
open_lora_folder
pick_latest_lora
```

### Z-Image panel currently has nowhere to load a trainer LoRA

In `NymphsAddon/Nymphs.py`:

```text
7947-8074  Nymphs Image panel draw path
```

The Z-Image panel currently exposes:

```text
runtime profile
guide image
prompt builders
manual prompt editing
saved prompt
width / height
steps / guidance
seed
variants
Generate Image
```

There is no visible LoRA block at all.

### Addon payload currently cannot send LoRA information

In `NymphsAddon/Nymphs.py`:

```text
2329-2358  _build_imagegen_payload / _build_imagegen_payload_for_prompt
```

Current payload shape is only:

```text
mode
prompt
width
height
steps
guidance_scale
seed
image
strength
```

There is no:

```text
lora_path
lora_scale
```

### Addon request worker just posts that payload directly

In `NymphsAddon/Nymphs.py`:

```text
5063-5089  _imagegen_worker
6764-6812  NYMPHSV2_OT_generate_image
```

The operator builds the payload, then the worker posts it directly to:

```text
POST /generate
```

That means any LoRA support added to the UI must also be added to the payload builder.

## Backend Findings

The local Z-Image backend also has no LoRA support yet.

### Request schema has no LoRA fields

In `/home/nymph/Z-Image/schemas.py`:

```text
GenerateRequest:
  mode
  prompt
  negative_prompt
  image
  width
  height
  steps
  guidance_scale
  seed
  strength
  model_id
```

No LoRA path or scale fields exist.

### API server does not normalize or forward LoRA fields

In `/home/nymph/Z-Image/api_server.py`:

```text
67-170
```

The request normalization and generate flow currently forwards only:

```text
prompt
negative_prompt
image
width
height
steps
guidance_scale
strength
seed
model_id
```

### Model manager generation calls have no LoRA inputs

In `/home/nymph/Z-Image/model_manager.py`:

```text
300-356
```

`generate_text_to_image(...)` and `generate_image_to_image(...)` do not accept:

```text
lora_path
lora_scale
```

So this must be treated as:

```text
addon work
+ backend schema work
+ backend pipeline/model-manager work
```

## Product Goal

The beginner should be able to:

```text
train a LoRA in Manager
open Blender
go to Nymphs Image
turn the LoRA on
pick the trained file
set strength
generate
compare LoRA off vs on
```

The user should not have to guess:

```text
where the file lives
whether the base model still loads
what strength means
whether epoch-0 or epoch-1 should be tried first
whether a Windows path or Linux path is required
```

## Recommended Addon UI

Only show the LoRA controls when:

```text
image backend == Z_IMAGE
```

Do not show them for Gemini.

### New panel block

Add a LoRA block in the Z-Image panel between:

```text
guide image controls
and
prompt / generation settings
```

Suggested label:

```text
Trained LoRA
```

Suggested controls:

```text
[ ] Use Trained LoRA
Path: <file path field>
Pick
Clear
Open Folder
Use Latest
Strength: 1.00
```

Suggested microcopy:

```text
Base Z-Image loads first.
The LoRA is an optional style or subject add-on.
Try the latest epoch first.
```

### Recommended addon properties

Add new state properties in `NymphsAddon/Nymphs.py`:

```text
zimage_use_trained_lora: BoolProperty
zimage_trained_lora_path: StringProperty(subtype="FILE_PATH")
zimage_trained_lora_strength: FloatProperty(default=1.0, min=0.0, max=2.0)
```

Optional later:

```text
zimage_use_latest_trained_lora: BoolProperty
zimage_last_resolved_lora_path: StringProperty
```

### Recommended operators

Add addon operators:

```text
nymphsv2.pick_zimage_trained_lora
nymphsv2.clear_zimage_trained_lora
nymphsv2.open_zimage_trained_loras_folder
nymphsv2.use_latest_zimage_trained_lora
```

The minimum friendly set is:

```text
Pick
Clear
Open Folder
```

`Use Latest` is strongly recommended because it gives the cleanest beginner path after training.

## Path Handling

This is the most important implementation detail.

The backend runs in WSL and needs a Linux path it can open directly.

The LoRA lives in WSL here:

```text
/home/<user>/ZImage-Trainer/loras/<name>/epoch-1.safetensors
```

But Blender on Windows will usually browse that file through:

```text
\\wsl.localhost\<distro>\home\<user>\ZImage-Trainer\loras\<name>\epoch-1.safetensors
```

### Existing helper already available

The addon already has helpers for exposing Linux paths to Blender:

```text
_to_blender_accessible_path(...)
_blender_accessible_path_candidates(...)
_blender_path_exists(...)
_resolved_wsl_distro_name(...)
_resolved_wsl_user_name(...)
```

See `NymphsAddon/Nymphs.py` around:

```text
2281-2313
4194-4307
```

### Missing inverse helper

The addon does not currently have the reverse helper for turning a picked UNC path back into a Linux path for the backend.

Add something like:

```text
_linux_path_from_blender_wsl_path(raw_path, state)
```

Expected behavior:

```text
\\wsl.localhost\NymphsCore\home\nymph\ZImage-Trainer\loras\my_first_lora\epoch-1.safetensors
```

becomes:

```text
/home/nymph/ZImage-Trainer/loras/my_first_lora/epoch-1.safetensors
```

Do not send the Windows UNC path to the backend.

## Recommended Payload Contract

Extend addon payload generation to include:

```text
lora_path
lora_scale
```

Suggested request shape:

```json
{
  "mode": "txt2img",
  "prompt": "mount fuji beyond a calm lake with shoreline trees",
  "width": 1024,
  "height": 1024,
  "steps": 16,
  "guidance_scale": 0.0,
  "seed": 12345,
  "lora_path": "/home/nymph/ZImage-Trainer/loras/my_first_lora/epoch-1.safetensors",
  "lora_scale": 1.0
}
```

For img2img, the same fields should still apply.

Do not overload existing `strength`.

Current meaning:

```text
strength = img2img strength
```

Recommended new meaning:

```text
lora_scale = LoRA influence strength
```

## Recommended Backend Changes

### Z-Image schema

Update `/home/nymph/Z-Image/schemas.py`:

```text
GenerateRequest:
  add lora_path: str | None = None
  add lora_scale: float | None = None
```

### Z-Image API server

Update `/home/nymph/Z-Image/api_server.py`:

```text
normalize lora_scale default to 1.0 when lora_path exists
validate lora_path exists
validate lora_scale is in a safe range
include lora_path and lora_scale in metadata json
forward both fields into the model manager calls
```

### Z-Image model manager

Update `/home/nymph/Z-Image/model_manager.py`:

```text
generate_text_to_image(..., lora_path=None, lora_scale=1.0)
generate_image_to_image(..., lora_path=None, lora_scale=1.0)
```

Implementation note:

```text
apply the LoRA to the active pipeline before inference
clear or unload it safely after inference
avoid stacking repeated loads across requests
```

The exact diffusers/Nunchaku mechanics depend on what the runtime can already support, but the contract should be designed now even if the backend implementation needs a separate experiment pass.

## Recommended Addon UX Behavior

### First usable version

MVP behavior:

```text
1. user trains a LoRA in Manager
2. user opens Nymphs Image
3. enables Use Trained LoRA
4. picks a .safetensors file from ZImage-Trainer/loras
5. sets strength
6. generates
```

### Better beginner version

Preferred beginner flow:

```text
1. user enables Use Trained LoRA
2. addon offers Use Latest
3. addon resolves the newest epoch file inside /home/<user>/ZImage-Trainer/loras
4. addon shows the resolved LoRA name clearly
5. user can still override with Pick
```

### Good defaults

Recommended defaults:

```text
Use Trained LoRA = off
LoRA strength = 1.0
Suggested first test file = latest epoch in the newest trainer folder
```

### Helpful status text

Suggested status copy:

```text
LoRA off: generating with base Z-Image only.
LoRA on: using my_first_lora / epoch-1 at strength 1.0.
```

## Suggested Implementation Order

1. Add addon state properties.
2. Add LoRA UI block to the Z-Image panel.
3. Add UNC-to-Linux path normalization helper.
4. Add `Pick / Clear / Open Folder / Use Latest` operators.
5. Extend `_build_imagegen_payload_for_prompt(...)`.
6. Extend backend `GenerateRequest`.
7. Extend backend API server normalization and metadata writing.
8. Extend backend model-manager calls.
9. Validate txt2img with LoRA.
10. Validate img2img with the same LoRA path.

## Validation Checklist

### Addon-only checks

```text
LoRA controls appear only for Z-Image
Pick stores a valid path
Windows UNC path is converted back to Linux path
Use Latest resolves a real file
Open Folder opens the trainer LoRA directory
```

### End-to-end checks

```text
base Z-Image still works with LoRA off
txt2img works with LoRA on
img2img works with LoRA on
metadata json records lora_path and lora_scale
changing LoRA strength affects output
repeated runs do not stack duplicate LoRAs
```

### Beginner UX checks

```text
user can understand where to click after training
user can tell which LoRA file is active
user can compare LoRA off vs on
user does not need to understand backend internals
```

## Product Summary

The trainer is now real.

The next gap is not training.
The next gap is using the trained result in the addon.

The most important rule for this handoff:

```text
Do not ship this as a hidden expert feature.
Give the user a clear Trained LoRA section in Nymphs Image.
```
