# Universal Model Fetch UI Plan

Date: 2026-05-13
Branch context: `modular`

Purpose: define the compact, Manager-styled, module-owned native model fetch
surface for Z-Image Turbo, TRELLIS.2, and future Nymphs backends.

Brain and LoRA are intentionally out of scope for this first model-fetch pass.
They need richer custom UIs and should be handled separately after this compact
native fetch contract is stable.

## Goal

Installed modules need a clear way to expose backend-owned setup actions from
the module details page.

Common examples:

- choose which model or quantized weight to download
- show source links so users can research model files
- run smoke tests
- save the selected runtime/model preset
- stream long-running downloads to Logs
- reuse a persistent Hugging Face token without asking every module again

The Manager should not become the generation UI for every backend. For Blender
backends, Blender may remain the normal launcher and consumer. The Manager
should install, configure, fetch, repair, launch, stop, inspect, and test
modules without owning backend-specific behavior.

The module owns:

- action choices and scripts
- model fetch validation
- model URLs and source links
- status and health checks
- smoke tests
- start/stop/open/log behavior
- cache/output/log path declarations
- selected runtime/model preset files

The Manager owns:

- the standard shell
- the right-side universal admin rail
- native rendering of manifest-declared module controls
- safe action routing into module-declared entrypoints
- log streaming/display for long-running module actions
- generic persistent user secrets, such as a Hugging Face token

The Manager should not hardcode Z-Image, TRELLIS, or any future backend.
Modules declare controls; the Manager renders them. There should be no temporary
per-module button fallback for model fetch.

## Generated Content Storage

Generated user content should not be stored inside backend source repos or
runtime install folders.

Backends may create images, meshes, previews, metadata, logs, and intermediate
artifacts. Those should live under shared data roots, not under paths like:

```text
$HOME/Z-Image
$HOME/TRELLIS.2
$HOME/NymphsModules/<module>
```

Recommended direction:

```text
$HOME/NymphsData/generated/<module-id>
```

Examples:

```text
$HOME/NymphsData/generated/zimage
$HOME/NymphsData/generated/trellis
$HOME/NymphsData/generated/lora
```

Existing module-specific output paths such as
`$HOME/NymphsData/outputs/zimage` can remain as compatibility paths during
migration, but the long-term contract should clearly separate:

```text
source/runtime: backend repo and venv files
cache/models:   downloaded reusable model files
generated:      user-created outputs and run metadata
logs:           diagnostic logs
config:         selected presets and module-owned settings
```

If the Manager later grows a `Storage`, `Files`, or `Generated` control, it
should open or manage these shared data roots. It should not encourage storing
generated content inside backend repos.

## Placement

Use compact sections in the center pane:

```text
// DETAILS
[status/details card]

// MODEL FETCH
[compact module-owned controls]

// MODULE ACTIONS
[Smoke Test] [Start] [Stop] [Logs] [...]
```

Native model fetch groups should:

- live in the main details pane, not the right `// MANAGE` rail
- look like the current Manager UI
- be compact enough to fit above `// MODULE ACTIONS` without forcing scroll
- avoid WebView2/local HTML for model fetch choices
- remain module-owned
- stream long actions to the normal Logs page
- expose source links where useful
- keep `Smoke Test` as a plain module-owned action

Do not put backend-specific model fetch controls in:

- the right `// MANAGE` rail
- the scrollable details text
- a separate WebView2 page
- a module-owned local HTML page
- a large new card that competes with the details card

The right `// MANAGE` rail stays for universal admin/lifecycle actions:

```text
Guide
Install
Update
Repair
Storage
Uninstall
Dir
```

## Universal Contract

Use `ui.manager_action_groups` for model fetch controls.

Keep `ui.manager_actions` only for simple module-owned commands such as:

- `Smoke Test`
- `Start`
- `Stop`
- `Logs`

Do not represent model variants as a pile of separate fallback buttons. Model
fetch needs fields, source links, token handling, and one submit action.

The action group is generic enough that future backends can reuse it. Model
fetch is the primary use. Future examples could include:

- selecting a runtime profile
- fetching an optional extension pack
- selecting a model variant
- setting a backend port
- choosing a quantization level
- running a module-owned environment check

Possible manifest shape:

```json
{
  "ui": {
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "links": [
          {
            "label": "Base",
            "url": "https://huggingface.co/example/base-model"
          },
          {
            "label": "Weights",
            "url": "https://huggingface.co/example/weights"
          }
        ],
        "fields": [
          {
            "name": "variant",
            "type": "segmented",
            "label": "Variant",
            "default": "balanced",
            "arg": "--variant",
            "options": [
              {
                "label": "Fast",
                "value": "fast",
                "description": "Lower VRAM"
              },
              {
                "label": "Balanced",
                "value": "balanced",
                "description": "Recommended"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "HF",
            "secret_id": "huggingface.token",
            "env": "NYMPHS3D_HF_TOKEN",
            "optional": true
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ]
  }
}
```

The Manager renders:

- section title
- compact source links
- segmented/select controls
- masked secret/token inputs
- submit button
- optional short descriptions/tooltips

The module still owns:

- choices
- labels
- source links
- action id
- script arguments
- validation
- download behavior
- selected preset persistence
- which env vars it accepts

## Field Types

Field types should stay deliberately small:

```text
segmented  short option lists, such as Q4/Q5/Q6/Q8
select     longer option lists
checkbox   simple boolean flags
secret     saved sensitive values, such as Hugging Face tokens
```

Avoid building a full declarative app framework. If a module needs
dynamic layout, nested workflows, previews, or rich interaction, it can still
use a custom UI later. This contract is for compact native setup controls.

## Action Execution

The Manager should convert declared fields into either arguments or environment
variables.

Recommended rules:

- normal fields use declared args, for example `--quant Q5_K_M`
- secret fields use environment variables, never command-line args
- Manager does not validate backend-specific combinations
- module scripts validate invalid choices and print clear errors
- long-running actions stream output to Logs
- result mode should support at least `show_logs` and `show_output`

Example:

```json
{
  "name": "quant",
  "type": "segmented",
  "label": "Quant",
  "arg": "--quant",
  "default": "Q5_K_M",
  "options": [
    { "label": "Q4", "value": "Q4_K_M" },
    { "label": "Q5", "value": "Q5_K_M" }
  ]
}
```

This runs:

```bash
scripts/module_fetch_models.sh --quant Q5_K_M
```

## Persistent Secrets

The Hugging Face token should be entered once and reused by any module action
that declares it needs Hugging Face authentication.

Recommended behavior:

- user enters the token once in the Manager
- Manager stores it as a generic saved Hugging Face secret
- modules declare that a fetch action accepts the Hugging Face token
- Manager injects the saved token into that module action as
  `NYMPHS3D_HF_TOKEN`
- module scripts never need to know where the token is stored
- user can clear or replace the saved token from the compact UI

This is Manager-owned infrastructure because the token belongs to the user, not
to one backend. The module still owns fetch choices, validation, URLs, and
scripts.

Safe handling requirements:

- render a password-style field
- persist the value without showing it again in plain text
- pass it only as a declared environment variable
- never echo the value to Logs, Details, or command previews
- allow clearing the saved token
- prefer OS credential storage where practical
- if file-backed, use user-only read/write permissions

Modules should decide whether the token is optional or required. Z-Image and
TRELLIS should treat it as optional unless Hugging Face requires it for the
selected model or the user wants authenticated downloads.

## Runtime Presets

Fetching a model and running a model are related but not identical.

Recommended default:

- successful normal fetch should save the selected runtime/model preset
- provide a future advanced "download only" path only if needed

Reason: users expect the model they fetched to be the model the backend starts
with. If a module downloads one variant but starts with another, the workflow
becomes confusing.

Preset files are module-owned. Example:

```text
$HOME/NymphsData/config/<module-id>/generation-preset.env
```

External clients, such as Blender addons, can later read or mirror these module
contracts once stable.

## Blender Addon Future Work

The Blender addon should be updated after the Manager/module model fetch
contract is stable. It should fit the module-owned contract instead of carrying
old hardcoded assumptions from the pre-modular runtime.

Short-term addon requirements:

- use the same shared Hugging Face cache as the modules
- use module-owned output and log paths
- understand selected model presets written by module fetch actions
- keep Blender as the generation UI, not the Manager
- avoid guessing a backend variant from old addon-only settings

Z-Image addon follow-up:

```text
cache:   $HOME/NymphsData/cache/huggingface
outputs: $HOME/NymphsData/outputs/zimage
logs:    $HOME/NymphsData/logs/zimage
preset:  $HOME/NymphsData/config/zimage/generation-preset.env
```

The addon should eventually read or mirror:

```text
Z_IMAGE_NUNCHAKU_PRECISION=int4|fp4
Z_IMAGE_NUNCHAKU_RANK=32|128|256
```

It should not assume rank alone is enough. `INT4` and `FP4` are generation
weights for Z-Image/Nunchaku. `BF16` belongs to LoRA training and is not a
Z-Image model fetch choice.

TRELLIS addon follow-up:

```text
cache:   $HOME/NymphsData/cache/huggingface
outputs: $HOME/NymphsData/outputs/trellis
logs:    $HOME/NymphsData/logs/trellis
preset:  $HOME/NymphsData/config/trellis/model-preset.env
```

The addon should eventually read or mirror the selected GGUF quant:

```text
TRELLIS_GGUF_QUANT=Q4_K_M|Q5_K_M|Q6_K|Q8_0
```

Future preferred shape:

- modules expose a small discovery/status file or command for addon consumers
- Blender asks the installed module for ports, cache paths, output paths, log
  paths, and selected model preset
- addon launch code uses module entrypoints or module-declared env vars where
  practical
- hardcoded addon paths remain only as compatibility fallback

The addon follow-up should include a smoke test against the modular runtime
layout before promotion.

## Smoke Tests

`Smoke Test` remains a separate module-owned action.

It is the backend's confidence check: installed, configured, fetched enough
models, and able to answer a minimal health/generation path. The Manager only
renders the button and streams the output.

Smoke tests should not be folded into model fetch controls. They belong in
normal `ui.manager_actions`, usually beside `Start`, `Stop`, and `Logs`.

## Initial Adopter: Z-Image Turbo

Z-Image generation uses the Nunchaku runtime. The UI should make the exact
Z-Image Turbo quantized weight manual and visible instead of hiding it behind
automatic detection.

Noob guide:

```text
RTX 20/30/40: start with svdq-int4_r128.
RTX 50:       start with svdq-fp4_r128.
Low VRAM:     use an r32 file.
Max quality:  use svdq-int4_r256 if the card can handle it.
```

Published Z-Image Turbo quantized weights for the Nunchaku runtime:

```text
svdq-int4_r32-z-image-turbo.safetensors
svdq-int4_r128-z-image-turbo.safetensors
svdq-int4_r256-z-image-turbo.safetensors
svdq-fp4_r32-z-image-turbo.safetensors
svdq-fp4_r128-z-image-turbo.safetensors
```

These are Z-Image Turbo inference weights, not separate models and not GGUF.
They are Nunchaku-compatible `.safetensors` files used by the acceleration
runtime. FP4 r256 is not offered because the published r256 weight is INT4-only.
The Manager does not need Z-Image-specific conditional UI logic.

Suggested compact display:

```text
// MODEL FETCH
Base · Weights
Guide  downloads base Z-Image Turbo plus one Nunchaku-compatible weight
Weight [svdq-int4_r128-z-image-turbo.safetensors v]
HF     [token field] [saved/clear]
       [Fetch Models]

// MODULE ACTIONS
[Smoke Test] [Start] [Stop] [Logs]
```

Hugging Face links:

```text
Base:    https://huggingface.co/Tongyi-MAI/Z-Image-Turbo
Weights: https://huggingface.co/nunchaku-ai/nunchaku-z-image-turbo
```

Suggested script interface:

```bash
scripts/zimage_fetch_models.sh --model svdq-int4_r32-z-image-turbo.safetensors
scripts/zimage_fetch_models.sh --model svdq-int4_r128-z-image-turbo.safetensors
scripts/zimage_fetch_models.sh --model svdq-int4_r256-z-image-turbo.safetensors
scripts/zimage_fetch_models.sh --model svdq-fp4_r32-z-image-turbo.safetensors
scripts/zimage_fetch_models.sh --model svdq-fp4_r128-z-image-turbo.safetensors
```

Optional token interface:

```bash
NYMPHS3D_HF_TOKEN=hf_xxx scripts/zimage_fetch_models.sh --model svdq-int4_r128-z-image-turbo.safetensors
```

The current script also accepts `--hf-token`, but the Manager should prefer
environment passing so token values do not appear in visible command text or
process arguments.

Equivalent explicit script interface remains supported for manual use:

```bash
scripts/zimage_fetch_models.sh --precision int4 --rank 32
scripts/zimage_fetch_models.sh --precision int4 --rank 128
scripts/zimage_fetch_models.sh --precision int4 --rank 256
scripts/zimage_fetch_models.sh --precision fp4 --rank 32
scripts/zimage_fetch_models.sh --precision fp4 --rank 128
```

The Manager UI should use the real Z-Image Turbo quantized weight filenames.
Logs can still show the technical precision/rank values.

Z-Image module-owned canonical paths:

```text
cache:   $HOME/NymphsData/cache/huggingface
outputs: $HOME/NymphsData/outputs/zimage
logs:    $HOME/NymphsData/logs/zimage
```

Z-Image Blender addon compatibility summary:

```text
precision: int4 or fp4
rank:      32, 128, or 256
cache:     $HOME/NymphsData/cache/huggingface
outputs:   $HOME/NymphsData/outputs/zimage
logs:      $HOME/NymphsData/logs/zimage
preset:    $HOME/NymphsData/config/zimage/generation-preset.env
```

## Initial Adopter: TRELLIS.2

Status as of 2026-05-14: implemented in `nymphnerds/trellis` with module-owned
native model fetch controls and pushed as module version `0.1.14`.

The Nymphs TRELLIS module starts the GGUF FastAPI adapter:

```text
scripts/api_server_trellis_gguf.py
```

The upstream TRELLIS source also contains Gradio apps:

```text
app.py
app_texturing.py
```

Those Gradio apps are not the focus of this plan. Model fetch should stay a
compact native Manager action section.

User-facing options:

```text
Q4_K_M  smallest download and lowest VRAM; best first proof test
Q5_K_M  recommended balance of quality, download size, and VRAM
Q6_K    heavier quality-focused option after Q5 works
Q8_0    largest local GGUF option; use with plenty of VRAM and disk
All     optional large download for Blender/addon quant switching later
```

Suggested compact display:

```text
// MODEL FETCH
Base · GGUF
Quant  [Q4] [Q5] [Q6] [Q8] [All]
HF     [token field] [saved/clear]
       [Fetch Models]

// MODULE ACTIONS
[Smoke Test] [Start] [Stop] [Logs]
```

Source links:

```text
Official repo:     https://github.com/microsoft/TRELLIS.2
Project page:      https://microsoft.github.io/TRELLIS.2/
GGUF models:       https://huggingface.co/Aero-Ex/Trellis2-GGUF
Support checkpoint:https://huggingface.co/microsoft/TRELLIS.2-4B
rembg u2net:       https://github.com/danielgatis/rembg/releases/tag/v0.0.0
```

The installed details guide must make clear that Fetch Models downloads more
than one selected GGUF file:

```text
shared GGUF support files
selected Aero-Ex/Trellis2-GGUF quant bundle
required microsoft/TRELLIS.2-4B support checkpoint
rembg u2net background-removal model
```

These files live under:

```text
cache:   $HOME/NymphsData/cache/huggingface
outputs: $HOME/NymphsData/outputs/trellis
logs:    $HOME/NymphsData/logs/trellis
config:  $HOME/NymphsData/config/trellis/model-preset.env
rembg:   $HOME/NymphsData/models/rembg
```

Suggested script interface:

```bash
scripts/trellis_fetch_models.sh --quant Q4_K_M
scripts/trellis_fetch_models.sh --quant Q5_K_M
scripts/trellis_fetch_models.sh --quant Q6_K
scripts/trellis_fetch_models.sh --quant Q8_0
scripts/trellis_fetch_models.sh --quant all
```

Optional token interface:

```bash
NYMPHS3D_HF_TOKEN=hf_xxx scripts/trellis_fetch_models.sh --quant Q5_K_M
```

The script accepts `--quant` and persists the selected normal fetch to
`$HOME/NymphsData/config/trellis/model-preset.env`.

TRELLIS addon compatibility has a short-term path alignment fix in addon
`1.1.236`: default port `8095`, cache/output/log env vars under
`$HOME/NymphsData`, and migration for stale saved addon port `8094`.

## Implementation Order

### 1. Manager Native Renderer

- implement `ui.manager_action_groups`
- render compact action groups between `// DETAILS` and `// MODULE ACTIONS`
- use existing Manager button and text styles
- no right-rail controls
- no WebView2/local HTML for model fetch
- no module-specific C# branches
- support compact layout only
- support links, segmented/select fields, checkboxes, masked secret fields, and
  submit buttons
- stream submit actions to Logs
- define safe persistent secret handling and clear-token behavior
- support declared args and env vars
- inject saved secrets only into actions that declare them
- keep `Smoke Test` in normal `ui.manager_actions`

### 2. Z-Image Module Interface

- remove the old Z-Image local HTML/WebView2 fetch UI
- declare the model fetch group in `ui.manager_action_groups`
- add a single `--model` / `--weight` argument using the real quantized
  `.safetensors` filename
- keep legacy `--gpu-family` and `--preset` arguments only for manual/backward
  compatibility
- do not offer invalid `FP4 r256`
- save selected generation preset after successful normal fetch
- keep HF token support through `NYMPHS3D_HF_TOKEN`

### 3. TRELLIS Module Interface

- done: declare the model fetch group in `ui.manager_action_groups`
- done: add `--quant`
- done: support `all`
- done: keep support checkpoint fetch
- done: accept `NYMPHS3D_HF_TOKEN` for authenticated Hugging Face downloads
- done: save selected GGUF quant after successful normal fetch
- done: explain shared GGUF files, selected quant bundle, support checkpoint,
  and rembg u2net in the installed details guide

### 4. Proof

Proof order:

1. Manager renders a generic action group from manifest only
2. Manager stores, replaces, clears, and injects the saved Hugging Face token
3. Z-Image fetch INT4 r32 or r128
4. Z-Image invalid combo handling
5. Z-Image authenticated fetch with the saved token
6. Z-Image status sees the selected fetched model
7. Z-Image smoke test reports backend health after fetch
8. TRELLIS fetch Q5_K_M
9. TRELLIS authenticated fetch with the saved token
10. TRELLIS smoke test reports backend health after fetch
11. TRELLIS fetch `all`
12. Logs page receives long download progress for both modules

### 5. Addon Follow-up

After the module-side behavior is stable:

- done short-term: update Blender addon cache/output/log env vars
- add precision awareness for Z-Image
- done short-term: align TRELLIS default addon launch port with module port
  `8095`
- next: align TRELLIS GGUF quant launch with module choice/preset discovery
- add or use module discovery command/file over hardcoded addon paths
- smoke-test Blender against module-fetched Z-Image and TRELLIS models

## Non-Goals

This plan does not build:

- Brain UI
- LoRA UI
- arbitrary native WPF plugin loading
- a full declarative app framework
- WebView2 replacement for rich module apps that genuinely need rich custom UI
- WebView2/local HTML model fetch pages for simple backend model choices
- TRELLIS Gradio hosting
- Z-Image Gradio, because current Z-Image generation is FastAPI-only

## Open Questions

- Should action groups be shown only when installed?
- Should source links open externally or inside the Manager guide/browser flow?
- Should successful fetch always save the selected runtime preset, or should
  each action declare whether it saves?
- Should token persistence use OS credential storage first, with encrypted or
  permission-restricted file storage as fallback?

Recommended defaults:

- use compact select/segmented controls for short option lists
- show action groups only when installed
- open source links externally
- save selected runtime/model preset after normal fetch
- keep backend-specific validation inside module scripts
- store the Hugging Face token once through generic Manager secret plumbing,
  then expose it only to module actions that declare the token field
