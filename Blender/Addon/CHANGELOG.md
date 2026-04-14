# Nymphs3D2 Changelog

This changelog lives in the addon repo because `Nymphs3D2` is now the Blender-side product surface.

This file covers the project from the earliest local Hunyuan handoffs through the current packaged extension state, not just the history of this repo itself.

## High-Level Project Arc

Across the full documented history, the project moved through seven phases:

1. official Tencent WSL baseline, CUDA repair, and `2.0 MV` addon patching
2. personal forks, custom API behavior, and a repeatable local machine setup
3. Windows launcher work, text-bridge support, and Blender handoff stabilization
4. one-click installer, lockfile installs, and backend progress/texture recovery work
5. `Nymphs3D2`, Blender-first workflow positioning, and texture-path/product-surface audit
6. repo split into a backend/helper repo and a separate Blender addon repo
7. packaging `Nymphs3D2` as a proper Blender extension with bundled dependencies and GitHub-friendly install flow

## Detailed Timeline

Newest entries first.

### 2026-04-13 panel responsiveness pass and manager-aligned runtime defaults
Source: follow-up Blender-side cleanup after the TRELLIS/Hunyuan backend selector fixes and the managed installer rollout
Context: the addon had the right feature surface, but it was starting to feel heavier than it should during normal sidebar use. The managed runtime target had also changed to the installer-created `NymphsCore` distro and `nymph` user, so the addon defaults needed to match the real installed environment.

Documented changes:

- bumped the packaged addon version to `1.1.109`
- updated the addon default WSL target to the managed installer runtime:
  - distro: `NymphsCore`
  - user: `nymph`
- corrected backend defaults and ordering so both feature panels now lead with `TRELLIS.2`
- updated `Nymphs Shape` so the backend selector is explicit and ordered:
  - `TRELLIS.2`
  - `Hunyuan 2mv`
- updated `Nymphs Texture` so the backend selector also defaults to and orders as:
  - `TRELLIS.2`
  - `Hunyuan 2mv`
- added a real top-level foldout for the `Nymphs Parts` panel instead of always drawing its work
- made the main addon panels collapsed by default:
  - `Server`
  - `Image Generation`
  - `Shape Request`
  - `Texture Request`
  - `Nymphs Parts`
- changed collapsed panels to early-return before running heavier UI work
- reduced idle redraw pressure by stopping the unconditional one-second full `VIEW_3D` refresh loop when nothing is active
- kept frequent redraws only while launches, jobs, or Parts runs are actually active
- added short-lived caching around redraw-time expensive lookups:
  - WSL distro enumeration
  - image prompt preset folder scans
  - image settings preset folder scans
  - TRELLIS preset folder scans
  - Parts repo/python status checks
- aligned the source `bl_info` version with the packaged extension version so source metadata and extension metadata no longer drift

Why it matters:

- the addon should feel noticeably less sticky during normal panel browsing, especially on Windows when WSL path checks are involved
- the Parts panel no longer taxes the UI when you are not actively using it
- fresh installs now point at the managed `NymphsCore` runtime by default instead of older local assumptions
- the visible backend choices now match the intended product direction:
  - `TRELLIS.2` first for shape and single-image texture work
  - `Hunyuan 2mv` available as the alternate lane

### 2026-04-13 Parts stabilization, runtime-status cleanup, and texture-panel simplification
Source: iterative Blender-side testing after the first working `Hunyuan3D-Part` passes and a broad UI/status cleanup sweep
Context: the addon had reached a point where `P3-SAM` and `X-Part` could run, but the feedback model, panel structure, and texture-path UI were still inconsistent. Recent work focused on making the addon testable rather than adding more hidden complexity.

Documented changes:

- added a dedicated `Nymphs X-Part` settings guide:
  - `docs/NYMPHS_XPART_SETTINGS_GUIDE.md`
- documented the practical local X-Part preset that actually worked on the current 16 GB machine:
  - `steps=20`
  - `octree=256`
  - `max boxes=6`
  - `cpu threads=16`
  - `float32`
- updated the addon defaults toward the documented X-Part path and later refined them based on actual survival-lane testing
- rebuilt the Parts panel into clearer collapsible sections:
  - `Experimental Backend`
  - `Stage 1: Analyze Mesh`
  - `Stage 2: Generate Parts`
  - `Import & Storage`
  - `Run Status`
- removed the redundant top-level `Parts Workflow` wrapper so the real sections are directly visible
- improved Parts progress reporting so the addon can show:
  - PID
  - stage
  - detail
  - progress
  - log path
- fixed the misleading “hung on pre-generation CUDA checks” feel by pairing addon parsing with better X-Part phase lines in the local Parts repo
- fixed Parts import behavior so new imported results are explicitly unhidden
- fixed Stage 1 / Stage 2 hide behavior:
  - P3-SAM can hide the original source mesh when requested
  - successful X-Part import can hide the prior P3-SAM segmented mesh instead of leaving both visible
- fixed a false `Python: Missing` report for the Parts env by using a more Blender/WSL-friendly path existence check
- added Runtimes-style `status | Start | Stop` strips directly into the `Image`, `Shape`, and `Texture` panels
- corrected the inactive-state flow so stopped backends show the start/stop strip immediately, not behind a useless nested foldout
- kept the same start/stop strip visible after launch so users can stop feature backends from the feature panels instead of going back to `Runtimes`
- fixed a status-routing bug where stale Parts stages such as `Diffusion Sampling` could leak into later TRELLIS or backend-launch status
- made Parts status participate only when a real Parts run is active, instead of piggybacking on generic busy state
- simplified the `Nymphs Texture` panel:
  - removed the redundant inner `Texture Request` foldout
  - made backend selection explicit
  - stopped inheriting texture behavior implicitly from the current 3D target
- split texture texturing into the two real supported lanes only:
  - `Hunyuan 2mv` = multiview image-guided texture lane
  - `TRELLIS.2` = single-image texture lane
- removed the extra texture method dropdown after confirming it only made the panel more confusing
- made the texture backend switch render as direct buttons instead of another dropdown
- built local test zips through `1.1.101` while stabilizing the UI and feedback model

Why it matters:

- the addon is now much closer to a coherent Blender product surface instead of a set of powerful but inconsistent experimental cards
- the feature panels can now launch and stop their own required runtimes without forcing users back into the global `Runtimes` panel for every action
- status feedback is less likely to mix unrelated jobs together, which was becoming a real trust problem during testing
- the texture panel now matches the actual product model:
  - `2mv` for multiview texture guidance
  - `TRELLIS` for single-image texture guidance
- the current packaged addon is now in a reasonable state for broader hardware testing, including the planned `5090` validation pass

### 2026-04-11 Nymphs Parts P3-SAM bring-up
Source: first real `Hunyuan3D-Part` prototype pass after the texture-panel and workflow planning work
Context: `Nymphs Parts` had started as a panel prototype only. The next requirement was to prove that `P3-SAM` could actually run on the current local machine, find a viable memory lane, and expose that path in the addon without pretending `X-Part` was equally ready.

Documented changes:

- added the first real `Nymphs Parts` addon integration surface in the local addon branch
- kept both experimental backend choices visible in the panel:
  - `P3-SAM`
  - `X-Part`
- added explicit parts backend config in the addon:
  - repo path
  - python path
  - source preparation
  - result/output state
- added low-memory `P3-SAM` controls in the addon:
  - `Points`
  - `Prompts`
  - `Prompt Batch`
- added a dedicated local `Hunyuan3D-Part` venv at `~/Hunyuan3D-Part/.venv-official`
- layered that venv over the existing Hunyuan torch stack for the first prototype instead of duplicating a full new multi-GB runtime
- installed and validated the core public `P3-SAM` dependencies, including:
  - `scikit-learn`
  - `fpsample`
  - `addict`
  - `easydict`
  - `pytorch-lightning`
  - `spconv-cu124`
  - `torch_scatter` for `torch 2.11.0+cu130`
- built the local `chamfer_3D` CUDA extension for the current GPU arch
- patched `P3-SAM` cache paths so local downloads land under the user cache instead of `/root`
- patched Sonata model loading to fall back cleanly when `flash_attn` is absent
- recorded `flash-attn` as the intended finished runtime for `Hunyuan3D-Part` if feasible later
- fixed two upstream `P3-SAM` memory/usability problems:
  - removed unconditional `DataParallel` duplication on single-GPU runs
  - removed the internal hard reset that ignored requested `point_num` / `prompt_num`
- proved a working `P3-SAM` low-memory lane on the bundled sample mesh with:
  - `point_num=30000`
  - `prompt_num=96`
  - `prompt_bs=4`
- added a stable wrapper command for later addon execution:
  - `~/Hunyuan3D-Part/scripts/run_p3sam_segment.py`
- verified exported outputs from that wrapper, including:
  - segmented GLB
  - segmented PLY
  - AABB GLB
  - face-id numpy output
  - JSON summary
- wired the addon `Nymphs Parts` action so `P3-SAM` now launches a real background WSL job instead of only showing prototype placeholder text
- kept `X-Part` visible in the panel but still intentionally non-executing because the public release remains incomplete
- added a working user guide for the current `P3-SAM` test flow:
  - `docs/NYMPHS_PARTS_P3SAM_GUIDE.md`
- fixed Windows Blender detection of WSL-only parts repo paths by checking Blender-accessible WSL UNC path candidates
- fixed the first addon-launched `P3-SAM` worker path so snapshot dict state is handled correctly instead of being read like live Blender property state
- added per-run `Nymphs Parts` log capture so backend failures are written into each output folder as `nymphs_parts_run.log`
- fixed Windows temp mesh path handoff into WSL by converting `C:\...` prepared-source paths to `/mnt/c/...`
- removed the fragile shell `MESH_PATH` indirection and passed the prepared mesh path directly to the `P3-SAM` wrapper
- built local test packages through `1.1.71` while iterating on the first real Blender-launched parts run
- completed the first successful Blender-launched `P3-SAM` segmentation run from a selected mesh:
  - prepared mesh: Windows temp exported GLB
  - output folder: `~/.cache/hunyuan3d-part/outputs/20260411-161212-P3-SAM-geometry_0.001`
  - output files included `p3sam_segmented.glb`, `p3sam_segmented.ply`, AABB outputs, face-id numpy output, `summary.json`, and `nymphs_parts_run.log`
  - run settings: `point_num=30000`, `prompt_num=96`, `prompt_bs=4`
  - input mesh size: `714151` faces, `357053` vertices
  - wrapper summary reported `part_count=2`

Why it matters:

- `Nymphs Parts` is no longer just a speculative UI branch; it now has a proven real runtime lane through `P3-SAM`
- the project now has a tested part-segmentation path that fits the current machine instead of only upstream defaults that exceed VRAM
- addon integration can now move forward from a real command/output contract rather than a research-only mock surface
- `X-Part` remains part of the plan, but the current product surface now distinguishes clearly between:
  - the practical first public integration path
  - the still-experimental decomposition lane
- Windows Blender to WSL path handling is now proven for the prepared mesh handoff, which was the first blocking issue for real user testing
- the next Parts work is now quality and UX evaluation rather than basic launch plumbing

### 2026-04-11 runtime card and image-panel cleanup
Source: iterative Blender UI testing around the image panel and Runtimes panel
Context: the Z-Image/Nunchaku path made negative prompts misleading, the MV Source profile blurred the difference between profiles and actions, and runtime config fields needed clearer, consistent advanced controls.

Documented changes:

- bumped the packaged addon through `1.1.64`
- removed negative prompt UI and request payload plumbing from the Z-Image image-generation flow
- stopped building and sending MV negative prompts because MV images use the same Z-Image Turbo/Nunchaku backend
- widened Profile and Prompt Preset dropdowns by moving their Apply/Load actions next to the section labels
- moved prompt `Text Editor`, `Edit`, and `Clear` into a more compact prompt control layout
- removed the built-in `MV Source` generation profile and ignored stale seeded `turbo_mv_source` profile JSON
- renamed the MV image button to `Generate 4-View MV` and clarified that Variants only affect single-image generation
- simplified the Runtimes panel by removing the checkbox/`Start Enabled` workflow
- gave runtime cards clearer backend names, compact `Port: ####` rows, and direct Start/Stop controls
- made runtime `Config Details` open directly under the foldout row
- changed runtime config fields to label-above-field layout so long paths do not crush labels
- added configurable Python executable paths for all runtimes:
  - `Hunyuan Python`
  - `Z-Image Python`
  - `TRELLIS Python`
- added hover descriptions for the Hunyuan launch toggles:
  - `Shape`
  - `Texture`
  - `Turbo`
  - `FlashVDM`

Why it matters:

- the image panel no longer advertises negative-prompt behavior that the active Nunchaku backend ignores
- users no longer need a confusing MV-specific profile to generate front/left/right/back images
- runtime configuration is more consistent across Z-Image, TRELLIS, and Hunyuan3D-2mv
- the Runtimes panel is now the explicit home for detailed backend names and launch configuration

### 2026-04-10 enum callback default fix
Source: Blender extension registration error on `imagegen_prompt_preset`
Context: Blender does not allow a string `default` value on `EnumProperty` when `items` is provided by a callback. The preset dropdown was still doing that, so `NymphsV2State` could not register even after the preset loader itself was made safer.

Documented changes:

- bumped the packaged addon version to `1.1.52`
- removed the invalid string default from the callback-backed preset enum
- added preset-key normalization so the current preset resolves to the shipped default or first available preset when the stored value is blank or stale
- updated load/save/delete and panel draw paths to keep the selected preset synchronized with the available preset set

Why it matters:

- Blender should stop rejecting `imagegen_prompt_preset` during class registration
- the preset dropdown remains dynamic and file-backed without depending on an invalid enum default declaration
- stale or missing preset selections should fall back cleanly instead of wedging addon startup

### 2026-04-10 reload-safe Blender registration fix
Source: Blender extension reload failure after the preset registration fix was published
Context: after fixing the enum-preset registration issue, Blender could still fail when reloading the extension metadata because `NymphsV2State` was already registered from the previous module instance, which blocked the next `register()` call with an "already registered as a subclass" error.

Documented changes:

- bumped the packaged addon version to `1.1.51`
- made addon registration clear any stale `Scene.nymphs3d2v2_state` pointer before rebuilding the module state
- made class registration reload-safe by unregistering any existing Blender class with the same name before registering the current module's class objects
- switched addon unregistration to the same safe cleanup path so stale reload state does not linger across extension refreshes

Why it matters:

- Blender extension metadata reloads should stop failing on stale class state
- the preset fix from `1.1.50` is still present, but the addon should now survive extension refresh/update cycles cleanly
- local extension installs are less likely to get wedged in a half-registered state after a failed reload

### 2026-04-10 prompt preset registration fix
Source: Blender extension startup failure after the image-panel preset system landed
Context: the new prompt preset dropdown could fail during extension registration because the enum items callback depended on preset-folder JSON state too early, which made Blender reject `NymphsV2State` with an `EnumProperty` registration error.

Documented changes:

- bumped the packaged addon version to `1.1.50`
- changed the prompt preset loader so built-in shipped presets are always available in memory before any filesystem-backed custom presets are read
- guarded preset-folder seeding/loading so registration no longer depends on the preset directory being readable during class registration
- kept JSON-backed custom prompt presets layered on top of the shipped defaults instead of removing that workflow

Why it matters:

- the extension should register cleanly again on startup
- the prompt preset system no longer depends on user config folder state just to let Blender load the addon
- custom presets remain supported without making addon registration brittle

### 2026-04-10 prompt-flow simplification pass
Source: addon UX cleanup after real prompt-writing feedback
Context: the prompt starter system was technically working, but the panel still felt more fragmented than it needed to. Negative prompt, seed, and MV stance controls were crowding the main writing surface, and the starter workflow was not reading as one coherent action.

Documented changes:

- bumped the packaged addon version to `1.1.42`
- kept direct inline editing for the main prompt
- changed the starter action to read as `Apply` rather than a vague `Load`
- simplified starter labels so the dropdown reads more like a practical recipe list
- added starter description text in the panel so the current selection explains itself
- moved negative prompt, seed, and MV-set stance under one `Optional Controls` foldout
- added `Use Base` for the shipped default negative prompt
- clarified that MV-set stance only affects `Generate MV Set`
- kept the larger editor for copy/paste work without forcing previews into it

Why it matters:

- the panel should feel more like one prompt workflow instead of several partial systems
- the main writing area stays visible while the less-common controls stay available
- users get a clearer explanation of what the starter, negative prompt, seed, and MV stance actually do

### 2026-04-10 text-prompt surface removal
Source: addon cleanup after the backend-family lock-in
Context: the addon source still exposed a leftover text-prompt bridge path even though the intended product flow had already shifted to `Z-Image` for prompt-to-image and then image-guided 3D generation.

Documented changes:

- bumped the packaged addon version to `1.1.41`
- removed the remaining addon-side text-prompt capability flag from the fallback capability surface
- removed the old text-prompt launch flag from the local `2mv` launch path
- removed the visible text-prompt toggle from the addon UI and runtime state
- replaced the old text-prompt VRAM estimate with explicit guidance to use `Z-Image` first

Why it matters:

- the published addon surface now matches the intended supported workflow more closely
- users are less likely to assume the old text-bridge path is still a supported lane

### 2026-04-10 backend-surface cleanup pass
Source: distro-slimming pass after locking the intended backend families
Context: the working product direction is now `Z-Image` via `Nunchaku`, official `TRELLIS.2`, and `Hunyuan 2mv`. The addon still carried stale `2.1` and stock non-Nunchaku image-runtime branches that no longer fit that target.

Documented changes:

- bumped the packaged addon version to `1.1.40`
- removed the remaining `Hunyuan 2.1` launch/display branches from the addon runtime surface
- removed the old stock `Z-Image Standard` path from the visible addon model-selection logic
- simplified `Z-Image` runtime handling so the addon now treats `Nunchaku` as the intended image runtime family
- removed dead legacy texture-size property surface that no longer belonged to the kept backends

Why it matters:

- the addon surface is now much closer to the intended lighter distro shape
- users only see the three backend families you actually want to keep investing in

### 2026-04-10 TRELLIS shape-panel readability pass
Source: first screenshot-driven cleanup after TRELLIS shape-panel wiring started working end to end
Context: the TRELLIS branch was live in the addon, but the shape options had become hard to read in a narrow Blender sidebar, and the result box was dumping ugly truncated paths.

Documented changes:

- bumped the packaged addon version to `1.1.39`
- changed TRELLIS option rendering so core values read as full-width controls instead of cramped paired rows
- grouped TRELLIS sampling settings into separate `Sparse Structure`, `Shape`, and `Texture` blocks
- shortened shape-result labels so the panel shows the folder name and mesh filename instead of chopped full paths

Why it matters:

- TRELLIS parameters should now be readable in the normal sidebar width
- the shape result area should be much easier to scan after a run

### 2026-04-10 TRELLIS startup and default-runtime follow-up
Source: first addon-side cleanup pass after the official TRELLIS adapter tests
Context: the TRELLIS backend itself was working, but the addon was not surfacing its startup progress clearly enough, and the fresh-session runtime defaults still were not quite right.

Documented changes:

- bumped the packaged addon version to `1.1.36`
- changed fresh-session runtime defaults so no services start pre-selected
- taught the server-panel startup parser to recognize TRELLIS adapter output such as:
  - `Preparing TRELLIS adapter`
  - `Preparing TRELLIS runtime`
  - `Loading TRELLIS models`
  - `Server ready`

Why it matters:

- Blender now opens with no runtime checkboxes pre-selected in a fresh session
- TRELLIS startup should stop looking like an immediate silent failure when it is actually booting and serving `/server_info`

### 2026-04-10 TRELLIS path-launch fix
Source: follow-up after testing the TRELLIS start action from Blender
Context: the default TRELLIS Python path used `~`, and the addon launch path was quoting it before shell expansion, so the start action could fail immediately while looking like it did nothing.

Documented changes:

- bumped the packaged addon version to `1.1.37`
- resolved the TRELLIS Python path to an absolute Linux path before building the WSL launch command

Why it matters:

- the default TRELLIS interpreter path should now launch correctly from the addon without requiring a manual path edit first

### 2026-04-10 3D target auto-switch follow-up
Source: runtime-selection UX cleanup after the first TRELLIS addon tests
Context: `Use For 3D` was too easy to miss, so starting a 3D runtime could still leave the shape panel pointed at a different backend.

Documented changes:

- bumped the packaged addon version to `1.1.38`
- starting a 3D runtime now automatically makes it the active 3D target
- the manual `Use For 3D` control remains as an override for multi-runtime cases

Why it matters:

- if you start `TRELLIS.2`, the shape panel should now switch to TRELLIS immediately
- the same behavior applies to `Hunyuan 2mv` and `Hunyuan 2.1`

### 2026-04-09 late texture guidance follow-up
Source: dedicated texture-panel MV guidance and wider 2mv texture-size choices
Context: once the end-to-end 2mv texture path was proven, the dedicated texture panel still lagged behind by only exposing single-image guidance.

Documented changes:

- bumped the packaged addon version to `1.1.33`
- added `Guidance Mode` to `Nymphs Texture`:
  - `Auto`
  - `Image`
  - `Multiview`
  - `Text`
- taught the dedicated texture request to send:
  - `mv_image_front`
  - `mv_image_back`
  - `mv_image_left`
  - `mv_image_right`
  when MV guidance is selected
- reused the existing MV slots and `Received from Z-Image` handoff marker instead of inventing a separate second MV store
- widened the 2mv texture-size presets to include:
  - `256`
  - `768`
  - `1536`
  in addition to the existing values

Why it matters:

- the separate texture panel can now logically follow the image-to-shape workflow instead of forcing everything back through one image
- 2mv users can test more texture-size tradeoffs without manually editing payloads

### 2026-04-09 late MV handoff UI follow-up
Source: MV image handoff cleanup between `Nymphs Image` and `Nymphs Shape`
Context: once MV generation was working, the image panel was repeating information that was more useful in the shape workflow itself.

Documented changes:

- bumped the packaged addon version to `1.1.32`
- removed the redundant `Assigned MV Views` summary box from `Nymphs Image`
- added a simple MV handoff marker in `Nymphs Shape`:
  - `Received from Z-Image`
  - plus a timestamp when available

Why it matters:

- the image panel stays focused on image generation
- the shape panel now shows the MV handoff where it is actually relevant
- the UI keeps the useful signal without repeating four more file-path rows

### 2026-04-09 late prompt preset follow-up
Source: additional watercolor starter for the remake prompt flow
Context: once the starter-load workflow was in place, it needed a style starter tailored to the kind of elegant painterly result you wanted to test.

Documented changes:

- bumped the packaged addon version to `1.1.31`
- added a new shipped prompt starter:
  - `Style: Minimalist Chinese Watercolor`

Why it matters:

- it gives you a usable stylistic starting point without losing the cleaner `2mv`-friendly framing and silhouette guidance
- it keeps the current prompt workflow simple:
  - load
  - tweak
  - generate

### 2026-04-09 late prompt UX follow-up
Source: `Z-Image` prompt workflow cleanup in the remake addon
Context: the image runtime became usable, but prompt editing still felt fragile and too easy to lose during iteration.

Documented changes:

- bumped the packaged addon version to `1.1.30`
- added a lightweight `Prompt Starter` selector in `Nymphs Image`
- added `Load` so a starter prompt can be dropped straight into the current prompt fields and then tweaked
- added direct in-panel editing for:
  - `Prompt`
  - `Negative Prompt`
- kept the wider `Expand` editor for longer prompt work instead of forcing everything through a popup
- added shipped starters for:
  - MV character base
  - fantasy painted character
  - cartoon identity sheet
  - object identity sheet
  - full-body framing fix
- added a clearer note that negative prompts have limited effect on the current `Z-Image Turbo / Nunchaku` lane

Why it matters:

- prompt iteration is now much less awkward during real testing
- the addon can start moving toward a reusable preset system without blocking on a larger prompt-library feature
- selecting a starter prompt now fits the workflow you asked for:
  - load it
  - tweak it
  - generate

### 2026-04-09 late follow-up
Source: `MV Lite` image-set generation and `Z-Image` default runtime cleanup
Context: after proving `Nunchaku r32` was the first practical `Z-Image` runtime, the addon needed a better default and a direct path from one prompt into a usable `2mv` multiview set.

Documented changes:

- bumped the packaged addon version to `1.1.29`
- made `Z-Image Nunchaku r32` the default `Z-Image` runtime preset in the addon
- added `Generate MV Set` to `Nymphs Image`
- added a lightweight MV prompt builder that derives:
  - `front`
  - `left`
  - `right`
  - `back`
  prompts from the current image prompt
- added an `MV Pose` control with:
  - `Soft A-Pose`
  - `Natural`
  - `T-Pose`
- auto-filled the existing multiview image slots from the generated MV image set
- switched the shape workflow to `Multiview` automatically after a successful MV image run

Why it matters:

- this is the first direct bridge from the new `Z-Image` runtime lane into the `2mv` multiview workflow
- it keeps MV development lightweight and prompt-driven instead of waiting for a bigger preset system first
- the addon default now reflects the runtime that actually worked in practice, not the slower stock path

### 2026-04-09
Source: `Z-Image` runtime breakthrough and remake workflow stabilization
Context: the stock BF16 `Z-Image-Turbo` path proved too heavy in real use, so the remake needed a practical runtime and cleaner Blender-side controls for testing it.

Documented changes:

- added explicit `Z-Image` runtime controls to the remake addon line
- added a simple `Model Choice` selector for:
  - `Z-Image Standard`
  - `Z-Image Nunchaku r32`
  - `Z-Image Nunchaku r128`
  - `Custom`
- kept the raw runtime controls available behind `Custom` instead of forcing users to edit low-level settings all the time
- taught the addon launch path to switch between the normal `.venv` and the dedicated `.venv-nunchaku` runtime env
- updated the `Z-Image` runtime card so its detail line can reflect live task progress rather than falling back too quickly to `Server ready`
- updated the top overview so it can reuse the richer `Z-Image` runtime detail when the generic image-progress path is too thin
- published the remake extension line through:
  - `1.1.26`
  - `1.1.27`
  - `1.1.28`

Why it matters:

- this is the first point where `Z-Image` genuinely felt viable for the product instead of just theoretically attractive
- the practical runtime breakthrough came from `Nunchaku`, not the stock BF16 path
- the addon now exposes the comparison points you actually need for product decisions:
  - stock runtime
  - `Nunchaku r32`
  - `Nunchaku r128`
- it also marks a shift in focus:
  - less time fighting raw runtime viability
  - more time improving prompt quality, prompt UX, and workflow fit for `2mv`

### 2026-04-08
Source: product naming cleanup and panel naming normalization
Context: `Nymphs3D2` is the stable public product name, while the older
`Blender 5.1 Test` wording and `v2` panel labels were leftover transitional
labels.

Documented changes:

- bumped the packaged addon version to `1.0.11`
- normalized the public extension name to `Nymphs3D2`
- removed the temporary `Blender 5.1 Test` naming from the package metadata
- published the renamed package line into the extension feed as:
  - `nymphs3d2-1.0.11.zip`
- normalized the preserved older extension archives in the feed to the same
  `nymphs3d2-<version>.zip` naming pattern
- renamed the visible addon panels to:
  - `Nymphs Server`
  - `Nymphs3D2 Shape`
  - `Nymphs3D2 Texture`

Why it matters:

- the addon now presents one stable product name across the source repo,
  package metadata, and Blender UI
- the extension feed now reads as one coherent `Nymphs3D2` release line instead
  of mixing stable branding with leftover temporary test package names
- the shared server layer is no longer labeled like an internal 3D-only `v2`
  panel, which leaves room for future sibling frontends such as `Nymphs2D2`

### 2026-04-07 late afternoon hotfix +0100
Source: Blender startup regression after the `1.0.9` publish
Context: the new WSL user dropdown appears to break Blender startup on at least one real install

Documented changes:

- bumped the packaged addon version to `1.0.10`
- kept the `WSL Target` controls beside `API Root`
- kept the installed-distro dropdown for `Distro`
- rolled `User` back to a plain text field for startup safety

Why it matters:

- the useful WSL target placement remains
- the most likely startup-risking change from `1.0.9` is removed
- this gives a safer recovery build before revisiting automatic user discovery

### 2026-04-07 early afternoon user-target follow-up +0100
Source: WSL user selection usability pass
Context: after adding the distro dropdown, the remaining manual field was the WSL username

Documented changes:

- bumped the packaged addon version to `1.0.9`
- replaced the free-text `WSL User` field with a dropdown
- query the selected distro for likely human users plus `root`
- keep `nymphs3d` as the preferred fallback for the managed installer flow
- moved the WSL target controls beside `API Root` and shortened their visible labels for Blender sidebar readability

Why it matters:

- selecting a different distro no longer leaves the username as a separate manual typo risk
- the local launch target is now much closer to a point-and-click WSL selection flow

### 2026-04-07 early afternoon follow-up +0100
Source: WSL target selection usability pass
Context: users needed a real installed-distro picker instead of manually typing the distro name

Documented changes:

- bumped the packaged addon version to `1.0.8`
- replaced the free-text `WSL Distro` field with a dropdown of installed WSL distros
- kept `Nymphs3D2` as the preferred default entry for the managed installer path

Why it matters:

- launching a backend from a non-default distro no longer depends on typing the exact distro name correctly
- the intended `Nymphs3D2` target remains obvious, but other installed distros are easier to select safely

### 2026-04-07 early afternoon +0100
Source: server panel discoverability pass
Context: the WSL distro chooser existed, but it was buried inside the hidden Advanced block and easy to miss

Documented changes:

- bumped the packaged addon version to `1.0.7`
- moved `WSL Distro` into the visible `Startup Settings` section
- moved `WSL User` into the visible `Startup Settings` section
- removed those two fields from the deeper `Advanced` block to avoid duplicate controls

Why it matters:

- the WSL target selection is now where users expect it when starting a local server
- changing distros no longer requires hunting through the less obvious advanced settings

### 2026-04-07 shortly after midday +0100
Source: hotfix for local server startup on managed installer distros
Context: the just-published `1.0.5` package restored old `/opt/nymphs3d/runtime/...` defaults that no longer match the managed distro layout

Documented changes:

- bumped the packaged addon version to `1.0.6`
- restored the default `2mv` path to `~/Hunyuan3D-2`
- restored the default `2.1` path to `~/Hunyuan3D-2.1`
- kept the new WSL distro and WSL user controls from the recent package line

Why it matters:

- the addon once again targets the actual managed `Nymphs3D2` runtime layout created by the installer
- local server start should no longer fail just because the addon is looking in the old `/opt` runtime location

### 2026-04-07 midday +0100
Source: extension feed refresh from the current addon source
Context: publishing the current live addon implementation instead of leaving the extension repo on the older `1.0.4` package

Documented changes:

- bumped the packaged addon version to `1.0.5`
- prepared a fresh extension package from the current source repo state
- preserved the previous published extension feed state with a git backup point before updating the public package

Why it matters:

- the published extension feed now matches the current addon source instead of serving stale `1.0.4` code
- users get a real update path in Blender instead of a silently replaced package under the same version number

### 2026-04-06 shortly after 01:00 +0100
Source: addon source cleanup for public packaging
Context: making the source repo itself cleaner for manual `Install from Disk` fallback use

Documented changes:

- removed the tracked `experiments/` directory from the public addon repo
- kept local-only experiment files in an ignored `.local_experiments/` stash instead
- kept the live addon implementation at repo root via `Nymphs3D2.py`
- removed stale tracked wheel files that no longer match the live `urllib`-based networking path
- bumped the packaged addon version to `1.0.4`

Why it matters:

- the public addon repo is now much closer to what users would expect if they download the source zip manually
- the supported install path is still the extension feed repo, but the source repo is no longer dragging obvious local-dev baggage into the public package shape

### 2026-04-06 early morning +0100
Source: local addon packaging and extension release follow-up
Context: promoting `Nymphs3D2v3` from local dev track to the live pullable Blender extension

Documented changes:

- switched the live addon entrypoint from `Nymphs3D2v2` to `Nymphs3D2v3`
- bumped the packaged Blender extension version to `1.0.3`
- updated the repo docs layout so research notes and workflow guides now live under `docs/`
- added a dedicated retexturing user guide and `2mv` texture-controls note
- prepared the addon state for publication through the separate `Nymphs3D2-Extensions` feed repo

Why it matters:

- this is the point where the newer `v3` workflow stops being just the lab rat and becomes the thing Blender should actually pull
- the extension package now lines up with the current retexturing work instead of shipping the older `v2` surface

### 2026-04-05 late evening +0100
Source: local `Nymphs3D2v3` and `Hunyuan3D-2.1` development session
Context: making `2.1` selected-mesh retexture real on a `16 GB` GPU without breaking normal `2.1` shape generation

Documented changes:

- proved that the earlier "just keep extending the normal `2.1` backend" direction was the wrong shape for this hardware and this workflow
- after testing both the broken and the overloaded paths, the project pivoted to a duplicated backend surface:
  - keep the normal `2.1` server for shape generation and shape-plus-texture
  - add a separate `2.1` texture-only server dedicated to selected-mesh retexture
- added a dedicated texture-only backend entrypoint and paint-only worker so the shape model no longer has to sit in VRAM during a retexture run
- confirmed that the texture-only server can take a selected Blender mesh, run the full `2.1` PBR texturing pipeline, and return a textured GLB successfully
- most importantly, this finally made full `2.1` selected-mesh PBR retexture complete successfully on a `16 GB` GPU instead of collapsing under the combined shape-plus-texture memory load
- tightened the Blender addon around that new path, including:
  - longer request timeout so Blender does not give up before long `2.1` texture jobs return
  - richer `2.1` texture-stage progress reporting
  - cleaner server status handling during long jobs
  - texture controls in the Texture panel for `Face Limit`, `Max Views`, `Texture Size`, and `Remesh Uploaded Mesh`
- shifted uploaded-mesh handoff toward a safer normalization path for `2.1` texture-only work, matching the lesson from Tencent's own app flow that `OBJ` is the safer paint input than a raw Blender-exported `GLB`

Why it matters:

- this is the first point where `2.1` selected-mesh retexture became real rather than theoretical
- this is a real hardware breakthrough for the project: `2.1` texture-only PBR retexture was made to run to completion on a `16 GB` card, which honestly felt a bit like sorcery by the end of the session
- it preserved the stable normal `2.1` shape path instead of sacrificing it for experiments
- most importantly, the user's instinct to duplicate the backend rather than keep bending the normal server turned out to be exactly right
- in practice, that intuition was the turning point: once the backend was allowed to split into a stable shape server and a dedicated texture-only server, the whole workflow finally clicked into place
- this was the "wait... it actually did it" moment: after all the VRAM misery, stalls, broken paths, and suspicious half-successes, the goblin came back textured and the machine did not explode
- it is also one of the more human moments in the project history: the hardware was clearly complaining, the vibe check was correct, and trusting that instinct produced the solution

### 2026-04-05
Commit: `699b690`
Repo: `Nymphs3D-Blender-Addon`
Title: Publish Blender 5.1 test build without bundled wheels

Documented changes:

- removed the bundled `requests` wheel set from the extension manifest
- kept the live addon on the `Nymphs3D2v2` rewrite and bumped the shipped addon version to `1.0.2`
- switched the packaged extension from a CPython-3.11-specific wheel bundle to a wheel-free package
- labeled the package as `Nymphs3D2 Blender 5.1 Test` so the public extension feed clearly marked it as a compatibility build at that stage
- kept `blender_version_min = 4.2.0`, so the test build still targets the existing `4.2+` addon line rather than a separate 5.1-only fork

Why it matters:

- the live addon code already used Python's stdlib `urllib` stack and did not import `requests`
- the previous package was unnecessarily tied to a specific Blender Python ABI through `charset_normalizer` wheels
- this made the extension package materially easier to install from Blender's remote repository flow on newer Blender builds such as `5.1`

### 2026-04-05 17:34:00 +0100
Commit: `8941b6b`
Repo: `Nymphs3D2-Extensions`
Title: `Publish Nymphs3D2 1.0.1 extension package`

Documented changes:

- published `nymphs3d2-1.0.1.zip` to the public extension repository
- refreshed `index.json` and `index.html` to point Blender at the new package version
- aligned the public extension feed with the newly promoted `Nymphs3D2v2` implementation

Why it matters:

- this is the first public extension-feed publish that explicitly tracks the promoted `v2` implementation rather than the earlier root-package baseline

### 2026-04-05 17:33:00 +0100
Commit: `beebc43`
Repo: `Nymphs3D-Blender-Addon`
Title: `Promote Nymphs3D2v2 as live addon implementation`

Documented changes:

- replaced the previous monolithic live addon implementation with the clean-slate `Nymphs3D2v2` rewrite
- rewrote addon state flow around a queue-driven event model and explicit backend snapshots instead of the older property-cache-heavy structure
- rewrote local backend control so Blender manages:
  - backend family selection between `2mv` and `2.1`
  - startup flags like texture-at-startup, text bridge, turbo, FlashVDM, and low-VRAM mode
  - repo path and port targeting for local WSL launches
- made workflow choice explicit in the addon logic, including direct selected-mesh retexture as a first-class mode
- kept backend truth polling as part of the rewrite, including `/server_info`, `/active_task`, and GPU status refresh
- switched the live addon entrypoint at repo root to import the `Nymphs3D2v2` code path
- added `experiments/__init__.py` so that implementation could be imported cleanly as a package module
- updated the v2 implementation metadata to present itself as `Nymphs3D2`
- bumped the packaged addon manifest version to `1.0.1`

Why it matters:

- this is the point where the clean rewrite became the shipped addon, not just an experimental sidecar file
- it marks a real code-architecture swap in the Blender client, not only a packaging change

### 2026-04-05 10:21:18 +0100
Commit: `519641d`
Repo: `Nymphs3D-Blender-Addon`
Title: `Move Nymphs3D2 extension package to repo root`

Documented changes:

- moved `__init__.py`, `blender_manifest.toml`, and `wheels/` to repo root
- made the repo ZIP much closer to a Blender `Install from Disk` artifact
- simplified friend-testing and distribution from GitHub

Why it matters:

- this made the repo itself behave more like an installable extension package instead of a source tree wrapped around one

### 2026-04-05 10:16:12 +0100
Commit: `e24fbf6`
Repo: `Nymphs3D-Blender-Addon`
Title: `Clean published addon repo links`

Documented changes:

- replaced local filesystem references in published docs with GitHub URLs
- cleaned the addon repo README for real external use

Why it matters:

- this made the addon repo usable as a shareable project surface rather than a local dev note

### 2026-04-05 10:14:18 +0100
Commit: `223f526`
Repo: `Nymphs3D-Blender-Addon`
Title: `Remove legacy addon file`

Documented changes:

- removed `blender_addon.py` from the addon repo
- focused the addon repo solely on `Nymphs3D2`

Why it matters:

- this simplified the repo and made `Nymphs3D2` the only active addon path

### 2026-04-05 10:12:55 +0100
Commit: `857894a`
Repo: `Nymphs3D-Blender-Addon`
Title: `Bundle requests in Nymphs3D2 extension`

Documented changes:

- restored `requests`-based HTTP behavior for the addon
- bundled `requests` and its dependencies as extension wheels
- removed the need for Blender users to have `requests` preinstalled separately

Bundled dependency set:

- `requests`
- `urllib3`
- `certifi`
- `charset_normalizer`
- `idna`

Why it matters:

- this fixed the distribution problem around third-party HTTP dependency availability while preserving the original addon networking approach

### 2026-04-05 10:07:40 +0100
Commit: `feaa346`
Repo: `Nymphs3D-Blender-Addon`
Title: `Package Nymphs3D2 as Blender extension`

Documented changes:

- converted `Nymphs3D2` from a loose addon file into a proper Blender extension package
- added `blender_manifest.toml`
- added extension-oriented README guidance
- added a build path for Blender's `extension build` flow

Why it matters:

- this is the point where the addon stopped being only a script and became a real packaged Blender extension

### 2026-04-05 09:54:10 +0100
Commit: `d6d9f4c`
Repo: `Nymphs3D` helper repo
Title: `Harden installer and split addon repo`

Documented changes:

- hardened the Windows + WSL installer flow
- made rerunning `Install_Nymphs3D.bat` an intended repair/update path
- reframed the helper repo as setup/backend infrastructure rather than the addon product
- strengthened verification and repair framing in the docs

Repo-boundary changes documented here:

- helper repo becomes the public-facing setup layer
- addon/frontend is treated as separate distribution
- standalone launcher stays in the helper repo as helper/runtime tooling

Why it matters:

- this is the helper-side half of the repo split that began with `ce3c989`

### 2026-04-05 09:47:50 +0100
Commit: `ce3c989`
Repo: `Nymphs3D-Blender-Addon`
Title: `Initial addon repo split`

Documented changes:

- created the separate private addon repo
- moved the addon source out of the backend/helper repo
- established the addon repo as the Blender-side frontend codebase

Why it matters:

- this is the formal repo-level split between helper/backend distribution and the Blender product surface

### 2026-04-04 19:15:23 +0100
Commit: `8ff3671`
Repo: `Nymphs3D` helper repo
Title: `Add texture upgrade audit`

Documented changes:

- created a formal audit of current texture controls versus backend capabilities
- recorded the gap between:
  - what `2.0 / 2mv` and `2.1` can do
  - what the launcher and addons currently expose

Key findings recorded in the doc:

- current UI reduced texture to a mostly binary switch
- `2.0` supports more texture-path distinction than the UI exposed
- `2.1` already had richer PBR-oriented controls than the UI exposed
- the safest next frontend wins would be:
  - explicit `2.1` PBR wording
  - explicit `2.0` standard vs turbo texture modes
  - later resolution and face-count controls for `2.1`

Why it matters:

- this is the clearest documented transition from baseline stability work to quality-of-result and product-surface refinement

### 2026-04-04 19:06:46 +0100
Commit: `75f52c4`
Repo: `Nymphs3D` helper repo
Title: `Clean roadmap and remove duplicate installer`

Documented changes:

- simplified roadmap framing
- removed duplicate installer confusion
- tightened the high-level project direction around one intended path

Why it matters:

- this pushed the repo closer to a single supported install story

### 2026-04-04 19:00:36 +0100
Commit: `963d4f4`
Repo: `Nymphs3D` helper repo
Title: `Clarify launcher and addon workflow split`

Documented changes:

- clarified the split between launcher responsibilities and addon responsibilities
- recorded the product direction more explicitly:
  - Blender as the main workflow surface
  - launcher as support, startup, and helper tooling
  - backend repos as implementation layers

Why it matters:

- this commit documents the beginning of the cleaner product boundary later completed on 2026-04-05

### 2026-04-04 18:54:00 +0100
Commit: `8c368d4`
Repo: `Nymphs3D` helper repo
Title: `Reduce repo docs to public-facing set`

Documented changes:

- removed a large set of internal planning, handoff, and session docs from the public-facing doc set
- kept only the docs considered useful for public users
- preserved the detailed internal/project narrative in git history rather than the visible docs tree

Important consequence:

- after this point, a lot of the project's real evolution stops being visible from the current docs alone
- historical reconstruction requires reading git history, not just the surviving files

### 2026-04-04 18:52:14 +0100
Commit: `1ad8e59`
Repo: `Nymphs3D` helper repo
Title: `Make beginner guide Blender-first after install`

Documented changes:

- rewrote the beginner guide around the expected post-install Blender workflow
- made the local API endpoint and Blender handoff more explicit
- reflected the intended user story: backend first, Blender immediately after

Why it matters:

- this is where the docs stopped reading like backend notes and started reading like a workflow product

### 2026-04-04 18:15:34 to 2026-04-04 18:56:16 +0100
Commits:

- `413bf4d` `Update install flow for Nymphs3D repo rename`
- `396b747` `Fix live Nymphs3D doc links`
- `77c571f` `Simplify README entry points`
- `8b61db5` `Clarify Blender-first frontend in README`
- `4aa2574` `Rename installer entry point and refine docs`
- `6573978` `Use direct installer download link in README`
- `96b6c7d` `Use release-based installer download`
- `5ca4744` `Use repo zip download in README`
- `e62e699` `Point README to installer archive asset`
- `023072f` `Use GitHub asset page link for installer archive`
- `d04b58e` `Use direct GitHub download URL for installer archive`
- `484d062` `Clarify installer archive extraction step`
- `c79ca7d` `Use direct launcher download link in README`
- `81c2077` `Fix launcher download path in README`

Documented changes:

- rebranded the public-facing flow around the `Nymphs3D` name
- repeatedly refined the installer and launcher download entrypoints
- moved README wording toward simpler entrypoints for first-time users
- made archive extraction and installer entrypoint wording more explicit

Why it matters:

- this cluster of commits shows the project actively searching for the least confusing public install path

### 2026-04-04 18:10:51 +0100
Commit: `5afadd1`
Repo: `Nymphs3D` helper repo
Title: `Add Nymphs3D2 and harden install flow`

Documented changes:

- introduced `Nymphs3D2` as a separate experimental addon file
- preserved the original addon while allowing a Blender-first evolution path
- added the Goblin single-image example as a public-facing proof of workflow
- added the disk/model footprint doc with practical measured storage numbers
- expanded install guidance around the hardened flow

Recorded `Nymphs3D2` direction from the session summary:

- separate addon file rather than replacing the original
- server control and generation workflow integrated into Blender
- capability-aware UI against the running server
- quieter console and cleaner progress wording

Recorded launcher/backend outcomes from the same period:

- launcher recovery after `2.0` startup regressions
- launcher-side port clearing before startup
- `2.0` backend made more robust under launcher pipe/logging conditions

Why it matters:

- this commit marks the project becoming more explicitly Blender-first while still keeping the standalone launcher alive as support tooling

### 2026-04-04 12:00:22 +0100
Commit: `45c5913`
Repo: `Nymphs3D` helper repo
Title: `Install from working environment locks`

Documented changes:

- recorded the move toward checked-in `requirements.lock.txt` based installs
- positioned lockfile installs as the way to reproduce a validated working machine state
- tightened install docs around reproducibility instead of loose dependency resolution

Why it matters:

- this reduced drift between a known-good setup and a fresh install

### 2026-04-04 11:39:57 +0100
Commit: `63a81f6`
Repo: `Nymphs3D` helper repo
Title: `Add one-click install flow and launcher updates`

Documented changes:

- introduced the first clearly documented one-click install path
- consolidated a large amount of project documentation into the repo's `docs/` directory
- added docs for launcher behavior, install flow, fork strategy, and broader pipeline thinking

What the one-click direction meant:

- Windows bootstrap became the intended beginner entrypoint
- WSL setup, backend installation, and local runtime preparation were treated as one guided flow

Other documented themes added at this point:

- packaging the Python launcher as a Windows `.exe`
- keeping backend repos as implementation layers
- treating installer reliability as product work, not just setup chores

Why it matters:

- this is where the repo became a full onboarding surface rather than just notes plus scripts

### 2026-04-03 to 2026-04-04 early morning
Sources:

- `session_handoff_2026-04-03_wsl_restart_quality_progress.md`
- `session_log_2026-04-04_hunyuan_progress_texture_recovery.md`

Documented changes:

- reverted Blender handoff back to the simpler direct-file `/generate -> GLB` contract
- removed an overly short timeout from non-textured direct generation because successful runs took longer than 30 seconds
- separated launcher progress work from Blender async handoff work
- recovered `2.1` generation stability, Blender handoff stability, and texture generation
- established the lesson that progress reliability is mostly a backend responsiveness and state-contract problem, not just a log parsing problem

Why it matters:

- this is a core architectural turning point for the project
- later `2.0` and launcher work explicitly built on the same lesson

### 2026-04-03 19:38:48 +0100
Source file timestamp: `session_handoff_2026-04-03_launcher_textbridge_regression.md`
Context: addon and backend contract debugging

Documented changes:

- identified the Nymphs addon line as the only Blender addon surface that mattered going forward
- recorded a key regression caused by changing `2.1` to async `uid/status/file` behavior while the addon still expected direct GLB bytes from `POST /generate`
- documented the practical requirement to keep launcher progress improvements from breaking the working text-to-mesh path

Why it matters:

- this is the first explicit statement of a contract that keeps showing up later:
  - Blender handoff must stay stable
  - launcher progress work must not be allowed to break generation

### 2026-04-03 14:14:29 +0100
Commit: `5e59cfa`
Repo: `Nymphs3D` helper repo
Title: `Refine launcher wording and add text-to-multiview roadmap`

Documented changes:

- clarified launcher wording for artist-facing use
- added a forward-looking roadmap for experimental text-to-multiview generation
- positioned text-to-multiview as a `2mv` experiment rather than a baseline feature

Technical direction recorded in docs:

- current text mode was documented as `text -> one generated image -> image-to-3D`
- future idea was `text -> generated multiview set -> 2mv -> mesh`
- main expected blocker was cross-view consistency, not just implementation effort

Why it matters:

- the docs show an early split between the stable baseline and experimental research ideas

### 2026-04-03 14:08:35 +0100
Commit: `c3b097c`
Repo: `Nymphs3D` helper repo
Title: `Add launcher prototype and 2.1 text-mode install support`

Documented changes:

- introduced the standalone launcher prototype
- defined the launcher as a Windows app that builds and runs WSL commands internally
- documented early support for `Hunyuan 2.1` text-prompt bridge startup

Launcher direction at this stage:

- user chooses backend, input mode, texture mode, turbo, FlashVDM, low-VRAM, and port
- app launches the corresponding WSL command
- launcher becomes the main non-terminal control surface

Why it matters:

- this is the start of the launcher as a real product surface rather than a temporary helper

### 2026-04-03 13:40:48 +0100
Commit: `165faf1`
Repo: `Nymphs3D` helper repo
Title: `Improve install docs and add preflight checks`

Documented changes:

- tightened the install documentation for non-technical users
- added preflight-style thinking to the setup flow
- started moving the project toward a guided install instead of a loose script collection

Why it matters:

- this is the first clear sign that beginner installation reliability had become a project priority

### 2026-04-03 13:37:29 +0100
Commit: `8df172f`
Repo: `Nymphs3D` helper repo
Title: `Add initial WSL install and run bundle for forked Hunyuan setup`

Documented changes:

- established the first public-facing repo docs around a forked Hunyuan local setup
- positioned the project as a Windows + WSL bundle for running custom Hunyuan workflows locally
- created the first artist-facing notes and installation framing

Why it matters:

- this is the point where the project became more than backend experiments and started to present itself as an installable workflow

### 2026-04-03 13:36:31 +0100
Source file timestamp: `artist_notes.md`
Context: early user-facing local note

Documented changes:

- defined the basic product split between `Hunyuan3D-2` for multiview/custom addon workflow and `Hunyuan3D-2.1` for newer single-image and PBR work
- recorded first-run expectations around slow model downloads and cache-building
- captured practical troubleshooting assumptions for venv activation, CUDA failures, and low-VRAM fallbacks

Why it matters:

- this is the first concise user-level articulation of how the two backend families were meant to coexist

### 2026-04-03 12:37:44 +0100
Source file timestamp: `fork_strategy_for_team_install.md`
Context: local strategy doc

Documented changes:

- explicitly recognized both `Hunyuan3D-2` and `Hunyuan3D-2.1` local checkouts as real forks, not clean upstream clones
- listed concrete local changes such as:
  - modified API servers
  - modified Gradio apps and model worker behavior
  - deletion/replacement of upstream addon files
  - custom multiview and Nymphs addon files
- recommended moving to personal/team forks as the basis for reproducible installs

Why it matters:

- this is where the project became clearly "your Hunyuan workflow" rather than just "Tencent upstream with notes"

### 2026-04-03 12:30:56 +0100
Source file timestamp: `reverse_engineered_wsl_hunyuan_blender_mcp_install.md`
Context: local machine state reconstruction

Documented changes and findings:

- reconstructed the actual WSL machine state under `/home/babyj`
- recorded that both `Hunyuan3D-2` and `Hunyuan3D-2.1` existed locally with Python `3.10` virtual environments
- documented CUDA `13.0`, large Hugging Face model cache footprint, and the Blender MCP server being present on the same machine
- treated the machine setup itself as something that needed to be turned into a reproducible install story

Why it matters:

- this is the bridge from "working personal machine" to "something that can be documented and reproduced"

### 2026-04-03
Source: `session_handoff_hunyuan_launcher_restart_summary.md`
Context: restart handoff

Documented changes:

- both Hunyuan repos were cleaned and forked to your GitHub
- `game3d-wsl-setup` was created as a setup/documentation repo
- `2.1` text prompt mode was made to work on your machine
- a Windows launcher app was built and was already close to replacing the raw WSL console workflow

Why it matters:

- this is the true formation point of the repo-backed project structure that later became `Nymphs3D`

### 2026-04-01
Source: `/home/babyj/Hunyuan3D-2_backup_2026-04-01/hunyuan3d_2mv_tomorrow_handoff.md`
Context: pre-`Nymphs3D` repo project state

Documented project state:

- main target was `Hunyuan3D 2.0 MV` running in Windows + WSL from the official Tencent repo
- Blender addon was already being treated as a key control surface that needed support for:
  - text prompt
  - single image
  - multiview `front/back/left/right`
  - texture toggle
  - selected-mesh texturing
- `2.1` was already recognized as the better single-image path, but not the right multiview target

Critical environment work already completed by this point:

- WSL Ubuntu baseline established
- CUDA `13.0` installed in WSL because the active Torch build required `cu130`
- texture build steps repaired with `CUDA_HOME=/usr/local/cuda-13.0`
- cache/symlink fixes worked around Hunyuan cache path mismatches
- `2.0` API, `2.0` Gradio, and `2.0 MV` Gradio were all recorded as working
- text-to-3D bridge support was already patched into `2.0`

Important early direction:

- use official Tencent repo in WSL as the backend base
- patch the Blender addon rather than abandoning Blender integration
- keep both API and Gradio working while adding multiview-focused Blender support

Why it matters:

- this is the earliest documented project baseline I found
- it shows the project did not begin as a general helper repo; it began as hands-on backend patching plus Blender workflow hacking around `2.0 MV`

## Backend And Launcher Work Recorded In Historical Docs

The addon repo did not exist when these changes were made, but they are important context for how `Nymphs3D2` emerged.

### `2.0 / 2mv` backend stability and progress work

Recorded in `docs/session_summary_2026-04-04_2mv_progress_and_docs.md` from the helper repo history:

- multiview texture handoff was fixed by normalizing dict-based prompt images into stable ordered image input
- `2.0` gained structured shared progress state and `/active_task`
- direct `POST /generate` on `2.0` was moved through `run_in_threadpool(...)` so the server stayed responsive during generation
- request validation was added for invalid combinations such as texture without texture mode enabled
- real texture-stage timing and sub-step logging were added

Project impact:

- launcher progress stopped being treated purely as a terminal parsing problem
- backend responsiveness and progress-state contracts became first-class concerns

### Launcher quality and packaging work

Recorded in `docs/frontend_launcher.md`, `docs/build_windows_exe.md`, and the same historical session summary:

- launcher gained start/stop control, live logs, status display, and command preview
- log noise was reduced substantially
- `2.0` progress handling was improved with a hybrid of structured task state and console-derived progress
- Tk-threading issues in followed-log updates were fixed
- launcher was repeatedly rebuilt as `dist/hunyuan_launcher.exe`
- docs consistently described the launcher as a controller, not a full AI package

Project impact:

- the standalone launcher became a serious runtime-control tool instead of a convenience wrapper

## Current Addon-Repo State

As of commit `beebc43` on `2026-04-05 17:33:00 +0100`, the addon repo's documented position is:

- the repo contains the Blender-side frontend code for `Nymphs3D2`
- the repo root is the actual extension package
- the repo root delegates to the promoted `Nymphs3D2v2` implementation
- the extension bundles `requests` and its dependencies
- Blender `4.2+` extension packaging/build is the intended distribution model
- the current published extension feed version is `1.0.1`
- the backend/helper repo is separate and responsible for local Windows + WSL setup, backend cloning/repair, runtime verification, and the standalone helper launcher

## Short Version

If the whole documented history is compressed to one line, it is this:

`Nymphs3D2` began as an experimental Blender-side branch inside the original Hunyuan helper/setup project, then became the main Blender-first frontend surface, and is now packaged as a proper standalone Blender extension in its own repo with bundled dependencies and a public extension feed tracking the promoted `v2` implementation.
