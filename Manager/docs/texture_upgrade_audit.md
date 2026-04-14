# Nymphs3D Texture Upgrade Audit

This document maps:

- what the current `Nymphs3D` frontend exposes
- what already exists in the forked Hunyuan repos
- what is worth exposing later
- what should stay hidden until the current baseline is more mature

It is an internal upgrade note, not a public-facing user guide.

## Current Frontend Surface

Today, `Nymphs3D` exposes a fairly small texture-related control set.

### Legacy launcher

The legacy launcher surface currently exposes:

Currently exposed:

- backend:
  - `2mv`
  - `2.1`
- input mode:
  - `single_image`
  - `multiview`
  - `text_prompt`
- texture:
  - on/off
- `2mv` only:
  - `Turbo`
  - `FlashVDM`
- `2.1` only:
  - `Low VRAM`

### Blender addons

Files:

- `/home/babyj/Nymphs3D-Blender-Addon/blender_addon.py`
- `/home/babyj/Nymphs3D-Blender-Addon/blender_addon_nymphs3d2.py`

Currently exposed:

- single-image generation
- multiview generation
- text-prompt generation
- selected-mesh texturing
- texture on/off
- backend-aware server control in `Nymphs3D2`

What is *not* really exposed:

- `2.0` standard-vs-turbo texture model choice
- `2.1` explicit PBR mode selection
- texture quality/detail settings
- mesh-face-count control for texture runs
- advanced texture helper pipelines

## Repo Capability Audit

## Hunyuan3D-2

File references:

- `/home/babyj/Hunyuan3D-2/README.md`
- `/home/babyj/Hunyuan3D-2/api_server_mv.py`
- `/home/babyj/Hunyuan3D-2/examples/fast_texture_gen_multiview.py`

What is clearly present:

- `hunyuan3d-paint-v2-0`
- `hunyuan3d-paint-v2-0-turbo`
- `hunyuan3d-delight-v2-0`
- multiview texture pipeline
- texture enhancement / delight stage
- texture generation on generated meshes and hand-crafted meshes

What this means:

- the repo has more than one texture model path
- your current frontend effectively treats texture as one generic switch
- the frontend does not let the user choose:
  - turbo texture path
  - standard texture path
  - any explicit “enhancement” mode

### Hunyuan3D-2 likely-upgradeable texture features

1. `2.0` standard texture mode
   - supported in repo: `yes`
   - currently exposed: `no`
   - likely value: `high`
   - reason:
     - users currently get one texture path
     - repo clearly distinguishes normal vs turbo paint

2. `2.0` turbo texture mode as an explicit label
   - supported in repo: `yes`
   - currently exposed: `implicitly`
   - likely value: `high`
   - reason:
     - it exists now, but not as a clean texture-mode concept

3. delight / enhancement stage as a visible option
   - supported in repo: `yes`
   - currently exposed: `not as a user-facing choice`
   - likely value: `medium`
   - reason:
     - it may help quality framing
     - but needs backend clarity before surfacing

4. advanced helper stack
   - ControlNet / IP-Adapter / SDXL-style helper paths
   - supported in repo: `yes`
   - currently exposed: `no`
   - likely value: `low-to-medium`
   - reason:
     - powerful, but too implementation-shaped and risky for normal users

## Hunyuan3D-2.1

File references:

- `/home/babyj/Hunyuan3D-2.1/README.md`
- `/home/babyj/Hunyuan3D-2.1/model_worker.py`
- `/home/babyj/Hunyuan3D-2.1/api_models.py`
- `/home/babyj/Hunyuan3D-2.1/API_DOCUMENTATION.md`
- `/home/babyj/Hunyuan3D-2.1/hy3dpaint/README.md`
- `/home/babyj/Hunyuan3D-2.1/hy3dpaint/textureGenPipeline.py`

What is clearly present:

- PBR texture pipeline
- albedo + metallic/roughness + normal handling
- texture generation config
- official examples with:
  - `max_num_view`
  - `resolution`
- API-level `face_count` support for texturing

What this means:

- your current frontend only exposes a binary `texture` switch
- the repo already supports richer texture-related control than that

### Hunyuan3D-2.1 likely-upgradeable texture features

1. explicit `PBR Texture` wording
   - supported in repo: `yes`
   - currently exposed: `not clearly`
   - likely value: `high`
   - reason:
     - the repo/product distinction is meaningful
     - users should understand that `2.1` is the PBR path

2. texture resolution choice
   - supported in repo: `yes`
   - currently exposed: `no`
   - likely value: `high`
   - reason:
     - real quality/performance tradeoff
     - user-comprehensible setting

3. face-count control for texture runs
   - supported in repo: `yes`
   - currently exposed: `no`
   - likely value: `medium-high`
   - reason:
     - useful on large meshes
     - could reduce failures or heavy texture runs

4. max-view / view-count paint control
   - supported in repo: `yes`
   - currently exposed: `no`
   - likely value: `medium`
   - reason:
     - real pipeline knob
     - but more advanced than a normal user may need

## Feature Matrix

| Feature | Repo Support | Current UI | Worth Exposing | Notes |
|---|---|---|---|---|
| `2.0` standard texture mode | Yes | No | Yes | Strong candidate |
| `2.0` turbo texture mode | Yes | Implicit only | Yes | Should become explicit |
| `2.0` delight/enhancement mode | Yes | No | Maybe | Needs backend clarification |
| `2.0` advanced helper stack | Yes | No | Later | Too advanced right now |
| `2.1` explicit PBR mode label | Yes | Weakly | Yes | Low-risk UI improvement |
| `2.1` texture resolution | Yes | No | Yes | Good quality/perf control |
| `2.1` face_count | Yes | No | Yes | Useful advanced control |
| `2.1` max_num_view | Yes | No | Maybe | Advanced tuning |

## Recommended Exposure Order

### Phase A: Low-risk UI upgrades

These should be easiest and safest.

1. label `2.1` texture as PBR texture
2. make `2.0` texture mode explicit:
   - `Standard Texture`
   - `Turbo Texture`
3. improve wording around selected-mesh texturing

### Phase B: Real user controls with backend value

1. add `2.1` texture resolution
2. add `2.1` face-count control
3. if backend separation is clean, add `2.0` standard-vs-turbo texture selection

### Phase C: Advanced/experimental controls

1. delight/enhancement mode
2. helper-pipeline options
3. deeper texture tuning

These should stay advanced-only unless they become very stable.

## Recommended UX Shape

Do **not** expose raw internal model names.

Instead, use bounded user-facing choices.

### For `2.0 / 2mv`

Suggested texture choices:

- `Fast Texture`
- `Standard Texture`
- maybe later: `Enhanced Texture`

### For `2.1`

Suggested texture choices:

- `PBR Texture`
- `PBR Texture (Higher Detail)`

Suggested advanced controls:

- `Texture Resolution`
- `Face Limit`

## Suggested Next Technical Steps

1. inspect the actual backend entrypoints for the cleanest way to switch:
   - `2.0` standard vs turbo texture
   - `2.1` resolution
   - `2.1` face_count
2. decide which of those can be exposed:
   - in launcher
   - in legacy addon
   - in `Nymphs3D2`
3. implement the low-risk UI wins first

## Bottom Line

Yes, the repos already contain texture-related capability that `Nymphs3D` does not currently surface.

The strongest missing opportunities are:

- explicit `2.0` texture mode choice
- explicit `2.1` PBR framing
- `2.1` texture detail controls such as resolution and face count

Those are much more promising upgrade targets than surfacing the deeper experimental helper code first.
