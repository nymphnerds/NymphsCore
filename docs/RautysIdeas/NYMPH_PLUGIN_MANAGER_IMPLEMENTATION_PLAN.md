# Nymph Plugin Manager Implementation Plan

Date: 2026-05-10
Branch: `modular`

## Purpose

Build the Nymph Plugin Manager around one strict lifecycle/status contract and module-owned Manager UI surfaces for all official modules:

```text
Brain
Z-Image
LoRA
TRELLIS
WORBI
```

WORBI is the first proof module because it is light and clearly exposes the preserved-data/install-state bug. It is not the whole scope.

Implementation direction:

```text
define the all-module contract -> prove it with WORBI -> apply it to every module -> then generalize Manager hosting
```

## The Real Goal

The Manager should become strict and boring at the lifecycle layer across every official module.

The goal is not to make every module UI identical.

The goal is to make these behaviors identical across modules:

- install
- update
- status/check
- start
- stop
- open/logs
- uninstall runtime
- delete data
- refresh state after actions
- destructive-action safety

At the same time, every module should be allowed to own its own Manager-facing config/control surface.

Core rule:

```text
Manager owns lifecycle, status truth, registry, safety, and host shell.
Modules own their manifest, entrypoints, and Manager-facing config/control surface.
```

This means the `main` branch Manager pages should be treated as workflow references, not as the future architecture.

On `main`, the Manager owns large hardcoded backend pages:

- Runtime Tools for Z-Image and TRELLIS
- Z-Image Trainer for LoRA/AI Toolkit training
- Brain tools for Brain service/model/OpenRouter controls

Those workflows are valuable. The ownership is wrong for the plugin model. The module-specific page bodies should move into module-owned Manager surfaces while the Manager keeps the shared shell, lifecycle rail, status refresh, confirmations, and logging.

## Why This Matters

Brain, Z-Image, LoRA, TRELLIS, and WORBI are different kinds of modules.

They should not be forced into one Manager-authored settings page.

They should share:

- install semantics
- uninstall semantics
- update semantics
- status output shape
- installed/running/data state meaning
- destructive action protections
- Manager action flow

They should not necessarily share:

- config fields
- advanced runtime controls
- health presentation
- logs presentation
- module-specific page body
- local workflow UI

The `main` branch UI proves why this matters:

- Brain needs service controls, model manager launch, OpenRouter key handling, and live monitor readouts.
- Z-Image needs model fetch, backend health, smoke test, and runtime dependency controls.
- LoRA needs a dense training workflow: datasets, captions, presets, adapter choice, job creation, queue controls, progress, and logs.
- TRELLIS needs GGUF quant selection, model fetch, adapter repair, smoke tests, and heavy-runtime status.
- WORBI needs lightweight app lifecycle and links/logs.

Those pages should not all be baked into `ManagerShellViewModel`.

## Target Ownership Model

### Manager Owns

- registry and manifest discovery
- manifest validation
- module descriptor cache
- status snapshot execution and parsing
- install/update/uninstall/delete-data orchestration
- status refresh truth
- shared lifecycle action rail
- host page shell
- declarative Manager surface renderer
- navigation placement
- permissions and confirmations
- dry-run rules
- destructive-action safety
- busy/error/progress/log behavior
- projection of status into cards, badges, buttons, and navigation
- shared secrets plumbing where the Manager is the appropriate owner, such as Hugging Face or OpenRouter token storage

### Base Runtime Install

The new Manager still needs an easy first-run install path, but it should not copy the old module-choice installer.

Base setup should do only this:

```text
install or repair the managed NymphsCore WSL distro
configure the default Linux user
normalize shell/profile paths
prepare the Manager shell for registry-driven module installs
```

Base setup should not hardcode optional module installs:

```text
Brain
Z-Image
LoRA
TRELLIS
WORBI
```

Those belong after base setup as registry module cards.

The base runtime also needs its own lifecycle. It is not a normal optional module, but it should be managed with the same boring status-truth discipline:

```text
install -> status -> repair -> update -> migrate
```

Base runtime status should track:

```text
Ubuntu version
WSL kernel availability
NymphsCore helper scripts version
system package readiness
CUDA/toolchain readiness
shared data/model/cache layout version
```

Suggested marker:

```text
/etc/nymphscore/base-runtime.json
```

Example:

```json
{
  "base_schema": 1,
  "ubuntu": "24.04",
  "manager_channel": "modular",
  "helper_scripts_commit": "abc123",
  "data_layout": 1,
  "created_at": "2026-05-10",
  "last_repair_at": "2026-05-10"
}
```

Base Runtime actions should be separate from module actions:

```text
status
repair
update_helper_scripts
update_system_packages
migrate_base
```

Rules:

- patch/minor base repairs can be a normal safe action
- helper script updates can be checked and applied separately from module updates
- Ubuntu major-version changes should be explicit migrations, not automatic upgrades
- migration should be offered only when tested and supported
- modules must not be installed as part of base setup
- module data/model stores should survive base repair and migration
- base runtime update state should not block viewing the module registry, but it may block install/start actions when the platform is unhealthy

### Module Owns

- manifest
- lifecycle entrypoint scripts
- status script
- Manager surface definition
- config page contents
- settings fields
- module-specific controls
- advanced runtime options
- health details
- module-specific logs/actions presentation
- explanatory labels and defaults for its own workflow
- module action names and argument contracts
- module-specific validation rules that can be represented declaratively

## All-Module Scope

The lifecycle and UI ownership work applies to every official module, not only WORBI.

Current target modules:

```text
brain
zimage
lora
trellis
worbi
```

Each module should converge on:

- one canonical `nymph.json` shape
- one `entrypoints` object
- one installed runtime marker contract
- one status output minimum
- one uninstall/delete-data policy declaration
- one module-owned Manager surface definition

The Manager should stop growing hardcoded pages for individual modules. If a module needs special UI, the module ships the UI definition and Manager hosts it.

## Main Branch UI Reference

The `main` branch is useful as a workflow inventory.

It currently has Manager-owned pages in:

```text
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Important page regions from `main`:

```text
Runtime Tools page
  Z-Image status, model fetch, smoke test
  TRELLIS status, GGUF quant selection, model fetch/repair, smoke test

Z-Image Trainer page
  LoRA dataset/metadata/caption workflow
  AI Toolkit launch/kill
  LoRA job creation/start/stop/delete
  training presets, adapter version, steps, rank, low VRAM, progress

Brain page
  Brain LLM/MCP/WebUI status
  local model and remote model display
  live monitor panel
  start/stop services
  WebUI start/open
  model manager launch
  update stack
  OpenRouter key apply
```

These workflows should be preserved, but moved out of Manager hardcoding.

Treat `main` as:

```text
workflow reference, not architecture reference
```

## The First Bug To Prove Against

WORBI is the first proof module because it exposes the current state problem clearly.

This bug is not unique to WORBI. Every module that preserves data after uninstall can hit the same class of false-installed state if installed truth is inferred from a directory that can legitimately remain after runtime uninstall.

Normal WORBI uninstall preserves user data:

```text
data
projects
config
logs
```

The broken installed-state pattern is:

```bash
if [[ -d "$INSTALL_DIR" ]]; then
  installed=true
fi
```

That makes this failure possible:

```text
uninstall preserves ~/worbi/logs
~/worbi still exists
status reports installed=true
Manager trusts installed=true
UI still shows installed/runtime actions
reinstall/uninstall feels stale or buggy
```

This is not primarily a XAML issue.

It is a bad lifecycle contract.

Required distinction:

```text
installed runtime != preserved user data
```

Installed runtime must mean:

```text
~/worbi/.nymph-module-version exists
```

Installed runtime must not mean:

```text
~/worbi directory exists
```

General rule for every module:

```text
installed runtime != install root exists
installed runtime != preserved data exists
installed runtime == module-declared runtime marker exists
```

Target markers:

```text
Brain    ~/Nymphs-Brain/.nymph-module-version
Z-Image  ~/Z-Image/.nymph-module-version
LoRA     ~/ZImage-Trainer/.nymph-module-version
TRELLIS  ~/TRELLIS.2/.nymph-module-version
WORBI    ~/worbi/.nymph-module-version
```

## Status Contract

Every module status script must print machine-readable `key=value` lines.

Minimum required keys:

```text
id=<module-id>
installed=true|false
version=<semver-or-unknown-or-not-installed>
running=true|false
state=available|installed|running|needs_attention|installing|uninstalling|updating
detail=<human summary>
install_root=<expanded absolute path>
```

Strongly recommended keys:

```text
runtime_present=true|false
data_present=true|false
health=ok|unavailable|unknown|degraded
pid=<pid-or-empty>
frontend_url=<url-or-empty>
backend_url=<url-or-empty>
logs_dir=<path-or-empty>
marker=<path-to-version-marker>
```

Module-specific status keys are allowed and expected.

Examples:

```text
Brain:
llm_running=true|false
mcp_running=true|false
open_webui_running=true|false
local_model=<model-or-empty>
remote_model=<model-or-empty>

Z-Image:
env_ready=true|false
models_ready=true|false|unknown
server_info_url=<url-or-empty>

LoRA:
repo_ready=true|false
venv_ready=true|false
node_ready=true|false
ui_ready=true|false
adapter_ready=true|false
model_ready=true|false
official_ui_running=true|false
gradio_running=true|false
worker_running=true|false
dataset_count=<number>
lora_count=<number>

TRELLIS:
env_ready=true|false
adapter_ready=true|false
runtime_ready=true|false
models_ready=true|false|unknown
quant=<quant>

WORBI:
backend=running|stopped|unknown
frontend=running|stopped|unknown
```

Manager lifecycle buttons should depend only on the standard keys.

Module-owned Manager surfaces may use standard keys and module-specific keys.

WORBI-specific target:

```text
id=worbi
installed=true|false
data_present=true|false
runtime_present=true|false
version=<semver-or-not-installed>
running=true|false
health=ok|unavailable|unknown
detail=<human summary>
```

Example after normal uninstall with preserved data:

```text
id=worbi
installed=false
data_present=true
runtime_present=false
version=not-installed
running=false
health=unavailable
detail=WORBI user data remains, but runtime files are not installed.
```

Manager lifecycle buttons should depend only on standard keys.

Custom module pages may interpret additional module-specific keys.

## Version Marker Contract

Each installed module needs a durable installed marker.

Preferred marker:

```text
<install_root>/.nymph-module-version
```

Rules:

- install writes the marker at the end of a successful install
- update preserves or rewrites the marker to the new version
- uninstall removes the marker during normal runtime uninstall
- status reads the marker to determine installed runtime
- preserved data never recreates or implies the marker

For WORBI:

```text
~/worbi/.nymph-module-version
```

For all modules:

```text
Brain    ~/Nymphs-Brain/.nymph-module-version
Z-Image  ~/Z-Image/.nymph-module-version
LoRA     ~/ZImage-Trainer/.nymph-module-version
TRELLIS  ~/TRELLIS.2/.nymph-module-version
WORBI    ~/worbi/.nymph-module-version
```

## Lifecycle Action Rule

Action output is not final state.

Status output is final state.

Required flow:

```text
run lifecycle action
capture output and exit code
run status
update Manager UI from status truth
```

Apply this to:

```text
install
update
start
stop
uninstall
delete data
```

Do not let optimistic state helpers become the source of truth.

If immediate feedback is needed, keep it temporary:

```text
Installing...
Uninstalling...
Refreshing status...
```

Then replace it with refreshed status.

## Manifest Contract V1

Current manifests are inconsistent. Do not support two permanent dialects.

WORBI currently uses one shape:

```json
"kind": "archive",
"source": { "archive": "...", "format": "tar.gz" },
"entrypoints": {
  "install": "...",
  "status": "...",
  "start": "...",
  "stop": "...",
  "open": "...",
  "logs": "...",
  "uninstall": "..."
},
"ui": { "page_kind": "worbi" },
"runtime": { "install_root": "~/worbi" }
```

Brain, Z-Image, LoRA, and TRELLIS currently use another shape:

```json
"packaging": "repo",
"repo": { "url": "...", "branch": "main" },
"install": { "path": "...", "script": "..." },
"manager": {
  "page": "custom",
  "status": "...",
  "start": "...",
  "stop": "...",
  "open": "...",
  "logs": "...",
  "uninstall": "..."
}
```

That mismatch matters now. The Manager registry installer already expects `entrypoints.install`, but most module manifests still expose actions under `manager`. This needs to converge before the plugin manager can be trusted.

Canonical V1 should look like this:

```json
{
  "manifest_version": 1,
  "id": "worbi",
  "name": "WORBI",
  "short_name": "WB",
  "version": "6.2.50",
  "description": "Local worldbuilding app managed by NymphsCore.",
  "category": "tool",
  "packaging": "archive",

  "source": {
    "type": "archive",
    "archive": "packages/worbi-6.2.50.tar.gz",
    "format": "tar.gz"
  },

  "install": {
    "root": "$HOME/worbi",
    "entrypoint": "scripts/install_worbi.sh",
    "version_marker": "$HOME/worbi/.nymph-module-version",
    "installed_markers": [
      "$HOME/worbi/.nymph-module-version",
      "$HOME/worbi/bin/worbi-start"
    ]
  },

  "entrypoints": {
    "status": "scripts/worbi_status.sh",
    "start": "scripts/worbi_start.sh",
    "stop": "scripts/worbi_stop.sh",
    "open": "scripts/worbi_open.sh",
    "logs": "scripts/worbi_logs.sh",
    "install": "scripts/install_worbi.sh",
    "update": "scripts/update_worbi.sh",
    "uninstall": "scripts/worbi_uninstall.sh"
  },

  "uninstall": {
    "entrypoint": "scripts/worbi_uninstall.sh",
    "preserve_by_default": ["data", "projects", "config", "logs"],
    "removes_by_default": ["runtime files", "generated launch scripts"],
    "supports_purge": true,
    "purge_allowed": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge"
  },

  "runtime": {
    "urls": [
      { "id": "frontend", "url": "http://localhost:8082" },
      { "id": "health", "url": "http://localhost:8082/api/health" }
    ],
    "logs_dir": "$HOME/worbi/logs"
  },

  "ui": {
    "page": "custom",
    "page_kind": "worbi",
    "sort_order": 50,
    "standard_lifecycle_rail": true,
    "manager_surface": {
      "type": "declarative",
      "schema": "ui/worbi.manager.json"
    }
  }
}
```

The same canonical shape should be applied to every module, with module-specific roots and surfaces.

Expected canonical roots:

```text
brain   install.root="$HOME/Nymphs-Brain"
zimage  install.root="$HOME/Z-Image"
lora    install.root="$HOME/ZImage-Trainer"
trellis install.root="$HOME/TRELLIS.2"
worbi   install.root="$HOME/worbi"
```

## Shared Model And Weights Store

Model downloads should not be scattered inside every module install root.

The plugin standard should define one shared artifact root for heavyweight model assets:

```text
$HOME/NymphsData/models
```

Module runtimes can still live in their own install roots, but model and weight downloads should use declared subdirectories under the shared root:

```text
$HOME/NymphsData/models/brain
$HOME/NymphsData/models/zimage
$HOME/NymphsData/models/lora
$HOME/NymphsData/models/trellis
$HOME/NymphsData/models/shared
```

Suggested environment variables:

```text
NYMPHS_DATA_ROOT="$HOME/NymphsData"
NYMPHS_MODELS_ROOT="$HOME/NymphsData/models"
NYMPHS_CACHE_ROOT="$HOME/NymphsData/cache"
NYMPHS_OUTPUTS_ROOT="$HOME/NymphsData/outputs"
```

Manifest Contract V1 should grow an `artifacts` section:

```json
"artifacts": {
  "models_root": "$HOME/NymphsData/models",
  "module_models": "$HOME/NymphsData/models/zimage",
  "shared_models": "$HOME/NymphsData/models/shared",
  "cache_root": "$HOME/NymphsData/cache/zimage",
  "outputs_root": "$HOME/NymphsData/outputs/zimage"
}
```

Rules:

- install scripts create the declared artifact directories if needed
- fetch/download actions write models to `artifacts.module_models` or `artifacts.shared_models`
- uninstall runtime does not remove shared models by default
- delete-data/purge may remove module-owned artifacts only when the manifest declares the scope
- modules should link or configure their native tools to read from the shared model store
- Hugging Face cache should be centralized where practical, rather than duplicated per module
- status should report model readiness without treating model presence as installed-runtime truth

Manifest rules:

- use `packaging`, not both `kind` and `packaging`
- use `source.type`, not several unrelated source shapes
- use one `entrypoints` object
- move legacy `manager.status/start/stop/open/logs/...` into `entrypoints`
- use `install.root`, not sometimes `runtime.install_root` and sometimes `install.path`
- use `install.entrypoint`, not `install.script`
- use `install.version_marker`
- use `install.installed_markers` for optional extra runtime checks, but do not let extra markers override the primary marker contract
- keep `ui.page=custom` for module-specific pages
- keep `ui.standard_lifecycle_rail=true`
- use `ui.manager_surface` for module-owned config/control body
- start with `manager_surface.type=declarative`
- allow `webview` later only with explicit local URL/path and sandbox rules

Canonical action names should start with:

```text
status
install
update
start
stop
open
logs
uninstall
```

Module-specific action names are allowed after that:

```text
Brain:
refresh
apply_openrouter_key
open_model_manager
start_webui
stop_webui
update_stack

Z-Image:
fetch_models
smoke_test
check_runtime_updates
apply_runtime_mode

LoRA:
refresh
start_official_ui
stop_official_ui
start_gradio_ui
create_job
start_job
stop_job
delete_job
open_datasets
open_loras
draft_captions

TRELLIS:
fetch_models
repair_adapter
smoke_test

WORBI:
open
logs
```

## Module-Owned Manager Surface

The module should bring its own Manager-facing config/control definition.

Bad direction:

```text
Manager knows every module's settings, controls, fields, and special UI.
Each module is forced to fit Manager hardcoding.
```

Better direction:

```text
Module ships manifest + lifecycle entrypoints + Manager surface definition.
Manager validates, hosts, and connects that surface to the shared lifecycle/status contract.
```

Recommended implementation path:

```text
Phase A: declarative config page
  Module manifest points to a JSON/YAML UI schema.
  Manager renders sections, fields, toggles, selects, paths, ports, buttons, and validation.

Phase B: optional embedded local web surface
  Module may expose a local HTML/WebView UI.
  Manager hosts it inside a bounded panel.
  Manager still owns lifecycle/status/destructive-action controls.

Phase C: native extension only if truly needed
  Avoid arbitrary native WPF plugin loading until the lifecycle contract is proven.
```

Do not start with arbitrary module-owned native WPF code.

### Phase A Surface Shape

Start with JSON. YAML can come later only if there is a strong reason.

Each module may ship:

```text
ui/manager.surface.json
```

The Manager reads this file from the cloned/cached module repo referenced by the manifest:

```json
"ui": {
  "page": "custom",
  "page_kind": "lora",
  "standard_lifecycle_rail": true,
  "manager_surface": {
    "type": "declarative",
    "schema": "ui/manager.surface.json"
  }
}
```

The declarative surface should support these building blocks first:

```text
section
text
status_badge
status_grid
metric
button
button_group
text_input
password_input
select
toggle
slider
number_input
path_link
log_panel
progress_bar
divider
```

Each control should bind to one of:

```text
status.<key>
setting.<key>
secret.<key>
action.<entrypoint>
```

Example action button:

```json
{
  "type": "button",
  "label": "Fetch Models",
  "action": "fetch_models",
  "enabled_when": "status.installed == true",
  "refresh_status_after": true
}
```

Example status badge:

```json
{
  "type": "status_badge",
  "label": "Models",
  "value": "status.models_ready",
  "map": {
    "true": { "text": "Ready", "tone": "ok" },
    "false": { "text": "Missing", "tone": "warning" },
    "unknown": { "text": "Unknown", "tone": "muted" }
  }
}
```

Example select that becomes an action argument:

```json
{
  "type": "select",
  "id": "trellis_quant",
  "label": "GGUF quant",
  "default": "Q5_K_M",
  "options": [
    { "value": "Q4_K_M", "label": "Q4_K_M" },
    { "value": "Q5_K_M", "label": "Q5_K_M" },
    { "value": "Q6_K", "label": "Q6_K" },
    { "value": "Q8_0", "label": "Q8_0" },
    { "value": "all", "label": "All" }
  ]
}
```

Example action using a setting:

```json
{
  "type": "button",
  "label": "Fetch TRELLIS Models",
  "action": "fetch_models",
  "args": {
    "quant": "setting.trellis_quant"
  },
  "refresh_status_after": true
}
```

### Main Branch Page Migration Map

Use the `main` branch pages as source material for module surfaces.

#### Brain Surface

Move from Manager-owned `Brain` page to `brain/ui/manager.surface.json`.

Initial declarative surface should include:

- service status grid for LLM, MCP, WebUI
- local model and remote model display
- live monitor metrics if status exposes them
- Start Brain action
- Stop Brain action
- Start/Open WebUI action
- Manage Models action
- Update Stack action
- OpenRouter key input
- Apply OpenRouter Key action
- Brain log panel

Brain-specific status keys should come from `brain_status.sh` and optional monitor keys.

#### Z-Image Surface

Move Z-Image parts of Runtime Tools to `zimage/ui/manager.surface.json`.

Initial declarative surface should include:

- backend readiness badge
- environment readiness
- model readiness
- server URL
- Fetch Models action
- Smoke Test action
- runtime log panel

Runtime dependency mode controls may remain Manager-owned at first if they affect shared runtime packages, but the long-term direction is for Z-Image/TRELLIS module actions to expose their own update/check/repair controls.

#### LoRA Surface

Move Z-Image Trainer page to `lora/ui/manager.surface.json`.

Initial declarative surface should include:

- trainer readiness badge
- dataset count
- LoRA count
- official UI and Gradio UI status
- LoRA/job name text input
- dataset picker or path selector
- open pictures/datasets/LoRAs actions
- caption with Brain action
- caption mode select
- preset select
- adapter version select
- sample prompt text input
- steps slider/number input
- checkpoint count select
- learning rate select
- LoRA rank select
- low VRAM toggle
- Add Job action
- Start Job action
- Stop Job action
- Delete Job action
- progress bar
- training log panel

LoRA is the strongest reason to avoid making the Manager own every module's page. Its workflow is too specific and will keep changing.

#### TRELLIS Surface

Move TRELLIS parts of Runtime Tools to `trellis/ui/manager.surface.json`.

Initial declarative surface should include:

- backend readiness badge
- environment readiness
- adapter readiness
- runtime readiness
- model readiness
- GGUF quant select
- Fetch Models action
- Repair Adapter action
- Smoke Test action
- runtime log panel

TRELLIS should still be tested last because it is heavy and has higher blast radius.

#### WORBI Surface

Move WORBI-specific page body to `worbi/ui/manager.surface.json`.

Initial declarative surface should include:

- app status badge
- health status
- frontend URL
- backend URL
- Open action
- Logs action
- optional app config fields later

WORBI remains the first proof module because this is the smallest surface.

### Manager Renderer Rules

The Manager renderer should:

- reject unknown control types unless explicitly allowed
- reject actions not declared in the manifest
- pass only declared args
- never infer delete paths from UI fields
- keep destructive actions in the shared lifecycle rail or explicit declared destructive-action section
- show module logs/progress in a shared way
- run `action -> status -> UI refresh` after every mutating action
- keep layout responsive and bounded inside the host page shell
- keep module surface failures isolated so a bad surface cannot crash the Manager shell

### What Not To Do

Do not:

- port the `main` branch XAML wholesale into `modular`
- keep adding module-specific hardcoded WPF pages to Manager
- load arbitrary module-supplied WPF assemblies in the first implementation pass
- let declarative UI controls bypass the manifest action allowlist
- let module UI decide installed/running state independently of status snapshots

## Manager Data Model Direction

The Manager should eventually separate static manifest data from dynamic status truth.

Target conceptual layers:

```text
NymphPluginDescriptor
  static manifest data
  id, name, category, entrypoints, install root, UI surface info

NymphStatusSnapshot
  dynamic status truth from status script
  installed, running, version, health, data_present, runtime_present

NymphManagerSurface
  module-owned UI definition
  sections, controls, status bindings, action bindings, validation hints

NymphModuleViewModel
  UI projection
  descriptor + latest snapshot + current action progress + rendered surface state
```

Current `ManagerShellViewModel` still manually constructs modules. Long term, this becomes:

```text
registry -> manifest fetch/cache -> descriptors -> status snapshots -> view models -> rendered module surfaces
```

But do not start by building the whole abstraction. Define the all-module contract, prove it with WORBI, then bring the other modules onto it deliberately.

## Destructive Action Safety

Uninstall and delete-data are different operations.

Normal uninstall:

```text
remove runtime
preserve user data by default
remove version marker
status says installed=false
status may say data_present=true
```

Delete data:

```text
explicit separate operation
requires confirmation
requires manifest-declared delete scopes
requires dry-run preview where possible
must not infer targets from UI state
```

For now, keep destructive purge/delete-data WORBI-only unless a module manifest explicitly declares safe delete scopes and the lifecycle engine has been proven.

## Implementation Phases

### Phase 1: Define The All-Module Contract In Code And Docs

Create or update Manager-side contract models before touching all module behavior:

```text
NymphPluginDescriptor
NymphStatusSnapshot
NymphManagerSurface
NymphModuleAction
NymphUninstallPolicy
```

This does not need to be a giant registry rewrite. Keep it small enough to support the current five modules.

Required contract decisions:

- canonical manifest V1 fields
- canonical status minimum keys
- version marker path
- action allowlist
- mutating action refresh rule
- declarative surface schema version
- shared lifecycle rail behavior
- destructive action policy

### Phase 2: Normalize Manifests For All Modules

Update module manifests in:

```text
/home/nymph/NymphsModules/brain/nymph.json
/home/nymph/NymphsModules/zimage/nymph.json
/home/nymph/NymphsModules/lora/nymph.json
/home/nymph/NymphsModules/trellis/nymph.json
/home/nymph/NymphsModules/worbi/nymph.json
```

Converge every manifest on:

- `manifest_version`
- `id`
- `name`
- `short_name`
- `version`
- `description`
- `category`
- `packaging`
- `source`
- `install.root`
- `install.entrypoint`
- `install.version_marker`
- `install.installed_markers`
- `entrypoints`
- `uninstall`
- `runtime`
- `ui.page`
- `ui.page_kind`
- `ui.standard_lifecycle_rail`
- `ui.manager_surface`

Do not keep `manager.status/start/stop/...` as a permanent dialect.

### Phase 3: Fix Module Scripts

Apply the marker/status/uninstall contract to every module.

Suggested order:

```text
1. WORBI    lightest proof case
2. LoRA     exercises rich module-owned UI/status without touching core image runtime
3. Z-Image  core image backend
4. Brain    service/model/secret complexity
5. TRELLIS  heaviest runtime, test last
```

For each module:

- install writes `<install.root>/.nymph-module-version`
- install prints installed version if possible
- status reads `.nymph-module-version`
- status reports `installed=false` when marker is absent
- status reports `runtime_present`
- status reports `data_present`
- status reports `version`
- status reports `state`
- status reports `detail`
- uninstall removes `.nymph-module-version`
- normal uninstall preserves declared data by default
- purge/delete-data remains explicit
- model/weight downloads go under the shared artifact root, not inside disposable runtime roots
- status exits `0` even when runtime is not installed

Preserved data examples:

```text
Brain    shared models, open-webui-data, mcp, secrets, logs
Z-Image  outputs, logs, shared model cache, Hugging Face cache
LoRA     datasets, loras, jobs, config, logs
TRELLIS  outputs, logs, shared model cache, Hugging Face cache
WORBI    data, projects, config, logs
```

### Phase 4: Prove With WORBI First

WORBI module scripts:

```text
/home/nymph/NymphsModules/worbi/scripts/
```

Key scripts:

```text
install_worbi.sh or installer_from_package.sh
worbi_status.sh
worbi_uninstall.sh
update_worbi.sh
```

Required behavior:

- install writes `~/worbi/.nymph-module-version`
- install prints installed version if possible
- status reads `.nymph-module-version`
- status reports `installed=false` when marker is absent
- status reports `data_present=true` when preserved folders remain
- uninstall removes `.nymph-module-version`
- uninstall preserves data by default
- purge/delete-data remains explicit

This phase proves the contract, not the final scope.

### Phase 5: Add Manager Status Snapshot Path

Relevant Manager files:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Check:

- how every module status output is parsed
- whether installed state comes only from `installed=`
- whether `data_present`, `runtime_present`, `state`, `health`, and `marker` can be surfaced
- whether action methods patch state optimistically before refresh
- whether module action buttons depend on the standard snapshot

First target:

```text
make current Manager behave correctly with corrected status for all five modules
```

Do not jump straight to a full registry rewrite.

### Phase 6: Add Declarative Surface Renderer

Implement a small renderer for `ui.manager_surface.type=declarative`.

First supported controls:

```text
section
text
status_badge
status_grid
button
text_input
password_input
select
toggle
slider
number_input
log_panel
progress_bar
```

Start with one or two surfaces:

```text
worbi/ui/manager.surface.json
lora/ui/manager.surface.json
```

WORBI proves the simple case. LoRA proves the rich page case from the `main` branch Z-Image Trainer workflow.

### Phase 7: Migrate Main Branch Workflows Into Module Surfaces

Use `main` as the reference.

Migration order:

```text
WORBI
LoRA
Z-Image
Brain
TRELLIS
```

For each module:

- create `ui/manager.surface.json`
- expose all required status keys
- expose all required module-specific actions
- map UI controls to manifest-declared actions
- keep shared install/update/uninstall/delete-data in Manager lifecycle rail
- test action output followed by status refresh

### Phase 8: Enforce Action Then Status

For every module:

```text
install -> status
update -> status
start -> status
stop -> status
uninstall -> status
delete data -> status
module-specific mutating action -> status
```

If optimistic UI state remains, it must be temporary and replaced by status truth.

### Phase 9: Test Repeatedly

```text
fresh status
install
status
start
status
stop
status
uninstall preserving data
status
reinstall
status
uninstall preserving data again
status
```

Expected after normal uninstall:

```text
installed=false
data_present=true if preserved data remains
runtime_present=false
Manager shows install/reinstall path
Manager does not show runtime controls as if runtime is installed
module-owned page body can explain preserved data without overriding lifecycle truth
```

### Phase 10: Generalize Registry/Navigation Slowly

After status and surfaces work for the five current modules, expand the registry/navigation layer.

Target:

```text
registry -> manifest fetch/cache -> descriptors -> status snapshots -> module view models -> rendered surfaces
```

Do this only after the lifecycle contract is proven.

## WORBI Proof Cycle

WORBI still gets a dedicated proof cycle because it is the smallest safe end-to-end test.

```text
fresh status
install WORBI
status
start WORBI
status
stop WORBI
status
uninstall preserving data
status
reinstall
status
uninstall preserving data again
status
```

Expected after normal uninstall:

```text
installed=false
data_present=true if preserved data remains
runtime_present=false
Manager shows install/reinstall path
Manager does not show runtime controls as if WORBI is installed
```

## Test Requirements

### Script-Level Tests

For every module:

```text
status before install
install
status after install
uninstall preserving data
status after uninstall
reinstall
status after reinstall
purge/delete-data dry run if available
```

Assertions:

- marker exists after install
- marker removed after uninstall
- data can remain without installed=true
- status output remains parseable key=value
- uninstall does not delete preserved data unless explicitly asked
- status exits `0` when runtime is absent
- `installed`, `runtime_present`, and `data_present` remain distinct
- module-specific status keys do not replace the standard keys

Minimum module script test matrix:

```text
Brain:
  install -> status -> start -> status -> stop -> status -> uninstall preserving data -> status

Z-Image:
  install -> status -> fetch_models if safe -> status -> smoke_test if models ready -> uninstall preserving data -> status

LoRA:
  install -> status -> start UI -> status -> stop UI -> status -> uninstall preserving data -> status

TRELLIS:
  status -> install only when intentionally testing heavy runtime -> status -> uninstall preserving data -> status

WORBI:
  install -> status -> start -> status -> stop -> status -> uninstall preserving data -> status -> reinstall -> status
```

### Manager-Level Tests

Verify:

- Home cards reflect every module's installed state correctly
- module pages reflect installed/runtime/data state correctly
- install button appears when runtime is absent
- runtime actions appear only when installed
- uninstall does not leave UI stuck as installed for any module
- reinstall after preserved-data uninstall works
- logs/progress show action output
- final state comes from status refresh
- module-owned declarative surfaces render without crashing the Manager
- module surface buttons only call manifest-declared actions
- module surface controls can read status keys and pass declared action args
- shared lifecycle rail remains Manager-owned

Module-owned UI regression checks:

```text
WORBI surface:
  status/health/URLs render
  open/logs actions work

LoRA surface:
  training controls render from declarative surface
  action buttons call LoRA manifest actions
  progress/log panel updates

Z-Image surface:
  readiness/model status render
  fetch/smoke actions remain status-truth-driven

Brain surface:
  service/model/OpenRouter controls render
  secrets are passed through Manager-owned secret plumbing, not plain manifest literals

TRELLIS surface:
  quant selector and repair/fetch/smoke actions render
  heavy tests run only when intentionally requested
```

### Shell Regression Tests

Verify shell navigation remains correct:

```text
Home -> System Checks -> Home
Home -> Logs -> Home
Module page -> Home
```

## No-Go List

Do not:

- attempt a big-bang implementation of every module UI at once
- convert PNG art to JPG
- use directory existence as installed truth
- trust lifecycle-complete docs without tests
- build broad generated registry architecture before WORBI works
- load arbitrary native WPF plugin UI code
- make Manager own every module's config fields
- infer delete-data targets from UI state
- treat action output as final state
- preserve both manifest dialects forever
- port the `main` branch hardcoded XAML pages wholesale into the new shell

## Files To Keep In Mind

Active Manager shell:

```text
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml.cs
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
Manager/apps/NymphsCoreManager/ViewModels/NymphModuleViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Older installer view model still exists but is not the active shell:

```text
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
```

Likely WORBI module scripts:

```text
/home/nymph/NymphsModules/worbi/scripts/
```

Other module repos/scripts:

```text
/home/nymph/NymphsModules/brain/
/home/nymph/NymphsModules/zimage/
/home/nymph/NymphsModules/lora/
/home/nymph/NymphsModules/trellis/
/home/nymph/NymphsModules/worbi/
```

Main branch workflow reference files:

```text
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

## Final Brief

This is an all-module plugin manager plan.

Make the lifecycle contract marker-driven and status-truth-driven for every official module:

```text
Brain
Z-Image
LoRA
TRELLIS
WORBI
```

Use WORBI as the first proof because it is light and clearly exposes the preserved-data false-installed bug.

Use the `main` branch Manager pages as workflow references for module-owned UI surfaces, not as hardcoded pages to port into `modular`.

The successful end state for the next implementation pass is:

```text
The Manager can discover all five modules, read one canonical manifest shape, run standard lifecycle actions, refresh state from status truth, and host module-owned declarative UI surfaces without hardcoding every module page.
```

The first proof milestone is:

```text
WORBI can install, uninstall while preserving data, reinstall, and refresh Manager UI correctly every time.
```

The next proof milestone is:

```text
LoRA's `main` branch Z-Image Trainer workflow can be represented as a module-owned declarative Manager surface.
```
