# Handoff - Runtime UI and Python Paths - 2026-04-11

## Current Direction

The Runtimes panel is the technical/configuration surface. The Image, Shape, and Texture panels should stay compact and user-facing.

Important product decisions from the latest UI pass:

- Do not use the old checkbox plus `Start Enabled` runtime flow.
- Each runtime card should have its own clear Start/Stop controls.
- Detailed backend names belong in Runtimes, not repeated in the Image panel.
- Runtime `Config Details` should open immediately below the foldout row.
- Long text fields should use label-above-field layout so labels do not get crushed by narrow Blender sidebar inputs.

## Latest Runtime Layout

Collapsed runtime cards should read roughly:

```text
Z-Image Turbo / Nunchaku r32
Port: 8090    > Config Details
Stopped       Start      Stop
```

Expanded config appears directly under `Config Details`.

## Python Path Change

All runtime backends now expose a configurable Python executable:

- Hunyuan3D-2mv: `~/Hunyuan3D-2/.venv/bin/python`
- Z-Image: `~/Z-Image/.venv-nunchaku/bin/python`
- TRELLIS.2: `~/TRELLIS.2/.venv/bin/python`

The launch command now uses these configured Python paths instead of hardcoding the Hunyuan/Z-Image venv executables.

Why this exists:

- TRELLIS already exposed this field.
- User asked why only TRELLIS had it.
- Consistent config is preferable here because all three backends are local WSL Python services.

## Negative Prompt Cleanup

Negative prompt was removed from the active addon image flow because the current Z-Image Turbo/Nunchaku backend does not pass `negative_prompt` through.

Current state:

- no negative prompt UI in Image panel
- no `negative_prompt` in single-image payloads
- no MV negative prompt helper
- prompt presets save/load only the main prompt

## MV Profile Cleanup

The old built-in `MV Source` profile was removed because it made users think a special profile was required for MV generation.

Current model:

- Prompt Preset: what the subject is
- Profile: speed/quality/size/runtime
- `Generate 4-View MV`: action that creates front, left, right, and back from the current prompt

Old local `turbo_mv_source.json` profile files are ignored so the removed built-in does not come back from seeded JSON.

## Hunyuan Toggle Tooltips

The Hunyuan runtime toggles now have hover descriptions:

- `Shape`: starts Hunyuan3D-2mv shape backend for image/multiview image-to-3D
- `Texture`: also loads Hunyuan3D-Paint, needed for shape+texture/retexture but costs startup time and VRAM
- `Turbo`: uses the distilled `hunyuan3d-dit-v2-mv-turbo` model folder
- `FlashVDM`: enables Hunyuan FlashVDM acceleration for the shape pipeline

Local research references:

- `/home/nymphs3d/Hunyuan3D-2/api_server_mv.py`
- `/home/nymphs3d/Hunyuan3D-2/docs/source/started/gradio.md`
- `/home/nymphs3d/Hunyuan3D-2/docs/source/modelzoo.md`
- `/home/nymphs3d/Hunyuan3D-2/hy3dgen/shapegen/pipelines.py`

## Next Test Focus

Ask the user to test the published build in Blender and check:

- runtime card spacing in collapsed and expanded states
- whether `Port: ####` plus `Config Details` feels balanced
- whether Python Path fields make the expanded runtime config too tall
- whether Hunyuan toggle hover text is clear enough
- whether removing `MV Source` makes the Profile vs Prompt Preset distinction clearer

