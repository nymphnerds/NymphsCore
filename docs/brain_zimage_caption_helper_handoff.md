# Brain Z-Image Caption Helper Handoff

Date: 2026-04-29

## Purpose

This handoff is for extending `Nymphs-Brain` so it can help the `Z-Image Trainer`
with caption drafting and prompt cleanup using the right model types.

The immediate product question is:

```text
Can Brain help beginners write Z-Image training captions?
```

The answer is:

```text
Yes, but not by trying to load the exact same Qwen safetensors text encoder
used inside the Z-Image image stack.
```

Brain should help with:

```text
caption drafting
caption rewriting
prompt cleanup
caption explanation for beginners
```

Brain should not be treated as:

```text
a replacement for the Z-Image text encoder
a direct loader for arbitrary DiffSynth/Hugging Face safetensors checkpoints
an auto-caption system that silently takes control away from the user
```

## Hard Product Rules

These are the user-facing rules established so far and should be preserved:

1. Captions remain user-controlled.
2. Any Brain-assisted captioning must be optional.
3. Drafts are suggestions only, not silent auto-generated replacements.
4. The beginner path must stay simple.
5. The trainer should still work without Brain installed.

## Current Brain Architecture

Current Brain is a separate optional LLM stack inside the managed WSL distro.

Current managed shape:

```text
/home/nymph/Nymphs-Brain
```

Current local model path:

```text
LM Studio CLI download/manage -> GGUF file -> llama-server on port 8000
```

Current remote fallback path:

```text
optional OpenRouter llm-wrapper
```

Current Brain is built around:

```text
GGUF local model loading
llama-server inference
optional OpenRouter remote delegation
```

Relevant evidence in source:

```text
Manager/scripts/install_nymphs_brain.sh
docs/NYMPHS_BRAIN_GUIDE.md
Manager/scripts/remote_llm_mcp/cached_llm_mcp_server.py
```

Important current limitation:

```text
Brain does not currently load arbitrary Hugging Face safetensors files.
It resolves GGUF files for llama-server.
```

## Why The YouTube Qwen Screenshot Matters

The screenshot from the Z-Image workflow shows a `Load CLIP` node using a
Qwen-family safetensors model on the image-generation side.

That means:

```text
Z-Image generation has a Qwen-based text side
prompt wording matters
caption wording matters
```

That does not mean:

```text
Brain should try to boot that exact safetensors file in llama-server
Brain can already use that exact model path
the trainer should switch to fully automatic captions
```

The correct takeaway is:

```text
Brain should use a suitable chat or vision-language helper model to assist
the user with captions, while the Z-Image runtime keeps using its own text encoder.
```

## Recommended Direction

Do not try to make Brain ingest DiffSynth text-encoder safetensors directly.

Instead, add a new Brain capability:

```text
Caption Helper
```

This helper should support one of these model classes:

### Option A: Local text-only GGUF helper

Use a normal local GGUF chat model for:

```text
rewrite my caption
make this clearer
turn my rough note into a better training caption
explain why this caption is weak
```

Pros:

```text
fits current Brain architecture best
lowest implementation risk
works without remote APIs
good first milestone
```

Cons:

```text
cannot inspect the image directly
user still needs to describe the image themselves
```

### Option B: Local vision-language GGUF helper

Use a local multimodal GGUF model with companion projector support when available.

This is the more powerful path for:

```text
look at this image and draft a caption
suggest a simpler beginner caption
describe the scene in prompt-like language
```

Pros:

```text
closest to what new users actually need
can generate first-draft captions from the image itself
still keeps everything local
```

Cons:

```text
depends on good LM Studio / llama-server support for the selected model
depends on GGUF plus any required mmproj-style support files
more UX and runtime complexity
```

Runtime note:

```text
Even a "small" local VLM helper can still be several GB in VRAM.
For example, a Qwen2.5-VL-7B Q4_K_M style helper can be around 6 GB.

That means the intended workflow may need to be:

1. start Brain caption helper
2. draft or rewrite captions
3. stop or unload the caption helper
4. start the actual LoRA training pass
```

Do not assume caption help and LoRA training should run at the same time on the same GPU.

### Option C: Remote wrapper helper

Use the existing `llm-wrapper` OpenRouter path for caption help.

Pros:

```text
easy to prototype quickly
can use stronger hosted models
no local VLM download requirement
```

Cons:

```text
not local-only
requires API key
less aligned with the product's offline/local story
```

## Recommended Rollout Order

### Milestone 1

Add optional text-only Brain assistance:

```text
Draft caption from my rough notes
Rewrite caption
Explain why this caption is weak
```

This uses the existing Brain local GGUF or remote wrapper path and does not
require direct image upload.

### Milestone 2

Add optional image-aware caption drafting:

```text
Draft caption from selected image
Draft all captions in this dataset folder
```

This should use any compatible downloaded local vision-language model if local support is good enough.

### Milestone 3

Allow lightweight prompt-side help after training:

```text
Generate example prompts for this LoRA
Explain how to activate the LoRA cleanly
```

## Concrete Caption Brain Spec

This is the recommended product shape for the first real image-aware captioning implementation.

### User-Facing Goal

The beginner should experience this as:

```text
1. Put images in the trainer pictures folder.
2. Turn on Caption Brain.
3. Click Draft Captions.
4. Review or edit the generated metadata.csv.
5. Start training.
```

The user should not need to think about:

```text
llama-server
GGUF
mmproj
temporary model switching
GPU process management
```

### Hard Behavior Rules

`Caption Brain` must:

```text
stay optional
write valid metadata.csv rows
produce drafts, not final authoritative captions
leave user edits intact when possible
stop using GPU memory before LoRA training starts
```

`Caption Brain` must not:

```text
silently overwrite all captions without warning
pretend a text-only Brain model can caption images
start LoRA training while the caption helper is still holding VRAM
require the user to manually switch their normal Brain chat model every time
```

## Runtime Orchestration

### Model Requirement

Caption Brain should require:

```text
a downloaded local vision-language GGUF model
the matching mmproj/projector file when that model family requires one
```

Important note:

```text
The current Brain runtime already knows how to pass --mmproj to llama-server
when a matching projector file is present beside the GGUF.
```

The missing product layer is orchestration and readiness checks, not the underlying llama-server capability.

### Readiness Check

The training page should consider Caption Brain "ready" only when all of these are true:

```text
1. Nymphs-Brain is installed.
2. At least one compatible downloaded local vision model exists.
3. A matching mmproj file exists if that model requires one.
4. The model folder can be resolved from the Brain models directory.
```

Readiness should be reported in plain language:

```text
Caption Brain ready
Caption Brain needs a compatible vision model
Caption Brain vision model is missing its projector file
```

### Temporary Model Switching

Do not force the user to make the vision model their everyday Brain chat model permanently.

Preferred behavior:

```text
1. Remember the current Brain local model selection.
2. Temporarily set the selected local model to the compatible vision model.
3. Start llama-server through lms-start.
4. Run caption drafting.
5. Stop Brain after drafting.
6. Restore the previous normal Brain local model selection.
```

If automatic restore is too risky in the first pass, an acceptable milestone-1 fallback is:

```text
temporarily switch the model
stop Brain after drafting
leave the selected model changed
show a clear status message to the user
```

But the ideal target is full temporary-session behavior.

### Start/Stop Sequence

Recommended Manager-side flow:

```text
Caption Brain enabled
-> check readiness
-> choose compatible vision model
-> update Brain model selection
-> run lms-start
-> wait for llama-server health on port 8000
-> draft captions
-> stop Brain with lms-stop
-> return user to editable captions state
```

Before LoRA training starts:

```text
if Caption Brain was used and Brain is still running
-> stop it
-> confirm VRAM is released as best as possible
-> then launch training
```

## Caption CSV Generation

### File Format

Caption Brain must write:

```csv
file_name,text
image_001.png,a childlike drawing of a bear building a small cabin in snow
image_002.png,a childlike drawing of a girl holding a red balloon near a tree
```

Rules:

```text
one CSV row per image
first column is the exact filename
second column is the draft caption
no extra hidden metadata columns in the first version
```

### Draft Modes

The first implementation should support at least these modes:

```text
Fill Blank Only
Overwrite All Drafts
Rewrite Selected Caption
```

Recommended defaults:

```text
first run: Fill Blank Only
explicit user action required for Overwrite All Drafts
```

### Prompting Behavior

The vision model should be prompted to produce:

```text
one short training caption per image
plain natural language
content-focused description
no markdown
no numbering
no explanations
```

For stylized datasets, prefer content-focused drafts rather than keyword soup.

Example desired style:

```text
a bear building a small wooden cabin in the snow
a woman singing on stage under bright lights
a child holding a kite in a grassy field
```

Avoid outputs like:

```text
kids drawing style, cute style, crayon style, bear, snow
```

### User Review Requirement

After drafting, the UI should make the review step obvious:

```text
Draft captions created. Review metadata.csv before training.
```

That message matters because even a strong VLM will sometimes:

```text
miss small objects
over-describe the scene
misread style or age
phrase details awkwardly for training
```

## Suggested UI Shape

Recommended additions to the Z-Image Trainer page:

```text
Caption Brain toggle
Draft Captions button
Caption Brain status text
Optional mode dropdown: Fill Blank Only / Overwrite All Drafts
```

Recommended status text examples:

```text
Caption Brain ready: Qwen2.5-VL-7B-Instruct
Caption Brain needs a downloaded vision model
Caption Brain is starting...
Caption Brain is drafting captions...
Stopping Caption Brain to free GPU memory...
```

## Model Management Requirements

The Brain model-manager flow should treat some downloaded models as vision-capable and ensure they are actually usable for image input.

That means:

```text
detect likely VLM models automatically
check whether an mmproj file is already present
auto-download the matching mmproj if missing
warn clearly if a compatible projector cannot be found
```

Important state distinction:

```text
downloaded vision model != vision-ready model
```

A model should only be considered vision-ready when:

```text
GGUF exists
required mmproj exists
Brain can resolve both at runtime
```

## Implementation Order

Recommended concrete build order:

1. Add readiness check for compatible downloaded vision models plus mmproj.
2. Add Manager orchestration for temporary model switching and Brain start/stop.
3. Add `Draft Captions` action that writes valid metadata.csv rows.
4. Add `Fill Blank Only` as the default draft mode.
5. Add `Overwrite All Drafts` and `Rewrite Selected Caption`.
6. Add restore-previous-model behavior if not done in step 2.

## Success Criteria

The first implementation is successful when a beginner can:

```text
drop images into the trainer folder
click Draft Captions
get one valid draft caption per image in metadata.csv
edit any caption they dislike
start training without manually managing Brain internals
```

## Architecture Recommendation

Do not create a heavyweight new Manager install setting like:

```text
Dedicated caption model selector
```

That is probably more system than we need right now.

Simpler direction:

```text
Brain remains generic model management
Brain can have many downloaded models on disk
the training page checks for a compatible downloaded model when Caption Brain is enabled
the training page starts that helper only when needed
the training page stops or unloads it before LoRA training begins
```

Important distinction:

```text
Downloaded model:
  available in Brain's model store on disk

Active local model:
  the model currently used by the normal Brain chat flow
```

These do not need to be the same thing.

That means a caption helper can be:

```text
downloaded
compatible
started on demand
not the user's normal Brain chat model
```

This keeps captioning separate from the normal Brain chat/coding workflow.

## User Experience Recommendation

Do not add a giant new Brain dependency wall to the trainer page.

Keep the trainer page simple:

```text
Open Pictures Folder
Open Captions File
Caption Brain
Start Training
```

Recommended helper actions:

```text
Draft Caption For Selected Image
Rewrite Current Caption
Explain Caption Tips
Draft All Missing Captions
```

Recommended Brain orchestration:

```text
1. User downloads a compatible model from the Brain side at some earlier point.
2. User enables Caption Brain on the training page.
3. Training page checks whether a compatible downloaded model exists.
4. If one exists, the page starts it on demand.
5. User drafts or rewrites captions.
6. Before Start Training, the page stops or unloads the helper to free VRAM.
```

Critical UX rule:

```text
Brain-generated captions should land in an editable draft view or in metadata.csv
only after explicit user approval.
```

Never silently fill captions behind the user's back.

## What To Avoid

Do not do these:

```text
do not wire Brain directly to Z-Image's internal safetensors text encoder path
do not make Brain a required dependency of the trainer
do not replace manual captions with invisible AI captions
do not assume one local Brain model is ideal for both coding chat and caption help
```

## Concrete Implementation Direction

### Manager / Model Config

Likely touch points:

```text
Manager/apps/NymphsCoreManager/Models/InstallSettings.cs
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Needed changes:

```text
add Brain helper status text
add training-page logic for compatible-model detection
add trainer-side commands for draft/rewrite caption actions
keep all helper actions optional and clearly labeled
```

### Brain Installer / Runtime

Likely touch points:

```text
Manager/scripts/install_nymphs_brain.sh
Manager/scripts/remote_llm_mcp/cached_llm_mcp_server.py
docs/NYMPHS_BRAIN_GUIDE.md
```

Needed changes:

```text
support optional multimodal local helper if GGUF/mmproj path is viable
provide a stable command or endpoint for caption helper calls
support on-demand startup of a compatible downloaded model
```

### New Helper Surface

Recommended addition:

```text
/home/nymph/Nymphs-Brain/bin/caption-helper
```

This can be a thin wrapper that:

```text
takes image path and/or user notes
chooses a compatible downloaded local model or optional remote helper
returns plain text caption suggestions
```

Potential follow-up:

```text
expose caption-helper through MCP or OpenAPI, similar to llm-wrapper
```

## Minimal Viable Technical Plan

### Phase 1

Text-only Brain caption assistance:

```text
input:
  user note or weak caption

output:
  one better training caption
```

No image upload needed yet.

### Phase 2

Image-aware local helper:

```text
input:
  image path

output:
  one draft caption
```

This phase depends on whether the chosen local multimodal GGUF path behaves
cleanly inside the current Brain architecture.

Important runtime behavior:

```text
If the selected local caption-helper model keeps several GB of VRAM resident,
the Manager should explicitly stop or unload it before starting trainer jobs.
This is especially important on single-GPU machines where Brain and training
share the same card.
```

### Phase 3

Bulk helper:

```text
scan dataset folder
draft captions for missing rows
leave existing user captions untouched
```

## Success Criteria

The feature is successful if:

1. A beginner can understand what a caption should look like.
2. Brain can help draft or rewrite captions without taking control away.
3. The trainer still works fully without Brain.
4. The local helper uses model formats Brain actually supports.
5. We do not confuse Brain's helper role with Z-Image's internal text-encoder role.
6. Caption-helper VRAM use does not silently sabotage the actual training run.

## Short Version

The clean path is:

```text
Do not teach Brain to run the exact Z-Image Qwen safetensors text encoder.

Do teach Brain to use a compatible downloaded helper model, started on demand,
using formats it already understands or can reasonably be extended to understand:

  local GGUF chat
  local GGUF multimodal
  optional OpenRouter fallback
```

That gives the user real caption help without collapsing the separation between:

```text
Brain = helper LLM system
Z-Image = image runtime and trainer system
```
