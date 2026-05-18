# TRELLIS Future Mesh Cleanup / Retopo Handoff

Date: 2026-04-25

## Decision

Remove the current `Mesh Cleanup` section from the addon for now.

Reason:

- It was custom Nymphs GGUF postprocess behavior, not a real TRELLIS generation pass.
- It only applied cleanly to shape-only export.
- Showing it beside `Structure Pass`, `Shape Pass`, and `Texture Pass` made it look like part of TRELLIS sampling, which was misleading.
- `Remove Flat Debris` was especially confusing because it was a narrow shape-only component filter, not a general cleanup system.

Current intended product state:

- No visible `Mesh Cleanup` section in the Shape panel.
- No visible `Remove Flat Debris` toggle.
- The addon should not send shape-only cleanup payload fields by default.
- The old backend flat-debris helper has been removed from the active GGUF adapter.

Follow-up from live textured GGUF testing:

- A textured Shape + Texture run with `Auto Remove Background` enabled still produced a large flat floor/backdrop plate.
- This does not mean the checkbox is unwired; the current GGUF adapter does call `rembg` during preprocessing.
- It means image background removal is not enough for hard source images where floor, shadow, or backdrop remain connected to the subject.
- Treat this cleanup work as important for GGUF usability, not just polish.

## Future Goal

Bring cleanup back later as a deliberate Nymphs advantage, but as a unified cleanup/retopo pass that works across:

- GGUF shape-only output.
- GGUF shape plus texture output.
- Future texture/retexture outputs where technically safe.

This should feel like a real postprocess/export stage, not a hidden side effect of shape generation.

## Future UI Direction

Do not restore the old simple checkbox.

Possible section name:

- `Postprocess`
- `Postprocess / Retopo`
- `Cleanup / Retopo`
- `Export Cleanup`

Suggested high-level mode:

- `Off`
- `Clean`
- `Retopo`
- `Custom`

Suggested debris mode:

- `Off`
- `Floor`
- `Backdrop`
- `Floating Fragments`
- `Aggressive`

The UI should make it obvious this happens after generation and before final import/export.

## Future Functions

Candidate controls for a real cleanup system:

- Remove flat floor/backdrop components.
- Remove small connected components.
- Keep largest component only.
- Preserve selected number of largest components.
- Fill holes by threshold.
- Smooth normals.
- Weighted/custom normals.
- Decimate to target faces.
- Remesh/retopo target resolution.
- Optional o-voxel remesh for shape-only.
- Optional pre-bake geometry cleanup for textured export.
- Optional post-GLB cleanup only if it preserves material/texture data.

## Technical Notes

Shape-only cleanup is simpler:

- Convert generated vertices/faces to `trimesh`.
- Split connected components.
- Remove components based on geometric heuristics.
- Export GLB.

Shape plus texture needs a different implementation:

- Prefer cleaning geometry before `o_voxel.postprocess.to_glb(...)`.
- Clean `vertices` and `faces`.
- Preserve texture volume data such as `attrs`, `coords`, `layout`, and `voxel_size`.
- Let `to_glb(...)` UV unwrap and bake only the cleaned geometry.

Postprocess note:

- A final-GLB postprocess pass can still be useful for operations that are safe after export, such as normals, metadata, import cleanup, or maybe component filtering if material and texture references survive intact.
- Prefer pre-bake geometry cleanup for textured export when deleting faces/components, because it avoids baking texture onto geometry that will be removed.
- Treat final-GLB deletion as a fallback or separate mode, and test material/UV preservation carefully.

## Current Backend Reminder

The old shape-only `remove_floor_like_components(...)` prototype was removed from the active GGUF adapter. Reintroduce cleanup later as a designed API, not by reviving the old checkbox one-for-one.

If this system returns, update the backend API with explicit payload fields such as:

```json
{
  "cleanup_mode": "clean",
  "debris_mode": "floor",
  "small_component_threshold": 0.00001,
  "keep_largest_components": 1,
  "fill_holes": false,
  "target_faces": 500000,
  "retopo_mode": "off"
}
```

## Testing Plan

When revived, test every mode against:

- Shape-only preserve/raw export.
- Shape-only remesh/retopo export.
- Shape plus texture export with texture baking.
- Character with thin details.
- Prop with intentional base/platform.
- Object with floating accessories.
- Noisy reference image with broad background debris.

The important pass/fail rule:

- Cleanup must not silently delete intentional bases, weapons, hair strands, small accessories, or texture data.
