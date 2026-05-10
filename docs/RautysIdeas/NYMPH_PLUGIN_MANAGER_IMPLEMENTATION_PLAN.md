# Nymph Plugin Manager Implementation Plan

Date: 2026-05-10
Branch: `rauty`

## Purpose

This is the single source of truth for the next Manager implementation pass.

It replaces the older scattered handoffs about:

- module lifecycle fixes
- Nymph Plugin standardization
- the `baffledtests` branch review
- next-session notes

The plan is deliberately concrete. The next work should not be another broad architecture sketch. It should make one module, WORBI, boring and reliable first, then generalize from the proven lifecycle.

## Current Repo State

Work is cleanly back on `rauty`.

The old `baffledtests` branch was reviewed, mined for one safe fix, then deleted locally and remotely.

Latest known pushed commits from this session:

```text
4063cc2 fix(manager): carry over system checks navigation fix
db7fa33 docs: add full next session handoff
```

The Manager release build passed after the System Checks fix.

Working release build route:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

When run from Bash, preserve UNC escaping:

```bash
/bin/bash -lc "powershell.exe -NoProfile -ExecutionPolicy Bypass -File '\\\\wsl.localhost\\NymphsCore_Lite\\home\\nymph\\NymphsCore\\Manager\\apps\\NymphsCoreManager\\build-release.ps1'"
```

## Non-Negotiable Decisions

### Stay On `rauty`

Continue all work on `rauty`.

Do not recreate or merge `baffledtests`.

### Do Not Bring Back `baffledtests` Churn

Reject these from `baffledtests` permanently:

- `MegaPhase4` namespace changes
- `MegaPhase4` assembly name
- `NymphsCore.MegaPhase4.exe`
- JPG conversion of sidebar portraits
- published binaries from that branch
- broad line-ending churn
- scaffolded registry services as the implementation baseline
- lifecycle-complete claims that were not proven in this repo

The sidebar portrait assets must remain PNG because they need transparency and are still WIP Photoshop assets.

### Keep The System Checks Fix

One useful fix from `baffledtests` was already carried over.

File:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
```

Fix:

```csharp
SelectedNavigationItem = null;
```

It was added to the fallback path in `SelectPrimaryPage`.

Reason:

`SystemChecks` is opened from a Home card, but it is not part of `PrimaryNavigationItems`. Without clearing `SelectedNavigationItem`, the UI can show Home as selected while the actual page is System Checks. Then clicking Home later can do nothing because the selected navigation item has not changed.

## The Real Goal

The Manager should become strict and boring at the lifecycle layer.

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

## Target Ownership Model

### Manager Owns

- registry and manifest discovery
- manifest validation
- install/update/uninstall/delete-data orchestration
- status refresh truth
- shared lifecycle action rail
- host page shell
- navigation placement
- permissions and confirmations
- dry-run rules
- destructive-action safety
- busy/error/progress/log behavior
- projection of status into cards, badges, buttons, and navigation

### Module Owns

- manifest
- lifecycle entrypoint scripts
- status script
- config page contents
- settings fields
- module-specific controls
- advanced runtime options
- health details
- module-specific logs/actions presentation
- explanatory labels and defaults for its own workflow

## The WORBI Bug To Prove Against

WORBI is the first proof module because it exposes the current state problem clearly.

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

Other modules use another shape:

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

Manifest rules:

- use `packaging`, not both `kind` and `packaging`
- use `source.type`, not several unrelated source shapes
- use one `entrypoints` object
- use `install.root`, not sometimes `runtime.install_root` and sometimes `install.path`
- use `install.version_marker`
- keep `ui.page=custom` for module-specific pages
- keep `ui.standard_lifecycle_rail=true`
- use `ui.manager_surface` for module-owned config/control body
- start with `manager_surface.type=declarative`
- allow `webview` later only with explicit local URL/path and sandbox rules

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

NymphModuleViewModel
  UI projection
  descriptor + latest snapshot + current action progress
```

Current `ManagerShellViewModel` still manually constructs modules. Long term, this becomes:

```text
registry -> manifest fetch/cache -> descriptors -> status snapshots -> view models
```

But do not start by building the whole abstraction. Prove WORBI first.

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

### Phase 0: Confirm Clean Start

Check:

```text
git status
git branch
```

Expected:

```text
on rauty
no baffledtests branch
origin/baffledtests absent
working tree clean except intentional changes
```

### Phase 1: Fix WORBI Scripts

Likely module repo path from prior work:

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

### Phase 2: Adjust Manager WORBI Parsing Only As Needed

Relevant Manager files:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Check:

- how WORBI status output is parsed
- whether `data_present` can be surfaced
- whether installed state comes only from `installed=`
- whether action methods patch state optimistically before refresh

First target:

```text
make current Manager behave correctly with corrected WORBI status
```

Do not jump straight to a full registry rewrite.

### Phase 3: Enforce Action Then Status

For WORBI:

```text
install -> status
update -> status
start -> status
stop -> status
uninstall -> status
delete data -> status
```

If optimistic UI state remains, it must be temporary and replaced by status truth.

### Phase 4: Test WORBI Repeatedly

Minimum test cycle:

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

### Phase 5: Add Strict WORBI Manifest

Only after WORBI lifecycle is stable:

- define strict WORBI manifest V1
- validate required fields
- include `install.version_marker`
- include lifecycle entrypoints
- include `ui.manager_surface`
- keep Manager host shell generic

### Phase 6: Generalize Slowly

After WORBI passes repeated lifecycle testing, expand in this order:

1. Brain
2. Z-Image
3. LoRA
4. TRELLIS last

TRELLIS should be last because it is heavy and recently had wiped-test-WSL risk.

## Test Requirements

### Script-Level Tests

For WORBI:

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

### Manager-Level Tests

Verify:

- Home cards reflect WORBI installed state correctly
- WORBI page reflects installed/runtime/data state correctly
- install button appears when runtime is absent
- runtime actions appear only when installed
- uninstall does not leave UI stuck as installed
- reinstall after preserved-data uninstall works
- logs/progress show action output
- final state comes from status refresh

### Shell Regression Tests

Verify the carried-over System Checks fix:

```text
Home -> System Checks -> Home
Home -> Logs -> Home
Module page -> Home
```

## No-Go List

Do not:

- standardize every module at once
- resurrect `baffledtests`
- reintroduce `MegaPhase4`
- convert PNG art to JPG
- use directory existence as installed truth
- trust lifecycle-complete docs without tests
- build broad generated registry architecture before WORBI works
- load arbitrary native WPF plugin UI code
- make Manager own every module's config fields
- infer delete-data targets from UI state
- treat action output as final state

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

## Final Brief

Continue on `rauty`.

Use this plan as the single handoff.

Make WORBI lifecycle state marker-driven and status-truth-driven before generalizing the Nymph Plugin standard.

The successful end state for the next implementation pass is:

```text
WORBI can install, uninstall while preserving data, reinstall, and refresh Manager UI correctly every time.
```

Only after that should the manifest-driven plugin architecture expand to the other modules.
