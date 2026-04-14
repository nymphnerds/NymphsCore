# 2mv Remake Progress Handoff

Date: `2026-04-09`
Branch family: `exp/2mv-remake`

## Purpose

This note captures the current state of the `2mv` remake work at the end of the session.

The product direction is now:

- `Z-Image` for image generation through `Nymphs2D2`
- `Hunyuan 2mv` for shape generation
- `TRELLIS.2` as the intended replacement secondary 3D lane
- Blender-first finishing workflow
- `Hunyuan 2.1` is no longer part of the intended shipped stack

For the longer-term goals, decisions, and model shortlist, also read:

- [2mv_product_remake_plan_2026-04-08.md](./2mv_product_remake_plan_2026-04-08.md)
- [marketing_positioning_draft_2026-04-09.md](./marketing_positioning_draft_2026-04-09.md)
- [trellis_frontend_audit_2026-04-10.md](./trellis_frontend_audit_2026-04-10.md)

## Active Repos

- `/home/nymphs3d/Nymphs3D`
- `/home/nymphs3d/Nymphs3D-Blender-Addon`
- `/home/nymphs3d/Nymphs2D2`
- `/home/nymphs3d/Nymphs3D2-Extensions`
- `/home/nymphs3d/Hunyuan3D-2`

All of the above are currently clean on `exp/2mv-remake`.
Exception:

- `/home/nymphs3d/Nymphs2D2` currently also contains a local untracked `Prompts/` reference folder used for prompt research

## What Was Built

### 1. `Nymphs2D2` backend scaffold

Repo:
- `/home/nymphs3d/Nymphs2D2`

Why:
- The remake needs a simple image-generation backend owned by Nymphs rather than relying on Hunyuan text-to-image.
- The backend must be able to swap models later without forcing an architecture rewrite.

What exists:
- FastAPI backend scaffold
- `txt2img` and `img2img`
- health and server info endpoints
- active task reporting
- configurable model id
- model prefetch utility
- `Z-Image-Turbo` support wired into the remake branch

Important files:
- [api_server.py](https://github.com/Babyjawz/Nymphs2D2/blob/exp/2mv-remake/api_server.py)
- [model_manager.py](https://github.com/Babyjawz/Nymphs2D2/blob/exp/2mv-remake/model_manager.py)
- [config.py](https://github.com/Babyjawz/Nymphs2D2/blob/exp/2mv-remake/config.py)
- [prefetch_model.py](https://github.com/Babyjawz/Nymphs2D2/blob/exp/2mv-remake/scripts/prefetch_model.py)

### 2. `Nymphs3D` installer/runtime wiring for `Nymphs2D2`

Repo:
- `/home/nymphs3d/Nymphs3D`

Why:
- `Nymphs2D2` needs to be installable and managed as part of the distro, not just run ad hoc.

What exists:
- installer/runtime path for `Nymphs2D2`
- repo path and branch wiring
- model prefetch integration
- verify/install hooks

Key commits:
- `bc44fb4` `Wire Nymphs2D2 into installer flow`
- `7e63a86` `Target Nymphs2D2 remake branch`

### 3. Blender addon image-generation lane

Repo:
- `/home/nymphs3d/Nymphs3D-Blender-Addon`

Why:
- The addon needed to prove the remake flow end to end:
  - prompt
  - image generation
  - shape generation
  - texture generation

What exists now:
- `Nymphs Image` panel
- prompt-driven image generation through `Z-Image`
- generated image assigned into the main `Image` field for the existing shape flow
- image output folder access
- image output folder clear action
- `Z-Image-Turbo + Nunchaku r32` is now the addon default
- model choice presets for:
  - `Z-Image Standard`
  - `Z-Image Nunchaku r32`
  - `Z-Image Nunchaku r128`
  - `Custom`
- `Generate MV Set` for prompt-driven `front / left / right / back` image generation
- prompt starter presets are now built into the addon
- dedicated texture guidance mode now supports:
  - `Auto`
  - `Image`
  - `Multiview`
  - `Text`
- dedicated texture requests can now reuse the MV slots instead of falling back to one image only

### 4. Blender addon multi-service server panel

Why:
- The old panel assumed one backend.
- The remake now needs multiple services:
  - `Hunyuan 2mv`
  - `Nymphs2D`
  - `Hunyuan 2.1`

What exists now:
- compact `Nymphs Server` panel
- services live in one list
- `TRELLIS.2` now starts from the addon and can drive the shape panel
- WSL target moved into `Advanced`
- top status can represent multiple services
- top job box now tracks image-generation progress too
- top overview and runtime cards now both try to surface live `Z-Image` task detail rather than only generic ready-state text

### 5. Prompt editing and MV prompt workflow improvements

Why:
- The sidebar prompt field was too small to work with long prompts.

What exists now:
- direct prompt editing in the panel
- direct negative-prompt editing in the panel
- a dedicated `Expand` prompt editor dialog for longer edits
- wrapped prompt previews in the panel
- starter prompt presets with `Load`, so shipped prompt ideas can be dropped into the current prompt and then tweaked
- a built-in `Minimalist Chinese Watercolor` starter
- `MV Pose` control with a `Soft A-Pose` default
- `Generate MV Set` to fill the MV slots from one prompt

Important note:
- Blender’s panel text field is still fundamentally single-line.
- The current compromise is:
  - direct quick edits in-panel
  - wider dialog for longer edits

### 6. Output retention and folder management

Why:
- Output files were either hard to find or too disposable.
- You wanted the ability to keep outputs and clear them manually.

What exists now:
- image outputs are kept and accessible
- shape outputs are now kept in a dedicated local output folder
- both image and shape panels now have:
  - `Open Folder`
  - `Clear Folder`

Important behavior:
- shape results are no longer treated as disposable temp files
- this should not affect the `2mv` backend pipeline itself, because the change is only in local post-response file handling

## Why These UI Changes Were Made

### Smaller server panel

Reason:
- the panel was getting too tall and too dense

Changes made:
- WSL target moved under `Advanced`
- `Hunyuan 2.1` kept in the main services list instead of a special section
- service rows simplified

### Backend names vs workflow names

Reason:
- panel titles should reflect workflow
- service status should reflect real backend identity

Current split:
- panel names:
  - `Nymphs Server`
  - `Nymphs Image`
  - `Nymphs Shape`
  - `Nymphs Texture`
- service/backend names:
  - `Hunyuan 2mv`
  - `Z-Image`
  - `Hunyuan 2.1`

## Current Published Addon Build

Latest published remake package:
- `1.1.33`

Extensions repo:
- `/home/nymphs3d/Nymphs3D2-Extensions`

Local artifact:
- [nymphs3d2-1.1.33.zip](https://github.com/Babyjawz/Nymphs3D2-Extensions/blob/exp/2mv-remake/nymphs3d2-1.1.33.zip)

Primary feed currently used:
- `https://raw.githubusercontent.com/Babyjawz/Nymphs3D2-Extensions/exp/2mv-remake/index.json`

Important note:
- branch feed caching was inconsistent during this session
- when in doubt, the local zip is the reliable fallback

## TRELLIS Frontend Audit

A screenshot-driven audit was recorded for the other TRELLIS Blender frontend
that talks to ComfyUI:

- [trellis_frontend_audit_2026-04-10.md](./trellis_frontend_audit_2026-04-10.md)

Why this matters:

- it separates real TRELLIS generation controls from runtime settings and
  Blender-side cleanup tools
- it records which controls are already in the official TRELLIS backend
- it records which ones already exist in Nymphs
- it marks UV unwrap as worth further investigation rather than treating it as
  a settled design choice

Current practical direction:

- keep true TRELLIS generation controls in `Nymphs Shape`
- later add TRELLIS runtime controls in `Nymphs Server`
- move cleanup-heavy controls into future Blender-side finish tools instead of
  copying another frontend blindly

## Important Technical Findings From Testing

### `Z-Image` worked as the right image-generation direction

Reason:
- Hunyuan text-to-image is too slow for the product goal
- `Nymphs2D2` / `Z-Image` proved the image-generation side can be separated cleanly

Latest state:
- `Z-Image-Turbo` is now fully prefetched locally
- `Nymphs2D2` has explicit `Z-Image` pipeline support wired in
- the stock BF16 `Z-Image-Turbo` runtime path was tested and judged too heavy for the desired workflow
- a separate `Nunchaku` experiment path in `.venv-nunchaku` is now working with:
  - `torch 2.11.0+cu130`
  - `diffusers 0.36.0`
  - `nunchaku` installed from the AppMana stable-ABI wheel index
  - `nunchaku-ai/nunchaku-z-image-turbo` `r32` quantized weights
- successful local smoke-test result:
  - transformer load: about `18.9s`
  - pipeline load: about `0.7s`
  - `1024x1024`, `8` steps inference: about `19.8s`
  - VRAM during denoise: about `1.3 GiB`
- `r128` was also tested and landed in a similar practical timing range on the `4080 SUPER`
- `Nymphs2D2` now has an experimental backend/runtime switch for `Nunchaku`
- the addon now exposes `Z-Image` runtime presets directly instead of requiring manual config edits
- the addon now defaults to `Z-Image Nunchaku r32`
- `Generate MV Set` is working and can feed the current `2mv` workflow directly
- sample output path:
  - `/home/nymphs3d/Nymphs2D2/outputs/nunchaku-r32-zimage-test.png`

### Current `Z-Image` verdict

Practical conclusion from this session:

- stock BF16 `Z-Image-Turbo` is not acceptable as the default runtime path on the target machine
- `Z-Image-Turbo + Nunchaku INT4` is the first image-generation path that feels product-viable
- `r32` is the safe lower-tier choice
- `r128` is now worth treating as the higher-end test/default candidate for stronger GPUs

### `Hunyuan 2mv` texture generation is the main slow bottleneck

Observed behavior:
- shape generation is reasonable
- texture generation spends a long time in multiview texture diffusion

Important code reality:
- one input image does not create a cheap texture path
- `2mv` still expands into the heavy multiview texture pipeline

Product implication:
- `Hunyuan 2mv` is increasingly a shape backend first
- texture generation likely needs either:
  - Blender-side finishing
  - or a separate texture backend

Important current addon improvement:
- the dedicated `Nymphs Texture` panel now supports MV texture guidance
- that means the separate texture pass can already reuse:
  - `front`
  - `back`
  - `left`
  - `right`
  guidance instead of forcing a single-image guide
- but this still does not change the underlying `2mv` texture cost profile

### `Hunyuan 2.1` is now a removal target

Current decision:

- `Hunyuan 2.1` should be removed from the shipped product path
- `TRELLIS.2` is the intended replacement lane

That means future cleanup should remove `2.1` from:

- addon UI
- launcher choices
- distro/runtime layout
- installer payload and defaults
- product-facing docs

## Recommended Next Task

Evaluate a dedicated mesh-texture backend.

Why:
- the runtime problem is now largely solved for the `4080 SUPER`
- prompt UX and MV prompt flow are now in a much better place
- the next major weakness is not image generation anymore, it is the heavy `2mv` texture lane

Recommended flow:
1. Keep `Nymphs2D2` architecture unchanged.
2. Keep `Hunyuan 2mv` as the working shape baseline.
3. Treat current `2mv` texture generation as the fallback/baseline, not the final answer.
4. Keep texture-backend research secondary to the now-working main stack.
5. Only treat a replacement as real once it beats the current fallback on:
   - install pain
   - runtime practicality
   - output quality
   - fit with Blender-side finishing

## Distro Rename Note

If the managed WSL distro name is changed away from `Nymphs3D2`:

- this does **not** require rebuilding the base `.tar` just to change the imported distro name
- the import name is chosen by the installer script at `wsl --import` time
- the main work is:
  - installer defaults
  - addon default WSL target
  - docs and wording
  - optional payload filename rename later for user-facing consistency

The current scripts already support a parameterized distro name; the hard part is coordinated product cleanup, not the base tar itself.

## Good Candidate Next

Start with `Z-Image-Turbo` as the image family, but treat `Nunchaku r32` as the current best runtime direction.

Reason:
- the stock BF16 path already failed the usability test
- the `Nunchaku r32` run produced the first practical speed/VRAM result on this machine

Useful candidates:

- `Z-Image-Turbo`
  - https://huggingface.co/Tongyi-MAI/Z-Image-Turbo
- `SDXL Base 1.0`
  - https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0
- `FLUX.1-schnell`
  - https://huggingface.co/black-forest-labs/FLUX.1-schnell
- `MV-Adapter`
  - https://github.com/huanngzh/MV-Adapter
- `Paint3D`
  - https://github.com/OpenTexture/Paint3D
- `Material Anything`
  - https://github.com/3DTopia/MaterialAnything
- `UniTEX`
  - https://github.com/YixunLiang/UniTEX
- `MeshGen`
  - https://huggingface.co/heheyas/MeshGen/tree/main

Important exclusion:

- `MVPaint` and `ComfyUI` should not be treated as part of the intended shipped product path from here

## Repo Direction Note

If `Z-Image` becomes the default long-term image-model family:

- `Nymphs2D2` should remain the stable product-facing backend
- a separate `Z-Image` fork should hold model-specific custom edits

This mirrors the pattern already learned from the Hunyuan side:

- keep product logic in the Nymphs wrapper repo
- keep model-specific hacks and experiments in the model fork

## Resume

Resume code:

```bash
codex resume 019d6ede-df6f-7b81-bd07-f66fd2d17b55
```

## Final State At End Of Session

Repos clean:
- tracked repos: yes
- local prompt-reference folder in `Nymphs2D2/Prompts`: still intentionally untracked

Main remake branch status:
- active and usable

Most important result:
- the core remake direction is no longer just a plan
- it now has:
  - a real image backend
  - addon integration
  - managed service UI
  - working MV prompt generation
  - dedicated MV-aware texture guidance in the texture panel
  - end-to-end proof of the prompt -> image -> shape -> texture workflow
