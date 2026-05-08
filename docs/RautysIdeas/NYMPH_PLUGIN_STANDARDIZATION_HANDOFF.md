# Nymph Plugin Standardization Handoff

Date: 2026-05-08
Branch: `rauty`

This handoff continues from `RAUTY_MODULE_LIFECYCLE_HANDOFF.md` and focuses on the next architectural direction: make Manager module control robust by standardizing a "Nymph Plugin" contract. The target is not identical UI for every module. The target is identical lifecycle behavior, state truth, safety rules, and Manager wiring for every module, while allowing custom module pages for TRELLIS, Z-Image, Brain, LoRA, WORBI, and future modules.

## Short Version

The current `rauty` Manager has the right shell shape:

- Home has installed and available modules.
- Available cards open detail pages.
- Module pages have a common header, live detail panel, manager contract buttons, right-side action rail, logs area, and module facts.
- Install/update/uninstall/delete actions route through shared Manager code.

But the actual lifecycle contract is still not robust enough:

- Manager still hardcodes the module roster.
- Manager still hardcodes install paths.
- Manager still has per-module status inference in C#.
- `nymph.json` exists, but it is not yet the full source of truth.
- Module manifests are similar but inconsistent.
- Normal uninstall can preserve data under an install root, then status may still report installed.
- Install scripts generally do not write a consistent `.nymph-module-version`.
- Install scripts generally do not emit `installed_module_version=x.y.z`.
- The Manager patches immediate UI state after actions, then runs refresh afterward, which can make state feel jumpy or stale.

The main fix direction:

```text
Module repo owns the manifest and entrypoints.
Manager owns the generic lifecycle engine and shell UI.
Status output is the source of truth after every action.
Remaining user data does not mean a module is installed.
```

## Critical Current Bug: WORBI Uninstall State

This explains the current "buggy asf" WORBI feeling.

WORBI normal uninstall preserves data:

```text
data
projects
config
logs
```

In `/home/nymph/NymphsModules/worbi/scripts/worbi_uninstall.sh`, normal uninstall removes everything except those preserved folders.

But `/home/nymph/NymphsModules/worbi/scripts/worbi_status.sh` currently says:

```bash
if [[ -d "$INSTALL_DIR" ]]; then
  installed=true
fi
```

That means normal uninstall can leave `~/worbi/logs` or another preserved folder, and then `status` still reports:

```text
installed=true
```

The Manager trusts `installed=` from WORBI status in `ManagerShellViewModel.ApplyWorbiState()`.

So the observed bug is probably not just a stale XAML refresh issue. It is a bad installed-state contract:

```text
preserved data folder exists -> status says installed -> Manager keeps showing installed actions
```

Required fix direction:

- WORBI install must write a durable installed marker, preferably `~/worbi/.nymph-module-version`.
- WORBI normal uninstall must remove that marker and runtime/program files.
- WORBI status must report installed based on the marker and/or required runtime files, not just root directory existence.
- WORBI status should distinguish `installed=false` and `data_present=true`.

Good status shape:

```text
id=worbi
installed=false
data_present=true
runtime_present=false
version=not-installed
running=false
detail=WORBI user data remains, but runtime files are not installed.
```

## Current Manager State

Main active shell:

```text
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml
Manager/apps/NymphsCoreManager/Views/MainWindow.xaml.cs
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
Manager/apps/NymphsCoreManager/ViewModels/NymphModuleViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Old installer-oriented view model still exists:

```text
Manager/apps/NymphsCoreManager/ViewModels/MainWindowViewModel.cs
```

The active window creates `ManagerShellViewModel`, not the old `MainWindowViewModel`.

Current shell startup:

```text
MainWindow.xaml.cs -> new ManagerShellViewModel(new InstallerWorkflowService())
Loaded -> InitializeAsync()
InitializeAsync() -> RefreshAsync()
RefreshAsync() -> system checks + runtime monitor + module state
```

Current hardcoded module roster is created inside `ManagerShellViewModel`:

```text
brain    -> /home/nymph/Nymphs-Brain
zimage   -> /home/nymph/Z-Image
lora     -> /home/nymph/ZImage-Trainer
trellis  -> /home/nymph/TRELLIS.2
worbi    -> /home/nymph/worbi
```

This should become manifest/registry driven.

## Current UI Skeleton Worth Keeping

The current XAML already has a good standard page shell:

- left navigation
- Home page
- installed module cards
- available module cards
- module detail page
- `// LIVE DETAIL`
- `// MANAGER CONTRACT`
- optional `// DEV CONTRACT`
- module logs panel
- right rail `// ACTIONS`
- module facts panel

This is the right general direction.

Keep the standard shell, but make the data and actions come from a strict module contract instead of Manager-side hardcoding.

Recommended page split:

```text
Standard Shell For Every Module
  header
  status/version
  lifecycle action rail
  logs/status/progress
  module facts

Custom Module Body
  WORBI-specific body
  TRELLIS-specific body
  Z-Image-specific body
  Brain-specific body
  LoRA-specific body
```

Do not try to make every module page identical. Standardize lifecycle and state. Let module pages be custom.

## Current Lifecycle Flow

Install currently does this:

```text
ManagerShellViewModel.InstallModuleAsync()
  confirm
  IsBusy=true
  RunNymphModuleInstallFromRegistryAsync()
  ApplyImmediateModuleInstallResult(isInstalled=true)
  ClearModuleUpdateAfterSuccessfulInstall()
  RefreshModuleStateAsync()
```

Update is similar:

```text
ManagerShellViewModel.UpdateModuleAsync()
  confirm
  IsBusy=true
  RunNymphModuleInstallFromRegistryAsync()
  ApplyImmediateModuleInstallResult(isInstalled=true)
  RefreshModuleStateAsync()
  CheckNymphModuleRegistryUpdatesAsync()
```

Uninstall/delete currently does this:

```text
ManagerShellViewModel.RunModuleUninstallAsync()
  confirm
  IsBusy=true
  RunNymphModuleUninstallAsync()
  ApplyImmediateModuleInstallResult(isInstalled=false)
  RefreshModuleStateAsync()
  if !module.IsInstalled and on module page -> go Home
```

Problem:

The Manager both patches state and refreshes state. If the refresh contract is wrong, the UI can flip back. If refresh is slow or incomplete, the patched state can be temporarily misleading.

Recommended lifecycle rule:

```text
Action output is progress/log only.
Status output is truth.
After every action, run status and replace module state from status.
```

## Current Service Flow

Registry/update/install/uninstall/action code lives in `InstallerWorkflowService`.

Important methods:

```text
CheckNymphModuleRegistryUpdatesAsync()
GetInstalledNymphModuleVersion()
GetNymphModuleManifestInfoAsync()
RunNymphModuleInstallFromRegistryAsync()
RunNymphModuleUninstallAsync()
RunNymphModuleActionAsync()
```

Current registry install:

```text
fetch registry JSON
find module
fetch nymph.json
clone/update module repo into ~/.cache/nymphs-modules/repos/<id>
copy nymph.json into ~/.cache/nymphs-modules/<id>.nymph.json
read entrypoints.install
run install script
```

Current generic action:

```text
read ~/.cache/nymphs-modules/repos/<id>/nymph.json
read entrypoints.<action>
run repo script if present
else run ~/.local/bin/<id>-<action>
```

This is good in spirit. It should be expanded into the single action mechanism for all modules, including status.

## Current Manifest Inconsistency

Local module manifests already contain many useful fields, but they are not standardized yet.

WORBI uses:

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

Z-Image/TRELLIS/Brain/LoRA use a different shape:

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

Standardization should normalize this. Do not support two permanent manifest dialects unless there is a compatibility migration layer.

## Proposed Nymph Plugin Manifest V1

Recommended canonical fields:

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
    "uninstall": "scripts/worbi_uninstall.sh"
  },

  "actions": [
    { "id": "status", "label": "Status", "kind": "standard", "refresh_after": true },
    { "id": "start", "label": "Start", "kind": "standard", "refresh_after": true },
    { "id": "stop", "label": "Stop", "kind": "standard", "refresh_after": true },
    { "id": "open", "label": "Open", "kind": "standard", "opens_url": true },
    { "id": "logs", "label": "Logs", "kind": "standard", "shows_logs": true }
  ],

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
    "standard_lifecycle_rail": true
  }
}
```

Notes:

- Use `packaging`, not both `kind` and `packaging`.
- Use `source.type`, not several unrelated source shapes.
- Use one `entrypoints` object, not `manager.status` plus `install.script` plus `entrypoints`.
- Use `install.root`, not `runtime.install_root` sometimes and `install.path` other times.
- Keep `ui.page=custom` for module-specific pages.
- Keep `standard_lifecycle_rail=true` so custom pages still use Manager-owned install/update/uninstall/delete.

## Proposed Status Output Contract

Every module status script should print machine-readable `key=value` lines. The Manager should parse these generically.

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
env_ready=true|false
models_ready=true|false|unknown
health=ok|unreachable|unknown
url=<primary url>
logs_dir=<absolute path>
version_marker=<absolute path>
```

Module-specific keys are allowed:

```text
adapter_ready=true|false
quant=Q5_K_M
open_webui_running=true|false
worker_running=true|false
official_ui_url=http://...
datasets=/home/nymph/ZImage-Trainer/datasets
```

But the Manager lifecycle rail should only depend on the standard keys.

Important rule:

```text
installed=true means executable/runtime/program files are installed.
data_present=true means user data remains.
These must be separate.
```

## Proposed Lifecycle State Machine

For every module:

```text
available
  -> installing
  -> installed
  -> running
  -> stopping
  -> installed
  -> uninstalling
  -> available_with_data
  -> installing
  -> installed
```

Delete data path:

```text
installed
  -> deleting
  -> available
```

Update path:

```text
installed
  -> checking_update
  -> update_available
  -> updating
  -> installed
```

Manager should represent `available_with_data` clearly:

```text
Install Module
Delete Saved Data
```

Do not call it installed. Do not show Start/Stop/Open as installed actions.

## Manager Lifecycle Engine Rules

The Manager should have one shared lifecycle engine with these rules:

1. Resolve module by immutable module id.
2. Load registry entry.
3. Load manifest.
4. Validate manifest.
5. Build an immutable module descriptor.
6. Run action through the descriptor.
7. Capture output as logs/progress.
8. Run status after every mutating action.
9. Replace module state from status.
10. Rebuild Home cards, navigation, detail page, facts, and action enabled states from the same state object.

Mutating actions:

```text
install
update
uninstall
delete-data/purge
start
stop
refresh
fetch-models
repair
```

Non-mutating actions:

```text
status
open
logs
smoke-test
```

Some non-mutating actions may still refresh afterward, but install/update/uninstall/delete/start/stop must refresh afterward.

Avoid this long term:

```text
ApplyImmediateModuleInstallResult(...)
then RefreshModuleStateAsync()
```

Prefer:

```text
Run action
Show action output
Run status
Apply status snapshot
```

If immediate optimistic UI is desired, keep it as a temporary action state only:

```text
module.ActionInProgress = install
module.DisplayState = Installing
```

Then discard it when status returns.

## Proposed Manager Data Types

Add or evolve toward these concepts:

```csharp
NymphPluginManifest
NymphPluginDescriptor
NymphPluginEntrypoints
NymphPluginInstallSpec
NymphPluginUninstallSpec
NymphPluginRuntimeSpec
NymphPluginUiSpec
NymphPluginActionSpec
NymphPluginStatusSnapshot
NymphPluginLifecycleService
```

`NymphModuleViewModel` should become display state only, not the source of truth.

Current:

```text
NymphModuleViewModel owns mutable installed/running/version/detail state.
ManagerShellViewModel mutates it from several paths.
```

Target:

```text
NymphPluginDescriptor = mostly static manifest data.
NymphPluginStatusSnapshot = current truth from status.
NymphModuleViewModel = projection of descriptor + snapshot + action progress.
```

## Concrete Current Weaknesses

### 1. Hardcoded Module Roster

`ManagerShellViewModel` constructs `_allModules` manually. This prevents modules from being truly registry/manifest-driven.

Target:

```text
registry -> manifest fetch/cache -> descriptors -> modules
```

Manager may keep a small known-module fallback during migration, but it should not be the final source.

### 2. Hardcoded State Logic

Current state methods:

```text
ApplyBrainState()
ApplyRuntimeModuleState("zimage")
ApplyRuntimeModuleState("trellis")
ApplyLoraState()
ApplyWorbiState()
```

These should be replaced by generic status parsing wherever possible.

Custom pages can interpret module-specific status keys, but the standard rail should not.

### 3. Hardcoded Install Root Mapping

`InstallerWorkflowService.GetNymphModuleInstallRoot()` maps module ids to roots:

```text
brain -> ~/Nymphs-Brain
zimage -> ~/Z-Image
trellis -> ~/TRELLIS.2
lora -> ~/ZImage-Trainer
worbi -> ~/worbi
```

This should move to `nymph.json`:

```json
"install": { "root": "$HOME/TRELLIS.2" }
```

### 4. Version Marker Logic Does Not Fit All Modules

`ReadInstalledModuleVersion()` first checks:

```text
/home/<user>/<moduleId>/.nymph-module-version
```

That works for `worbi`, but not naturally for:

```text
brain   -> /home/nymph/Nymphs-Brain
zimage  -> /home/nymph/Z-Image
trellis -> /home/nymph/TRELLIS.2
lora    -> /home/nymph/ZImage-Trainer
```

The version marker path must come from manifest `install.version_marker`.

### 5. Install Scripts Do Not Consistently Mark Success

The handoff rule says install scripts should print:

```text
installed_module_version=x.y.z
```

The local module install scripts inspected do not consistently write `.nymph-module-version` or print `installed_module_version=...`.

This should become mandatory for Nymph Plugin V1.

### 6. Uninstall Preserves Data But Status Often Uses Root Existence

WORBI is the clearest example. Others are less obviously broken because their status checks a more specific runtime marker, but the standard should forbid directory-existence-only installed detection when uninstall preserves data under the same root.

### 7. Delete Data Is Temporarily WORBI-Only

The current Manager intentionally shows delete data only for WORBI:

```text
ShowDeleteModuleData => DisplayedModule?.Id == "worbi"
```

Keep this safety guard until the manifest contract supports explicit delete scopes and dry-run preview for every module.

Eventually:

```json
"uninstall": {
  "supports_purge": true,
  "purge_allowed": false,
  "purge_requires": ["typed-confirmation"],
  "delete_scopes": [...]
}
```

Heavy modules should not expose purge until tested with dummy modules.

## Proposed Safety Rules

Never infer deletion targets from mutable UI state.

Every destructive operation must capture immutable values before confirmation:

```text
targetId
targetName
installRoot
manifestHash or manifestUrl
action
purge flag
```

Audit before action:

```text
AUDIT module delete requested:
  id=<id>
  name=<name>
  install_root=<root>
  manifest=<manifest_url>
  purge=<true|false>
```

Destructive action requirements:

- manifest must declare `supports_purge=true`
- manifest must declare install root
- uninstall script must support `--dry-run`
- Manager should run dry-run first and show summary
- purge should be disabled for heavy modules until tested
- module id must be safe regex
- entrypoint path must be safe relative path
- install root must be under an allowed root
- install root must not be `$HOME`, `/`, `/home/nymph`, or empty
- for non-WORBI runtime modules, continue blocking purge until the lifecycle engine is proven

## Proposed Action Output Rules

Every action script should be boring.

All modules:

```text
status
start
stop
open
logs
install
uninstall
```

Optional standard actions:

```text
refresh
fetch-models
smoke-test
repair
configure
```

Action scripts should:

- exit `0` on success
- exit non-zero on failure
- print useful `key=value` lines for status-like operations
- print URLs plainly for `open`
- avoid giant interactive flows unless explicitly opened in terminal
- be idempotent where possible

Install scripts must:

- preserve user data by default
- install runtime/program files
- install wrappers if needed
- write `.nymph-module-version`
- print `installed_module_version=x.y.z` only after success

Uninstall scripts must:

- stop services first
- remove runtime/program files
- preserve declared user data by default
- remove `.nymph-module-version`
- support `--dry-run`
- support `--yes`
- support `--purge` only if declared
- leave status as `installed=false`

Status scripts must:

- never report installed based only on root folder existence
- report `data_present=true` separately
- report `runtime_present=true` separately
- report `installed=true` only when runtime/program markers exist

## Registry Rules

Current registry:

```text
github.com/nymphnerds/nymphs-registry
```

Current expected module repos:

```text
github.com/nymphnerds/worbi
github.com/nymphnerds/zimage
github.com/nymphnerds/trellis
github.com/nymphnerds/brain
github.com/nymphnerds/lora
```

Registry entry should stay small:

```json
{
  "id": "worbi",
  "name": "WORBI",
  "channel": "test",
  "trusted": true,
  "manifest_url": "https://raw.githubusercontent.com/nymphnerds/worbi/main/nymph.json"
}
```

Manifest owns behavior. Registry only points to trusted manifests.

## Suggested Implementation Phases

### Phase 1: Fix WORBI Contract

Do this before touching heavy modules.

- Update WORBI install to write `~/worbi/.nymph-module-version`.
- Update WORBI install to print `installed_module_version=6.2.50`.
- Update WORBI uninstall to remove the marker on normal uninstall.
- Update WORBI status:
  - `installed=true` only if marker or runtime markers exist.
  - `data_present=true` if preserved folders exist.
  - `runtime_present=true` if server/bin/runtime files exist.
- Verify install -> uninstall -> reinstall repeatedly.

Expected result after normal uninstall:

```text
installed=false
data_present=true
runtime_present=false
running=false
version=not-installed
```

### Phase 2: Standardize Manifest Shape

Pick one manifest schema. Migrate WORBI plus local module manifests to it.

Normalize:

```text
kind -> packaging
manager.status -> entrypoints.status
install.path -> install.root
install.script -> install.entrypoint
runtime.install_root -> install.root
```

Add:

```text
install.version_marker
install.installed_markers
ui.standard_lifecycle_rail
```

### Phase 3: Add Manifest Parser Models

Add C# models for the manifest. Do not keep parsing only ad hoc fields from `JsonElement`.

The Manager should validate:

- id matches registry id
- entrypoint paths are safe relative paths
- install root is present
- uninstall/delete rules are explicit
- status entrypoint exists

### Phase 4: Generic Status Refresh

Create one generic status runner:

```text
RunNymphModuleStatusAsync(moduleId)
```

It should:

- load descriptor
- run `entrypoints.status`
- parse key/value output
- return `NymphPluginStatusSnapshot`

Then replace per-module lifecycle state inference with generic status for the standard shell.

Custom pages may still use extra keys.

### Phase 5: Lifecycle Engine

Create one lifecycle service:

```text
InstallAsync(id)
UpdateAsync(id)
UninstallAsync(id)
DeleteDataAsync(id)
RunActionAsync(id, action)
RefreshStatusAsync(id)
```

Every mutating action should end with:

```text
RefreshStatusAsync(id)
ApplySnapshot(id, snapshot)
```

### Phase 6: UI Projection Cleanup

Keep `NymphModuleViewModel`, but make it a projection:

```text
manifest descriptor + status snapshot + current action progress
```

Avoid using the same mutable object as both command parameter and source of truth. Capture module id for actions, then resolve fresh state.

### Phase 7: Heavy Module Migration

After WORBI is boring:

1. Add a dummy tiny module for destructive lifecycle testing.
2. Migrate Brain manifest and status.
3. Migrate Z-Image manifest and status.
4. Migrate LoRA manifest and status.
5. Migrate TRELLIS manifest and status last.

TRELLIS should be last because of the previous destructive incident and heavier recovery cost.

## Test Matrix

WORBI required tests:

```text
available -> detail page
manifest info appears before install
install -> status installed=true
home card moves to installed
detail page action rail changes
start -> running=true
stop -> running=false
logs -> logs panel fills
uninstall -> installed=false, data_present=true
home card moves to available
detail page stays coherent or intentionally shows available state
reinstall -> installed=true
delete data -> installed=false, data_present=false
repeat lifecycle twice
```

Navigation stress tests:

```text
open WORBI page
start install
click Home after completion
open WORBI again
uninstall
open another module during refresh
ensure audit/action target remains worbi
```

Update tests:

```text
installed version lower than remote
Check for Updates
Update Module visible
Update Module runs install/update
status returns new version
Update Module hidden
```

Safety tests:

```text
delete data hidden for non-WORBI
purge blocked for non-WORBI service/runtime modules
dry-run preview works before enabling purge broadly
invalid entrypoint path rejected
invalid module id rejected
missing status script marks module needs attention, not installed
```

## Build/Run Notes

Use the existing handoff rule:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

Remember the distro distinction:

```text
NymphsCore_Lite = dev/source WSL checkout
NymphsCore      = real managed runtime WSL used by Manager
```

Runtime lifecycle tests target:

```text
wsl.exe -d NymphsCore --user nymph -- ...
```

Do not run destructive tests on:

```text
TRELLIS.2
Z-Image
LoRA
Brain
```

until the generic lifecycle engine is proven with WORBI and/or a tiny dummy module.

## Recommended Next Session Start

Start with this exact question:

```text
Can WORBI normal uninstall leave preserved data while status says installed=false?
```

If no, fix WORBI first.

Then:

```text
Can Manager run install/uninstall/reinstall and always rebuild from status?
```

If no, fix Manager lifecycle refresh second.

Then:

```text
Can manifest describe enough for Manager to remove hardcoded module paths?
```

If no, standardize manifest schema third.

## Bottom Line

The Manager should become boring and strict at the lifecycle layer.

Custom pages can stay rich and module-specific, but install/update/uninstall/delete/status must not be custom UI behavior. They must be one shared Manager-owned lifecycle system driven by one standard manifest and one standard status schema.

The first proof is WORBI. If WORBI cannot repeatedly install, uninstall, reinstall, and refresh cleanly, the format is not ready for TRELLIS, Z-Image, Brain, or LoRA.
