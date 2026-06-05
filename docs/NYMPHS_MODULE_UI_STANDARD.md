# Nymphs Module UI Standard

This is the NymphsCore custom module UI standard.

It captures the look and interaction contract proven by Nymphs Image,
TRELLIS.2, and Pixal3D. Use it when building module-owned Manager UIs,
especially generation modules that show source media, generation controls,
progress, output previews, and output browsers.

The goal is consistency: a user should feel that Nymphs Image, TRELLIS.2,
Pixal3D, TripoSplat, and future generation modules are part of the same Nymphs
product family. Upstream Gradio or project UIs may exist for debugging or
reference, but the Manager-facing module UI should follow this standard.

Read this alongside `docs/NYMPHS_MODULE_MAKING_GUIDE.md`. The module guide owns
the lifecycle, manifest, install/update, status, publishing, and registry
contract. This UI standard owns the module-owned frontend look, layout, preview,
progress, picker, and backend route conventions.

## Proof Modules

Current proof UIs:

```text
NymphsModules/zimage/nymph_image.html
NymphsModules/trellis/nymph_trellis.html
NymphsModules/pixal3d/nymph_pixal3d.html
```

Current proof launch pattern:

```text
NymphsModules/zimage/scripts/zimage_nymph_ui.sh
NymphsModules/trellis/scripts/trellis_nymph_ui.sh
NymphsModules/pixal3d/scripts/pixal3d_nymph_ui.sh
```

When this standard and a proof UI disagree, update the standard first if the
new behavior is intentional and should become shared.

## Workbench Families

Nymphs module UIs should choose a defined workbench family instead of inventing
a new shell for each module.

Current families:

```text
2D image workbench    Nymphs Image style: compact prompt/source rail, large image preview, output picker strip
3D GLB workbench      TRELLIS/Pixal3D style: 360px source/control rail, model-viewer stage, GLB progress strip
```

Shared brand rules apply to both families:

- same dark teal/lime product language
- same compact command buttons
- same section-title language
- same thin progress bars
- same local `/nymph` Manager UI contract
- same shared data root pattern under `$HOME/NymphsData`

Do not mix the families casually. A 3D module such as TripoSplat should use the
3D GLB workbench dimensions. A dense image-generation module with prompts,
gallery browsing, and image-folder picking should use the 2D image workbench
architecture.

## Manager Contract

Custom generation UIs should be module-owned `local_url` UIs.

Manifest shape:

```json
{
  "runtime": {
    "host": "127.0.0.1",
    "port": 7001,
    "health_url": "http://127.0.0.1:7001/health",
    "server_info_url": "http://127.0.0.1:7001/server_info",
    "frontend_url": "http://127.0.0.1:7001/nymph"
  },
  "entrypoints": {
    "open_nymph_ui": "scripts/example_nymph_ui.sh",
    "stop": "scripts/example_stop.sh"
  },
  "ui": {
    "standard_lifecycle_rail": true,
    "manager_ui": {
      "type": "local_url",
      "title": "Example UI",
      "url": "http://127.0.0.1:7001/nymph",
      "requires_running": true,
      "start_action": "open_nymph_ui",
      "stop_action": "stop"
    }
  }
}
```

The `open_nymph_ui` action must start or reuse the module backend and print:

```text
url=http://127.0.0.1:<port>/nymph
module_ui_url=http://127.0.0.1:<port>/nymph
```

Use the module guide's port planning standard. New module services should avoid
the established `808x` and `809x` ports unless the team reserves one
intentionally.

## Backend Routes

The backend should serve the Nymphs UI and lightweight status routes without
loading heavyweight models.

Required routes:

```text
GET  /nymph          Nymphs custom UI HTML
GET  /health         cheap service health
GET  /server_info    cheap runtime/config summary
```

Recommended generation routes:

```text
GET  /progress       current progress snapshot for the active session
GET  /active_task    simple global task snapshot, if the module uses this shape
POST /api/warmup     optional explicit warmup
POST /api/generate   generation endpoint
POST /api/export     optional export endpoint
POST /api/stop_runtime optional in-UI cleanup endpoint
```

Recommended image workbench routes:

```text
GET    /outputs/<relative-path>       safe file response under the output root
GET    /api/outputs                   recent output records for the gallery strip
POST   /api/outputs/delete            delete selected managed outputs
POST   /api/outputs/move              move selected managed outputs into a group folder
POST   /api/outputs/folder/delete     delete one managed group folder
GET    /api/presets                   user prompt/settings presets
POST   /api/presets/<kind>            save one preset
DELETE /api/presets/<kind>/<id>       delete one preset
GET    /api/loras                     optional local LoRA run/checkpoint browser
GET    /api/openrouter/status         optional cloud/provider secret readiness
POST   /api/openrouter/key            optional save provider key
DELETE /api/openrouter/key            optional remove provider key
POST   /generate                      local image generation
POST   /api/gemini/generate           optional cloud image generation
POST   /api/vision/caption            optional image caption helper
POST   /api/parts/plan                optional part planning
POST   /api/parts/extract             optional part extraction
```

Serve local assets from the module backend:

```text
/assets/vendor/model-viewer-4.0.0.min.js
/assets/vendor/lucide-0.468.0.min.js
/outputs/<generated-file>.glb
/outputs/<generated-file>.png
/tmp/<preview-or-preprocessed-file>
```

Do not rely on remote CDNs for the Manager-hosted UI.

## Data Directories

Module UIs must keep generated outputs, user presets, logs, and config under
shared Nymphs data roots, not inside disposable source roots.

Shared roots:

```text
$HOME/NymphsData/outputs/<module-id>
$HOME/NymphsData/logs/<module-id>
$HOME/NymphsData/config/<module-id>
$HOME/NymphsData/cache/<module-id>
```

Nymphs Image also proves shared image preset directories that can be reused by
other image-facing modules:

```text
$HOME/NymphsData/config/image_presets
$HOME/NymphsData/config/image_settings_presets
$HOME/NymphsData/config/image_prompts
```

Environment override pattern:

```text
NYMPHS_DATA_ROOT
NYMPHS_IMAGE_PRESET_DIR
NYMPHS_IMAGE_PRESETS_DIR
NYMPHS_IMAGE_SETTINGS_PRESET_DIR
NYMPHS_IMAGE_SETTINGS_PRESETS_DIR
NYMPHS_IMAGE_PROMPT_TEMPLATES_DIR
```

Rules:

- Output URLs must be relative to the module's declared output root.
- File-serving routes must reject path traversal.
- Move/delete routes must operate only on managed output records under the
  output root.
- Manual folder picker images are browser-local and must be marked
  `managed: false`; the backend must not delete or move them.
- Generated image metadata should sit beside the image as JSON when the module
  needs prompt, provider, batch, or item labels.

## Brand Assets

Nymphs Image proves the preview forest background as a reusable Nymphs image
workbench asset.

Current copies:

```text
NymphsModules/zimage/ui/nymphs_preview_forest.png
NymphsModules/nymphs-sprite/ui/nymphs_preview_forest.png
```

Canonical asset properties:

```text
filename: nymphs_preview_forest.png
dimensions: 1402 x 1122
format: PNG RGB
sha256: c5a74b42d7b9bf52b7bafffefe1edd4f6fdc7008adef5b1b0979430ad1818982
```

Use it as the empty/idle image preview background for 2D image workbenches:

```css
.image-shell {
  background:
    url("/ui/nymphs_preview_forest.png") center center / cover no-repeat,
    rgba(4, 12, 11, 0.82);
}
```

Rules:

- Modules using the 2D image workbench should vendor this PNG under their own
  installed `ui/` folder or another safe module-served path.
- Do not fetch it from another module's installed root at runtime.
- Do not use remote URLs for this asset.
- Keep the same filename unless there is a versioned replacement.
- If the asset changes intentionally, update this standard with the new
  dimensions and hash.

## Page Skeleton

Use a two-pane application shell:

```html
<main class="app">
  <aside class="sidebar">
    <!-- Source, runtime, advanced, status -->
  </aside>
  <section class="stage">
    <!-- Top status, viewer, progress strip -->
  </section>
</main>
```

TRELLIS uses a nested `.viewer > .stage > .model-shell` shape. Pixal3D uses
`.stage > .workspace > .preview > .viewer > .pane > .pane-body`. Either nested
shape is acceptable, but the visible result must match: fixed left rail,
full-height right preview, GLB viewer, empty state, and progress strip.

## 3D Layout Constants

These values are part of the 3D GLB workbench brand standard.

```css
.app {
  height: 100vh;
  min-height: 560px;
  display: grid;
  grid-template-columns: 360px minmax(400px, 1fr);
  background: rgba(2, 8, 7, 0.72);
}

.sidebar {
  min-width: 0;
  height: 100vh;
  overflow-y: auto;
  padding: 18px 20px 16px;
  border-right: 1px solid var(--line);
}

@media (max-width: 760px) {
  body { overflow: auto; }
  .app {
    grid-template-columns: 1fr;
    height: auto;
    min-height: 100vh;
  }
  .sidebar {
    height: auto;
    border-right: 0;
    border-bottom: 1px solid var(--line);
  }
}
```

Rules:

- Desktop left sidebar width is exactly `360px`.
- Desktop right pane is `minmax(400px, 1fr)`.
- Collapse point is exactly `760px`.
- Desktop app height is `100vh`; sidebar scrolls internally.
- Mobile/tablet collapsed layout stacks sidebar above viewer.
- Do not make the sidebar wider for one module because labels are long. Shorten
  labels or move expert controls into the advanced section.

## 2D Image Layout Constants

Nymphs Image proves the compact image workbench for prompt-heavy image tools.
This family is intentionally denser than the 3D GLB workbench because it must
fit model selection, prompt management, source/guide image controls, and part
extraction controls beside a large image viewer and output strip.

Current Nymphs Image shell:

```css
.app {
  height: 100vh;
  height: 100dvh;
  min-height: 0;
  display: grid;
  grid-template-columns: clamp(260px, 28vw, 300px) minmax(300px, 1fr);
  min-width: 0;
  background: rgba(2, 8, 7, 0.72);
}

.sidebar {
  min-width: 0;
  height: 100vh;
  height: 100dvh;
  overflow-y: auto;
  border-right: 1px solid var(--line);
  padding: 15px 14px 14px;
  display: flex;
  flex-direction: column;
}

@media (max-width: 600px) {
  body { overflow: auto; }
  .app {
    height: auto;
    min-height: 100vh;
    grid-template-columns: 1fr;
  }
  .sidebar {
    height: auto;
    border-right: 0;
    border-bottom: 1px solid var(--line);
  }
  .stage {
    height: 82vh;
    min-height: min(620px, 100vh);
  }
}
```

Rules:

- Use this compact layout only for image workbenches with large prompt/source
  control density.
- New 3D modules must not copy this compact rail; they should use the 360px 3D
  rail.
- If a future image module can fit the 360px family without losing the Nymphs
  Image picker architecture, update this standard and migrate holistically.

## Standardization Rule

Consistency means choosing and preserving one of the shared families, not
making each module bespoke.

Use:

```text
3D GLB module      360px rail, 760px collapse, model-viewer stage
2D image module    clamp(260px, 28vw, 300px) rail, 600px collapse, image preview and output strip
```

Do not create a third rail width, breakpoint, progress strip shape, or picker
architecture for a one-off module. If the existing families do not fit, update
this standard first and then audit the proof modules.

## Color System

Use this base palette unless a proof UI intentionally updates the shared brand.

```css
:root {
  color-scheme: dark;
  --bg: #07100f;
  --surface: #0b1514;
  --surface-2: #101b1d;
  --line: #1b3536;
  --line-2: #284446;
  --text: #f0faf7;
  --muted: #9bb9b4;
  --teal: #29e6da;
  --lime: #97df48;
  --amber: #ff8a2a;
  --danger: #ff5e6c;
  --font-head: Bahnschrift, "Arial Narrow", "Segoe UI", sans-serif;
  --font-mono: "Cascadia Mono", "DM Mono", ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  --font-body: Inter, "Segoe UI", system-ui, sans-serif;
}
```

Body background:

```css
body {
  margin: 0;
  overflow: hidden;
  background:
    linear-gradient(125deg, #020807 0%, #03100e 44%, #061411 78%, #081816 100%);
  color: var(--text);
  font-family: var(--font-body);
  font-size: 14px;
  letter-spacing: 0;
}
```

Rules:

- Keep the dark teal base, teal/lime accents, amber warnings, and muted text.
- Do not switch a module to an upstream project's branding inside Manager.
- Do not use broad decorative gradients, orbs, bokeh, or marketing-page art.
- Letter spacing on normal text is `0`; section micro-labels may use `0.06em`.

## Typography

Use body text for controls and status, mono text for command labels, section
markers, file pills, and technical status.

Recommended values:

```css
body { font-size: 14px; }
.section-title {
  color: var(--lime);
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}
.section-title::before { content: "//"; color: var(--teal); }
label {
  color: #dcefeb;
  font-size: 11.5px;
  font-weight: 750;
}
.note {
  color: #789d98;
  font-size: 11.5px;
  line-height: 1.35;
}
```

Section titles should read like Nymphs module panels:

```text
// Source
// Runtime
// Advanced parameters
// Status
```

## Sidebar Sections

The left rail should be dense, repeatable, and work-focused.

Section standard:

```css
.section {
  padding: 15px 0;
  border-top: 1px solid rgba(33, 71, 74, 0.72);
}
.section:first-child {
  padding-top: 0;
  border-top: 0;
}
```

Recommended order for 3D generation modules:

```text
Source
Runtime or Run
Advanced parameters
Status
```

Source section should include:

```text
source header
image drop zone
source command row
run command row
utility command row
```

## Source Drop Zone

Use a square source preview with image containment:

```css
.drop {
  position: relative;
  aspect-ratio: 1 / 1;
  width: 100%;
  display: grid;
  place-items: center;
  padding: 10px;
  border: 1px dashed rgba(41, 230, 218, 0.36);
  border-radius: 8px;
  background:
    radial-gradient(circle at 50% 28%, rgba(41, 230, 218, 0.08), transparent 34%),
    rgba(11, 21, 20, 0.66);
  overflow: hidden;
  cursor: pointer;
}

.drop img {
  display: none;
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  padding: 10px;
  object-fit: contain;
  object-position: center;
}
```

The source image must never crop. Use `object-fit: contain`.

Use a subtle transparent/checker treatment for selected images when useful, but
keep it quiet and consistent with TRELLIS/Pixal3D.

## Command Buttons

Buttons are compact command controls, not large marketing CTAs.

Button standard:

```css
.btn {
  min-height: 28px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 5px 8px;
  border: 1px solid var(--line-2);
  border-radius: 5px;
  color: var(--teal);
  background: rgba(15, 26, 28, 0.85);
  font-family: var(--font-mono);
  font-size: 10.5px;
  font-weight: 850;
  line-height: 1.1;
  white-space: nowrap;
  cursor: pointer;
}
.btn::before { content: "//"; color: var(--lime); }
.btn.primary {
  color: #06110f;
  border-color: var(--lime);
  background: linear-gradient(90deg, var(--lime), rgba(41, 230, 218, 0.92));
  font-weight: 950;
}
.btn.primary::before { color: #06110f; }
.btn.danger {
  color: #ffb15f;
  border-color: rgba(255, 139, 39, 0.52);
  background: rgba(37, 20, 12, 0.82);
}
```

Command row standard:

```css
.command-deck {
  display: grid;
  gap: 7px;
  margin-top: 10px;
}
.command-row {
  display: grid;
  gap: 7px;
  align-items: stretch;
}
.command-row .btn {
  width: 100%;
  min-width: 0;
  height: 30px;
  min-height: 30px;
  padding: 3px 8px;
  font-size: 9.8px;
  text-align: center;
}
.source-tools { grid-template-columns: 1.22fr 0.78fr; }
.run-tools { grid-template-columns: 1fr 1fr; }
.run-actions,
.utility-actions { grid-template-columns: repeat(3, minmax(0, 1fr)); }
```

Rules:

- Use short labels: `Warm`, `Generate`, `Export`, `Open GLB`, `Clear GPU`,
  `Kill`, `Open Outputs`.
- Primary action is the run/generate action.
- Danger action is only for kill/stop/destructive runtime cleanup.
- Keep command rows stable. Disabled buttons stay in place.
- If lucide icons are present, they may be hidden to preserve the proven `//`
  command style.

## Fields And Controls

Controls should fit inside the 360px rail.

Use two-column grids for normal runtime controls:

```css
.controls,
.params {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}
.field,
.toggle-row,
.toggle-field { min-width: 0; }
.field.wide,
.toggle-row.wide { grid-column: 1 / -1; }
input,
select {
  width: 100%;
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--line-2);
  border-radius: 6px;
  outline: none;
}
```

Use:

- `select` for runtime presets, model weights, resolution, texture size.
- `checkbox` for binary options such as low VRAM, source prep, RMBG GPU.
- `range` for step counts when the current value should stay visible.
- `number` for seed, faces, max tokens, guidance, and FOV.
- `details.advanced` for expert parameters.

Do not put long prose inside the app UI. Put setup explanation in Manager
Details or module docs.

## Advanced Panel

Advanced controls should be available but visually secondary.

Rules:

- Use a native `details` block.
- Label it `Advanced parameters`, `Advanced TRELLIS Hooks`, or a similarly
  module-specific expert label.
- Keep advanced grids two-column except for sliders and long toggles.
- Do not put first-run required choices only in advanced.

## Status Block

The sidebar status block summarizes current UI state and output.

Recommended fields:

```text
State or Mode
Model or Weights
Camera, Texture, or Runtime
Output
```

Use muted mono-style status for paths, runtime URLs, and technical hints. Keep
details concise.

## Viewer Standard

3D output UIs use `model-viewer`.

Include the vendored script:

```html
<script type="module" src="/assets/vendor/model-viewer-4.0.0.min.js"></script>
```

Use the element shape:

```html
<div class="empty" id="empty"><div>Generated GLB appears here.</div></div>
<model-viewer
  id="model"
  camera-controls
  auto-rotate
  shadow-intensity="0.6"
  exposure="1">
</model-viewer>
```

Model viewer CSS:

```css
model-viewer {
  display: none;
  width: 100%;
  height: 100%;
  min-height: 280px;
  background:
    radial-gradient(circle at 50% 42%, rgba(41, 230, 218, 0.08), transparent 38%),
    #030807;
}
```

TRELLIS-style full-stage viewer may use:

```css
model-viewer {
  width: 100%;
  height: 100%;
  min-height: 420px;
  display: none;
  background: transparent;
}
```

Rules:

- Use `camera-controls`, `auto-rotate`, `shadow-intensity="0.6"`, and
  `exposure="1"`.
- Empty state text is `Generated GLB appears here.` unless the module has a
  stronger output type.
- Keep the viewer hidden until a valid GLB is loaded.
- Reset or replace the `model-viewer` before loading a new GLB if stale WebGL
  state can remain.
- Use cache-busted output URLs when replacing a GLB generated at the same path.

## Image Viewer Standard

2D image workbenches use a large image preview shell plus an output picker
strip. This is the Nymphs Image pattern.

Stage shape:

```html
<section class="stage">
  <section class="viewer">
    <div class="image-shell">
      <img id="result-image" alt="Generated image">
      <div class="empty" id="empty" aria-label="Generated image appears here."></div>
    </div>
  </section>
  <section class="progress">
    <!-- status, progress, output strip, gallery -->
  </section>
</section>
```

CSS:

```css
.stage {
  min-width: 0;
  min-height: 0;
  height: 100%;
  display: grid;
  grid-template-rows: minmax(0, 1fr) auto;
  background: rgba(7, 16, 15, 0.88);
}

.viewer {
  min-height: 0;
  display: grid;
  place-items: center;
  padding: 16px 24px 14px;
  position: relative;
  overflow: hidden;
}

.image-shell {
  width: 100%;
  height: 100%;
  max-height: none;
  min-height: 0;
  border: 1px solid rgba(40, 68, 70, 0.9);
  border-radius: 8px;
  background:
    url("/ui/nymphs_preview_forest.png") center center / cover no-repeat,
    rgba(4, 12, 11, 0.82);
  display: grid;
  place-items: center;
  overflow: hidden;
  position: relative;
}

.image-shell img {
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  object-fit: contain;
  object-position: center center;
  display: none;
  position: relative;
  z-index: 2;
}

.empty {
  display: grid;
  gap: 8px;
  place-items: center;
  color: var(--dim);
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 900;
  text-align: center;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  padding: 20px;
  position: relative;
  z-index: 1;
}
.empty::before {
  content: "Generated image appears here.";
  color: var(--cyan);
  font-size: 11px;
  line-height: 1;
  white-space: nowrap;
}
```

Rules:

- Use `object-fit: contain`; generated images must never crop.
- The image preview background is part of the Nymphs Image look. Use a module
  asset such as `/ui/nymphs_preview_forest.png` plus the dark fallback color.
- Empty state text should say `Generated image appears here.`
- Use cache-busted output URLs when refreshing generated images.
- If the current image is missing or broken, hide it and restore the empty
  state.

## Viewer Background And Shell

The viewer area must feel like the same Nymphs stage across modules.

Pixal3D pane body:

```css
.pane-body {
  display: grid;
  place-items: center;
  border: 1px solid rgba(40, 68, 70, 0.9);
  border-radius: 8px;
  background:
    radial-gradient(circle at 50% 36%, rgba(41, 230, 218, 0.06), transparent 38%),
    rgba(4, 12, 11, 0.72);
  overflow: hidden;
  position: relative;
}
```

TRELLIS stage:

```css
.stage {
  min-width: 0;
  min-height: 0;
  height: 100%;
  position: relative;
  display: grid;
  grid-template-rows: 1fr auto;
  border-left: 1px solid rgba(33, 71, 74, 0.58);
  background:
    radial-gradient(circle at 50% 18%, rgba(41, 230, 218, 0.07), transparent 36%),
    rgba(3, 10, 10, 0.35);
}
```

Choose the Pixal3D framed pane for modules with a topbar/warmup strip and the
TRELLIS full-stage shell for simpler direct-generate modules. Do not invent a
third visual language without updating this standard.

For 2D image modules, use the Nymphs Image `.image-shell` instead of
`model-viewer`. Do not wrap the image preview in extra cards; the shell is the
frame.

## Preview And Progress Strip

Every generation UI must include a compact progress strip connected to the
viewer. This is part of the Nymphs look.

### 3D Progress Strip

Pixal3D-style strip:

```html
<div class="top-progress">
  <span class="top-progress-stage" id="progress-stage">Idle</span>
  <div class="top-progress-copy">
    <span class="top-progress-text" id="top-progress-text">Idle</span>
    <span class="top-progress-extra" id="top-progress-extra">Waiting for source image.</span>
  </div>
  <div class="bar"><span id="top-progress-bar"></span></div>
</div>
```

CSS:

```css
.top-progress {
  min-width: 0;
  min-height: 44px;
  display: grid;
  grid-template-columns: auto minmax(82px, max-content) minmax(0, 1fr);
  grid-template-rows: auto 6px;
  align-items: center;
  gap: 7px 10px;
  padding: 8px 18px;
  border-top: 1px solid var(--line);
  background: rgba(11, 23, 24, 0.92);
  color: var(--muted);
  font-size: 12px;
}
.top-progress-stage {
  color: var(--text);
  font-weight: 900;
  white-space: nowrap;
}
.top-progress-copy {
  min-width: 0;
  display: grid;
  gap: 3px;
}
.top-progress-text,
.top-progress-extra {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.top-progress-extra {
  color: var(--muted);
  font-size: 10px;
}
.bar {
  grid-column: 1 / -1;
  height: 6px;
  overflow: hidden;
  border-radius: 999px;
  background: #223532;
}
.bar span {
  display: block;
  width: 0%;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--teal), var(--lime), var(--amber));
  transition: width 160ms ease;
}
```

TRELLIS-style dock:

```css
.progress-dock {
  display: grid;
  gap: 7px;
  padding: 12px 14px;
  border-top: 1px solid rgba(33, 71, 74, 0.66);
  background: rgba(7, 16, 16, 0.8);
}
.bar {
  height: 5px;
  border-radius: 999px;
  overflow: hidden;
  background: rgba(33, 71, 74, 0.58);
}
.bar span {
  background: linear-gradient(90deg, var(--lime), var(--teal));
  transition: width 0.24s ease;
}
```

Rules:

- Progress strip is always visible.
- It should not push or resize the viewer unpredictably.
- Use stage, detail, extra text, and a thin bar.
- Poll progress every 750-1000ms while a task is active.
- Use `0%` for idle, partial percentages during work, and `100%` for complete.
- On errors, stop polling and surface the error in the strip and toast/status.

### 2D Image Progress And Output Strip

Nymphs Image combines generation status, selected model, output folder browser,
selection actions, and thumbnail gallery in the lower strip.

HTML shape:

```html
<section class="progress">
  <div class="status-line">
    <span id="runtime-text">No model loaded</span>
    <span id="output-label">No output</span>
  </div>
  <div class="model-line"><span>Selected</span> <span id="selected-model-text">checking</span></div>
  <div class="progress-line">
    <span class="progress-stage" id="progress-stage">Idle</span>
    <span id="progress-detail">Waiting for prompt.</span>
    <span id="progress-percent">0%</span>
  </div>
  <div class="bar"><span id="progress-bar"></span></div>
  <div class="strip-head">
    <div class="strip-title"><span>Browse</span><span id="strip-label">Outputs</span></div>
    <div class="strip-controls">
      <button id="strip-prev" type="button" title="Previous output">&lt;</button>
      <button id="strip-next" type="button" title="Next output">&gt;</button>
    </div>
  </div>
  <div class="strip-browser">
    <select class="folder-select" id="output-folder" aria-label="Output folder"></select>
    <div class="strip-select-actions">
      <!-- folder menu and selection actions -->
    </div>
  </div>
  <input id="folder-picker" type="file" accept="image/*" webkitdirectory multiple hidden>
  <div class="gallery" id="gallery"></div>
</section>
```

CSS:

```css
.progress {
  padding: 9px 24px 10px;
  display: grid;
  gap: 6px;
  align-content: end;
  background: rgba(11, 23, 24, 0.92);
}
.status-line {
  display: flex;
  justify-content: space-between;
  gap: 14px;
  color: var(--muted);
  font-family: var(--font-mono);
  font-size: 11px;
  overflow: hidden;
}
.model-line {
  min-width: 0;
  overflow: hidden;
  color: var(--dim);
  font-family: var(--font-mono);
  font-size: 10.5px;
  white-space: nowrap;
  text-overflow: ellipsis;
}
.progress-line {
  display: grid;
  grid-template-columns: 160px minmax(0, 1fr) 86px;
  gap: 10px;
  color: var(--muted);
  align-items: center;
}
.progress-stage {
  color: var(--text);
  font-family: var(--font-mono);
  font-size: 10px;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}
.bar {
  height: 6px;
  border-radius: 999px;
  background: #223532;
  overflow: hidden;
}
.bar span {
  display: block;
  height: 100%;
  width: 0%;
  background: linear-gradient(90deg, var(--cyan), var(--green), var(--warn));
  transition: width 0.22s ease;
}
```

Rules:

- The image progress area is a work strip, not a separate card.
- Runtime/model/output lines stay above the progress bar.
- Folder browser and thumbnail gallery stay below the progress bar.
- Poll `/active_task` around every `700ms` while generation is active.
- Keep recent output limit bounded, usually `80`.

## Warmup Strip

For modules that benefit from explicit model warmup, use the Pixal3D strip above
the preview:

```css
.warmup-strip {
  min-height: 44px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  grid-template-rows: auto 5px;
  align-items: center;
  gap: 7px 10px;
  padding: 8px 18px;
  border-bottom: 1px solid var(--line);
  background: rgba(9, 20, 20, 0.9);
}
.warmup-bar {
  grid-column: 1 / -1;
  height: 5px;
  overflow: hidden;
  border-radius: 999px;
  background: #213733;
}
.warmup-bar span {
  display: block;
  width: 0%;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--teal), var(--lime), var(--amber));
}
```

Use this only when warmup is a real user-visible runtime state. Do not add a
warmup strip to modules that do not need it.

## Image Picker And Output Browser

Nymphs Image proves the shared image picker architecture: a large current
preview, an output folder selector, previous/next buttons, selection actions,
and a horizontal thumbnail gallery.

CSS:

```css
.strip-browser {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) auto;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}
.folder-select {
  height: 28px;
  font-size: 11px;
  font-family: var(--font-mono);
}
.strip-select-actions {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  gap: 6px;
  min-width: 0;
  white-space: nowrap;
}
.gallery {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: 124px;
  gap: 8px;
  overflow-x: auto;
  overscroll-behavior-inline: contain;
  min-height: 0;
}
.gallery:empty { display: none; }
.thumb {
  border: 1px solid rgba(255,255,255,0.09);
  border-radius: 8px;
  background: #08100f;
  padding: 5px;
  display: grid;
  grid-template-rows: 60px auto;
  gap: 5px;
  cursor: pointer;
  min-width: 0;
  position: relative;
}
.thumb img {
  width: 100%;
  height: 60px;
  object-fit: contain;
  object-position: center center;
  border-radius: 5px;
  background: #020403;
}
.thumb span {
  color: var(--muted);
  font-size: 11px;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}
.thumb.selected { border-color: var(--green); }
.thumb.checked { border-color: var(--cyan); }
```

Output view model:

```js
{
  outputs: [],
  localOutputs: [],
  localObjectUrls: [],
  visibleOutputs: [],
  selectedOutputPaths: new Set(),
  selectedOutput: null,
  outputView: "recent",
  viewerSource: "outputs",
  manualFolderName: ""
}
```

Output records should include:

```json
{
  "name": "image.png",
  "path": "/absolute/path/for-managed-output.png",
  "relative_path": "group/image.png",
  "folder": "group",
  "url": "/outputs/group/image.png",
  "metadata_path": "/absolute/path/for-managed-output.json",
  "provider": "zimage",
  "mode": "txt2img",
  "prompt": "short prompt",
  "batch_id": "zimage-...",
  "batch_label": "Z-Image Variants",
  "batch_type": "zimage",
  "item_label": "Z 1",
  "item_index": 1,
  "item_total": 4,
  "created": 1770000000.0,
  "metadata": {}
}
```

Folder views:

```text
recent                 all recent outputs by modified time
date:YYYY-MM-DD        outputs grouped by generated/modified date
folder:<name>          managed output group folder
manual folder name     browser-local directory selected with webkitdirectory
```

Rules:

- The gallery shows managed outputs from `/api/outputs` plus browser-local
  manual folder outputs.
- Managed output records may be moved or deleted by backend routes.
- Manual folder records must be marked `managed: false` and must never be sent
  to backend delete/move routes.
- Gallery wheel events should scroll horizontally.
- Thumbnails are 124px columns with 60px contained images.
- `Select`, `Select All`, `Clear`, `Move Selected`, `Delete`, and `Delete
  Folder` are optional only when the module has no managed output browser.
- Destructive actions must confirm and must operate only under the module
  output root.

## Image Source Reuse

Image workbenches should let users promote the current preview image into a
source or guide image without forcing a filesystem round trip.

Pattern:

```js
async function outputAsFile(output) {
  if (output.file instanceof File) return output.file;
  const response = await fetch(output.url, { cache: "no-store" });
  if (!response.ok) throw new Error("Could not read current preview image.");
  const blob = await response.blob();
  return new File([blob], output.name || "output.png", { type: blob.type || "image/png" });
}
```

Use this for:

```text
Use Selected as Source
Use Selected as Guide
Use Selected as Parts Source
```

Rules:

- The selected preview is the source of truth for reuse actions.
- Reuse actions should call the same source setter as normal file picking.
- Source previews must also use `object-fit: contain`.
- Blob URLs for manual folder files must be revoked when replaced.

## Prompt And Preset Architecture

Nymphs Image proves shared prompt/preset directories and APIs. Reuse this shape
for prompt-heavy image modules.

Preset kinds:

```text
subject
style
view
negative
part_guidance
saved
settings
```

Directory contract:

```text
$HOME/NymphsData/config/image_presets/<kind>__<preset>.json
$HOME/NymphsData/config/image_settings_presets/<preset>.json
$HOME/NymphsData/config/image_prompts/parts.py
```

API contract:

```text
GET    /api/presets
POST   /api/presets/<kind>
DELETE /api/presets/<kind>/<preset_id>
```

Rules:

- Packaged defaults may seed user preset directories once.
- User-edited prompt presets live in shared NymphsData config paths.
- Preset ids must be slug-safe.
- `settings` presets store a `values` object; prompt-like presets store
  `prompt` or `style`.
- Prompt editors should be modal or details-based, not a separate page.
- Do not store provider API keys inside prompt presets.

## Scrollbars

Use the thin teal scrollbar treatment:

```css
* {
  scrollbar-width: thin;
  scrollbar-color: rgba(41, 230, 218, 0.78) transparent;
}
::-webkit-scrollbar { width: 6px; height: 6px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb {
  border-radius: 999px;
  background: rgba(41, 230, 218, 0.72);
}
::-webkit-scrollbar-thumb:hover {
  background: rgba(41, 230, 218, 0.95);
}
::-webkit-scrollbar-corner { background: transparent; }
```

## Toasts

Toasts are small status confirmations, not primary workflow UI.

Pixal3D-style:

```css
.toast {
  position: fixed;
  right: 18px;
  bottom: 18px;
  max-width: 360px;
  padding: 12px 14px;
  border: 1px solid #365f59;
  border-radius: 8px;
  color: var(--text);
  background: #111d1b;
  box-shadow: 0 18px 48px rgba(0,0,0,0.35);
}
```

TRELLIS-style centered toast is also acceptable:

```css
.toast {
  position: fixed;
  left: 50%;
  bottom: 18px;
  transform: translateX(-50%);
  max-width: min(720px, calc(100vw - 36px));
}
```

Use one toast style consistently inside a module.

## JavaScript State Contract

Keep frontend state explicit and boring.

Recommended 3D state keys:

```js
{
  imageFile,
  imageUrl,
  preparedFile,
  outputUrl,
  outputName,
  busy,
  progressTimer,
  warmupTimer,
  modelReady
}
```

Recommended 2D image state keys:

```js
{
  sourceFile,
  sourceDataUrl,
  outputs: [],
  localOutputs: [],
  localObjectUrls: [],
  visibleOutputs: [],
  selectedOutputPaths: new Set(),
  selectedOutput,
  outputView: "recent",
  viewerSource: "outputs",
  manualFolderName: "",
  activeBatchId,
  parts: [],
  busy,
  progressTimer,
  userPresets,
  serverInfo
}
```

Rules:

- Disable source replacement and destructive runtime actions during active
  generation unless the action is an explicit kill/stop path.
- Disable `Generate` until a source image and required warmup/prep state are
  ready.
- Clear stale output preview state when a new source is selected.
- Keep progress timers scoped and clear them after completion or failure.
- Use `navigator.sendBeacon` or a Manager action bridge only for explicit
  cleanup on UI close; do not hide destructive data deletion in unload hooks.
- Revoke browser-created object URLs when manual folder outputs are replaced.
- Keep selected output keys stable across refreshes by using `relative_path`,
  then `path`, then `url` as fallback identity.

## Module Action Bridge

Use the Manager action bridge for module-owned actions that should run outside
the web backend or survive a blocked backend:

```js
window.chrome.webview.postMessage({
  type: "module_action",
  requestId: "kill-1",
  action: "kill",
  args: { reason: "ui_kill" }
});
```

Use this for:

- `kill`
- `open_outputs`
- backend restart through `open_nymph_ui`
- other manifest-declared module actions

Do not run arbitrary shell from the frontend.

## Accessibility And Fit

Rules:

- Controls must not overlap at the default Manager WebView size.
- Long option names should be shortened in labels and explained in tooltips,
  Details, or docs.
- Text inside buttons must fit inside the 360px rail.
- Source preview and model viewer must maintain stable dimensions while loading.
- Use `aria-live="polite"` on progress/warmup strips when practical.
- Native inputs are preferred over custom widgets unless the proof modules adopt
  a shared replacement.

## What Not To Do

Do not:

- Embed upstream Gradio as the primary Manager UI when a Nymphs custom UI is
  feasible.
- Change the left rail width from `360px`.
- Change the collapse point from `760px`.
- Replace `model-viewer` with a different preview for GLB modules without
  updating this standard.
- Put a landing page or marketing hero inside a module UI.
- Add large decorative cards around every section.
- Put cards inside cards.
- Use remote CDN assets.
- Run model downloads, model scans, or heavy imports during page load.
- Use module-specific Manager code for custom UI behavior.
- Let one module's labels or advanced controls stretch the shared layout.

## New 3D Module Checklist

Before considering a custom 3D module UI ready:

- Uses `local_url` Manager UI at `/nymph`.
- `open_nymph_ui` starts/reuses backend and prints `url=` and `module_ui_url=`.
- Left rail is exactly `360px`.
- Collapse point is exactly `760px`.
- Right pane uses `model-viewer` for GLB preview.
- Empty state says generated GLB will appear there.
- Viewer background matches TRELLIS/Pixal3D dark teal stage.
- Source drop zone is square and uses `object-fit: contain`.
- Command rows use compact `//` Nymphs buttons.
- Runtime controls fit in two columns inside the rail.
- Advanced controls are behind `details`.
- Progress strip is always present and visually connected to the viewer.
- Progress bar uses the Nymphs thin rounded bar treatment.
- Backend has cheap `/health` and `/server_info`.
- Progress polling does not require heavyweight model imports.
- Generated outputs are served from the module output directory.
- UI uses vendored assets, not CDNs.
- Closing Manager or UI does not leave unmanaged generation/fetch jobs behind.

## New Image Module Checklist

Before considering an image workbench UI ready:

- Uses `local_url` Manager UI at `/nymph`.
- Uses the Nymphs Image compact workbench family unless this standard changes.
- Keeps image rail width at `clamp(260px, 28vw, 300px)`.
- Keeps image collapse point at `600px`.
- Uses large `.image-shell` preview with contained image output.
- Empty state says generated image appears there.
- Has the Nymphs Image progress block with runtime, selected model, stage,
  detail, percent, and thin bar.
- Has output browser strip with folder/date/recent views when outputs are
  browsable.
- Uses 124px thumbnail columns with 60px contained thumbnail images.
- Separates managed outputs from manual browser-local folder picks.
- Sends only managed output paths to backend move/delete routes.
- Uses safe backend file serving under the declared output root.
- Supports source/guide reuse from the current preview when image-to-image,
  guide images, or part extraction exist.
- Stores user prompt/settings presets under shared NymphsData config paths.
- Keeps provider secrets out of presets, logs, and output metadata.
- Uses compact `//` command buttons and the shared color/type system.
- Does not add a third sidebar width, breakpoint, or gallery design.

## TripoSplat Target

The TripoSplat module should follow this standard directly:

```text
same 360px left rail
same 760px collapse
same source image drop zone
same compact command rows
same runtime/advanced/status section stack
same model-viewer GLB preview
same dark viewer background
same progress strip
same local_url /nymph Manager contract
```

TripoSplat may use upstream Gradio code as reference or internal backend
plumbing, but the Manager-facing UI should look and behave like a Nymphs 3D
module.
