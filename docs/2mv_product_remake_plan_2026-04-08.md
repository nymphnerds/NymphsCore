# `2mv` Product Remake Plan

Date: `2026-04-08`

Status: planning brief for experimental branches only

This document describes the intended remake direction for the Nymphs product family.
It is deliberately written before code changes so the current working system can remain
stable while the next-generation workflow is explored on isolated branches.

## Current Decision Summary

As of `2026-04-09`, the active remake direction is:

- `Z-Image` through `Nymphs2D2` for image generation
- `Hunyuan 2mv` for shape generation
- `TRELLIS.2` as the replacement secondary 3D lane
- Blender-first finishing workflow
- manual image workflows stay supported
- `Hunyuan 2.1` is no longer part of the intended shipped stack
- `Z-Image-Turbo` remains the preferred image family, but the practical runtime direction is now the lighter `Nunchaku` path rather than stock BF16

This means the current experimental stack is:

- image generation: `Nymphs2D2` surfaced as `Z-Image`
- shape generation: `Hunyuan 2mv`
- secondary replacement 3D lane: `TRELLIS.2`
- finishing: Blender nodes, painting, and later texture-backend experiments

For the latest implementation state, read:

- [handoff_2026-04-09_2mv_remake_progress.md](./handoff_2026-04-09_2mv_remake_progress.md)

## Model Shortlist

These are the currently relevant model and backend candidates to test against the remake goals.

### Image Models For `Nymphs2D`

- `Z-Image-Turbo`
  - current preferred image-model direction for `Nymphs2D`
  - best current image family to replace Playground as the default
  - note: the stock BF16 runtime path was too heavy on the target `4080 SUPER`
  - current best runtime direction is `Nunchaku` `INT4 r32`
  - link: https://huggingface.co/Tongyi-MAI/Z-Image-Turbo

- `Nunchaku Z-Image-Turbo r32`
  - current best runtime experiment for making `Z-Image-Turbo` practical on the target GPU
  - successful local smoke test on `RTX 4080 SUPER`:
    - transformer load about `18.9s`
    - pipeline load about `0.7s`
    - `1024x1024`, `8` steps inference about `19.8s`
    - denoise VRAM about `1.3 GiB`
  - quantized weights link: https://huggingface.co/nunchaku-ai/nunchaku-z-image-turbo
  - stable-ABI wheel helper fork used for the experiment: https://github.com/AppMana/forks-nunchaku-stable-abi

- `Nunchaku Z-Image-Turbo r128`
  - current higher-quality follow-up candidate after the successful `r32` test
  - on the target `4080 SUPER`, it landed in a similar practical timing class to `r32`
  - likely better fit for stronger cards if quality holds up
  - quantized weights link: https://huggingface.co/nunchaku-ai/nunchaku-z-image-turbo

- `Playground v2.5`
  - current working image model in the experimental backend
  - link: https://huggingface.co/playgroundai/playground-v2.5-1024px-aesthetic

- `SDXL Base 1.0`
  - best neutral baseline to compare against Playground
  - link: https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0

- `FLUX.1-schnell`
  - worth testing later as a speed/quality comparison
  - note: larger and gated
  - link: https://huggingface.co/black-forest-labs/FLUX.1-schnell

- `MV-Adapter`
  - not a base model, but a multiview-consistency adapter worth testing later
  - link: https://github.com/huanngzh/MV-Adapter

### Texture Backend Candidates

- `Paint3D`
  - promising candidate for a dedicated mesh-texturing backend
  - image-conditioned and text-conditioned texture workflow
  - link: https://github.com/OpenTexture/Paint3D

- `Material Anything`
  - strong current candidate if the product later wants a more explicit PBR-material lane
  - link: https://github.com/3DTopia/MaterialAnything

- `UniTEX`
  - newer and interesting, but still looks less release-settled than the first three choices
  - link: https://github.com/YixunLiang/UniTEX

- `MeshGen`
  - real and current, but broader than the narrow “replace the texture backend only” goal
  - link: https://huggingface.co/heheyas/MeshGen/tree/main

- `TRELLIS.2`
  - current intended replacement for the former `2.1` lane
  - broader high-end image-to-3D plus texturing lane, not just a drop-in texture replacement
  - current official public inference entrypoints are still single-image for both shape generation and texturing
  - if the official path keeps proving worthwhile, evaluate a dedicated `Nymphs` fork of the official repo to explore real multi-image or multiview-conditioned shape and texturing flows
  - keep this as a separate experimental lane until the adapter/API contract is clear
  - link: https://github.com/microsoft/TRELLIS.2

## Planned Repo Split For `Z-Image`

If `Z-Image` becomes a core part of the shipped image workflow, the intended repo split is:

- `Nymphs2D2`
  - stable product-facing backend
  - API service
  - installer and launcher integration
  - model selection
  - Nymphs workflow logic

- `Z-Image` fork
  - model-specific custom edits
  - inference tweaks
  - image-edit helpers
  - experiments too model-specific to live inside `Nymphs2D2`

Important rule:

- `Nymphs2D2` stays the stable wrapper
- model-specific code should go into a `Z-Image` fork if custom edits become necessary

## Core Decision

The future product direction is:

- center the product around `Hunyuan 2mv`
- remove `2.1` from the shipped product stack
- keep current manual image workflows that already work well
- add a dedicated image-generation lane as an optional upstream convenience
- make Blender the main finishing environment for texture editing, PBR refinement, and painting

This is a shift from:

- multi-backend Hunyuan frontend

to:

- practical Blender-first creation pipeline

## Product Thesis

The sellable workflow should be:

1. start from either:
   - user-supplied image(s)
   - or a single text prompt
2. if text is used, generate ideal `2mv` input images through a dedicated image-generation subsystem
3. run `2mv` for mesh generation and initial texture generation
4. import the result into Blender as an editable material workflow
5. refine the material in Blender
6. optionally derive or author extra PBR information in Blender
7. optionally paint directly on the model in Blender

The product goal is speed, clarity, editability, and low user confusion.

## Guiding Principles

- keep the current working system intact until the remake is proven
- do not break the existing manual image workflows
- do not force users to become prompt engineers
- hide backend complexity behind simple Blender-facing actions
- expose model/runtime experiments as product-friendly choices, not raw ML jargon, wherever possible
- prefer one obvious workflow over multiple overlapping backend lanes
- use `2mv` for what it is good at: fast practical shape and base texture generation
- use Blender for what it is good at: editable materials, painting, and finishing

## What Stays

These should remain supported:

- user-supplied single image input
- user-supplied multiview image input
- `2mv` mesh generation
- `2mv` texture generation
- the practical existing workflows that already produce good results

Manual image input is not being replaced by the image-generation lane.
The image-generation lane is an additional input source, not a mandatory one.

## What Changes

These are the major intended changes:

- `2.1` is no longer treated as the long-term core backend
- direct Hunyuan text-to-3D is no longer treated as the main future workflow
- the addon is reshaped around a cleaner pipeline
- retexture becomes primarily a Blender-side workflow for exact UV preservation
- image generation becomes a dedicated upstream system rather than being conflated with mesh generation
- prompt quality and prompt presetting become a real part of the product, not an afterthought
- the current addon now already supports:
  - prompt starters
  - MV set generation
  - MV-guided dedicated texture requests

## Current UX Status

The prompt UX phase is no longer theoretical.

Current state:

- direct prompt editing exists in the addon
- reusable prompt starters exist in the addon
- `Generate MV Set` exists in the addon
- `Z-Image` defaults now point at the practical `Nunchaku r32` path

So the next UX risk is no longer “can prompt UX be improved at all”.
It is:

- which prompt starters and MV prompt patterns actually produce the best `2mv` inputs
- how much of that should become a curated hidden system versus exposed user choices

## Next Backend Phase: Dedicated Texture Backend Evaluation

The next serious backend phase should focus on replacing or sidelining the current `2mv` texture bottleneck.

Why:

- image generation is now practical
- prompt and MV generation are now practical
- shape generation is already in a good place
- texture generation remains the slowest and least settled part of the pipeline

Recommended direction:

- keep `2mv` texture as the working fallback
- evaluate a dedicated mesh-texture backend next
- compare candidates primarily on:
  - install pain
  - runtime practicality
  - texture quality
  - fit with Blender-side finishing

## Managed Distro Rename Note

If the managed WSL distro name changes away from `Nymphs3D2`:

- this does **not** require rebuilding the base tar just to change the imported distro name
- the effective distro name is chosen by the installer at `wsl --import` time
- the real work is coordinated cleanup across:
  - installer defaults
  - addon default WSL target
  - docs and wording
  - optional payload filename rename later for user-facing polish

So the rename should be treated as an installer/config/docs pass, not a base-image rebuild, unless the contents of the exported distro itself also need changing.

## Why `2.1` Is Being Removed

Current working view:

- `2.1` is too slow for the desired product workflow
- `2.1` texturing is too slow
- `2.1` text-to-generation is too slow
- `2.1` PBR quality is not strong enough to justify the cost
- `2mv` is the more practical and preferred generation lane

This no longer means “maybe later”.
It means the product remake should now remove `2.1` deliberately from the shipped path.

## Intended End-State Architecture

The target architecture has three layers.

### 1. Image Preparation Layer

Purpose:

- prepare ideal image input for `2mv`

Input:

- user prompt
- or user-supplied images

Output:

- single image
- or preferred multiview image set

Likely responsibilities:

- image-runtime prompt expansion
- view-specific prompt templating
- transparent or clean background handling
- recentering / normalization / cleanup
- generating front / back / left / right images suitable for `2mv`

### 2. `2mv` 3D Generation Layer

Purpose:

- generate the initial mesh and initial base texture result

Input:

- preferred: multiview image set
- fallback: single image

Output:

- mesh
- base texture result

Key principle:

- `2mv` is the only intended long-term Hunyuan generation lane if the remake succeeds

### 3. Blender Finishing Layer

Purpose:

- make the generated result editable and finishable inside Blender

Responsibilities:

- import the generated asset into Blender
- build editable node graphs
- derive or author extra material maps
- support painting directly on the model
- support retexturing without changing the mesh or UVs when possible

## Main User Workflows

There are two major workflows.

### Workflow A: New Asset Creation

This is the main product path.

High-level flow:

1. user enters one master prompt or supplies images manually
2. if text is used, the image runtime generates image(s)
3. system prefers a multiview set
4. `2mv` generates mesh and initial texture result
5. addon imports editable result into Blender
6. user refines materials and optionally paints

Important rule:

- one prompt should conceptually drive the whole system

But internally the system may derive multiple hidden prompts for:

- image generation
- per-view image generation
- background rules
- negative prompts
- consistency constraints

The user should not have to hand-author all of that every time.

### Workflow B: Retexture Existing Mesh

This is the main simplification insight from planning.

Preferred retexture direction:

- keep the existing mesh
- keep the existing UVs
- use prompt-driven or image-driven texture ideation upstream
- apply new texture ideas in Blender without changing the shape

This is intentionally different from backend-heavy retexture regeneration.

The desired result:

- exact shape preservation
- exact UV preservation
- faster iteration
- more user control

Retexture input sources should be:

- user-supplied guide image
- prompt-generated texture concept from the image runtime

Retexture output should be:

- updated Blender material / texture maps on the same mesh

This is simpler and more powerful than re-sending every retexture idea through a backend mesh texturing loop.

Current practical note:

- the addon now does support MV-guided dedicated texture requests into `2mv`
- so backend retexture is still an active baseline
- but it is still not the preferred long-term answer unless it stops being the main bottleneck

## Input Philosophy

The remake should support two upstream input modes.

### Manual Input Mode

User provides:

- single image
- or multiview image set

This mode must stay because it already works well and is valuable.

### Generated Input Mode

User provides:

- one high-level prompt

System provides:

- a single image
- or preferably a multiview image set

Generated input must feed the same downstream `2mv` workflow as manual input.

## Image Runtime Role

The image runtime is not being added as a generic image toy.
Its role is highly specific:

- generate ideal source images for `2mv`
- reduce prompt-engineering burden on the user
- produce views and backgrounds that suit mesh generation
- optionally generate texture ideas for Blender-side retexture workflows

This subsystem should be judged by one main question:

- does it produce better `2mv` inputs with less work from the user?

## Image Runtime Requirements

The image runtime subsystem will eventually need to support:

- a single master prompt
- hidden prompt expansion
- front / back / left / right generation
- consistency control across views
- clean or removable backgrounds
- output normalization for `2mv`
- optional prompt-driven texture ideation for retexture

## Model Investigation Note

One candidate that should be investigated for the SD subsystem is `MV-Adapter`.

Reason:

- it is specifically aimed at multi-view consistent image generation rather than only single-image text-to-image
- it may be useful for generating `2mv`-ready front / back / side view sets
- it appears relevant to the hardest upstream problem in this remake: view consistency

Current assumption:

- `MV-Adapter` should be treated as a model / adapter research candidate for the image-preparation layer
- it does not change the product architecture by itself
- it should be evaluated against plain `SDXL` and other candidate image models during backend selection

This note is a reminder to include `MV-Adapter` in the model benchmark and backend investigation work.

This should almost certainly live behind its own UI section rather than being mixed directly into the current `2mv` request controls.

## Blender UI Direction

The future addon UI should likely separate concerns more clearly.

Probable major panels:

- `Server`
  - start / stop local services
  - backend status
  - launch configuration

- `Image Generation`
  - one master prompt
  - SD style/options
  - generate a single image or a multiview set

- `3D Generation`
  - use manual or generated images
  - run `2mv`

- `Material Finish`
  - editable shader graph
  - PBR derivation / refinement
  - texture painting helpers

- `Retexture`
  - prompt or guide image for texture ideas
  - Blender-side texture replacement or refinement on existing mesh

The exact final UI is open for design, but the conceptual separation should be cleaner than the current mixed backend-driven layout.

## `2.1` Removal Work

`2.1` is now a removal target, not just a maybe.
The work now is coordinated product cleanup.

Required cleanup points:

- remove `2.1` from addon UI
- remove `2.1` from launcher choices
- remove `2.1` from installer payload and runtime defaults
- remove `2.1` from the managed distro layout
- keep only historical references in docs where needed
- make `TRELLIS.2` the explicit replacement secondary 3D lane

Short version:

- remove `2.1` cleanly across the whole product surface

## What Must Not Happen

These are explicit non-goals for the early experimental work:

- do not destabilize the current working installer / launcher / addon setup
- do not leave half-removed `2.1` traces across product-facing surfaces
- do not rebuild the distro first
- do not merge every idea from the Skyrim addon wholesale
- do not turn the Blender addon into a ComfyUI clone
- do not let `ComfyUI` or `MVPaint` drift into the shipped product path
- do not expose too many raw backend controls in the main workflow

## Relationship To Existing PBR Addon Work

The existing Skyrim PBR addon work shows that Blender-side material refinement is viable.

Important lesson:

- the useful ideas should be extracted and rebuilt cleanly inside Nymphs

Likely reusable concepts:

- derive roughness from source texture
- derive metallic from source texture
- derive normal from source texture
- derive AO if useful
- create a practical Principled-based node graph
- provide artist-friendly tweak controls

Likely not to be ported directly:

- Skyrim/NIF-specific logic
- MO2/VFS path logic
- game-specific export and patch tools

## Experimental Scope

The experimental branches should prove the new architecture in phases.

### Phase 1: Prove `2mv` Blender Finishing

Goal:

- prove `2mv` can be the only core Hunyuan lane

Deliverables:

- `2mv` generation result imported into Blender in an editable way
- workable material graph for base texture editing
- ability to paint directly on the model

### Phase 2: Add Blender-Side PBR Refinement

Goal:

- replace the practical reason to keep `2.1`

Deliverables:

- Blender-side roughness / metallic / normal derivation
- simple material presets or controls
- practical finishing workflow

### Phase 3: Define SD Input Layer

Goal:

- specify the upstream image-generation system

Deliverables:

- SD backend choice
- request/response contract
- prompt templating strategy
- view generation plan

### Phase 4: Simplify Product Surface

Goal:

- remove user-facing complexity

Deliverables:

- remove `2.1` from the experimental product branch
- add `TRELLIS.2` as the explicit replacement lane
- simplify addon UI
- simplify launcher / installer assumptions

### Phase 5: Strip `2.1` From Distribution

This is now an explicit cleanup phase.

Deliverables:

- no `2.1` in distro packaging
- no `2.1` launcher modes
- no `2.1` installer payload
- no `2.1` addon runtime entry in the shipped build
- no `MVPaint` or `ComfyUI` product-facing traces outside historical notes
- updated docs and product messaging

## Repo Responsibilities

### `Nymphs3D-Blender-Addon`

Primary responsibilities:

- UI changes
- manual image workflow preservation
- generated image workflow integration
- editable import / material workflow
- Blender-side retexture workflow
- Blender-side PBR refinement tools

### `Hunyuan3D-2`

Primary responsibilities:

- `2mv` generation path only
- any experimental support needed for cleaner editable result handoff
- keep backend contract narrow and practical

### `Nymphs3D`

Primary responsibilities:

- launcher changes
- installer changes
- distro contents
- repo-level docs
- backend lifecycle / startup orchestration

## Branching Strategy

Current stable work must stay untouched.

Historical note:

- the remote repos now publish the kept work from `main`
- the old `exp/2mv-remake` remote branch links are obsolete
- local snapshot branches preserve the old branch history where needed

Use experimental branches in all affected repos.

Recommended branch name:

- `exp/2mv-blender-finishing-workflow`

Why this name:

- it describes the replacement workflow
- it no longer needs to preserve `2.1` as a possible shipped lane
- it keeps the branch framed around the positive target state

Repos that carried the remake work:

- [Nymphs3D-Blender-Addon](https://github.com/Babyjawz/Nymphs3D-Blender-Addon/tree/main)
- [Hunyuan3D-2](https://github.com/Babyjawz/Hunyuan3D-2/tree/main)
- [Nymphs3D](https://github.com/Babyjawz/Nymphs3D/tree/main)

Possible future repo:

- image-generation backend repo, once the final runtime split settles

## Immediate Next Step

The immediate next step is not implementation.

It is:

- create the experimental branches
- keep stable branches untouched
- use the branches for design validation and workflow proof

The first implementation target on those branches should be:

- prove the `2mv -> Blender editable finishing` workflow

That proof should come before:

- distro rebuild
- launcher rewrite
- installer cleanup
- full `2.1` removal
- full image-runtime integration polish

## Final Planning Summary

The intended product remake is:

- `2mv` as the only core Hunyuan backend
- `TRELLIS.2` as the replacement secondary 3D lane
- manual image support preserved
- dedicated image generation added as an optional upstream system
- Blender-side retexture and PBR refinement as first-class workflows
- `2.1` removed from the shipped product surface

If the remake succeeds, Nymphs becomes:

- a simpler, faster, Blender-first creation pipeline

not:

- a multi-lane Hunyuan backend controller
