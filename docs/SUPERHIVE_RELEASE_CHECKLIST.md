# Superhive Release Checklist

This checklist is the practical release path for the Blender addon-focused
NymphsCore launch.

The goal is an "addon-first" public release where a new user can install
NymphsCore Manager, install the required modules, install the Blender addon from
Superhive, and run the core image, parts, shape, texture, and 3D workflows
without needing developer knowledge.

## Release Scope

This checklist is for the Superhive / Blender addon release path. TripoSplat is
not a Superhive v1 release dependency.

TripoSplat working notes, dev-registry state, runtime testing, Gaussian splat
preview work, Blender-side questions, and splat-to-mesh conversion research now
live in:

```text
../../NymphsModules/triposplat/docs/TRIPOSPLAT_HANDOFF.md
```

Do not spend Superhive release time wiring docs, addon panels, tutorial videos,
or listing copy around TripoSplat. Keep that work in the splat module handoff
and research lane.

Use this alongside:

- [Nymphs Module Making Guide](NYMPHS_MODULE_MAKING_GUIDE.md)
- [Nymphs Module UI Standard](NYMPHS_MODULE_UI_STANDARD.md)
- [Blender Addon User Guide](BLENDER_ADDON_USER_GUIDE.md)
- [Nymphs-Brain Guide](NYMPHS_BRAIN_GUIDE.md)
- [Install Disk And Model Footprint](FOOTPRINT.md)

## Release Thesis

The public release should be centered on the Blender addon and the backend
modules it needs.

Public hero stack:

- NymphsCore Manager
- Nymphs Blender addon
- Nymphs Image
- Brain
- TRELLIS.2
- Pixal3D

Dev or backbench stack:

- Nymphs World
- Nymphs Sprite
- WORBI, if the release should stay addon-focused
- LoRA, if training should not be part of the first Superhive path

The release is not "everything NymphsCore can do." It is the cleanest public
path for creating image references, extracting useful character/object parts,
generating 3D drafts, and bringing the results back into Blender.

## Non-Negotiable Workflow Rules

Before using this checklist, keep these rules visible:

- Source work happens in the dev WSL/repo checkout.
- The managed/runtime/test WSL is for end-user testing only.
- Do not manually sync files into the test WSL unless explicitly approved.
- Do not hand-edit installed module files, version markers, cached manifests, or
  runtime state.
- Test like an end user: publish module, update registry if needed, then use
  Manager install/update.
- Module install/update scripts own writing the installed `nymph.json` and
  `.nymph-module-version`.
- The marker file is the installed-version source of truth.
- Installed workflow buttons belong in the module `nymph.json`, not the
  registry.
- Registry edits are only for published module metadata/catalog changes.
- Never advertise a registry/module version that is not pushed and available
  from GitHub raw.
- Manager changes require version bump, Windows release zip build, commit, and
  push as one coherent release change.
- Local sanity checks are allowed only when clearly labelled as local sanity
  checks. They do not replace the real published test path.

## Current Source State

Snapshot from the release planning pass on 2026-06-05.

Core module state:

- `zimage` / Nymphs Image: `0.1.101`
- `brain`: `0.1.13`
- `trellis`: `0.1.38`
- `pixal3d`: `0.1.112`
- `lora`: `0.1.43`
- `worbi`: `6.3.24`
- `nymphs-world`: `0.2.13`, dev registry
- `nymphs-sprite`: `0.1.13`, dev registry

Current public registry includes:

- `brain`
- `zimage`
- `trellis`
- `pixal3d`

Current dev registry includes:

- `nymphs-world`
- `nymphs-sprite`
- `lora`
- `worbi`

Known source gaps:

- TripoSplat is tracked separately in its module handoff, not in this release
  checklist.
- Blender addon service list is still centered on `Z-Image` and `TRELLIS.2`.
- Blender addon docs and GitPages still refer to TRELLIS.2 port `8094`, while
  the module manifest serves TRELLIS.2 on `8095`.
- Manager UIs already expose more capability than the addon docs describe.
- Nymphs Image Manager UI supports Brain Vision planning and Qwen local
  extraction, but addon parts documentation still describes Gemini as the
  parts path.
- GitPages still says the addon baseline has only two required modules.
- WORBI and LoRA are public modules even though they may distract from the
  addon-first launch.
- GitPages mobile wiki layout keeps a narrow sidebar on small screens and
  should be reworked for comfortable phone reading.

## Definition Of Done

The release is ready when all of these are true:

- [ ] Fresh user can install Manager from the public zip.
- [ ] Fresh user can install the Blender addon from the intended Superhive path.
- [ ] Fresh user can install every public hero module from Manager.
- [ ] Fresh user can fetch required model assets from Manager.
- [ ] Nymphs Image works in Manager and Blender.
- [ ] Z-Image Turbo generation works in Manager and Blender.
- [ ] Qwen Image Edit works in Manager and Blender where exposed.
- [ ] Gemini Flash works in Manager and Blender when OpenRouter is configured.
- [ ] Brain local vision planning works from the intended UI surface.
- [ ] Local parts extraction path is clearly documented and tested.
- [ ] TRELLIS.2 works in Manager and Blender.
- [ ] Pixal3D works in Manager and Blender if it is advertised as a Blender
  backend.
- [ ] Texture and retexture paths work from Blender.
- [ ] GitPages presents the public release stack clearly.
- [ ] GitPages mobile layout is readable.
- [ ] Videos/tutorials cover the actual user path.
- [ ] Public registry advertises only pushed, raw-available module versions.
- [ ] Dev/backbench modules are not mixed into the main public path.

## Phase 0: Freeze The Release Shape

Purpose: stop the target from moving while the release is being hardened.

Decisions to make:

- [x] Move LoRA to dev/backbench for Superhive v1.
- [x] Move WORBI to dev/backbench for Superhive v1.
- [ ] Decide whether Pixal3D must be Blender-integrated for Superhive v1, or
  can be Manager-only in v1.
- [x] Keep TripoSplat dev/experimental for Superhive v1; do not require Manager
  or Blender release integration.
- [ ] Decide whether "local parts extraction" means:
  - Brain Vision local planning plus Qwen local extraction, or
  - Brain Vision planning plus Gemini extraction fallback, or
  - both paths exposed with clear labels.
- [ ] Decide the exact Superhive addon package version target.
- [ ] Decide the exact Manager version target.

Output:

- [ ] A one-page release target statement in this checklist or a release issue.
- [ ] A final public module list.
- [ ] A final dev/backbench module list.

Do not proceed to registry reshaping until this phase is settled.

## Phase 1: Backend Readiness Audit

Purpose: confirm each backend works through its module-owned, Manager-installed
path before wiring or documenting the addon around it.

Use the real test path:

```text
module source edit -> version bump -> push module -> verify raw nymph.json ->
registry update if needed -> Manager install/update -> test
```

### Nymphs Image

Current module: `NymphsModules/zimage`

Key runtime:

```text
http://127.0.0.1:8090
http://127.0.0.1:8090/nymph
```

Checklist:

- [ ] Install/update Nymphs Image through Manager.
- [ ] Verify installed marker reports the expected version.
- [ ] Verify `GET /health`.
- [ ] Verify `GET /server_info`.
- [ ] Verify `GET /nymph`.
- [ ] Fetch complete recommended image model package.
- [ ] Confirm status reports Z-Image ready.
- [ ] Confirm status reports Qwen Image Edit readiness when Qwen weights are
  fetched.
- [ ] Confirm status reports Brain readiness and local parts readiness when
  Brain is installed and configured.
- [ ] Generate one Z-Image text-to-image output.
- [ ] Generate one Z-Image image-to-image output if the selected runtime enables
  image-to-image.
- [ ] Generate one Qwen Image Edit output from a guide image.
- [ ] Generate one Gemini Flash output with saved/shared OpenRouter key.
- [ ] Generate one Gemini guide-image output.
- [ ] Use output picker recent view.
- [ ] Use output picker date view.
- [ ] Use output picker folder view.
- [ ] Move selected outputs into a folder.
- [ ] Delete selected managed outputs.
- [ ] Confirm browser-local/manual picked folders are not deleted by managed
  output actions.
- [ ] Confirm preview forest background appears where expected.

Known risks:

- Qwen Image Edit is heavier than Z-Image Turbo; test memory behavior.
- Z-Image image-to-image may depend on runtime flags and current Diffusers
  support.
- OpenRouter key paths exist both in Brain and Nymphs Image; document which
  shared key is used where.

### Brain

Current module: `NymphsModules/brain`

Key runtimes:

```text
llama-server: http://127.0.0.1:8000/v1
Open WebUI:   http://127.0.0.1:8081
mcpo:         http://127.0.0.1:8099
MCP:          http://127.0.0.1:8100
```

Checklist:

- [ ] Install/update Brain through Manager.
- [ ] Verify installed marker reports the expected version.
- [ ] Open Brain page in Manager.
- [ ] Run `Manage Models`.
- [ ] Download/select a local vision model.
- [ ] Confirm matching `mmproj` is detected or placed beside the GGUF.
- [ ] Start Brain.
- [ ] Verify `GET http://127.0.0.1:8000/v1/models`.
- [ ] Verify a multimodal chat completion with one image and one short prompt.
- [ ] Verify Brain can stop and restart without losing model selection.
- [ ] Verify Open WebUI starts separately.
- [ ] Verify OpenRouter key application for optional wrapper.
- [ ] Verify Brain status text is clear when no model is configured.

Recommended first vision model path:

```text
Qwen/Qwen3-VL-4B-Instruct-GGUF:Q4_K_M
```

Fallbacks to test:

```text
Qwen/Qwen3-VL-2B-Instruct-GGUF:Q4_K_M
Qwen/Qwen3-VL-8B-Instruct-GGUF:Q4_K_M
ggml-org/Qwen2.5-VL-7B-Instruct-GGUF
```

Known risks:

- Brain guide currently says the Blender addon does not depend on Brain. That
  must change if local parts are part of the public addon story.
- Vision model management depends on LM Studio CLI and matching projector
  handling.

### TRELLIS.2

Current module: `NymphsModules/trellis`

Key runtime:

```text
http://127.0.0.1:8095
http://127.0.0.1:8095/nymph
```

Checklist:

- [ ] Install/update TRELLIS.2 through Manager.
- [ ] Verify installed marker reports the expected version.
- [ ] Fetch `Q5_K_M` first unless testing a lower VRAM path.
- [ ] Verify `GET /health`.
- [ ] Verify `GET /server_info`.
- [ ] Verify `GET /nymph`.
- [ ] Generate a shape-only GLB from a clean source image.
- [ ] Generate a shape plus texture GLB.
- [ ] Run retexture from an existing mesh.
- [ ] Confirm generated outputs and logs are under `$HOME/NymphsData`.
- [ ] Confirm model viewer loads the GLB.
- [ ] Confirm progress strip reports meaningful stages.
- [ ] Confirm addon default port is updated to `8095`.
- [ ] Confirm GitPages and addon guide no longer say `8094`.

Known risks:

- TRELLIS.2 and Pixal3D share runtime assumptions around `$HOME/TRELLIS.2/.venv`.
- FlashAttention/build cache handling must not spill into fragile temp paths.

### Pixal3D

Current module: `NymphsModules/pixal3d`

Key runtimes:

```text
Manager UI:       http://127.0.0.1:8097/nymph
Blender API goal: confirm current API contract before addon wiring
```

Checklist:

- [ ] Install/update Pixal3D through Manager.
- [ ] Verify installed marker reports the expected version.
- [ ] Fetch the recommended first model profile.
- [ ] Verify `GET /health`.
- [ ] Verify `GET /server_info`.
- [ ] Verify `GET /nymph`.
- [ ] Generate one textured GLB through the Manager UI.
- [ ] Confirm model viewer loads the GLB.
- [ ] Confirm source preview, top command strip, progress strip, and output
  controls follow the UI standard.
- [ ] Confirm gated/license messaging is visible before model fetch.
- [ ] Confirm Pixal3D install/repair/uninstall does not break TRELLIS.2 shared
  runtime behavior.
- [ ] Decide and implement Blender addon API wiring if Pixal3D is a Superhive
  v1 Blender backend.

Known risks:

- Current GitPages notes say Pixal3D generation still uses safetensors until the
  GGUF loader bridge is implemented.
- Pixal3D may be Manager-ready before it is addon-ready.

## Phase 2: Blender Addon Integration

Purpose: make the addon match the advertised release stack.

Current addon source:

```text
NymphsExt/Nymphs.py
```

Current narrowness from planning pass:

- `SERVICE_ORDER` contains `n2d2` and `trellis`.
- Default TRELLIS port appears to be `8094`.
- Parts operators still require OpenRouter network access.
- Pixal3D does not appear wired as a selectable Blender backend.
- TripoSplat is intentionally not a Superhive v1 Blender backend.

Checklist:

- [ ] Update TRELLIS default port to `8095`.
- [ ] Confirm old general API URL behavior is still harmless.
- [ ] Add Pixal3D service metadata if Pixal3D is in addon v1.
- [ ] Add Pixal3D shape request payload builder or adapter.
- [ ] Add Pixal3D import path for returned GLB.
- [ ] Add Pixal3D capability/status probing.
- [ ] Do not add TripoSplat service metadata for Superhive v1.
- [ ] Add local parts planner provider selection to Blender UI.
- [ ] Route Brain Vision planning without requiring OpenRouter network access.
- [ ] Route Qwen local extraction without requiring OpenRouter network access.
- [ ] Keep Gemini Flash as cloud fallback.
- [ ] Update status copy so users know which backend is local and which is
  remote.
- [ ] Update output folder names if new backends need separate directories.
- [ ] Verify `Stop All` handles every addon-managed service.
- [ ] Verify service start/stop does not manually patch runtime files.
- [ ] Bump addon version.
- [ ] Build extension package through the normal release flow.
- [ ] Test install/update through Blender's extension path.

Addon acceptance tests:

- [ ] Fresh Blender session shows the Nymphs tab.
- [ ] Server panel refreshes runtime state.
- [ ] Z-Image start/stop works.
- [ ] TRELLIS.2 start/stop works on `8095`.
- [ ] Pixal3D start/stop works if exposed.
- [ ] Z-Image txt2img creates an image.
- [ ] Z-Image img2img creates an image if supported.
- [ ] Gemini Flash creates an image when OpenRouter is configured.
- [ ] Qwen Image Edit creates an image from a guide image.
- [ ] Brain Vision plans parts locally.
- [ ] Qwen local extraction extracts selected parts.
- [ ] TRELLIS shape imports a GLB.
- [ ] TRELLIS shape plus texture imports a textured GLB.
- [ ] TRELLIS retexture imports a retextured GLB.
- [ ] Pixal3D GLB imports if exposed.

## Phase 3: Manager And Registry Focus

Purpose: make the Manager catalog match the addon-first product story.

Current Manager behavior:

- Public registry is loaded normally.
- Developer registry is included only when Developer Mode is on.
- Home page has installed and available module sections.
- It does not currently group normal modules into "release" and "dev" sections
  inside the public catalog.

Checklist:

- [ ] Decide whether dev/backbench modules are hidden from public registry or
  shown in a visible public "Dev" section.
- [ ] If hidden, move module entries from `nymphs.json` to `nymphs-dev.json`.
- [ ] If shown, implement Manager grouping/filtering and bump Manager version.
- [ ] Keep `nymphs-world` and `nymphs-sprite` in dev registry.
- [x] Move `worbi` to dev registry for Superhive v1.
- [x] Move `lora` to dev registry for Superhive v1.
- [x] Keep TripoSplat out of the public registry for Superhive v1.
- [ ] Ensure sort order makes the hero stack feel intentional:
  - Brain
  - Nymphs Image
  - TRELLIS.2
  - Pixal3D
- [ ] Confirm installed/available card text is not misleading for Manager-only
  versus Blender-integrated modules.
- [ ] Confirm module detail actions come from installed module manifests.
- [ ] Confirm registry entries do not contain installed workflow buttons.

Registry publish checklist:

- [ ] Module repo pushed.
- [ ] Raw `nymph.json` available from GitHub.
- [ ] Raw manifest version matches intended advertised version.
- [ ] Manifest hash calculated from the public raw content.
- [ ] Registry version bumped.
- [ ] Registry updated date set.
- [ ] Registry pushed.
- [ ] Manager refresh sees the new catalog state.

Manager change checklist:

- [ ] Manager version bumped.
- [ ] Windows release zip built with the repo's normal script.
- [ ] Code change, version bump, and release artifact committed together unless
  repo policy says otherwise.
- [ ] Manager repo pushed.
- [ ] Installed/running Manager is not manually patched for shortcut testing.

## Phase 4: GitPages And Docs

Purpose: make the website match the release and read well on mobile.

Known GitPages source paths:

```text
NymphsCore/home/index.html
NymphsCore/index.html
NymphsCore/home/videos.html
NymphsCore/home/guides/
```

Important source note:

- `NymphsCore/index.html` and `NymphsCore/home/index.html` are not currently
  byte-identical. Before editing public pages, confirm which file GitHub Pages
  serves, then keep the served/canonical copy coherent.

Wiki restructure checklist:

- [ ] Replace "Blender Addon Baseline: Only two required" with the release hero
  stack.
- [ ] Keep TripoSplat out of the main Superhive release story.
- [ ] Update Server Panel docs to TRELLIS.2 port `8095`.
- [ ] Update Image Panel docs for:
  - Z-Image Turbo
  - Qwen Image Edit
  - Gemini Flash
  - Brain Vision planning
  - local parts extraction route
- [ ] Update Shape Panel docs for TRELLIS.2 and optional Pixal3D if exposed.
- [ ] Update Texture Panel docs for TRELLIS.2 and any Pixal3D limitations.
- [ ] Add a "3D Backend Choices" section:
  - TRELLIS.2: GLB, texture/retexture, default Blender path
  - Pixal3D: textured GLB, separate 3D backend
- [ ] Move WORBI, LoRA, Nymphs World, and Nymphs Sprite into a Dev / More
  Modules area if they are not part of the main release path.
- [ ] Update source links and model/license notes.
- [ ] Update disk footprint numbers after final module tests.
- [ ] Link to the module UI standard for module authors.

Mobile checklist:

- [ ] Replace fixed tiny mobile wiki sidebar with a mobile-friendly nav pattern.
- [ ] Test at phone width around `390px`.
- [ ] Test at tablet width around `768px`.
- [ ] Ensure long headings wrap cleanly.
- [ ] Ensure code blocks do not force horizontal page overflow.
- [ ] Ensure video cards and callouts fit without text overlap.
- [ ] Ensure nav does not hide the first content section.
- [ ] Ensure the sidebar portrait/art does not steal reading space on mobile.
- [ ] Confirm the download button remains reachable.

Docs checklist:

- [ ] Update [Blender Addon User Guide](BLENDER_ADDON_USER_GUIDE.md).
- [ ] Update [Nymphs-Brain Guide](NYMPHS_BRAIN_GUIDE.md) if Brain is now part
  of local parts release path.
- [ ] Update [Install Disk And Model Footprint](FOOTPRINT.md).
- [ ] Update [Features](FEATURES.md).
- [ ] Update [Roadmap](ROADMAP.md).
- [ ] Keep docs explicit about local versus cloud features.
- [ ] Keep docs explicit about Manager-only versus Blender-integrated features.

## Phase 5: Tutorials And Videos

Purpose: give users a clear runway from install to useful output.

Video page currently contains placeholders. Replace them with real release
tutorials.

Required tutorial set:

- [ ] Install Manager and first launch.
- [ ] Install Blender addon from Superhive.
- [ ] Install and fetch Nymphs Image.
- [ ] Generate first Z-Image reference.
- [ ] Generate/edit with Gemini Flash.
- [ ] Edit/extract with Qwen Image Edit.
- [ ] Install Brain and download a local vision model.
- [ ] Plan parts with Brain Vision.
- [ ] Extract parts locally.
- [ ] Generate first TRELLIS.2 GLB.
- [ ] Texture and retexture with TRELLIS.2.
- [ ] Generate first Pixal3D GLB if Pixal3D is public-addon-facing.
- [ ] Full Blender loop: image -> parts -> shape -> texture -> imported result.

Each tutorial should state:

- required modules
- required model downloads
- whether it uses a local model, OpenRouter, or both
- expected wait time
- expected output folder
- common failure signs
- what to test next

Video acceptance checklist:

- [ ] Videos are linked from GitPages.
- [ ] Videos are reachable on mobile.
- [ ] Videos match current UI labels.
- [ ] Videos do not show dev-only modules in the main public path.
- [ ] Videos do not show manual runtime patching or unsupported shortcuts.
- [ ] Written guide exists for every video.

## Phase 6: End-To-End Published Testing

Purpose: prove the actual user path.

Do not count local sanity checks as this phase.

Fresh install test:

- [ ] Start from a clean or deliberately reset test environment.
- [ ] Install Manager from published zip.
- [ ] Bootstrap/import managed WSL runtime through Manager.
- [ ] Install public hero modules through Manager.
- [ ] Fetch required models through Manager.
- [ ] Install Blender addon from release channel.
- [ ] Run addon first launch.
- [ ] Run every public tutorial path.
- [ ] Stop all services.
- [ ] Restart Manager and Blender.
- [ ] Confirm installed states persist.
- [ ] Confirm no module appears as broken due only to stopped services.
- [ ] Confirm update checks behave.

Update test:

- [ ] Start from prior public Manager version.
- [ ] Update Manager through documented path.
- [ ] Update each module through Manager.
- [ ] Confirm installed markers update.
- [ ] Confirm installed `nymph.json` updates via module scripts.
- [ ] Confirm generated outputs and model caches are preserved.
- [ ] Confirm addon update path works.

Repair test:

- [ ] Interrupt one non-destructive module install or simulate missing runtime
  files in a controlled dev test.
- [ ] Use Manager repair/update path.
- [ ] Confirm module recovers without manual runtime edits.
- [ ] Confirm preserved outputs/logs/caches remain preserved.

Uninstall test:

- [ ] Normal uninstall preserves declared data.
- [ ] Purge removes declared runtime files.
- [ ] Data delete removes declared user data only when explicitly chosen.
- [ ] Uninstalling Pixal3D does not remove shared TRELLIS runtime unexpectedly.
- [ ] Uninstalling TRELLIS does not remove shared runtime unexpectedly when
  Pixal3D still needs it.

## Phase 7: Superhive Release Gate

Purpose: avoid shipping an attractive listing before the install path is solid.

Package gate:

- [ ] Addon version bumped.
- [ ] Addon package built.
- [ ] Addon package install tested in Blender.
- [ ] Addon metadata is accurate.
- [ ] Superhive listing copy matches actual public features.
- [ ] Screenshots show current UI.
- [ ] Videos show current UI.
- [ ] Support links point to GitPages and repository issue/support route.

Public feature wording gate:

- [ ] Do not claim Pixal3D Blender support unless addon path is tested.
- [ ] Do not present TripoSplat as a Superhive v1 feature.
- [ ] Do not call local parts fully local if Gemini is still required for a
  required stage.
- [ ] Do not imply Brain is optional if a highlighted local parts workflow needs
  Brain.
- [ ] Do not imply LoRA/WORBI are part of the main release if they are moved to
  dev/backbench.

Final smoke:

- [ ] Manager download link works.
- [ ] Registry raw URL works.
- [ ] Every public module raw manifest URL works.
- [ ] Every public manifest hash matches.
- [ ] GitPages home/wiki loads on desktop.
- [ ] GitPages home/wiki loads on mobile.
- [ ] Superhive addon installs.
- [ ] First image tutorial succeeds.
- [ ] First 3D tutorial succeeds.
- [ ] Texture/retexture tutorial succeeds.

## Release Board

Use this as the live high-level board.

```text
TODO
- Freeze public vs dev module list.
- Update addon service/backend support.
- Update GitPages release story.
- Produce tutorial videos.

IN PROGRESS
- Nymphs Image Manager UI already covers local/cloud image and parts paths.

BLOCKED / NEEDS DECISION
- Is Pixal3D required in Blender for Superhive v1?

DONE
- Public module standard exists.
- Public module UI standard exists.
- World and Sprite are already dev-registry modules.
- LoRA and WORBI are dev-registry modules for Superhive v1.
```

## Quick Next Actions

The recommended next working order is:

1. Decide public/dev module list for release v1.
2. Fix the addon TRELLIS port mismatch.
3. Verify Nymphs Image local parts path through Manager.
4. Decide exact Blender-side parts behavior.
5. Wire Pixal3D into Blender only if it is truly a release v1 requirement.
6. Restructure GitPages around the final hero stack.
7. Produce videos after the UI and labels stop moving.
8. Run published end-to-end tests.
9. Package and submit to Superhive.

This order keeps the release from being driven by docs or marketing before the
backend and addon contracts are genuinely proven.
