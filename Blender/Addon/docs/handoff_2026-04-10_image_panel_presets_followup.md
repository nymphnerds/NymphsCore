# Handoff: Image Panel Presets Follow-Up

Date: 2026-04-10 21:35 BST

## Published State

- Extension `1.1.47` is live on the extension feed.
- Feed URL:
  `https://raw.githubusercontent.com/nymphnerds/NymphsExt/main/index.json`
- Addon source commit for `1.1.47`: `46d48dc`
- Extension feed commit for `1.1.47`: `285aa59`

## Local Changes After 1.1.47

The addon has a small unpublished label-only patch:

- image generation `Guidance` display label changed to `Guide`
- prompt preset row label changed from `Presets` to `Preset`

This should be released as the next extension version.

## Image Panel UX Decisions

- Z-Image is the only image backend in this panel, so the panel should not show backend details.
- Core generation controls should stay visible:
  - prompt
  - seed
  - width / height
  - steps / guide
  - generate image / generate MV set
  - open folder / clear folder
- Negative prompt should stay collapsed by default.
- Helper text should generally move to mouseover descriptions, not visible panel labels.
- `Open Folder` and `Clear Folder` are important and should stay visible in the result area.

## Prompt Editor State

Implemented locally and released in `1.1.47`:

- `Prompt > Edit` opens only the main prompt.
- expanded `Negative Prompt > Edit` opens only the negative prompt.
- the edit popup no longer forces main prompt and negative prompt into the same editor.

## Current Built-In Presets

`1.1.47` replaced the older character-heavy starter list with these asset-focused prompt presets:

- Clean Asset Concept
- Stylized Prop
- Character Asset
- Creature Asset
- Building Asset
- Hard Surface Asset

Presets currently load prompt and negative prompt only. They intentionally do not change seed, width, height, steps, or guide.

## Next Preset Work

Still needed:

- rename the workflow fully from starter language to prompt presets where any old wording remains
- add `Save Preset`
- add `Delete Preset`
- add `Open Presets Folder`
- store presets as hand-editable JSON files
- keep preset JSON prompt-only:

```json
{
  "name": "Stylized Prop",
  "prompt": "...",
  "negative_prompt": "..."
}
```

Do not store panel generation settings in prompt presets.

## Image Editing Note

The current Nunchaku Turbo Z-Image runtime is txt2img-only. The local backend explicitly blocks img2img for Nunchaku.

Standard Z-Image code has img2img paths, but that is not the fast Nunchaku path currently used by default.

User mentioned that a Z-Image edit model is expected soon. Best next step is to wait for that model before adding an `Edit Existing Image` UI, rather than exposing a misleading button now.

Planned future image-edit flow:

- select source image
- write edit prompt
- run Z-Image edit model
- assign edited image to main Image field for TRELLIS / Hunyuan workflows

## TRELLIS / Flash-Attn Status

Earlier TRELLIS shape+texture failed while flash-attn was compiling. At that time no TRELLIS API port was listening.

As of this handoff, flash-attn compile processes are no longer visible in `ps`, but TRELLIS still needs a clean restart and `/server_info` probe before judging shape+texture again.

Recommended next test:

1. Start TRELLIS from Blender.
2. Confirm TRELLIS status reaches ready.
3. Probe `/server_info`.
4. Run one simple image-to-shape+texture job.

