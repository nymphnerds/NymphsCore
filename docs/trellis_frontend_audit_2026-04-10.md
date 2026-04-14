# TRELLIS Frontend Audit

Date: `2026-04-10`

Purpose:

- record what was observed in the other Blender TRELLIS frontend screenshot
- separate real TRELLIS backend controls from frontend/runtime conveniences
- decide what belongs in `Nymphs Shape`, `Nymphs Server`, or future Blender-side tooling

Reference screenshot:

- `/home/nymphs3d/screenshots/Screenshot 2026-04-09 214135.png`

Compared against:

- official `TRELLIS.2` repo in `/home/nymphs3d/TRELLIS.2`
- local community wrapper repo in `/home/nymphs3d/ComfyUI-TRELLIS2`
- current addon code in `/home/nymphs3d/Nymphs3D-Blender-Addon`

## Main Conclusion

The other addon mixes together four different kinds of controls:

1. real TRELLIS generation parameters
2. runtime/backend loading choices
3. export and postprocess options
4. Blender- or frontend-side mesh cleanup conveniences

For Nymphs, these should stay split.

Best product shape:

- `Nymphs Shape`
  - only true TRELLIS generation controls
- `Nymphs Server`
  - TRELLIS runtime/backend controls
- future Blender cleanup / finish tools
  - mesh cleanup, repair, and postprocess actions

## Compared Controls

### Frontend Harness Controls

These are not core TRELLIS generation features.

- `ComfyUI URL`
  - wrapper/frontend transport setting
  - not needed in the normal Nymphs local backend path
- `Output Folder`
  - frontend convenience
  - Nymphs already has local output-folder handling
- `Prompt Template`
  - frontend workflow helper
  - relevant later for Nymphs prompt/preset work, not for TRELLIS itself

### Real TRELLIS Generation Controls

These are backend-native and belong in `Nymphs Shape`.

- `Image`
  - yes, real input
  - already in Nymphs
- `Resolution`
  - yes, real TRELLIS pipeline choice
  - already in Nymphs as:
    - `512`
    - `1024_cascade`
  - official code also supports `1024`
- `Seed`
  - yes
  - already in Nymphs
- `SS Guidance Strength`
  - yes
  - already in Nymphs
- `SS Sampling Steps`
  - yes
  - already in Nymphs
- `Shape Guidance Strength`
  - yes
  - already in Nymphs
- `Shape Sampling Steps`
  - yes
  - already in Nymphs
- `Texture Guidance`
  - yes
  - already in Nymphs
- `Texture Steps`
  - yes
  - already in Nymphs
- `Max Tokens`
  - yes
  - already in Nymphs

### Runtime / Loader Controls

These are real, but they belong in `Nymphs Server`, not the shape request box.

- `Attn Backend`
  - real runtime setting
  - community wrapper exposes:
    - `auto`
    - `flash_attn`
    - `xformers`
    - `sdpa`
    - `sageattn`
  - not yet exposed in Nymphs UI
  - worth adding later under TRELLIS runtime settings
- `Vram Mode`
  - wrapper/runtime behavior
  - likely maps to model lifetime / cache policy such as keep loaded vs unload
  - not yet exposed in Nymphs
  - worth investigating later as a runtime setting
- `Precision`
  - exposed in the wrapper loader, even though it was not visible in the screenshot
  - should live with runtime settings, not in the shape panel

### Export / Postprocess Controls

These are real and partly already in Nymphs.

- `Decimation Target`
  - real official TRELLIS export control
  - already in Nymphs
- `Texture Size`
  - real official TRELLIS export control
  - already in Nymphs
- `Remesh`
  - real official TRELLIS export control
  - official export currently hardcodes `remesh=True`
  - worth exposing more deliberately in Nymphs later
- `Backend: cumesh`
  - real implementation detail
  - not a good first user-facing control
  - `cumesh` is part of the internal CUDA postprocess path

### Mesh Cleanup / Blender-Side Controls

These should probably not be shoved into the TRELLIS shape request UI.

- `Remove Small Components`
- `Join Components`
- `Fill Holes`
- `Max Hole Edges`
- `Refine Holes`
- `Clean Mesh`
- `Clean Iterations`
- `Inner Loops`
- `Auto Import`

Recommended placement:

- future `Mesh Cleanup` or `Finish` section in Blender
- or later Blender-side nodes/operators

Important note:

- `Auto Import` behavior already exists implicitly in Nymphs after successful generation
- it is just not currently a user-facing toggle

### Unclear / Custom Control

- `Control After Generate`
  - this was visible in the screenshot
  - it was not identified clearly in the official TRELLIS code or the current local wrapper audit
  - likely custom to that frontend or a renamed local parameter
  - if needed later, inspect that frontend’s source directly before copying it

## What Official TRELLIS Already Does Internally

Official TRELLIS already performs several heavy postprocess steps behind the scenes:

- hole filling
- simplification
- connected-component cleanup
- non-manifold repair
- optional remeshing
- UV unwrapping
- texture baking

Important consequence:

- some controls shown in the other addon are not adding brand-new capability
- they are mostly exposing or steering steps that already happen internally

## UV Unwrap Note

`UV Unwrap` is worth further investigation.

Current state:

- official TRELLIS texturing and export paths already perform UV unwrapping internally
- this currently happens inside the TRELLIS postprocess stack rather than as a user-facing Blender step

Why it matters:

- it may be useful later to expose whether UV generation should be:
  - fully backend-managed
  - Blender-managed
  - or intercepted before final export

Future questions:

- should Nymphs let Blender own UV unwrap for some workflows?
- should TRELLIS unwrap stay internal for the quick path?
- should there be an advanced export mode that preserves more intermediate mesh state?

## Recommended Future Work

### Add Later To `Nymphs Shape`

- TRELLIS `1024` mode in addition to `512` and `1024_cascade`
- possibly a more explicit TRELLIS remesh/export choice

### Add Later To `Nymphs Server`

- `Attn Backend`
- `Precision`
- `VRAM Mode` / keep-loaded policy

### Add Later As Blender-Side Tools

- remove small components
- join components
- fill holes
- refine holes
- clean mesh
- mesh cleanup iterations
- mesh repair / postprocess operators
- optional auto-import toggle

### Investigate Further

- explicit UV unwrap strategy and whether it belongs in Blender, TRELLIS, or both
- whether backend postprocess settings should be curated instead of surfaced raw
- the source of `Control After Generate` if parity with the other addon becomes important

## Current Nymphs Position

Current TRELLIS support in Nymphs is already meaningful:

- official TRELLIS backend adapter works
- TRELLIS shape generation works
- TRELLIS textured export works
- key generation parameters are already exposed in the addon

So the next step is not to clone another frontend blindly.

The better approach is:

- keep true TRELLIS controls
- add only the runtime knobs that matter
- move cleanup-heavy controls into Blender-side tooling where they belong

Important product note:

- the local `ComfyUI` wrapper path was useful as a research harness only
- it should not remain part of the intended shipped distro, launcher, or addon architecture
