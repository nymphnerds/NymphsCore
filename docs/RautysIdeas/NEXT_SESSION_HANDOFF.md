# Next Session Handoff: Full Nymph Plugin Standardization Plan

Date: 2026-05-10
Branch: `rauty`

## Read This First

This is the full detailed handoff for the next session.

It consolidates:

- the original Nymph Plugin standardization plan
- the WORBI lifecycle bug analysis
- the `baffledtests` branch review and cleanup
- the System Checks fix that was carried over
- the new architecture decision from today: modules own their Manager-facing config/control surface

Use this file as the next-session starting point.

Deeper reference still exists here:

```text
docs/RautysIdeas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md
```

But the next agent should be able to start from this handoff without needing to reconstruct the whole conversation.

## Executive Summary

The Manager should become boring and strict at the lifecycle layer.

The goal is not identical UI for every module.

The goal is identical lifecycle behavior, state truth, destructive-action safety, and Manager wiring for every module, while allowing every module to bring its own Manager-facing config/control page.

Core rule:

```text
Manager owns lifecycle, status truth, registry, safety, and host shell.
Modules own their manifest, entrypoints, and Manager-facing config/control surface.
```

Every lifecycle action should follow this pattern:

```text
run action script
capture output and exit code
run status script
update Manager UI from status truth
```

Action output is progress/log material. Status output is state truth.

The first proof module is WORBI. Do not generalize to Brain, Z-Image, TRELLIS, or LoRA until WORBI is stable through repeated install/uninstall/reinstall cycles.

## Branch And Repo State

Current branch:

```text
rauty
```

Current remote state after cleanup:

```text
rauty is pushed and synced with origin/rauty
```

Latest pushed commit:

```text
4063cc2 fix(manager): carry over system checks navigation fix
```

The Manager release build passed after the System Checks fix.

The old `baffledtests` branch was deleted locally and remotely after review. Do not resurrect it.

## What Happened With `baffledtests`

`baffledtests` was reviewed as a reference branch.

It had a few useful ideas, but too much churn to merge:

- `MegaPhase4` namespace changes
- `MegaPhase4` executable rename
- JPG conversion of PNG sidebar art
- published binary churn
- broad docs churn
- possible line-ending-only script churn
- registry/service scaffolding that was not actually wired into the active Manager flow
- lifecycle-complete claims that were not proven from this repo alone

Final decision:

```text
Use baffledtests as a source of observations only.
Do not merge it.
Do not recreate it.
Do not carry its branch identity or namespace changes forward.
```

## What Was Taken From `baffledtests`

Only one code fix was taken: the System Checks navigation state fix.

File:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
```

Fix:

```csharp
SelectedNavigationItem = null;
```

This was added to the fallback path in `SelectPrimaryPage`.

Reason:

`SystemChecks` is opened from the Home page but is not part of `PrimaryNavigationItems`. Without clearing `SelectedNavigationItem`, Home can remain selected while the actual page is System Checks. Then clicking Home later can do nothing because the selected navigation item did not change.

Verification already performed:

- release build passed
- fix pushed to `origin/rauty`

## What Was Rejected From `baffledtests`

Do not bring back:

- JPG sidebar portrait conversion
- `.csproj` changes from `*.png` to `*.jpg`
- `NymphsCore.MegaPhase4` namespace
- `NymphsCore.MegaPhase4` assembly name
- `NymphsCore.MegaPhase4.exe`
- published binaries from that branch
- scaffolded services as implementation baseline
- broad line-ending churn
- lifecycle "complete" docs as proof

Important art note:

The sidebar portraits must stay PNG because they need transparent backgrounds and are still WIP Photoshop assets.

## Core Problem Being Solved

The current Manager shell has the right broad shape, but lifecycle state is not robust enough.

Current weaknesses:

- module roster is still hardcoded
- install paths are still hardcoded
- status inference is partly per-module C#
- manifests exist but are not yet the full source of truth
- module manifests are inconsistent
- normal uninstall can preserve data under an install root
- status can mistake preserved data for installed runtime
- install scripts generally do not write a consistent `.nymph-module-version`
- install scripts generally do not emit a consistent installed version
- Manager sometimes patches immediate UI state optimistically, then refreshes afterward
- stale or wrong status can make UI flip back or feel buggy

The robust target:

```text
Module repo owns manifest + lifecycle entrypoints + Manager surface definition.
Manager owns generic lifecycle engine + status truth + safety + host shell.
Status output after an action is the final state.
Preserved data never means runtime is installed.
```

## WORBI Bug: The Concrete Proof Case

WORBI currently demonstrates the bug clearly.

Normal uninstall preserves:

```text
data
projects
config
logs
```

The old status logic can treat the presence of the install root as installed:

```bash
if [[ -d "$INSTALL_DIR" ]]; then
  installed=true
fi
```

That means this bad chain can happen:

```text
uninstall preserves ~/worbi/logs
~/worbi still exists
status says installed=true
Manager trusts status
UI still shows installed actions
reinstall/uninstall feels stale and buggy
```

This is not primarily a XAML refresh issue.

It is a bad installed-state contract.

Required WORBI distinction:

```text
installed runtime != preserved user data
```

Installed runtime should mean:

```text
~/worbi/.nymph-module-version exists
```

Installed runtime must not mean:

```text
~/worbi directory exists
```

Good WORBI status output:

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

## Status Contract

Every module status script should print machine-readable `key=value` lines.

Minimum standard keys:

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

For WORBI specifically:

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

Manager lifecycle rail should depend only on the standard keys.

Custom module pages may interpret additional module-specific keys.

## Version Marker Contract

Each installed module should have a durable installed marker.

Preferred marker:

```text
<install_root>/.nymph-module-version
```

Rules:

- install writes the marker at the end of a successful install
- update preserves or rewrites the marker to the new version
- uninstall removes the marker during normal runtime uninstall
- status reads the marker to determine installed runtime
- preserved data does not recreate or imply the marker

For WORBI:

```text
~/worbi/.nymph-module-version
```

## Lifecycle Action Rule

The Manager must treat action output and status output differently.

Action output:

```text
progress
logs
errors
exit code
human feedback
```

Status output:

```text
final module state
installed/running/version/health/data_present/runtime_present
```

Required flow:

```text
Install:
  run install
  capture output
  run status
  update UI from status

Update:
  run update or install/update entrypoint
  capture output
  run status
  update UI from status

Start:
  run start
  capture output
  run status
  update UI from status

Stop:
  run stop
  capture output
  run status
  update UI from status

Uninstall:
  run uninstall
  capture output
  run status
  update UI from status

Delete data:
  require explicit confirmation/dry-run
  run delete/purge
  run status
  update UI from status
```

Do not let `ApplyImmediateModuleInstallResult` or similar optimistic state become the final truth.

If immediate feedback is desired, use temporary action state only:

```text
Installing...
Uninstalling...
Refreshing status...
```

Then replace with status truth.

## Module-Owned Manager Surface

This is the important architecture update from 2026-05-10.

The module should contain its own Manager-facing config/control definition.

Bad direction:

```text
Manager knows every module's settings, controls, fields, and special UI.
Each module is forced to fit Manager's hardcoded plumbing.
```

Better direction:

```text
Each module ships a Manager-facing config/control definition.
Manager validates it, hosts it, and connects it to the shared lifecycle/status contract.
```

Manager owns:

- registry and manifest discovery
- install/update/uninstall/delete-data flow
- status refresh truth
- navigation placement
- shared lifecycle rail
- permissions and confirmations
- dry-run rules
- destructive-action safety
- busy/error/log/progress behavior
- host shell consistency

Module owns:

- config page contents
- settings fields
- module-specific controls
- health details
- advanced runtime options
- module-specific logs/actions presentation
- explanatory labels and defaults for its own workflow

This matters because WORBI, TRELLIS, Z-Image, Brain, and LoRA are different kinds of modules. They should share lifecycle semantics, not one Manager-authored page shape.

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

Do not start by loading arbitrary module-owned native WPF code.

## Manifest Contract

Current manifests are inconsistent.

WORBI uses one style:

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

Other modules use another style:

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

Do not keep two permanent manifest dialects.

Proposed V1 shape:

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
- use `source.type`, not unrelated source shapes
- use one `entrypoints` object
- use `install.root`, not sometimes `runtime.install_root` and sometimes `install.path`
- use `install.version_marker`
- keep `ui.page=custom` for module-specific pages
- keep `ui.standard_lifecycle_rail=true`
- use `ui.manager_surface` for the module-owned config/control surface
- start with `manager_surface.type=declarative`
- allow `webview` later only with explicit local URL/path and sandbox rules

## Manager Data Model Direction

Manager should stop treating mutable UI view models as the source of truth.

Use three conceptual layers:

```text
NymphPluginDescriptor
  mostly static manifest data
  id, name, category, entrypoints, install root, UI surface info

NymphStatusSnapshot
  dynamic status truth from status script
  installed, running, version, health, data_present, runtime_present

NymphModuleViewModel
  UI projection
  descriptor + latest snapshot + current action progress
```

The current `ManagerShellViewModel` still constructs modules manually. That should eventually become:

```text
registry -> manifest fetch/cache -> descriptors -> status snapshots -> view models
```

But do not build this whole abstraction before WORBI proves the lifecycle truth model.

## Destructive Action Safety

Uninstall and delete-data are different operations.

Normal uninstall:

```text
remove runtime
preserve user data by default
remove version marker
status should say installed=false
status may say data_present=true
```

Delete data:

```text
explicit separate operation
requires confirmation
requires manifest-declared delete scopes
should support dry-run preview
should not be inferred from UI state
```

For now, destructive purge/delete-data should remain WORBI-only unless a module manifest explicitly declares safe delete scopes and the lifecycle engine has been proven.

Never infer deletion targets from mutable UI state.

## Implementation Order For Next Session

Do this in order.

### 1. Confirm Clean Branch

```text
git status
git branch
```

Expected:

```text
on rauty
clean or only intentional handoff doc changes
no baffledtests branch
origin/baffledtests absent
```

### 2. Start With WORBI Scripts

Find WORBI module scripts. Likely external module repo path from prior work:

```text
/home/nymph/NymphsModules/worbi/scripts/
```

Key files:

```text
install_worbi.sh or installer_from_package.sh
worbi_status.sh
worbi_uninstall.sh
update_worbi.sh
```

Required script behavior:

- install writes `~/worbi/.nymph-module-version`
- install prints installed version if possible
- status reads `.nymph-module-version`
- status reports `installed=false` when marker is absent
- status reports `data_present=true` when preserved folders remain
- uninstall removes `.nymph-module-version`
- uninstall preserves data by default
- purge/delete-data remains explicit

### 3. Adjust Manager Status Parsing Only As Needed

Manager should trust the corrected status output.

Avoid broad Manager rewrites at first.

In Manager:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
```

Check:

- how WORBI status output is parsed
- whether `data_present` can be surfaced
- whether installed state comes from `installed=`
- whether action methods patch state optimistically before refresh

First target:

```text
make current Manager behave correctly with corrected WORBI status
```

Do not jump straight to a full registry service rewrite.

### 4. Ensure Action Then Status

For WORBI install/update/start/stop/uninstall:

```text
run action
refresh status
project UI from refreshed status
```

If immediate state patching remains, it must not override final status.

### 5. Test WORBI Repeatedly

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

Expected key assertion:

```text
after normal uninstall:
installed=false
data_present=true if data remains
runtime_present=false
Manager shows install/reinstall path, not installed runtime controls
```

### 6. Only Then Add Strict Manifest Work

Once WORBI is boring:

- define strict WORBI manifest
- validate required fields
- expose lifecycle entrypoints from manifest
- include `ui.manager_surface`
- keep Manager host shell generic

### 7. Only Then Expand

After WORBI passes repeated lifecycle testing:

1. Brain
2. Z-Image
3. LoRA
4. TRELLIS last

TRELLIS should be last because it is heavy and recently had wiped-test-WSL risk.

## What Not To Do

Do not:

- standardize every module at once
- resurrect `baffledtests`
- reintroduce `MegaPhase4`
- convert PNG art to JPG
- use directory existence as installed truth
- merge broad generated architecture
- trust lifecycle-complete docs without testing
- build the whole registry abstraction before WORBI works
- load arbitrary native WPF plugin UI code
- make Manager own every module's config fields

## Useful Build Command

The working release build route used in this session was:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

When run through Bash, escaping may need to preserve the UNC path:

```bash
/bin/bash -lc "powershell.exe -NoProfile -ExecutionPolicy Bypass -File '\\\\wsl.localhost\\NymphsCore_Lite\\home\\nymph\\NymphsCore\\Manager\\apps\\NymphsCoreManager\\build-release.ps1'"
```

Previous direct `dotnet.exe build` using `wslpath -w` failed because Windows did not see the generated UNC path as existing in this environment. Use the release script path above.

## Final Brief

Continue on `rauty`.

Ignore `baffledtests`; it is deleted and should stay gone.

Implement the Nymph Plugin standard from the lifecycle truth upward, not from broad generated scaffolding downward.

Make WORBI the proof:

```text
marker-driven installed state
preserved data separate from runtime
status after every action
Manager UI projected from status truth
module-owned Manager config surface described by manifest
```

Once WORBI is reliable, then generalize.
