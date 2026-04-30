# NymphsCore Manager Roadmap

Local-only planning note. Do not publish this until the commercial install story is ready.

Last updated: 2026-04-30

## Requested Work Queue

Ordering rule:

- newest requested work goes at the top of this section
- older requested work stays lower down

### Nunchaku Z-Image LoRA support

Goal:

- make trained Z-Image LoRAs work on the fast Nunchaku runtime, not just the slower fallback path

Work:

- finish the `nymphnerds/nunchaku` fork path for native Z-Image LoRA loading
- keep Nunchaku-native hooks and control support intact while applying LoRAs
- make Manager `Repair Runtime` install the forked Nunchaku build reliably
- confirm the live Z-Image backend uses native Nunchaku LoRA methods when available
- verify txt2img still works with LoRA enabled
- verify guide-image / image-edit flows still behave correctly without regressing
- add clearer runtime-side logging for:
  - LoRA path selected
  - LoRA strength
  - native Nunchaku LoRA load success/failure
- add a smoke-test or validation path for LoRA-capable runtime installs

Exit condition:

- a user can train a Z-Image LoRA, select it in the addon, and use it on the Nunchaku runtime without manual patching

### Addon LoRA workflow polish

Goal:

- make the Blender-side LoRA flow clear enough that a first-time user can actually test a trained LoRA without confusion

Work:

- keep the run-folder plus checkpoint picker flow clean and understandable
- make it obvious which checkpoint is selected and which file is actually being used
- keep `Use Latest` as a convenience path without hiding manual checkpoint choice
- make runtime limitations visible in plain language:
  - guide image / image edit vs txt2img
  - runtime busy states
  - LoRA unsupported vs failed to load
- keep prompt editing stable so the prompts panel does not disappear after apply
- keep server detail/status UI concise instead of dumping backend internals
- add a simple “LoRA on/off same seed” testing workflow later if it still feels needed

Exit condition:

- a user can select a trained LoRA in Blender, understand what is being used, and test it without backend guesswork

### Curated cloud image model selection in the addon

Goal:

- make the current `Gemini Flash` cloud path feel like a proper `Cloud Image` backend with a small, curated model list instead of one hardcoded provider label

Work:

- rename the addon cloud image path from `Gemini Flash` to something more provider-neutral like `Cloud Image`
- keep OpenRouter as the first implementation path
- add a curated default model list instead of exposing the entire OpenRouter catalog
- group choices in a simple way:
  - best quality
  - balanced
  - budget
- include the strongest current image models plus a few cheaper options
- allow a later optional `Refresh Models` flow from OpenRouter, but keep the curated shortlist as the main UX
- filter out models that are not image-capable or that do not match the addon's current request shape
- keep guide-image/edit compatibility visible per model where possible
- consider an OpenAI API path later, but do not tie this to ChatGPT subscription login

Exit condition:

- a user can pick from a short, understandable cloud image model list without reading provider docs or model IDs

### User-configurable `.wslconfig` in the installer

Goal:

- let users choose WSL memory and swap settings in the installer so the local backend can fit a wider range of PC specs

Work:

- add a Manager screen or advanced settings section for `.wslconfig`
- explain the settings in normal-user language:
  - memory cap
  - swap size
  - what these mean for slower vs stronger PCs
- ship safe presets for common machine tiers
- allow a custom/manual mode for advanced users
- validate values before writing `C:\Users\<user>\.wslconfig`
- clearly warn when changes require `wsl --shutdown` or a Windows restart to fully apply
- show the current detected `.wslconfig` values when present
- provide a restore/recommended-default option
- make it clear that bad values can hurt performance or stability
- document recommended ranges by RAM tier in the install docs

Exit condition:

- a user with a low-memory or high-memory PC can tune WSL from the installer without editing `.wslconfig` by hand
- support can ask users which preset or values they chose during troubleshooting

## Current Product Shape

NymphsCore Manager is the Windows setup, repair, and diagnostics app for the local backend runtime used by the Nymphs Blender addon.

Current public baseline:

- Windows-first installer/manager app
- dedicated WSL distro:
  - `NymphsCore`
- managed Linux user:
  - `nymph`
- normal local API:
  - `http://localhost:8080`
- backend stack:
  - `TRELLIS.2` for single-image image-to-3D and texture/retexture workflows
  - `Z-Image` / Nunchaku for local image generation
- model prefetch is recommended, but still optional
- runtime tools handle readiness checks, model fetch, and smoke tests

Current public docs:

- `Manager/README.md`
- `docs/ABSOLUTE_BEGINNER_INSTALL_GUIDE.md`
- `docs/FOOTPRINT.md`
- `Blender/Addon/docs/USER_GUIDE.md`

Current public download path:

- `Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip`

## Product Rule

The Manager is not the Blender addon and should not read like the full paid product.

The Manager owns:

- backend install
- backend repair
- WSL/runtime setup
- model prefetch
- runtime health checks
- support logs
- local API readiness

The addon owns:

- Blender user workflows
- generation controls
- asset import/export behavior
- product-facing creative UX

The docs should keep this boundary clear so customers understand why the addon needs the local backend.

## Already Done

- WPF manager app exists.
- Release package builds as `NymphsCoreManager.exe`.
- Release zip is published in the repo.
- Base distro import path exists.
- Managed distro name is now `NymphsCore`.
- Managed Linux user is now `nymph`.
- System check screen covers:
  - admin status
  - base tar presence
  - drive availability
  - WSL availability
  - existing distros
  - NVIDIA visibility
- Install location screen chooses the target Windows drive/folder.
- Runtime setup always prepares required backend environments.
- Model prefetch can be turned on or off.
- Hugging Face token is accepted for the current run without being permanently written into the distro.
- Runtime Tools screen exists.
- Runtime Tools can:
  - check backend status
  - fetch missing models
  - run smoke tests
- Public docs were refreshed around the current Manager flow.
- README button now opens `Manager/README.md`.
- Release packaging strips Python bytecode and legacy parts wrapper cruft.
- Old handoff docs and roadmaps are local-only.

## Current Risks

### First-Run Trust

The biggest commercial risk is not the existence of scripts. It is whether a non-technical user can tell whether the system is working during long setup and first backend launch.

Risk areas:

- long model downloads look stuck
- first smoke test can take longer than expected
- skipped prefetch pushes download anxiety into Blender
- WSL/NVIDIA failures can feel opaque
- support requests need a repeatable log bundle

### Package Weight And Download Story

The manager zip is small enough to ship, but `NymphsCore.tar` and model downloads remain large.

Risk areas:

- two-file install is still clunky
- Google Drive base tar link is not ideal long-term distribution
- customers may miss the step where `NymphsCore.tar` sits beside the exe
- model prefetch can download about `72 GB`
- ready-to-run install is about `92 GB`

### Runtime Drift

The manager wraps several moving upstream systems.

Risk areas:

- upstream model snapshots can change
- upstream repos can introduce dependency changes
- Python environments can break on fresh machines
- CUDA / NVIDIA / WSL combinations vary across users
- local dev experiments can accidentally leak into packaged scripts

### Naming Drift

Old names still exist in source paths and historical scripts.

Risk areas:

- `NymphsCoreManager` source namespace/project name
- `Hunyuan3D-2` path names needed by `2mv`
- legacy docs under archive
- old backup repo names

Rule:

- customer-facing text should say `NymphsCore Manager`
- internal project names can be cleaned when it reduces confusion and does not risk the release

## Phase 1: Stabilize The Commercial Install Path

Goal:

- make the current manager install path boring and repeatable on a clean Windows machine

Work:

- test install from the public zip plus `NymphsCore.tar`
- test install with model prefetch on
- test install with model prefetch off
- test rerun/repair after an interrupted model download
- test rerun/repair after an interrupted Python dependency step
- confirm Runtime Tools status after each scenario
- confirm smoke tests for:
  - `TRELLIS.2`
  - `Z-Image`
- capture screenshots for support/docs

Exit condition:

- a clean-machine install can be completed without opening a Linux shell
- an interrupted install can be repaired by rerunning the manager
- support can diagnose the common failure cases from logs alone

## Phase 2: Improve Runtime Tools

Goal:

- make Runtime Tools the normal support and confidence screen

Work:

- add clearer per-backend readiness details
- distinguish:
  - missing runtime
  - missing model
  - failed dependency
  - backend starts but API does not respond
- add one-click log folder open
- add one-click diagnostics export
- include:
  - latest installer log
  - runtime status output
  - WSL distro list
  - NVIDIA/WSL visibility result
  - free disk space summary
  - manager version/build date
- make smoke test progress more explicit
- show when a backend is downloading models versus starting the server

Exit condition:

- a user can send one diagnostics bundle instead of guessing which file matters

## Phase 3: Package And Distribution

Goal:

- make the install package feel like a product, not a dev artifact

Work:

- decide whether to keep the two-file flow:
  - manager zip
  - `NymphsCore.tar`
- decide long-term hosting for `NymphsCore.tar`
- add checksums for:
  - manager zip
  - base distro tar
- add version metadata to the manager app
- add visible build/version in the UI
- consider an actual signed installer wrapper
- decide whether the repo should continue hosting binary zip artifacts directly
- keep release script cleaning packaged scripts
- ensure zip never includes:
  - `__pycache__`
  - `.pyc`
  - local outputs
  - legacy wrappers
  - dev-only docs
  - tokens or local config

Exit condition:

- the download page can tell a normal user exactly which files to get, how large they are, and how to verify them

## Phase 4: Documentation Polish

Goal:

- prepare docs for commercial users and support handoff

Work:

- add a screenshot-led install guide
- add a common errors page
- add a support bundle guide
- add a system requirements page
- define tested GPU/VRAM tiers
- define Windows/driver/WSL minimums
- add expected install timing by connection tier
- add a "what happens during install" diagram
- keep addon docs linked, but separate
- keep roadmap/handoff/dev notes local-only

Exit condition:

- the public Manager docs can stand alone for a first-time buyer without exposing internal planning notes

## Phase 5: Addon Alignment

Goal:

- make the addon and manager feel like two parts of one product

Work:

- confirm addon defaults target the managed `NymphsCore` runtime
- confirm addon can start/check supported backends cleanly
- decide where backend start belongs:
  - manager only
  - addon only
  - both, with clear responsibilities
- make addon errors point to Manager Runtime Tools when backend setup is incomplete
- ensure preset/docs language matches current backend names
- keep public extension repo docs aligned with Manager docs

Exit condition:

- a user who installs the backend first and addon second does not need to understand WSL or backend process details

## Phase 6: Repair, Update, And Uninstall

Goal:

- make post-install lifecycle safe enough for customers

Work:

- add explicit repair action
- add model refetch action
- add backend dependency refresh action
- add managed repo update check with clearer UI
- add base distro reinstall flow
- add uninstall/remove managed distro flow
- warn before destructive actions
- preserve generated outputs unless user explicitly chooses removal
- document where outputs live

Exit condition:

- the manager can handle normal customer lifecycle tasks without manual WSL commands

## Phase 7: Hardening And Signing

Goal:

- reduce Windows trust friction

Work:

- decide signing route
- sign manager executable
- sign installer wrapper if added
- document publisher identity
- reduce SmartScreen warnings over time
- add release hashes
- keep build reproducibility notes

Exit condition:

- install no longer feels like a suspicious unsigned dev build

## Technical Cleanup Backlog

- Source project/namespace rename is complete. Keep follow-up cleanup focused on packaging and legacy archive content only.
- Remove stale `2.1` wording from non-archive docs and UI if any remains.
- Remove remaining legacy repo references from scripts as they are cleaned up.
- Move dev-only docs deeper under ignored local docs or archive.
- Consider excluding binary release artifacts from source control after a better release-hosting path exists.
- Add a package manifest describing what the zip should contain.
- Add a release smoke check that opens the zip and validates expected files.
- Add script tests for package cleanup exclusions.

## Support Backlog

- Add diagnostics export.
- Add "copy error summary" button.
- Add log redaction for tokens and local paths where useful.
- Add a support checklist:
  - Windows version
  - GPU model
  - driver version
  - free disk space
  - model prefetch on/off
  - latest log
  - screenshot
- Add known-issue snippets for:
  - missing `NymphsCore.tar`
  - WSL unavailable
  - NVIDIA unavailable in WSL
  - low disk space
  - interrupted model download
  - first launch looks slow

## Release Gates

Before calling the Manager commercial-ready:

- clean Windows install passes with prefetch on
- clean Windows install passes with prefetch off
- repair path works after interrupted install
- Runtime Tools can fetch missing models
- Runtime Tools smoke tests pass for supported backends
- docs match the current UI
- package excludes local/dev cruft
- support bundle exists
- system requirements are explicit
- addon install guide points back to Manager when backend is missing
- public repo only exposes intended docs

## Short Priority Order

1. Clean-machine install test with model prefetch on.
2. Clean-machine install test with model prefetch off.
3. Runtime Tools diagnostics export.
4. Screenshot-led install guide.
5. Version/build metadata in the manager UI.
6. Checksums for manager zip and base distro tar.
7. Signed installer/executable plan.
8. Addon error-path alignment with Manager Runtime Tools.
