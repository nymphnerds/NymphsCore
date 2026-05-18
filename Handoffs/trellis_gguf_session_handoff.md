# TRELLIS.2 GGUF Session Handoff

Date: 2026-04-25

This is the "start here" note for the next Codex session. The goal is to continue the TRELLIS.2 GGUF backend integration for the Nymphs addon, especially optimized 16 GB GPU use, FlashAttention visibility, mesh cleanup, texture support, and a solid preset/UI system.

## User Goal

The user wants a separate TRELLIS.2 GGUF pipeline in the Nymphs addon system, optimized for a 16 GB RTX 4080 Super. The main motivations are:

- Run TRELLIS.2 locally with lower VRAM than the official full model path.
- Use GGUF quantized weights.
- Confirm/use FlashAttention.
- Support shape generation and eventually texturing.
- Expose practical controls in Blender without overwhelming the UI.
- If the GGUF path becomes stable and high quality enough, the user is willing to drop the official path from normal support in favor of out-of-the-box 16 GB VRAM support.
- The desired end state is GGUF-first: useful custom features, more hooks exposed, most technical settings hidden in an Advanced section, and a thoroughly tested preset system.

The user is tired and anxious about git confusion. In the next session, be explicit about which repo owns what.

## Strategic Product Direction

The user's current preference is:

- Make TRELLIS GGUF the primary supported path if shape and texture quality prove reliable.
- Keep official TRELLIS available during validation, especially to compare artifacts and texturing behavior.
- Build the user-facing workflow around tested presets that actually apply immediately.
- Keep normal UI simple and move risky/technical controls into Advanced.
- Treat mesh cleanup/retopo as a future strength of the Nymphs path, but do not let it destabilize the default generation path.
- Preserve geometry by default; make remesh/retopo an explicit advanced mode or fallback.

Important UX note:

- Preset selection now auto-applies. There is no separate Apply button for TRELLIS shape presets.
- The Resolution dropdown remains the actual active pipeline mode (`512`, `1024`, `1024 Cascade`, `1536 Cascade`).
- Future preset work should make the preset system feel "solid": obvious active state, tested recipes, and ideally a Custom/Modified state when current settings no longer match a named preset.
- As of addon `1.1.187`, the Shape panel is preset-first and uses a flat set of peer collapsible sections instead of one nested `Advanced Settings` area.
- Resolution is no longer visible beside Preset by default, because that was confusing. It is available inside the `Generation` section.
- The custom GGUF `Mesh Cleanup` section has been removed from the current addon UI. It was shape-only Nymphs postprocess behavior, not a TRELLIS pass.
- Presets are now intended to be GGUF-first recipes for active controls: resolution, token budget, foreground ratio, sparse resolution, sampler choices, texture size, face target, texture export remesh, UV angle, shape pass, texture pass, and structure/coarse pass values.

## Repo Map

Primary code repos:

- `/home/nymph/NymphsCore`
  - Branch: `trellis2-gguf-backend-prototype`
  - Tracks: `origin/trellis2-gguf-backend-prototype`
  - Purpose: manager/runtime scripts, TRELLIS adapters, packaged manager publish copy.

- `/home/nymph/NymphsExt`
  - Branch: `trellis2-gguf-backend-prototype`
  - Tracks: `origin/trellis2-gguf-backend-prototype`
  - Purpose: Blender addon, extension package, `index.json`.

- `/home/nymph/TRELLIS.2`
  - Purpose: installed Microsoft TRELLIS.2 runtime/test checkout.
  - Not the source owner for Nymphs adapter code.
  - Runtime scripts in `/home/nymph/TRELLIS.2/scripts/` are copied from `NymphsCore`.

Handoff docs:

- `/home/nymph/Handoffs/trellis_unexposed_hooks.md`
- `/home/nymph/Handoffs/trellis_gguf_session_handoff.md` (this file)
- `/home/nymph/Handoffs/trellis_future_mesh_cleanup_retopo.md`

## Update: Mesh Cleanup Removed From Current Addon

The current `Mesh Cleanup` section was removed from the addon UI because it was custom Nymphs GGUF shape-only postprocess behavior, not an actual TRELLIS generation pass.

Current intended state:

- Do not show `Mesh Cleanup` in the Shape panel.
- Do not show `Remove Flat Debris`.
- Do not send the old shape-only cleanup payload fields from the addon.
- The old backend flat-debris helper was removed from the active GGUF adapter.

Future direction:

- Bring cleanup back later as a unified `Postprocess / Cleanup / Retopo` system.
- It must work across shape-only and shape+texture paths, not only shape-only.
- See `/home/nymph/Handoffs/trellis_future_mesh_cleanup_retopo.md`.

Live test note:

- After the cleanup UI was removed, a GGUF Shape + Texture test with `Auto Remove Background` enabled still produced a large flat floor/backdrop plate.
- Quick code review confirms the background-removal checkbox is wired into the GGUF adapter, but the adapter relies on `rembg`; if floor/shadow/backdrop pixels remain after preprocessing, TRELLIS can still generate them as geometry.
- Cleanup/postprocess should be treated as a high-priority future pass for the GGUF path, not as optional polish.

## Update: GGUF Selected-Mesh Retexture Fix

The user tried `Nymphs Texture > Retexture Selected Mesh` on the GGUF runtime and hit an HTTP 500 while the UI showed `GGUF Q5_K_M`. The error text referenced `shape_enc_next_dc_f16c32_fp16` with the upstream default `Q8_0`.

Root cause:

- GGUF shape generation does not need the shape SLat encoder, so earlier shape/texture tests did not hit this.
- Selected-mesh retexture calls `pipeline.texture_mesh(...)`, which calls `encode_shape_slat(...)`.
- The GGUF pipeline's `load_shape_slat_encoder()` loads a non-GGUF support checkpoint and does not pass the selected quant.
- The standalone model-manager shim only searched the GGUF model root, where the support encoder is not present.

Fix:

- `trellis_gguf_common.py` now treats `ckpts/shape_enc_next_dc_f16c32_fp16` as an explicit required GGUF support checkpoint.
- If that checkpoint is already in the local Hugging Face cache, the shim uses it.
- If it is missing, the shim fetches only that support checkpoint from `microsoft/TRELLIS.2-4B`.
- This does not require keeping the official TRELLIS runtime path installed, but the GGUF retexture path still needs this non-GGUF encoder checkpoint because upstream `texture_mesh(...)` encodes the selected mesh into shape SLat before texturing.
- The source helper, packaged Manager helper, and active `/home/nymph/TRELLIS.2/scripts/trellis_gguf_common.py` were synced.
- Verified the shim resolves the shape encoder locally.

Manager ownership update:

- `Manager/scripts/prefetch_models.sh` now fetches the required GGUF support checkpoint during TRELLIS.2 GGUF model prefetch.
- `Manager/scripts/verify_install.sh` now fails if the support checkpoint is missing in local-only verification.
- `Manager/scripts/runtime_tools_status.sh` now reports TRELLIS.2 GGUF models as not ready if the support checkpoint is missing.
- Matching `publish/win-x64/scripts/...` copies were synced.

## Update: Manager Runtime Tools Fetch/Repair Fix

Fresh installer testing showed:

- Runtime Tools reported Z-Image as ready.
- Runtime Tools reported TRELLIS.2 GGUF as `Needs Attention` with detail: `Managed TRELLIS GGUF adapter is missing. Run repair or update the manager package.`
- Pressing the TRELLIS card `Fetch` button ran the all-backend model-only prefetch path.
- That rechecked/downloaded Z-Image and Nunchaku weights, then failed when TRELLIS prefetch could not import `trellis_gguf_common` because the adapter helper was the missing file.

Fix in progress:

- `Manager/scripts/prefetch_models.sh` now accepts `--backend all|zimage|trellis`.
- `prefetch_models.sh` now accepts `TRELLIS_GGUF_QUANT=all` and loops all valid TRELLIS GGUF quants: `Q4_K_M`, `Q5_K_M`, `Q6_K`, and `Q8_0`.
- The Manager installer UI now has a TRELLIS.2 GGUF download selector and defaults to `All quants`, so fresh installs can cache every quant in one pass while runtime smoke/status still use `Q5_K_M` as the concrete launch quant.
- Z-Image card Fetch now calls Z-Image-only prefetch.
- TRELLIS card Fetch now calls TRELLIS-only prefetch when models are missing.
- If TRELLIS detail says the managed adapter or GGUF runtime packages are missing, the card button label becomes `Repair` and syncs the packaged adapter scripts into `~/TRELLIS.2/scripts/` instead of downloading models.
- `runtime_tools_status.sh` now treats either missing adapter file (`api_server_trellis_gguf.py` or `trellis_gguf_common.py`) as the managed adapter missing state.
- Runtime Tools summary and post-fetch success text now require both core backends to be test-ready, not merely model-ready.
- First repair test failed with `mkdir: cannot create directory '/scripts': Permission denied`; the repair command now uses an explicit `/home/<user>/TRELLIS.2/scripts` target path instead of relying on shell-expanded `NYMPHS3D_TRELLIS_DIR`.
- Source script and packaged `publish/win-x64/scripts/prefetch_models.sh` were synced and passed `bash -n`.

Build note:

- This WSL shell cannot execute Windows/.NET binaries (`dotnet` is not installed in WSL and Windows exe interop returns `Exec format error`), so the WPF manager EXE/zip still need a Windows-side rebuild before this fix is present in the installer binary.

## Current Git State

At time of writing:

- `NymphsCore` is clean on `trellis2-gguf-backend-prototype`.
- `NymphsExt` is clean on `trellis2-gguf-backend-prototype`.

Update after 2026-04-25 morning:

- `NymphsCore` commit `5a60537 Allow toggling TRELLIS GGUF floor cleanup` was pushed.
- `NymphsExt` commit `50f5b6d Move TRELLIS runtime selection out of shape panel` was pushed.
- Addon feed now points to `nymphs-1.1.176.zip`.
- TRELLIS Runtime and GGUF Quant are owned by the Runtimes panel only.
- Nymphs Shape now shows a read-only runtime summary and a GGUF `Remove Floor Plane` job option.

Later update from the same morning:

- `NymphsCore` commit `4bae6d7 Prefer official-style GGUF shape export` was pushed.
- `NymphsCore` commit `a372e8a Orient GGUF shape exports for Blender` was pushed.
- `NymphsExt` commit `018ceff Auto apply TRELLIS shape presets` was pushed.
- Addon feed now points to `nymphs-1.1.180.zip`.
- Shape-only GGUF generation worked for the user after the official-style export change.
- The huge plane was gone in the successful shape-only test.
- The model initially lay flat after official-style export, so `a372e8a` added only the Blender-friendly axis transform:
  - `x -> -x`
  - `y -> z`
  - `z -> y`
- Do not reintroduce the old custom remesh as the default just to fix orientation.

Useful commands:

```bash
git -C /home/nymph/NymphsCore status --short --branch
git -C /home/nymph/NymphsExt status --short --branch
```

Recent NymphsCore commits:

- `a372e8a Orient GGUF shape exports for Blender`
- `4bae6d7 Prefer official-style GGUF shape export`
- `01a8424 Tolerate GGUF textured mesh without simplify`
- `5a60537 Allow toggling TRELLIS GGUF floor cleanup`
- `7ad8cc6 Fix TRELLIS GGUF textured mesh export`
- `9f9b76f Strip floor-like components from TRELLIS GGUF meshes`
- `eb3c6ff Add TRELLIS GGUF o-voxel converter fallback`
- `e087afd Patch TRELLIS GGUF DINOv3 extractor`

Recent NymphsExt commits:

- `018ceff Auto apply TRELLIS shape presets`
- `4102a8f Mirror TRELLIS runtime summary in texture panel`
- `59d28cb Use neutral TRELLIS shape preset labels`
- `1db9e3d Trim TRELLIS shape stopped prompt`
- `50f5b6d Move TRELLIS runtime selection out of shape panel`
- `f8bb8d4 Show TRELLIS GGUF attention runtime in UI`

## Blender Extension Feed

Branch feed URL:

```text
https://raw.githubusercontent.com/nymphnerds/NymphsExt/trellis2-gguf-backend-prototype/index.json
```

Current addon package:

- Version: `1.1.180`
- File: `/home/nymph/NymphsExt/nymphs-1.1.180.zip`
- Feed: `/home/nymph/NymphsExt/index.json`
- Hash in feed: `sha256:ff1f3527637ae4df5f1c2a68661f8134e498333ea2b35b8af64112d5d860671c`

If changing addon UI, bump version to the next version, rebuild zip, update `blender_manifest.toml`, update `index.json`, commit, push.

The `zip` command may not be installed. Use Python's zipfile module if needed.

## Current Working Runtime

The user tests in WSL distro:

```text
NymphsCore_Lite
```

Observed launch command from Blender:

```bash
/home/nymph/TRELLIS.2/.venv/bin/python scripts/api_server_trellis_gguf.py --host 0.0.0.0 --port 8094 --python-path /home/nymph/TRELLIS.2/.venv/bin/python --gguf-quant Q5_K_M
```

Check server:

```bash
wsl.exe -d NymphsCore_Lite --user nymph -- bash -lc 'pgrep -af api_server_trellis_gguf.py || true; curl -s http://127.0.0.1:8094/server_info; echo; curl -s http://127.0.0.1:8094/active_task; echo'
```

Stop server to force reload after copying scripts:

```bash
wsl.exe -d NymphsCore_Lite --user nymph -- bash -lc 'pkill -f "scripts/api_server_trellis_gguf.py --host 0.0.0.0 --port 8094" || true'
```

Note: `pkill` may return odd shell codes because it can kill the matching wrapper shell. Verify with `pgrep`.

## Current Backend Behavior

GGUF `/server_info` has reported:

- `backend`: `TRELLIS.2-GGUF`
- `gguf_quant`: `Q5_K_M`
- `attention_backend`: `flash_attn`
- `sparse_attention_backend`: `flash_attn`
- `model_ready`: `true`
- texturing/retexture available according to server info

Addon now displays runtime like:

```text
Loaded: GGUF Q5_K_M / flash_attn
```

## Current Shape UI Direction

The user wants a simpler first-run workflow:

- Image.
- Auto Remove Background.
- Also Generate Texture.
- Preset.
- Generate button.

Technical controls should be hidden by default:

- Resolution/pipeline type.
- Seed.
- Token budget.
- Mesh cleanup.
- Early/shape/texture pass guidance internals.
- Texture size and face target.

Current implementation:

- Addon `1.1.187` removes the nested `Advanced Settings` wrapper and adds the first broad GGUF hook exposure pass.
- The Shape panel now uses flat peer collapsible sections in pipeline order:
  - `Generation`: resolution, seed, tokens, GGUF foreground ratio, sparse structure resolution, global sampler.
  - `Structure Pass`: early sparse/coarse controls plus GGUF sampler override.
  - `Shape Pass`: main shape refinement controls plus GGUF sampler override.
  - `Texture Pass`: texture working resolution, texture sampler, and texture guidance controls, shown only when texture generation is enabled.
  - `Export Settings`: texture size, face target, textured export remesh controls, and GGUF texture material/UV options.
  - `Mesh Cleanup`: GGUF shape export mode, remesh resolution, and flat debris cleanup.
- `Structure Pass` is collapsed by default and appears before `Shape Pass`, matching the actual pipeline order.
- Built-in TRELLIS presets expanded to a broad test matrix of 26 recipes, including preview, low-poly, balanced, texture-heavy, character, hard-surface, noisy-image cleanup, raw/cleanup comparison, high-poly bake source, official comparison, and 1536 experimental variants.

Next preset-system improvements:

- Add a clear `Custom` or `Modified` state when current settings no longer match the selected preset.
- Make presets feel like tested recipes, not just saved values.
- Consider showing a compact read-only summary under the preset such as `512 | Texture 2048 | Faces 500k`, without exposing the full Resolution dropdown by default.
- Separate user preset management (`Save/Delete/Open`) from day-to-day generation, possibly under a small preset menu or advanced preset tools.

## Problems Already Fixed

### DINOv3 extractor mismatch

Error was:

```text
'DINOv3ViTModel' object has no attribute 'layer'
```

Fix:

- In `trellis_gguf_common.py`, monkeypatch `DinoV3FeatureExtractor.extract_features`.
- It uses `self.model.layer` if present, otherwise `self.model.model.layer`.

### o-voxel tiled converter missing

Error was:

```text
cannot import name 'tiled_flexible_dual_grid_to_mesh' from 'o_voxel.convert'
```

Fix:

- In `trellis_gguf_common.py`, if `o_voxel.convert` lacks `tiled_flexible_dual_grid_to_mesh`, add fallback alias.
- Fallback drops `tile_size` and calls `flexible_dual_grid_to_mesh`.

### Huge floor plane

User got a successful GGUF mesh, but it had a huge floor plane.

Fix:

- Added `remove_floor_like_components(mesh)` to `api_server_trellis_gguf.py`.
- Enabled by default unless env `TRELLIS_GGUF_REMOVE_FLOOR_PLANE` is `0`, `false`, or `no`.
- Removes separate connected components that look wide/flat/low like a generated floor.
- Applies to GGUF shape-only export path.

Important limitation:

- This cleanup does not currently apply to the GGUF shape+texture direct `out_mesh.export()` path.
- It only removes separate components, not floor geometry fused into the character.

Latest observation:

- After switching to official-style GGUF shape export, one shape-only result had no huge plane even without the old custom remesh path.
- However, testing with `Remove Floor Plane` off produced a much worse result with broad flat/background debris.
- So the cleanup is not obsolete. It is currently acting more like background/flat-debris cleanup than simple floor removal.
- The user notes the cleanup is effective, but seems to add significant generation/export time.

Likely future rename/design:

- Rename visible concept from `Remove Floor Plane` to something more honest, such as `Clean Flat Debris` or `Remove Floor/Backdrop`.
- Eventually expose cleanup modes:
  - `Off`
  - `Floor`
  - `Backdrop`
  - `Aggressive Debris`
- Keep this under Advanced or a future `Mesh Cleanup / Retopo` section once defaults are stable.

### Old custom GGUF remesh

The old custom GGUF shape-only export path did this:

- Took GGUF mesh vertices/faces.
- Sent them into `cumesh`.
- Built a BVH.
- Ran `cumesh.remeshing.remesh_narrow_band_dc(...)`.
- Exported the remeshed result as GLB.

This was not the official shape-only TRELLIS export style. It was a Nymphs GGUF-only postprocess experiment to get usable GLB output from GGUF `MeshWithVoxel`.

Current conclusion:

- The old remesh path may have caused or worsened the huge plane.
- Official-style export gives better shape results so far.
- Keep remesh only as a fallback/advanced future feature, not default.
- Retopology is still interesting later, but needs a deliberate UI and test matrix.

### GGUF shape-only orientation

After official-style shape export, the model came in lying flat in Blender.

Fix:

- `api_server_trellis_gguf.py` now applies the Blender-oriented axis transform before exporting GLB.
- This keeps official-style export while matching the orientation Blender expects.
- Commit: `a372e8a Orient GGUF shape exports for Blender`.

## Current Addon TRELLIS Controls

The Nymphs Shape panel currently exposes:

- image path
- auto remove background
- also generate texture
- TRELLIS preset
- resolution/pipeline type
- GGUF quant
- seed
- max tokens
- early/sparse pass controls
- shape pass controls
- texture pass controls when texture is enabled
- texture size
- decimation/faces mostly in texture-related UI

Payload builder:

- `_build_shape_payload(state)` in `/home/nymph/NymphsExt/Nymphs.py`
- `_build_texture_payload(state, mesh_b64, mesh_format="glb")`
- `_texture_option_payload(state, include_use_remesh=False)`

Important current gap:

- `_texture_option_payload(... include_use_remesh=True)` accepts the flag but does not include `texture_use_remesh` in payload.

## Current Testing Notes

The user successfully tested GGUF shape-only after addon `1.1.179`/`1.1.180` work:

- Shape generation worked.
- No huge plane in the successful official-style export test.
- A later test with `Remove Floor Plane` disabled was much worse, showing broad flat/background debris.
- Cleanup helps quality but likely adds time.
- Need to verify whether artifacts are GGUF-only by comparing official vs GGUF on the same image/settings/seed where possible.

Recommended test order next:

1. Install/pull addon `1.1.180` or newer.
2. Stop any running TRELLIS runtime in Blender.
3. Start TRELLIS GGUF again from Runtimes or Shape/Texture.
4. Run shape-only GGUF with `Remove Floor Plane` on.
5. Confirm orientation is now upright after `a372e8a`.
6. Run shape-only GGUF with `Remove Floor Plane` off on the same input and note artifacts/time.
7. Run official shape-only on the same input if VRAM allows, to compare whether flat/background artifacts are GGUF-specific.
8. Run GGUF shape+texture after shape-only is confirmed.
9. If shape+texture fails, capture exact error text. Errors after `4bae6d7` are especially useful because they show whether official-style export is working or falling back.

Useful notes to capture per test:

- Runtime: Official or GGUF.
- GGUF quant.
- Preset and active resolution.
- Seed.
- Shape only vs shape+texture.
- `Remove Floor Plane` on/off.
- Whether artifacts appear.
- Whether model stands upright.
- Generation/export time.
- Exact error text.

Latest texture test note:

- User ran GGUF shape+texture and got a mesh, but no visible texture.
- The mesh looked better than shape-only, likely because the shape+texture path still uses `o_voxel.postprocess.to_glb(..., remesh=True)` with cleanup/UV/postprocess, while shape-only uses official-style raw export.
- The backend reported the job as completed, not failed.
- No recent obvious TRELLIS/Blender error log was found under the WSL home.
- The addon is targeting `NymphsCore_Lite` at `http://127.0.0.1:8094`.
- Patch applied locally: GGUF textured export now writes embedded standard PNG-compatible textures by calling `glb.export(..., extension_webp=False)` instead of requiring `EXT_texture_webp`.
- Patch also adds backend print diagnostics showing whether texture attrs and material textures exist during export.
- Source, manager publish copy, and live runtime copy were updated:
  - `/home/nymph/NymphsCore/Manager/scripts/trellis_adapter/api_server_trellis_gguf.py`
  - `/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/win-x64/scripts/trellis_adapter/api_server_trellis_gguf.py`
  - `/home/nymph/TRELLIS.2/scripts/api_server_trellis_gguf.py`
- Old GGUF server on port `8094` was stopped so Blender should launch the patched runtime on the next run.
- Next action: start TRELLIS GGUF from Blender Runtimes or run Shape+Texture again; if no texture appears, capture Blender console/server output containing the new `[trellis-gguf-api] textured export` and `[trellis-gguf-api] textured material` lines.

Follow-up after another shape+texture pass:

- User still saw "great mesh, no texture" in Blender.
- Latest output inspected:
  - `C:\Users\babyj\AppData\Local\Temp\nymphs_shape_outputs\20260425-101202-shape.glb`
  - WSL path: `/mnt/c/Users/babyj/AppData/Local/Temp/nymphs_shape_outputs/20260425-101202-shape.glb`
- The GLB is not geometry-only:
  - `images`: 2 embedded PNGs.
  - `textures`: 2.
  - `materials`: 1.
  - material has `baseColorTexture` and `metallicRoughnessTexture`.
  - mesh primitive is bound to material `0`.
  - no `EXT_texture_webp` extension used/required.
- `trimesh.load(..., force="scene")` reads:
  - `TextureVisuals`
  - `PBRMaterial`
  - base color texture size `(2048, 2048)`
  - metallic/roughness texture size `(2048, 2048)`
  - UV shape `(350730, 2)`
- Extracted base-color image is nonblank:
  - mean RGBA roughly `[48.39, 39.53, 33.09, 254.33]`
  - extrema show real color variation.
- Conclusion: GGUF texture generation and GLB embedding are working for this file. The next issue is Blender import/display/material assignment, not missing texture data from the backend.
- Official/GGUF comparison:
  - Older official textured outputs in the same folder also use 2 images, 2 textures, `baseColorTexture`, `metallicRoughnessTexture`, and one material assigned to the primitive.
  - The newest GGUF output matches that core structure and now uses embedded PNGs instead of WebP, so the GLB itself is valid and Blender-friendly.
- Addon patch applied in `NymphsExt`:
  - Version bumped to `1.1.181`, then refined to `1.1.182`.
  - New build: `/home/nymph/NymphsExt/nymphs-1.1.182.zip`.
  - Feed updated in `/home/nymph/NymphsExt/index.json`.
  - On import, the addon now parses embedded GLB textures, extracts them beside the GLB, creates an explicit Principled BSDF node material, links base color plus metallic/roughness, and assigns it to imported mesh objects.
  - As of `1.1.182`, this is conditional: if Blender's GLTF importer already creates image texture nodes, the addon leaves them alone.
  - This is now a true Blender-side fallback for cases where `bpy.ops.import_scene.gltf(...)` imports geometry but does not visibly wire texture nodes.
- Next likely checks:
  - Install addon `1.1.182`.
  - Run GGUF Shape+Texture again.
  - If Blender's native importer creates texture nodes, no sidecar extraction/status change should be needed.
  - If fallback runs, check whether status says `Mesh imported into Blender with embedded texture material.`
  - Inspect whether sidecar files such as `*-baseColor.png` and `*-metallicRoughness.png` appear beside the GLB in `C:\Users\babyj\AppData\Local\Temp\nymphs_shape_outputs`.
  - If still invisible, inspect imported object's material slots/nodes after the fallback runs.

Possible reason for shape panel collapsing mid-pass:

- Confirmed/fixed in addon `1.1.196`.
- `_sync_shape_texture_state()` was clearing `shape_generate_texture` during transient backend capability refreshes.
- The current code preserves the user's `Also Generate Texture` intent and lets submit-time capability checks decide whether the server can honor it.
- User confirmed the panel no longer collapses mid-pass after this fix.

## Future Mesh Cleanup / Retopo Direction

The user is keen on making mesh cleanup and retopology a real Nymphs advantage once GGUF is stable.

Immediate product decision:

- The o-voxel/remesh postprocess must become available as an option for GGUF shape-only output.
- Reason: shape+texture produced a better mesh than shape-only, likely because shape+texture still goes through `o_voxel.postprocess.to_glb(..., remesh=True)` while shape-only now uses official-style raw export.
- Do not make remesh the only/default path yet. Results depend heavily on the model/input.
- The needed comparison is no longer just `shape-only` vs `shape+texture`; it is:
  - GGUF shape-only, Preserve/raw official-style export.
  - GGUF shape-only, Clean/remesh export.
  - GGUF shape+texture, current o-voxel remesh/UV/texture export.
- This should be exposed as an export/cleanup mode, not hidden behind the texture toggle.

Cleanup feature direction:

- The removed `Remove Floor Plane` / `Remove Flat Debris` behavior was useful but underspecified; it should evolve into a broader GGUF cleanup system.
- Visible naming should avoid implying it only removes a literal floor plane.
- Current addon `1.1.196` does not show the old `Mesh Cleanup` section or `Remove Flat Debris` checkbox.
- A live textured GGUF test still produced a wide floor/backdrop plate with `Auto Remove Background` enabled, so this should return as a proper postprocess system rather than a one-off shape-only toggle.
- Future cleanup controls should distinguish:
  - flat/floor/backdrop component removal,
  - small floating fragment removal,
  - hole filling,
  - remesh/retopo mode,
  - decimation/face target.
- Suggested cleanup modes:
  - `Preserve`
  - `Clean`
  - `Retopo`
- Suggested debris removal modes:
  - `Off`
  - `Floor`
  - `Backdrop`
  - `Aggressive Debris`

Near-term practical controls:

- Target faces / decimation target.
- Cleanup mode instead of a simple floor checkbox.
- Shape-only mesh export mode:
  - `Preserve` / official-style raw export.
  - `Clean` / o-voxel postprocess without aggressive retopo if possible.
  - `Remesh` / o-voxel remesh path.
- Remove small components / floating fragments.
- Fill holes threshold.
- Smooth or weighted normals.
- Foreground crop/padding (`foreground_ratio`) because it may strongly affect background debris.

Preset research notes:

- Official TRELLIS.2 docs/source expose `pipeline_type` values `512`, `1024`, `1024_cascade`, and `1536_cascade`, and `max_num_tokens` is used by cascade shape sampling as a local detail/latent budget, not an API/billing token.
- RunComfy's ComfyUI TRELLIS.2 workflow documentation reinforces the flow split:
  - preprocess image,
  - generate mesh/voxel structure,
  - postprocess/unwrap/rasterize/bake texture,
  - simplify/fill holes/smooth normals/export.
- The ComfyUI workflow also suggests useful recipe families: Simple, Advanced, Only Mesh, Better Texture, High Quality, Max Quality, Lowpoly, MultiView, TextureMesh, and Qwen/RMBG-assisted cleanup.
- Nymphs presets should therefore be tested as complete recipes rather than single setting tweaks.

Advanced/future controls:

- Optional o-voxel or dual-contouring remesh mode.
- Remesh resolution.
- Remesh band/project controls if exposed safely.
- UV unwrap and texture quality controls.
- Optional Blender post-export modifiers: decimate, voxel remesh, smooth/weighted normals, limited dissolve.

Suggested UI:

- Keep normal generation compact.
- Add a collapsed `Mesh Cleanup / Retopo` section later.
- Modes: `Preserve`, `Clean`, `Retopo`.
- Do not make retopo/remesh default until it has a test matrix and known failure cases.

## Important Handoff Doc For Hooks

Read this next:

```text
/home/nymph/Handoffs/trellis_unexposed_hooks.md
```

It documents hooks and hook candidates. As of addon `1.1.187` and the matching GGUF backend update, these are now exposed/wired:

- GGUF `foreground_ratio`.
- GGUF sparse structure resolution override.
- GGUF global/stage sampler overrides: default, Euler, Heun, RK4, RK5.
- GGUF textured export remesh toggle, remesh band, and project-back amount.
- GGUF shape and shape+texture `Faces` target.
- GGUF textured export `UV Angle`.
- GGUF retexture material/UV hooks: alpha mode, double-sided material, bake vertices, custom normals, UV method, UV angle, inpainting.

Removed from the current addon after the settings audit:

- GGUF shape export mode: Auto Fallback, Preserve, Remesh.
- GGUF `remesh_resolution`.
- GGUF `remove_floor_plane` / UI `Remove Flat Debris`.

Still not fully productized:

- `texture_use_remesh`
- multi-view/multi-image status
- `num_samples`
- `preprocess_image`
- `return_latent`
- sparse structure resolution
- cascade actual-resolution reporting
- o-voxel postprocess controls
- texture/PBR controls
- runtime/performance controls

## Multi-View Clarification

There are two similar-sounding ideas:

### True TRELLIS multi-image input

Not currently present in the official TRELLIS.2 pipeline or our adapters.

Shape generation sends one image only.

### Addon multi-view image generation

Partial/dormant code exists in addon:

- `IMAGEGEN_MV_VIEW_SPECS`
- `_build_mv_prompt(...)`
- `_gemini_mv_worker(...)`
- `_imagegen_mv_worker(...)`
- `mv_front`, `mv_back`, `mv_left`, `mv_right`

But:

- `NYMPHSV2_OT_generate_image.execute(...)` sets `state.imagegen_generate_mv = False`.
- `imagegen_generate_mv` is described as a legacy flag.
- No visible operator currently calls the MV workers.
- TRELLIS shape does not use `mv_front`.
- TRELLIS texture/retexture can fall back to `mv_front` only.

Do not describe this as a working TRELLIS multiview reconstruction feature.

## Current GGUF Hook Implementation

Addon `1.1.187` and the synced GGUF API script expose the main practical GGUF hooks in Blender. The UI is intentionally GGUF-first; official TRELLIS remains useful for comparison, but no longer drives the advanced control layout.

Suggested UI names:

- `Foreground`
  - Float slider.
  - Range: `0.60` to `1.00`.
  - Default: `0.85`.
  - GGUF shape/retexture.

- `Remesh`
  - Enum: `512`, `768`, `1024`.
  - Default: `768`.
  - GGUF shape-only.

- `Remove Floor Plane`
  - Bool.
  - Default: true.
  - GGUF shape-only first.

Suggested payload keys:

```json
{
  "foreground_ratio": 0.85,
  "remesh_resolution": 768,
  "remove_floor_plane": true
}
```

## Files To Edit Next

Addon:

- `/home/nymph/NymphsExt/Nymphs.py`
  - Add properties near existing TRELLIS properties.
  - Add payload fields in `_build_shape_payload`.
  - Add `foreground_ratio` to `_texture_option_payload` if useful for GGUF retexture.
  - Draw new controls in shape panel only when GGUF runtime is selected.

Backend:

- `/home/nymph/NymphsCore/Manager/scripts/trellis_adapter/api_server_trellis_gguf.py`
  - Change `remove_floor_like_components(mesh)` to allow payload-controlled enable/disable.
  - Pass `remove_floor_plane` from `run_shape_request()` into `export_geometry()`.

Publish copy:

- `/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/win-x64/scripts/trellis_adapter/api_server_trellis_gguf.py`

Live runtime copy:

- `/home/nymph/TRELLIS.2/scripts/api_server_trellis_gguf.py`

## Build/Package Notes

When addon changes are ready:

1. Update `/home/nymph/NymphsExt/blender_manifest.toml` version.
2. Build new zip in `/home/nymph/NymphsExt`.
3. Update `/home/nymph/NymphsExt/index.json`.
4. Commit and push `NymphsExt`.

Possible Python zip command shape:

```bash
python3 -m zipfile -c nymphs-1.1.176.zip Nymphs.py __init__.py blender_manifest.toml prompt_presets
```

Then:

```bash
sha256sum /home/nymph/NymphsExt/nymphs-1.1.176.zip
wc -c /home/nymph/NymphsExt/nymphs-1.1.176.zip
```

For backend changes:

1. Patch source in `NymphsCore`.
2. Copy to manager publish copy.
3. Copy to `/home/nymph/TRELLIS.2/scripts/`.
4. Commit and push `NymphsCore`.
5. Stop GGUF server.

## User Testing Checklist

Ask the user to test:

- GGUF shape-only with `Q5_K_M`, `1024 Cascade`, tokens around `49152`.
- Confirm UI says `GGUF Q5_K_M / flash_attn`.
- Try same input with:
  - `Foreground 0.75`
  - `Foreground 0.85`
  - `Remesh 512`
  - `Remesh 768`
  - `Remove Floor Plane` on/off if the asset has a deliberate base.
- Check whether the accidental floor plane is gone.
- Check whether mesh detail/polycount feels acceptable.
- Then test `Also Generate Texture` separately because textured GGUF path has different export behavior.

## Things To Be Careful About

- Do not use the Hugging Face token the user pasted earlier. It should be considered compromised; if mentioned, tell them to revoke/regenerate it.
- Do not revert user changes.
- Be calm and explicit about repo ownership.
- The user was worried that TRELLIS edits were uncommitted or in the wrong repo. Reassure with actual paths and branch status.
- Do not call the addon MV code "TRELLIS multiview" unless a real multi-image TRELLIS pipeline is added.
- The floor-plane cleanup is a Nymphs GGUF postprocess feature, not a native TRELLIS feature.

## Quick Status Answer For Next Session

If the user asks "where are we?", say:

```text
We have the GGUF backend prototype working on the test branch. Blender can install addon 1.1.175 from the branch feed, launch TRELLIS.2-GGUF with Q5_K_M, and the server reports flash_attn. The main next step is exposing three practical GGUF controls in the addon: foreground ratio, remesh resolution, and remove floor plane. The repos are clean on trellis2-gguf-backend-prototype.
```
