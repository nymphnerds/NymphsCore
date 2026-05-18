# TRELLIS Hooks Not Exposed In Nymphs Addon Yet

Date: 2026-04-25

Update:

- The old visible GGUF `Mesh Cleanup` / `Remove Flat Debris` shape-only controls were removed from the addon because they were custom Nymphs postprocess controls, not TRELLIS pass settings.
- The old backend flat-debris helper was also removed from the active GGUF adapter.
- Future cleanup/retopo work should be designed as one explicit `Postprocess / Cleanup / Retopo` system for both shape-only and shape+texture paths.
- Live GGUF Shape + Texture testing still produced a large floor/backdrop plate with `Auto Remove Background` enabled, so cleanup/postprocess is now a high-priority usability gap.
- The background checkbox is wired, but it depends on `rembg`; if the source image retains floor, shadow, or backdrop pixels after preprocessing, TRELLIS can still generate those as mesh geometry.
- See `/home/nymph/Handoffs/trellis_future_mesh_cleanup_retopo.md`.

This note covers the TRELLIS.2 controls and extension points that are available in the current source tree but are not yet exposed in the Blender addon UI/payload. It separates hooks that the Nymphs backend already understands from hooks that exist upstream but still need adapter work.

Relevant source:

- Addon UI and payloads: `/home/nymph/NymphsExt/Nymphs.py`
- Official adapter: `/home/nymph/NymphsCore/Manager/scripts/trellis_adapter/api_server_trellis.py`
- GGUF adapter: `/home/nymph/NymphsCore/Manager/scripts/trellis_adapter/api_server_trellis_gguf.py`
- Official TRELLIS image-to-3D pipeline: `/home/nymph/TRELLIS.2/trellis2/pipelines/trellis2_image_to_3d.py`
- Official TRELLIS texture pipeline: `/home/nymph/TRELLIS.2/trellis2/pipelines/trellis2_texturing.py`
- o-voxel GLB postprocess: `/home/nymph/TRELLIS.2/o-voxel/o_voxel/postprocess.py`

## Already Exposed In The Addon

The addon already exposes these TRELLIS controls:

- Runtime choice: official vs GGUF.
- GGUF quant: `Q4_K_M`, `Q5_K_M`, `Q6_K`, `Q8_0`.
- Source image path.
- Auto background removal.
- Shape plus optional texture generation.
- Shape preset.
- Pipeline type: `512`, `1024`, `1024_cascade`, `1536_cascade`.
- Seed.
- `max_num_tokens`.
- Sparse structure sampler params:
  - `steps`
  - `guidance_strength`
  - `guidance_rescale`
  - `guidance_interval`
  - `rescale_t`
- Shape SLat sampler params:
  - `steps`
  - `guidance_strength`
  - `guidance_rescale`
  - `guidance_interval`
  - `rescale_t`
- Texture SLat sampler params:
  - `steps`
  - `guidance_strength`
  - `guidance_rescale`
  - `guidance_interval`
  - `rescale_t`
- Texture working resolution: `512`, `1024`, `1536`.
- Final texture size: `1024`, `2048`, `4096`.
- Decimation target, currently visible mainly in texture/advanced texture contexts.

## Update: GGUF Hooks Exposed In Addon 1.1.187

Addon `1.1.187` and the matching GGUF adapter update expose the main GGUF hook batch:

- `foreground_ratio`
- `sparse_structure_resolution`
- global/stage sampler overrides: `sampler`, `sparse_structure_sampler`, `shape_sampler`, `tex_sampler`
- GGUF shape export mode: preserve, remesh, auto fallback
- `remesh_resolution`
- `remove_floor_plane`
- textured export remesh controls: `export_remesh`, `export_remesh_band`, `export_remesh_project`
- GGUF retexture material/UV controls: `texture_alpha_mode`, `texture_double_sided`, `texture_bake_vertices`, `texture_custom_normals`, `texture_uv_method`, `texture_uv_angle`, `texture_inpainting`

The project direction is GGUF-first. Official TRELLIS remains useful for comparison but should not shape the advanced UX.

## Backend Hooks Already Available But Not Exposed

These can be added to Blender with relatively small addon changes because the backend already accepts or uses them.

### `foreground_ratio`

Status: GGUF adapter already accepts it for shape generation and retexturing.

Where:

- GGUF shape: `api_server_trellis_gguf.py` reads `payload["foreground_ratio"]`, default `0.85`.
- GGUF retexture: same key, default `0.85`.

Why it matters:

- Controls the crop/padding around the detected foreground after background removal.
- This is one of the best first knobs for the floor-plane/background-junk issue.
- Smaller values crop tighter; larger values keep more surrounding area.

Suggested UI:

- Name: `Foreground`
- Type: float slider.
- Range: `0.60` to `1.00`.
- Default: `0.85`.
- Show for GGUF shape and GGUF texture/retexture.

Payload:

```json
{
  "foreground_ratio": 0.85
}
```

### `remesh_resolution`

Status: GGUF shape-only adapter already accepts it.

Where:

- GGUF shape-only export calls `export_geometry(mesh_with_voxel, payload["remesh_resolution"])`.
- Current default is `768`.

Why it matters:

- This is the closest GGUF shape-only knob to a polycount/detail target.
- Lower is lighter/faster and can reduce enormous exports.
- Higher preserves more surface detail but costs more VRAM/time.
- It is not exactly "face count"; it changes the extraction/remesh grid resolution.

Suggested UI:

- Name: `Remesh`
- Type: enum.
- Values:
  - `512` light/faster.
  - `768` balanced default.
  - `1024` denser/higher detail.
- Show only for GGUF shape-only at first.

Payload:

```json
{
  "remesh_resolution": 768
}
```

### GGUF floor-plane cleanup toggle

Status: removed from the current addon and active GGUF adapter.

Where:

- The previous GGUF adapter helper `remove_floor_like_components()` has been removed from the active adapter.
- The previous UI payload key `remove_floor_plane` should not be revived as a one-off checkbox.

Why it matters:

- This is not a native TRELLIS feature.
- It is a Nymphs GGUF postprocess cleanup for accidental flat base planes.
- Live testing proved this is still needed, but it should return as part of a proper `Postprocess / Cleanup / Retopo` stage that supports both shape-only and shape+texture.

Payload:

```json
{
  "cleanup_mode": "clean",
  "debris_mode": "floor",
  "keep_largest_components": 1
}
```

### `texture_use_remesh`

Status: addon property exists, but payload helper currently ignores it.

Where:

- Addon property: `texture_use_remesh`.
- `_texture_option_payload(state, include_use_remesh=False)` accepts `include_use_remesh`, but does not include the value in the returned dict.

Why it matters:

- The UI already has the concept, but it is not wired through.
- Official texturing pipeline currently does not accept this directly.
- o-voxel `to_glb()` does have a `remesh` option for GLB postprocess, so this is more likely useful for textured export/postprocess than for retexture.

Suggested action:

- Decide whether it should target o-voxel GLB postprocess only, or also a future mesh-preprocess path.
- If kept, wire payload and backend support together in the same change.

Possible payload:

```json
{
  "use_remesh": false
}
```

## Upstream TRELLIS Hooks Available But Adapter Work Needed

These exist in the installed TRELLIS.2 source, but our official/GGUF API wrappers do not currently expose them as request fields.

### Multi-view / multi-image input

Status: not currently available in the official TRELLIS.2 pipeline or Nymphs TRELLIS adapters.

Important distinction:

- The addon has old/partial multi-view image generation support, but that is a separate image-reference workflow.
- TRELLIS shape generation still sends one image as `image`.
- TRELLIS texture/retexture can fall back to `mv_front` only if `image_path` is empty.
- No current adapter sends `front`, `back`, `left`, and `right` together to TRELLIS.

Where the partial addon hooks are:

- View specs: `IMAGEGEN_MV_VIEW_SPECS` in `/home/nymph/NymphsExt/Nymphs.py`
- Multi-view prompt builder: `_build_mv_prompt(...)`
- Old worker for Gemini: `_gemini_mv_worker(...)`
- Old worker for Z-Image: `_imagegen_mv_worker(...)`
- Slot properties:
  - `mv_front`
  - `mv_back`
  - `mv_left`
  - `mv_right`
- TRELLIS image source helper: `_trellis_image_source_path(...)`

Current wiring:

- `NYMPHSV2_OT_generate_image.execute(...)` explicitly sets `state.imagegen_generate_mv = False`.
- `imagegen_generate_mv` is now described as a legacy flag.
- There is no visible operator currently calling `_gemini_mv_worker` or `_imagegen_mv_worker`.
- `_build_shape_payload(...)` calls `_trellis_image_source_path(..., allow_front_fallback=False)`, so shape generation does not use `mv_front`.
- `_build_texture_payload(...)` calls `_trellis_image_source_path(..., allow_front_fallback=True)`, so texture/retexture may use `mv_front`, but not the other views.

Why it matters:

- Multi-view generated reference images could be useful upstream of TRELLIS: generate front/side/back references, then choose the best front view for image-to-3D.
- It is not the same as true multi-view TRELLIS reconstruction.
- True multi-view TRELLIS would need a different model/pipeline or adapter logic that can condition on multiple images.

Suggested action:

- Treat this as an addon workflow feature, not a TRELLIS backend hook.
- If revived, add a visible `Generate Views` action in the image panel.
- Keep `mv_front/back/left/right` as reference slots.
- For TRELLIS shape, either continue using only `mv_front`, or add a separate future multi-view backend only if the model actually supports multi-image conditioning.

Suggested priority: medium for reference-generation convenience, low for TRELLIS backend until there is a real multi-image TRELLIS path.

### `num_samples`

Status: official image-to-3D pipeline supports it; adapters hard-code one sample.

Where:

- `Trellis2ImageTo3DPipeline.run(..., num_samples=1, ...)`.
- Official adapter passes `num_samples=1`.
- GGUF adapter does not expose a sample count.

Why it matters:

- Would let one request generate multiple candidate meshes from one image/seed setup.
- Higher memory and output-handling complexity.

Suggested priority: low until the addon has a multi-result picker.

### `preprocess_image`

Status: official pipeline supports it; addon exposes only `remove_background`.

Where:

- Official pipeline `run(..., preprocess_image=True)`.
- Official texture pipeline `run(..., preprocess_image=True)`.
- Current adapters convert `auto_remove_background` into `preprocess_image` or a custom GGUF preprocess path.

Why it matters:

- Official TRELLIS preprocess does more than plain background removal: it crops and normalizes the alpha/object area.
- Current addon label says background removal, not full TRELLIS crop behavior.
- GGUF adapter uses custom `foreground_ratio`, which is more tunable than official default.

Suggested action:

- Keep current user-facing `Auto Remove Background`.
- Add `Foreground Ratio` for GGUF first.
- Consider a future explicit `Use TRELLIS Crop` advanced toggle only if real test cases need it.

### `return_latent`

Status: official image-to-3D pipeline supports it; adapter discards latents.

Where:

- `Trellis2ImageTo3DPipeline.run(..., return_latent=False)`.

Why it matters:

- Enables preview rendering from latent, reusing latent for later texture experiments, or advanced debugging.
- Could support "generate shape now, texture later without redoing everything" if a persistence format is designed.

Suggested priority: medium/low. Powerful, but needs storage and UI design.

### Conditioning resolution / negative conditioning

Status: lower-level pipeline methods expose this, but `run()` fixes most choices internally.

Where:

- `get_cond(image, resolution, include_neg_cond=True)`.
- The pipeline internally uses `512` conditioning for sparse structure and `1024` conditioning for higher-res shape/texture.

Why it matters:

- Advanced quality/speed experiments could use lower or higher conditioning paths.
- `include_neg_cond=False` would disable negative conditioning and may reduce memory a bit, but sampler assumptions must be checked.

Suggested priority: low. Not a friendly UI knob yet.

### Sparse structure resolution

Status: low-level hook exists; top-level `run()` chooses it from `pipeline_type`.

Where:

- `sample_sparse_structure(cond, resolution, ...)`.
- `run()` maps:
  - `512` -> sparse resolution `32`
  - `1024` -> sparse resolution `64`
  - `1024_cascade` -> sparse resolution `32`
  - `1536_cascade` -> sparse resolution `32`

Why it matters:

- Higher sparse resolution may capture more structure, but can cost much more memory.
- Lower can be faster and simpler.

Suggested action:

- Do not expose until the adapter has a guarded override and tests.
- If exposed, use an expert-only enum such as `Auto`, `32`, `64`.

### Cascade high-resolution token fallback

Status: indirectly exposed through `max_num_tokens`, but not visible as "actual resolution used".

Where:

- `sample_shape_slat_cascade()` reduces target resolution in steps of `128` until token count fits, stopping at `1024`.

Why it matters:

- The addon lets the user set `1536_cascade` and `max_num_tokens`, but does not report whether TRELLIS silently reduced the actual high-res shape resolution.
- This explains some "why did 1536 not look denser?" cases.

Suggested action:

- Add runtime logging or response metadata if possible.
- UI hook is not a setting; it is an observability feature.

## Sampler Hooks Not Fully Exposed

The addon already exposes the main sampler params. These remaining sampler-level hooks are available but not surfaced.

### Sampler implementation/class

Status: configured by pipeline JSON, not exposed.

Where:

- Pipeline loads sampler classes from config:
  - `sparse_structure_sampler`
  - `shape_slat_sampler`
  - `tex_slat_sampler`
- Current installed sampler class is the Euler family.

Why it matters:

- If alternate sampler classes are added to configs or the GGUF extension, this could become a quality/speed switch.
- Right now, the local source mainly exposes Euler/CFG/guidance-interval behavior.

Suggested priority: low unless the GGUF extension adds alternate samplers such as heun/rk variants.

### `verbose` and `tqdm_desc`

Status: available in sampler methods, but adapters let TRELLIS use its defaults.

Why it matters:

- Mostly logging/progress behavior, not output quality.

Suggested priority: skip for UI. Maybe use internally for cleaner logs.

### `pred_x_t` and `pred_x_0`

Status: sampler returns intermediate predictions; pipeline discards them.

Why it matters:

- Useful for research/debug previews, not normal generation.

Suggested priority: skip for now.

## Mesh Export And Postprocess Hooks Not Exposed

These are useful for polycount, texture quality, and mesh cleanliness.

### Shape-only GGUF export resolution

Status: same as `remesh_resolution`; already accepted by GGUF adapter but missing UI.

Recommended first implementation:

- Add `GGUF Remesh Resolution` before adding true face-count controls for GGUF shape-only.

### Official textured export `decimation_target`

Status: backend accepts it; addon exposes it only in texture-related sections.

Where:

- Official adapter `export_textured_mesh(mesh, texture_size, decimation_target)`.
- o-voxel `to_glb(..., decimation_target=1000000, ...)`.

Why it matters:

- This is the real face/vertex budget-style control for textured official exports.
- It does not currently help GGUF shape-only export.

Suggested action:

- Make UI copy clear:
  - `Faces` for textured official export.
  - `Remesh` for GGUF shape-only export.

### o-voxel `remesh`

Status: o-voxel supports it; official adapter does not expose it.

Where:

- `to_glb(..., remesh=False, remesh_band=1, remesh_project=0.9, ...)`.

Why it matters:

- Can rebuild topology before UV baking.
- May improve ugly topology, but can damage delicate geometry.

Suggested UI:

- Advanced texture export toggle: `Remesh Before Texture Export`.
- Default off.

Payload/backend keys:

```json
{
  "export_remesh": false,
  "export_remesh_band": 1.0,
  "export_remesh_project": 0.9
}
```

### o-voxel hole filling threshold

Status: hard-coded in o-voxel postprocess.

Where:

- `mesh.fill_holes(max_hole_perimeter=3e-2)`.

Why it matters:

- Could help open shells or accidental holes.
- Too high can close intentional gaps.

Suggested priority: low. Keep internal unless repeated mesh defects appear.

### Small connected component threshold

Status: hard-coded in o-voxel postprocess.

Where:

- `mesh.remove_small_connected_components(1e-5)`.

Why it matters:

- Could remove floating fragments.
- Too aggressive may remove small accessories.

Suggested priority: medium if user keeps seeing floating fragments.

### UV clustering options

Status: o-voxel exposes them; adapter does not.

Where:

- `mesh_cluster_threshold_cone_half_angle_rad`
- `mesh_cluster_refine_iterations`
- `mesh_cluster_global_iterations`
- `mesh_cluster_smooth_strength`

Why it matters:

- Affects UV charting and texture bake layout.
- Expert-only; bad values may produce worse UVs.

Suggested priority: low. Useful only if texture seams/UV quality become a problem.

### Texture export format/options

Status: official app exports textured GLB with `extension_webp=True`; Nymphs official adapter currently uses its export helper path.

Why it matters:

- WebP textures can reduce GLB size.
- PNG/JPEG choices may matter for Blender compatibility and quality.

Suggested priority: medium after texture path is stable.

Potential UI:

- `Texture Compression`: `Auto`, `WebP`, `PNG`.

## Texturing Hooks Not Exposed

### Texture mesh encoder resolution

Status: already exposed as `texture_resolution`.

Note:

- This controls mesh voxelization/texture generation resolution for retexturing.
- Current valid values are `512`, `1024`, and `1536` in the official adapter, but upstream texturing code really chooses `512` model for `512` and `1024` model for anything else.

### Texture size

Status: already exposed as final texture map size.

### PBR channels

Status: upstream outputs PBR material data:

- base color
- alpha
- metallic
- roughness

Why it matters:

- The addon currently imports the GLB result, but does not expose per-channel controls.
- Potential future options:
  - force opaque alpha
  - metallic multiplier
  - roughness multiplier
  - export base color only

Suggested priority: low/medium for art control after GGUF shape pipeline is stable.

## Runtime/Performance Hooks Not Exposed In Addon

These are backend/runtime choices rather than TRELLIS model settings.

### Attention backend display/control

Status: display was added in the UI through `/server_info`; no user control.

Where:

- GGUF server reports:
  - `attention_backend`
  - `sparse_attention_backend`

Why it matters:

- For the 16 GB 4080 Super target, verifying `flash_attn` is important.
- Exposing a selector is risky because invalid choices can break startup.

Suggested action:

- Keep display-only for now.
- Add a warning if backend is not `flash_attn`.

### GGUF model root

Status: backend supports env `TRELLIS_GGUF_MODEL_ROOT`; addon does not expose it.

Why it matters:

- Useful if models live on a different disk.
- Also useful for testing partial downloads or local snapshots.

Suggested priority: medium for install/settings panel, not generation panel.

### Include texture models in GGUF download/load

Status: backend uses `include_texture=True/False` depending on request.

Why it matters:

- Shape-only should avoid texture model load where possible.
- Shape+texture and retexture require texture GGUF files.

Suggested action:

- Keep automatic.
- In UI, show whether texture GGUF files are present/ready.

## Highest-Value Next Hooks To Add

Recommended first batch:

1. `foreground_ratio`
2. GGUF `remesh_resolution`
3. `remove_floor_plane` payload/UI toggle
4. Clearer face-count vs remesh wording in UI

Recommended second batch:

1. Expose GGUF texture readiness from `/server_info`.
2. Add response/log metadata for actual cascade resolution after token fallback.
3. Wire `texture_use_remesh` only if official textured export needs it.
4. Add optional texture compression/export settings after texture generation is stable.

Avoid for now:

- Sampler class switching.
- Negative-conditioning toggles.
- UV clustering knobs.
- Low-level hole/component thresholds.
- Latent persistence, until the addon has a clear save/reuse workflow.
