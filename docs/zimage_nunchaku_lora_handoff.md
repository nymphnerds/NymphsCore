# Z-Image Nunchaku LoRA Handoff

Date: 2026-05-04

## Scope

This handoff is about one pipeline:

```text
Blender addon -> Manager -> Z-Image runtime -> Nunchaku Z-Image LoRA
```

Main product goals:

```text
1. Use downloaded and Manager-trained Z-Image LoRAs from Blender
2. Make LoRA activation easy enough that users do not need to understand trigger-word theory
3. Keep the Trainer guide, Manager UI, and Blender UI consistent with the real workflow
```

Current status in one sentence:

```text
Z-Image LoRAs now work end-to-end from Blender through Manager into Nunchaku again, including the user's own yamamoto LoRA, and the current remaining concern is overall sluggishness / possible leak or polling pressure to investigate in a future session.
```

## Environments

Keep these separate:

```text
Dev/build WSL:
  NymphsCore_Lite

Live/test WSL:
  NymphsCore
```

Important:

```text
- Blender talks to the live/test runtime
- Manager is often rebuilt from the dev/build workspace
- do not assume the local Linux workspace and the live Blender runtime are the same environment
```

## Source Repos And Key Paths

### Runtime/backend

```text
/home/nymph/Z-Image
```

Key files:

- [model_manager.py](/home/nymph/Z-Image/model_manager.py:1)
- [api_server.py](/home/nymph/Z-Image/api_server.py:1)

### Manager overlay copies

These must stay in sync with runtime/backend edits:

- [model_manager.py](/home/nymph/NymphsCore/Manager/scripts/zimage_backend_overlay/model_manager.py:1)
- [api_server.py](/home/nymph/NymphsCore/Manager/scripts/zimage_backend_overlay/api_server.py:1)
- [model_manager.py](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/win-x64/scripts/zimage_backend_overlay/model_manager.py:1)
- [api_server.py](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/win-x64/scripts/zimage_backend_overlay/api_server.py:1)

### Manager app

```text
/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager
```

Key files touched in the activation work:

- [InstallerWorkflowService.cs](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs:908)
- [MainWindowViewModel.cs](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs:1197)
- [MainWindow.xaml](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:1942)

### Blender addon

```text
/home/nymph/NymphsAddon
```

Key files:

- [Nymphs.py](/home/nymph/NymphsAddon/Nymphs.py:1)
- [blender_manifest.toml](/home/nymph/NymphsAddon/blender_manifest.toml:1)
- [CHANGELOG.md](/home/nymph/NymphsAddon/CHANGELOG.md:1)

Current addon version:

```text
1.1.232
```

Current packaged zip:

- [nymphs-1.1.232.zip](/home/nymph/NymphsAddon/dist/nymphs-1.1.232.zip:1)

### Trainer guide

- [training.html](/home/nymph/NymphsCore/home/guides/training.html:1)

## Research And References Used

### Official docs

- Z-Image usage docs:
  `https://nunchaku.tech/docs/nunchaku/usage/zimage.html`
- FLUX LoRA usage docs:
  `https://nunchaku.tech/docs/nunchaku/usage/lora.html`
- FLUX Python LoRA converter docs:
  `https://nunchaku.tech/docs/nunchaku/python_api/nunchaku.lora.flux.html`

### Community/reference implementations

- ComfyUI Nunchaku Z-Image wrapper behavior was the most useful practical reference
- User found a group test post showing Z-Image Turbo + Nunchaku style LoRAs can work in practice:
  `https://sketchbooky.wordpress.com/2026/02/07/group-test-z-image-turbo-nunchaku-style-loras/`

### Local transcript research

The most important transcript finding:

```text
Ostris does not treat trigger usage as a hard universal rule.
Some LoRAs work fine with no extra phrase.
Some work better with one.
```

Practical product conclusion:

```text
The system should store and recommend an activation phrase when useful, instead of expecting the user to understand trigger theory first.
```

## Critical Runtime Discovery

The `400 Bad Request` problem earlier in the debugging came from `diffusers` type-checking the supplied pipeline components during `from_pretrained(...)`.

That means:

```text
Passing a wrapped transformer directly into ZImagePipeline.from_pretrained(...) was rejected.
```

Fix:

```text
- create the raw Nunchaku transformer first
- pass the raw transformer into ZImagePipeline.from_pretrained(...)
- only after pipeline creation, wrap pipeline.transformer with a deferred LoRA wrapper
```

This is implemented in:

- [model_manager.py](/home/nymph/Z-Image/model_manager.py:20)
- overlay copies listed above

## Current Working Runtime Design

### Deferred wrapper

Current runtime uses:

```text
DeferredNunchakuLoraWrapper
```

Defined in:

- [model_manager.py](/home/nymph/Z-Image/model_manager.py:20)

What it does:

```text
- stores the desired LoRA file path and strength
- waits until forward() time
- resets prior LoRA state
- composes the requested LoRA onto the Nunchaku model
- logs wrapper.compose before inference
```

Important log line:

```text
[nymphs:zimage:lora] wrapper.compose path=... strength=...
```

### Runtime stage logging

Important stage/error logs:

```text
[nymphs:zimage:stage] generate.begin
[nymphs:zimage:stage] txt2img.call.begin
[nymphs:zimage:stage] pipeline.txt2img.begin
[nymphs:zimage:stage] pipeline.txt2img.returned
[nymphs:zimage:stage] pipeline.txt2img.image_extracted
[nymphs:zimage:stage] save.begin
[nymphs:zimage:stage] save.end
[nymphs:zimage:stage] generate.end
[nymphs:zimage:error] ...
```

## Blender Addon Work Completed

### 1. LoRA library support

Blender now supports:

```text
- direct .safetensors files under ~/ZImage-Trainer/loras
- folder/run-based trainer LoRAs
- manual file picking
- opening the LoRA folder from the UI
```

### 2. Lag reduction

Addon lag work shipped in `1.1.221`:

```text
- moved the automatic GPU probe off Blender's main thread
- added in-flight guards for background backend probes
```

### 3. LoRA activation metadata

Addon metadata work shipped in `1.1.222`:

```text
- reads LoRA-side metadata from nymphs_lora.json
- saves edited activation metadata back out
- can auto-append activation text to the backend prompt
```

### 4. Wording cleanup

Addon wording cleanup shipped in `1.1.223`:

```text
Old wording:
  Auto Use Trigger
  Trigger Text

Current wording:
  Use Recommended Activation
  Recommended Activation
```

Also:

```text
The status line now says:
  Using: ...
```

Current addon references:

- [Nymphs.py](/home/nymph/NymphsAddon/Nymphs.py:6623)
- [Nymphs.py](/home/nymph/NymphsAddon/Nymphs.py:8709)

## Manager Work Completed

### 1. Manager now writes LoRA metadata

When a Z-Image Trainer job is created, Manager now writes:

```text
~/ZImage-Trainer/loras/<lora-name>/nymphs_lora.json
```

This metadata currently includes:

```text
schema_version
source
display_name
activation_text
auto_use_trigger
lora_type
notes
```

Reference:

- [InstallerWorkflowService.cs](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs:908)

### 2. Manager now shows the recommendation in the Trainer UI

Manager does not yet give the user a fully editable activation field, but it now shows a clear:

```text
Recommended activation
```

This is driven from the LoRA name + preset and is meant to remove guesswork.

References:

- [MainWindow.xaml](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:1942)
- [MainWindowViewModel.cs](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs:1197)

Current behavior:

```text
all current presets:
  recommended activation = "<lora name>"
```

## Trainer Guide Work Completed

The public trainer guide was updated to match the product as it actually works now.

Updated guide:

- [training.html](/home/nymph/NymphsCore/home/guides/training.html:1)

What was added/updated:

```text
- current Trainer workflow
- recommended activation explained in plain English
- Blender usage section
- Manager metadata sidecar note
- current addon wording
- stale "Style High Noise" wording replaced with "Strong Style"
```

## Known Public Control LoRAs

The user downloaded known-working public test LoRAs into:

```text
~/ZImage-Trainer/loras
```

Known control files:

```text
Storybook_Folk_Art_Z-Image_Turbo.safetensors
John_Howe_ZIT_v1.safetensors
Freehand_Ink_Drawing_1nk_ZIT.safetensors
childrens_book_illustration2_000001600.safetensors
```

These were useful because they gave a control group independent of the user's own training workflow.

## What Is Proven Working Now

### Public LoRAs work end-to-end

Successful live logs proved:

```text
- wrapper.compose fires
- LoRA keys match
- quantized and unquantized branches update
- inference completes
- image saves
- Blender receives the result
```

Strong proof points from the load log:

```text
updated_lora_modules: 120
updated_weight_modules: 30
unmatched_prefix_count: 0
POST /generate HTTP/1.1 200 OK
```

Interpretation:

```text
The Blender addon path is working.
The Manager overlay path is working.
The runtime LoRA application path is real and functional.
```

### The user's yamamoto LoRA also works

This is the biggest status change from the earlier handoff.

The old handoff said:

```text
The user's own Manager-trained LoRA still appears to fail or hang.
```

That is no longer accurate.

What is now proven:

```text
- yamamoto loads
- yamamoto applies
- yamamoto can complete inference
- yamamoto can produce clearly stylized output
```

Most important practical finding:

```text
Prompt wording matters a lot for yamamoto.
Using the LoRA name itself as activation text is now the intended default product direction.
```

So the current read is:

```text
yamamoto is not broken
the Manager training output is usable
activation wording is part of the real user experience
```

## LoRA Comparison / Research Findings

The public LoRAs and the user's `yamamoto` LoRAs were compared structurally.

What was found:

```text
- yamamoto files are not malformed
- same 480 tensor layout
- same key patterns
- no NaNs
- no Infs
- overall magnitudes are sane
```

So:

```text
This is not a simple "bad safetensors file" problem.
```

Later tests also showed:

```text
yamamoto can complete at low and medium strengths
yamamoto can also complete at 1.0 when prompted in a way that clearly activates the style
```

That weakens the earlier "hard strength threshold hang" theory.

Current interpretation:

```text
Earlier failures were likely a mix of weak activation, session-state weirdness, and incomplete prompt activation testing, not a proof that yamamoto is fundamentally broken.
```

## Activation Product Direction

This is the current product answer in simple terms:

```text
Users should not have to think about trigger theory.
By default, the selected LoRA name should be used automatically as activation text.
```

Current implementation state:

```text
Manager:
  writes LoRA-side metadata
  defaults activation text to the LoRA name

Addon:
  reads the metadata
  shows "Use Activation"
  shows/edit "Activation"
  inserts activation into the visible prompt block system
  defaults to the selected LoRA file name when metadata is empty
```

Important note:

```text
The addon prompt now uses the normal managed prompt architecture.
There is no hidden send-time append anymore.
```

So the current state is:

```text
simple enough for normal use
still flexible if a LoRA needs custom activation text later
```

## Build / Verification Caveat

Addon work is verified:

```text
- py_compile passed
- packaged zip built successfully
```

Manager/source/runtime edits are in place, and the user later rebuilt Manager successfully from Windows PowerShell.

So:

```text
Manager source changed
runtime overlay copies changed
Windows Manager build was produced from the user side
```

## Current Practical State

Fast summary:

```text
1. downloaded/public Z-Image LoRAs work in Blender
2. Manager-trained yamamoto LoRA also works
3. LoRA activation is now product-simple: the LoRA name is the default activation
4. Manager now writes activation metadata
5. Blender now reads and uses that metadata
6. the addon now inserts activation into the visible prompt block system
7. docs were updated to explain the real workflow
8. current unresolved concern is overall sluggishness / possible memory leak or polling pressure
```

## Next Sensible Steps

If work resumes later, the best next product steps are:

```text
1. investigate Blender / whole-PC sluggishness
2. check for polling pressure, timer buildup, or memory leak behavior in addon/runtime
3. test repeated LoRA generations over time to see whether slowness grows session-by-session
4. only after performance is stable, continue any further LoRA UX polish
```

## Things To Avoid

```text
- do not touch the working LoRA backend path casually once it is stable again
- do not reintroduce stale "yamamoto is broken" assumptions
- do not overcomplicate activation UX; simple default is LoRA name
- do not forget to bump addon version on addon changes
```
