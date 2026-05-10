# Rauty Return Plan: baffledtests Review and Next Steps

Date: 2026-05-09
Branch context: `rauty`
Reference branch: `origin/baffledtests`

## Purpose

This doc captures the safe plan for returning to the Manager work after the `baffledtests` comparison.

The main conclusion is simple:

`baffledtests` should be treated as a reference branch, not a merge branch.

It contains a couple of useful fixes and ideas, but it also contains test-branch artifacts, broad churn, and changes that should not be carried into `rauty`.

## Current Local State

At the time of review, local `rauty` was behind `origin/rauty` by one commit:

```text
origin/rauty: 311cc41 Add UNIT_TESTING_HANDOFF.md - comprehensive guide for AI agents on unit testing
```

Before doing implementation work, pull latest `origin/rauty`.

## High-Level Recommendation

Do not merge `baffledtests` wholesale.

Instead:

1. Pull latest `rauty`.
2. Manually port the System Checks navigation fix.
3. Optionally manually port the sidebar LLM context/speed monitor idea.
4. Reject the JPG asset conversion and all `MegaPhase4` namespace/output changes.
5. Treat the registry/manifest work as reference only.
6. Build the real Nymph Plugin standardization cleanly on `rauty`, starting with WORBI.

## Safe To Port

### 1. System Checks Navigation Fix

This is the cleanest piece of `baffledtests`.

Problem:

The System Checks page is not part of `PrimaryNavigationItems`. It is opened from the Home page card. When it opens, the page changes to System Checks, but `SelectedNavigationItem` can remain set to Home. Then clicking Home in the sidebar does nothing because the selected navigation item has not changed.

Fix:

In `ManagerShellViewModel.SelectPrimaryPage`, when the requested page is not found in `PrimaryNavigationItems`, clear the selected nav item before setting the page:

```csharp
SelectedNavigationItem = null;
CurrentPageKind = pageKind;
SelectedModule = null;
DisplayedModule = null;
```

Why this is safe:

- It matches the existing pattern used by module pages.
- It fixes a real UI state mismatch.
- It is tiny and easy to verify.
- It does not require any `baffledtests` branch structure.

Verification:

- Home -> System Checks -> Home works.
- Home -> Logs -> Home still works.
- Module page -> Home still works.
- System Checks sidebar visual state no longer lies.

### 2. Sidebar LLM Context and Speed Monitor

This is a useful idea, but should be ported manually.

The `baffledtests` implementation adds sidebar labels for:

```text
LLM Context
LLM Speed
```

It reads Brain/LMS logs and formats values like:

```text
LLM Context: 163,840
LLM Speed: 46.4 t/s
```

Porting rule:

Copy the behavior, not the branch.

Keep:

- Current `NymphsCoreManager` namespace.
- Current project name.
- Current executable name.
- Current PNG sidebar assets.
- Existing runtime monitor shape unless a small extension is needed.

Expected unavailable state:

```text
LLM Context: N/A
LLM Speed: N/A
```

Verification:

- Manager starts when Brain is not running.
- Sidebar monitor does not throw or freeze.
- LLM values show `N/A` when unavailable.
- If Brain/LMS is running and logs contain metrics, context and speed render.

## Do Not Port

### 1. JPG Sidebar Portrait Conversion

Reject this.

The PNG portraits must remain PNG because the artwork is still WIP and needs transparent backgrounds for ongoing Photoshop work.

Do not take:

- JPG portrait files.
- `.csproj` changes from `*.png` to `*.jpg`.
- Published ZIP artifact based on JPG conversion.

### 2. `MegaPhase4` Namespace and EXE Rename

Reject this.

`baffledtests` changes the Manager project to:

```xml
<RootNamespace>NymphsCore.MegaPhase4</RootNamespace>
<AssemblyName>NymphsCore.MegaPhase4</AssemblyName>
```

This must not be carried into `rauty`.

Keep:

```xml
<RootNamespace>NymphsCoreManager</RootNamespace>
<AssemblyName>NymphsCoreManager</AssemblyName>
```

Also reject:

- `NymphsCore.MegaPhase4.exe`
- `NymphsCore.MegaPhase4.pdb`
- Any namespace rewrites to `NymphsCore.MegaPhase4.*`

### 3. Published Binaries

Do not use published binaries from `baffledtests` as source of truth.

They include branch artifacts and are not clean implementation evidence.

### 4. Broad Line-Ending Churn

Some script diffs appear noisy and may be CRLF-only churn. Do not port broad file replacements unless the actual content change is reviewed and needed.

### 5. Registry Services As-Is

`baffledtests` adds:

- `NymphRegistryService`
- `NymphStateDetectionService`
- `NymphHostService`
- Nymph manifest model records

These are useful reference material, but should not be copied as-is.

Reasons:

- The services are not clearly wired into the active Manager source.
- `ManagerShellViewModel` still hardcodes the module list.
- State detection still falls back to `Directory.Exists(installPath)`.
- The host service has questionable WSL path/command assumptions.
- The manifest dialect is not the final contract described in the standardization handoff.

## Standardization Direction

The standardization idea is still correct.

The implementation should be rebuilt cleanly on `rauty`.

The Manager needs a stable backend contract for Nymph Plugins:

```text
install
update
status/check
start
stop
open/logs
uninstall runtime
delete data
```

The key rule:

Manager UI state should come from status truth, not optimistic action assumptions.

For every lifecycle action:

```text
run action script
capture exit code and output
run status script
update Manager state from status output
```

## WORBI Proof Module

Use WORBI as the first proof module.

The current class of bug is:

```text
uninstall preserves data folders
status checks whether ~/worbi exists
Manager thinks WORBI is still installed
```

The fix direction:

Installed runtime and preserved data must be separate states.

WORBI status should report:

```text
installed=true|false
data_present=true|false
version=<version or empty>
running=true|false
health=ok|unavailable|unknown
```

Installed means:

```text
<install_root>/.nymph-module-version exists
```

Installed must not mean:

```text
<install_root> directory exists
```

Uninstall should:

- stop runtime if running
- remove runtime files
- remove `.nymph-module-version`
- preserve user data by default
- leave `data_present=true` if data remains
- report `installed=false`

Delete data should be a separate, explicit operation.

## First Real Implementation Milestone

The first standardization milestone should be backend-only and narrow.

Target:

WORBI lifecycle becomes boring and reliable.

Required behaviors:

1. Fresh install writes `.nymph-module-version`.
2. Status reads `.nymph-module-version`.
3. Uninstall removes `.nymph-module-version`.
4. Preserved data does not count as installed runtime.
5. Manager runs status after every lifecycle action.
6. Manager updates UI only from refreshed status.
7. Reinstall after uninstall works without stale state.
8. Delete-data is explicit and separate from uninstall.

## Manifest Shape For First Pass

Start with one strict WORBI manifest. Keep it small.

Minimum fields:

```json
{
  "manifest_version": 1,
  "id": "worbi",
  "name": "WORBI",
  "version": "6.2.49",
  "source": {
    "type": "archive",
    "path": "packages/worbi-6.2.49.tar.gz",
    "format": "tar.gz"
  },
  "install": {
    "root": "~/worbi",
    "version_marker": ".nymph-module-version"
  },
  "entrypoints": {
    "install": "scripts/install_worbi.sh",
    "status": "scripts/worbi_status.sh",
    "start": "scripts/worbi_start.sh",
    "stop": "scripts/worbi_stop.sh",
    "open": "scripts/worbi_open.sh",
    "logs": "scripts/worbi_logs.sh",
    "update": "scripts/update_worbi.sh",
    "uninstall": "scripts/worbi_uninstall.sh"
  },
  "uninstall": {
    "preserve_by_default": ["data", "projects", "config", "logs"],
    "supports_purge": true,
    "requires_confirmation": true
  },
  "runtime": {
    "frontend_url": "http://localhost:8082",
    "health_url": "http://localhost:8082/api/health"
  }
}
```

Do not expand this to all modules until WORBI proves the contract.

## Later Expansion

After WORBI passes repeated lifecycle testing, apply the same pattern to:

1. Brain
2. Z-Image
3. TRELLIS.2
4. LoRA

Each module can have different UI and different runtime needs, but lifecycle state should be standardized.

The standard part is:

- install root
- version marker
- status output keys
- action result handling
- preserved data semantics
- action -> status refresh rule

The non-standard part is:

- module-specific frontend page
- runtime controls
- health checks
- logs
- advanced settings

## Suggested Work Order On Return

1. Pull latest `origin/rauty`.
2. Port System Checks navigation fix.
3. Port sidebar LLM context/speed monitor manually, if desired.
4. Build Manager.
5. Verify navigation and sidebar monitor behavior.
6. Commit that small batch.
7. Start WORBI lifecycle standardization from the handoff spec.
8. Test WORBI install/uninstall/reinstall until state is stable.
9. Only then start manifest-driven Manager wiring.

## Commit Suggestions

Small bugfix batch:

```text
fix(manager): port system checks nav and sidebar LLM metrics
```

WORBI lifecycle batch:

```text
fix(manager): make worbi lifecycle status marker-driven
```

Manifest prototype batch:

```text
feat(manager): add strict worbi nymph manifest contract
```

## Final Rule

Use `baffledtests` for ideas only.

The real implementation should be done cleanly on `rauty`, with no `MegaPhase4` namespace changes, no JPG asset conversion, no binary artifact merge, and no optimistic Manager state as the source of truth.
