# Handoff: Addon UI, Parts Runtime, and 5090 Test Prep

Date: 2026-04-13

Owner context:
- current local focus is shifting back to installer work
- addon/runtime state should be preserved so another tester can validate on a `5090`

## Current addon build

- latest local addon zip:
  - `/home/nymphs3d/Nymphs3D-Blender-Addon/dist/nymphs3d2-1.1.101.zip`
- sha256:
  - `309d2b70df9570b750c781652af51e2272ecbc20c1c41e3cc06ec7c8365aa7b8`

## Current state summary

- `Nymphs Parts` is no longer a placeholder-only panel.
- local `P3-SAM` and `X-Part` execution paths are wired through the addon.
- a clean official Parts env exists at:
  - `~/Hunyuan3D-Part/.venv-official`
- the old mixed Parts/Hunyuan venv situation was a real problem and is no longer the intended runtime path.
- the addon now has better per-feature runtime controls and less confusing panel nesting.

## X-Part state

- local small-lane `X-Part` completion has been proven.
- the export crash was narrowed down and improved by lowering export chunking in the local Parts repo.
- the current practical local preset on the 16 GB machine was:
  - `steps=20`
  - `octree=256`
  - `max boxes=6`
  - `cpu threads=16`
  - `float32`
- official-like heavier settings still need broader validation.

## Texture panel state

Texture is now intentionally simplified to the real supported lanes:

- `Hunyuan 2mv`
  - multiview image-guided texturing
  - uses `Front / Back / Left / Right`
- `TRELLIS.2`
  - single-image texturing
  - uses one guidance image

Important:
- no texture-method dropdown anymore
- no text-to-texture lane in the texture panel
- `Z-Image` remains the text-to-image source lane instead

## Status / feedback state

Recent work cleaned up status routing:

- feature panels now expose direct `Start / Stop` runtime controls
- stale Parts progress should no longer leak into unrelated TRELLIS or runtime-launch status
- Parts progress/status now keys off an actual active Parts run instead of generic busy state

This area still needs real testing under load because it changed a lot in one sweep.

## Main testing goals for the 5090 pass

1. Install `nymphs3d2-1.1.101.zip` and check panel structure.
2. Verify `Image`, `Shape`, and `Texture` all show sane local `Start / Stop` controls.
3. Verify runtime status does not cross-contaminate:
   - no stale Parts stage shown during TRELLIS launch
   - no unrelated texture/image status shown during Parts runs
4. Validate texture panel behavior:
   - `Hunyuan 2mv` shows multiview slots
   - `TRELLIS.2` shows a single image field
5. Validate Parts behavior:
   - Stage 1 import visibility
   - Stage 2 hiding of Stage 1 segmented mesh
   - result collection behavior
6. Re-test `X-Part` with stronger hardware and see whether official-like settings become viable without the current compromise preset.

## Specific 5090 questions worth answering

- does `X-Part` still need the reduced local survival settings, or can it handle much closer-to-official defaults?
- does the current export chunk reduction remain necessary on the `5090`?
- are there any runtime-status regressions that only appear when multiple backends are started and stopped repeatedly?
- does the texture panel feel clear enough with only:
  - `Hunyuan 2mv`
  - `TRELLIS.2`

## Known local follow-up areas

- installer work is still ongoing elsewhere
- addon UI likely still needs another cleanup pass after broader testing, but the current goal is validation, not more speculative redesign
- if the next screenshot reveals the same “nested redundant panel” pattern in `Image` or `Shape`, flatten those holistically rather than as one-off fixes

## Files most recently touched for this handoff

- addon UI/runtime logic:
  - `/home/nymphs3d/Nymphs3D-Blender-Addon/Nymphs3D2.py`
- addon manifest:
  - `/home/nymphs3d/Nymphs3D-Blender-Addon/blender_manifest.toml`
- addon changelog:
  - `/home/nymphs3d/Nymphs3D-Blender-Addon/CHANGELOG.md`
- X-Part settings guide:
  - `/home/nymphs3d/Nymphs3D-Blender-Addon/docs/NYMPHS_XPART_SETTINGS_GUIDE.md`

## Practical recommendation

Do not start the next round by adding more UI options.

The addon is finally at a point where it needs external validation more than more local complexity. The next useful information is:
- how it behaves on the `5090`
- which X-Part settings become practical there
- whether the new status/runtimes model feels trustworthy in real use
