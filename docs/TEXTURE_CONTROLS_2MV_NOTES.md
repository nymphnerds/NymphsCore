# `2mv` Texture Controls Notes

This note tracks what the current `2mv` / `2.0` texture backend really supports, what is already exposed in the Blender addon, and what could reasonably be exposed later.

## Current Exposed Controls

These are the `2mv` texture controls that are now grounded in the live backend contract.

### Texture Request Controls

- `Face Limit`
- `Texture Size`

Current `Texture Size` values:

- `512 px`
- `1024 px`
- `2048 px`

## Current Startup / Model Controls That Affect Texture

These are not "texture panel" controls, but they do change runtime behavior or output characteristics for `2mv`.

- `Texture`
- `Turbo`
- `FlashVDM`

Practical reading:

- `Turbo` is the bigger speed vs quality tradeoff
- `FlashVDM` is more shape-generation-oriented and matters less for selected-mesh retexture than `Turbo`

## Real Internal `2mv` Texture Pipeline Settings

These are present in the local Tencent `2mv` texture pipeline but are not all exposed as user-facing addon controls.

From the current pipeline:

- candidate camera azimuths
- candidate camera elevations
- candidate view weights
- `render_size`
- `texture_size`
- `bake_exp`
- `merge_method`

There is also a 10-step texture process, including:

- prompt prep
- recentering
- de-lighting
- UV generation
- normal guidance rendering
- position guidance rendering
- multiview diffusion
- resize
- bake
- seam inpaint / save

## Reasonable Future Controls

These are the bounded `2mv` controls that would make sense to expose later if needed.

### Strong Candidates

- `Render Size`
- `Bake Strength` / `Bake Exponent`
- `Merge Method`

### Probably Not Worth Exposing Directly

- raw camera azimuth arrays
- raw camera elevation arrays
- raw per-view weight arrays
- low-level diffusion internals

## Current Product Guidance

- use `2mv` as the practical everyday selected-mesh retexture path
- use `2.1` texture-only when the heavier PBR-oriented path is specifically desired
- keep normal `2.1` for shape generation and shape+texture generation

## Implementation Reminder

`2mv` texture size used to be hardcoded at `2048`. It is now configurable through the addon request path and backend request handling.
