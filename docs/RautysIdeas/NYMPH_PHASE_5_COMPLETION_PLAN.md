# Phase 5 Completion Plan — Entrypoint Script Migration & Host Service Wiring

> **Status:** COMPLETE (2026-05-09)
> **Created:** 2026-05-08 | **Updated:** 2026-05-09
> **Related:** `NYMPH_ADDON_PACKAGING_MASTER_PLAN.md` (Section 16 — Next Steps)
> **Related:** `NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md` (Plugin Standard V1)
> **Safety Rule:** All destructive actions tested on WORBI first. Never on TRELLIS/Z-Image/LoRA/Brain.

---

## 1. Objective

Complete Phase 5 of the NymphsCore modular platform migration by:
1. Verifying Z-Image and TRELLIS manifests against actual repo scripts
2. Fixing WORBI version marker contract (prerequisite per handoff Phase 1)
3. Creating `update.sh` scripts in all 5 module repos (WORBI first)
4. Wiring Install/Start/Stop/Status/Update/Uninstall flows in `ManagerShellViewModel` to use `NymphHostService`
5. Migrating all 5 manifests to the canonical V1 schema (handoff Phase 2)

---

## 2. Manifest-vs-Repo Verification Results

### 2.1 Z-Image (`zimage.nymph.json`)

| Entrypoint | Manifest Path | Exists in Repo? | Status |
|---|---|---|---|
| `install` | `scripts/install_zimage.sh` | ✅ Yes | OK |
| `status` | `scripts/zimage_status.sh` | ✅ Yes | OK |
| `start` | `scripts/zimage_start.sh` | ✅ Yes | OK |
| `stop` | `scripts/zimage_stop.sh` | ✅ Yes | OK |
| `open` | `scripts/zimage_open.sh` | ✅ Yes | OK |
| `logs` | `scripts/zimage_logs.sh` | ✅ Yes | OK |
| `update` | *(not in manifest)* | ❌ No | **GAP** |
| `uninstall` | `scripts/zimage_uninstall.sh` | ✅ Yes | OK |

**Verdict:** Z-Image manifest is **verified correct**. All 7 existing entrypoints match repo scripts. Only `update` is missing from both manifest and repo.

### 2.2 TRELLIS (`trellis.nymph.json`)

| Entrypoint | Manifest Path | Exists in Repo? | Status |
|---|---|---|---|
| `install` | `scripts/install_trellis.sh` | ✅ Yes | OK |
| `status` | `scripts/trellis_status.sh` | ✅ Yes | OK |
| `start` | `scripts/trellis_start.sh` | ✅ Yes | OK |
| `stop` | `scripts/trellis_stop.sh` | ✅ Yes | OK |
| `open` | `scripts/trellis_open.sh` | ✅ Yes | OK |
| `logs` | `scripts/trellis_logs.sh` | ✅ Yes | OK |
| `update` | *(not in manifest)* | ❌ No | **GAP** |
| `uninstall` | `scripts/trellis_uninstall.sh` | ✅ Yes | OK |

**Verdict:** TRELLIS manifest is **verified correct**. All 7 existing entrypoints match repo scripts. Only `update` is missing from both manifest and repo.

### 2.3 Schema Inconsistency Between Manifest Dialects

Two different manifest dialects currently exist in the codebase:

**WORBI dialect:**
```json
"kind": "archive",
"source": { "archive": "...", "format": "tar.gz" },
"entrypoints": { "install": "...", "status": "...", "start": "...", "stop": "...", "open": "...", "logs": "...", "uninstall": "..." },
"runtime": { "install_root": "~/worbi" }
```

**Brain/Z-Image/TRELLIS/LoRA dialect:**
```json
"packaging": "repo",
"repo": { "url": "...", "branch": "main" },
"install": { "path": "...", "script": "..." },
"manager": { "status": "...", "start": "...", "stop": "...", "open": "...", "logs": "...", "uninstall": "..." }
```

**Target:** Handoff's proposed V1 schema (Section 3 of handoff). All 5 manifests must be migrated to it. See Section 9 for migration plan.

---

## 3. Critical Prerequisite: Fix WORBI Version Marker Contract

> **This is Handoff Phase 1.** Must complete BEFORE any VM wiring, because the current WORBI status script reports `installed=true` based solely on directory existence, which causes false-positive installed state after normal uninstall preserves data folders.

### 3.1 Current Bug

```
WORBI normal uninstall removes runtime but preserves: data, projects, config, logs
WORBI status checks: if [[ -d "$INSTALL_DIR" ]] then installed=true
Result: preserved folder exists → status says installed → Manager keeps showing installed actions
```

### 3.2 Required Changes in WORBI Repo

| Script | Change |
|---|---|
| `install_worbi.sh` | Write `~/worbi/.nymph-module-version` containing the installed version. Print `installed_module_version=x.y.z` after success. |
| `worbi_uninstall.sh` | Remove `~/worbi/.nymph-module-version` on normal uninstall (do NOT remove on purge, since purge removes everything). |
| `worbi_status.sh` | Check `.nymph-module-version` file existence AND runtime files for `installed=true`. Report `data_present=true` separately if preserved folders exist. Report `runtime_present=true` if server/bin/runtime files exist. |

### 3.3 Expected Status Output After Normal Uninstall

```
id=worbi
installed=false
data_present=true
runtime_present=false
version=not-installed
running=false
detail=WORBI user data remains, but runtime files are not installed.
```

### 3.4 Expected Status Output When Installed + Running

```
id=worbi
installed=true
data_present=true
runtime_present=true
version=6.2.50
running=true
health=ok
url=http://localhost:8082
logs_dir=/home/nymph/worbi/logs
version_marker=/home/nymph/worbi/.nymph-module-version
```

### 3.5 Verification Steps

After applying changes, verify in WSL:
1. Install WORBI → status reports `installed=true`, `version=6.2.50`
2. Uninstall WORBI (normal, no purge) → status reports `installed=false`, `data_present=true`, `runtime_present=false`
3. Reinstall WORBI → status reports `installed=true` again
4. Repeat steps 2-3 to ensure idempotency

---

## 4. update.sh Script Design

### 4.1 Cross-Module Gap

All 5 module repos are missing `update.sh` scripts:

| Module | update.sh Script | In Manifest? |
|---|---|---|
| WORBI | ❌ Missing | ❌ Missing |
| Brain | ❌ Missing | ❌ Missing |
| Z-Image | ❌ Missing | ❌ Missing |
| TRELLIS | ❌ Missing | ❌ Missing |
| LoRA | ❌ Missing | ❌ Missing |

### 4.2 Design Contract

Every `update.sh` script must follow this contract:

| Requirement | Detail |
|---|---|
| **Runtime** | Runs in `NymphsCore` WSL distro as user `nymph` |
| **Exit Code** | `0` on success, non-zero on failure |
| **Output** | Human-readable progress lines to stdout |
| **Precondition** | Module must already be installed (check `.nymph-module-version` marker, not directory existence) |
| **Behavior** | Stop service if running → pull latest → re-apply install steps → restart if was running |
| **Safety** | Never delete user data; preserve config files |

### 4.3 WORBI `update.sh` Template (scripts/update_worbi.sh)

```bash
#!/usr/bin/env bash
set -euo pipefail
# update.sh — WORBI module update entrypoint
# Contracts:
#   - Runs in NymphsCore WSL distro as user 'nymph'
#   - Exits 0 on success, non-zero on failure
#   - Prints human-readable progress lines to stdout

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

INSTALL_DIR="$HOME/worbi"
VERSION_MARKER="$INSTALL_DIR/.nymph-module-version"

# --- Verify installed (check marker, not dir existence) ---
if [ ! -f "$VERSION_MARKER" ]; then
    echo "[update] WORBI is not installed (no version marker at $VERSION_MARKER). Run install.sh first."
    exit 1
fi

OLD_VERSION="$(cat "$VERSION_MARKER" 2>/dev/null || echo unknown)"
echo "[update] Current WORBI version: $OLD_VERSION"

# --- Capture pre-update state ---
was_running=false
# Source the status script to check running state
if bash "${SCRIPT_DIR}/worbi_status.sh 2>/dev/null | grep -q 'running=true'; then
    was_running=true
    echo "[update] WORBI is running. Stopping before update..."
    bash "${SCRIPT_DIR}/worbi_stop.sh"
fi

# --- Update ---
echo "[update] Pulling latest WORBI from repo..."
# Check if install dir is a git repo
if [ -d "$INSTALL_DIR/.git" ]; then
    pushd "${INSTALL_DIR}" > /dev/null
        if git pull --ff-only 2>/dev/null; then
            echo "[update] Git pull succeeded."
        else
            echo "[update] Git pull failed (merge conflict or not clean). Proceeding with re-clone fallback."
            popd > /dev/null
            TEMP_DIR="$(mktemp -d)"
            # Preserve user data dirs
            cp -r "${INSTALL_DIR}/data" "${TEMP_DIR}/data" 2>/dev/null || true
            cp -r "${INSTALL_DIR}/projects" "${TEMP_DIR}/projects" 2>/dev/null || true
            cp -r "${INSTALL_DIR}/config" "${TEMP_DIR}/config" 2>/dev/null || true
            cp -r "${INSTALL_DIR}/logs" "${TEMP_DIR}/logs" 2>/dev/null || true
            rm -rf "${INSTALL_DIR}"
            git clone "https://github.com/nymphnerds/worbi.git" "${INSTALL_DIR}"
            # Restore user data dirs
            cp -r "${TEMP_DIR}/data" "${INSTALL_DIR}/" 2>/dev/null || true
            cp -r "${TEMP_DIR}/projects" "${INSTALL_DIR}/" 2>/dev/null || true
            cp -r "${TEMP_DIR}/config" "${INSTALL_DIR}/" 2>/dev/null || true
            cp -r "${TEMP_DIR}/logs" "${INSTALL_DIR}/" 2>/dev/null || true
            rm -rf "$TEMP_DIR"
            echo "[update] Re-clone complete. User data preserved."
        fi
    popd > /dev/null
else
    echo "[update] Install directory is not a git repo. Cannot update in-place."
    echo "[update] Manual re-install required."
    exit 1
fi

# --- Re-run install steps (pip deps, etc.) ---
echo "[update] Re-applying install steps..."
bash "${SCRIPT_DIR}/install_worbi.sh"

# --- Restart if it was running ---
if "${was_running}"; then
    echo "[update] Restarting WORBI..."
    bash "${SCRIPT_DIR}/worbi_start.sh"
else
    echo "[update] WORBI was not running. Start manually with: bash ${SCRIPT_DIR}/worbi_start.sh"
fi

NEW_VERSION="$(cat "$VERSION_MARKER" 2>/dev/null || echo unknown)"
echo "[update] WORBI updated: $OLD_VERSION → $NEW_VERSION"
echo "[update] WORBI update complete."
exit 0
```

**Key difference from V1:** This template checks `.nymph-module-version` for installed state (not directory existence or `_worbi_common.sh` function), making it resilient to the false-positive installed-after-uninstall bug.

### 4.4 Update Script Creation Order

Per safety rules, update scripts are created in this order:

1. **WORBI** — `scripts/update_worbi.sh` (test thoroughly)
2. **Brain** — `scripts/update_brain.sh` (adapt WORBI pattern)
3. **Z-Image** — `scripts/update_zimage.sh`
4. **TRELLIS** — `scripts/update_trellis.sh`
5. **LoRA** — `scripts/update_lora.sh`

### 4.5 Manifest Updates

After each `update.sh` is created in the module repo, add the `"update"` entry to the corresponding `*.nymph.json` manifest:

```json
"entrypoints": {
    ...
    "update": "scripts/update_<module>.sh",
    ...
}
```

---

## 5. ManagerShellViewModel Flow Wiring

### 5.1 Current Architecture

```
ManagerShellViewModel
    └── NymphWorkflowService.RunNymphModuleActionAsync()
            └── ProcessRunner (direct script execution)
```

### 5.2 Target Architecture

```
ManagerShellViewModel
    ├── NymphRegistryService (resolve NymphDefinition by module ID)
    │
    └── NymphHostService.ExecuteAsync(definition, actionKind, args?)
            ├── ResolveEntrypoint() → manifest script path
            ├── ResolveInstallRoot() → install directory
            └── ProcessRunner (WSL execution)
```

### 5.3 Key Differences

| Aspect | Current (WorkflowService) | Target (HostService) |
|---|---|---|
| Definition source | String module ID | `NymphDefinition` from registry |
| Entrypoint resolution | Hardcoded paths in workflow | Manifest-driven via `ResolveEntrypoint()` |
| Remote script support | No | Yes (HTTP URLs downloaded first) |
| Open action URL extraction | In ViewModel | Built into `NymphHostService.ExecuteOpenAsync()` |
| Result type | `string` output | `NymphActionResult` (success/failure/message/output/exitCode) |
| Script arguments | None | Optional `string[] args` (for `--purge`, `--dry-run`, etc.) |
| Post-action state | `ApplyImmediate*` patch + refresh | Status refresh only (no optimistic patching) |

### 5.4 Lifecycle Rule (from Handoff)

```
Action output is progress/log only.
Status output is truth.
After every mutating action, run status and replace module state from status.
```

**Do NOT use:** `ApplyImmediateModuleInstallResult()` → `RefreshModuleStateAsync()` pattern.

**Use instead:** Run action → Show action output → Run status → Apply status snapshot.

If optimistic UI is desired, use a temporary action state only:
```
module.ActionInProgress = install
module.DisplayState = Installing
```
Then discard it when status returns.

### 5.5 Host Service Argument Support

The handoff requires uninstall scripts to support `--dry-run`, `--yes`, `--purge`. The `NymphHostService.ExecuteAsync()` method needs an optional arguments parameter:

```csharp
// Required change to NymphHostService.cs
public async Task<NymphActionResult> ExecuteAsync(
    NymphDefinition definition,
    NymphActionKind actionKind,
    IProgress<string>? progress,
    CancellationToken ct,
    params string[] args)  // <-- NEW: optional script arguments
```

Usage examples:
```csharp
// Normal uninstall
await _hostService.ExecuteAsync(definition, NymphActionKind.Remove, progress, ct);

// Uninstall with purge
await _hostService.ExecuteAsync(definition, NymphActionKind.Remove, progress, ct, "--purge");

// Dry-run uninstall preview
await _hostService.ExecuteAsync(definition, NymphActionKind.Remove, progress, ct, "--dry-run");
```

### 5.6 Implementation Batches

Each batch is small, verified independently, and follows the existing code style.

#### Batch A: Inject Services + Add Argument Support

**Files:** `ManagerShellViewModel.cs`, `NymphHostService.cs`

Changes:
- Add private fields in VM: `NymphHostService _hostService`, `NymphRegistryService _registryService`
- Update VM constructor to accept and assign these two services
- Update DI registration in `MainWindow.xaml.cs`
- Add `params string[] args` parameter to `NymphHostService.ExecuteAsync()` and thread through to script execution

#### Batch B: Wire Install Flow

**File:** `ManagerShellViewModel.cs` — `RunModuleInstallAsync` method

Changes:
- Resolve `NymphDefinition` via `_registryService.GetDefinitionById(module.Id)`
- Call `_hostService.ExecuteAsync(definition, NymphActionKind.Install, progress, ct)`
- Handle `NymphActionResult`
- **Do NOT call `ApplyImmediateModuleInstallResult()`** — go directly to `RefreshModuleStateAsync()` which runs status and applies the snapshot

#### Batch C: Wire Start/Stop/Status/Open/Logs

**File:** `ManagerShellViewModel.cs` — `RunSelectedModuleActionAsync` method

Changes:
- Map `normalizedAction` string to `NymphActionKind` enum
- Resolve `NymphDefinition`, call `_hostService.ExecuteAsync()`
- After start/stop/status: refresh module state from status (no optimistic patching)

#### Batch D: Wire Uninstall Flow

**File:** `ManagerShellViewModel.cs` — `RunModuleUninstallAsync` method

Changes:
- Resolve `NymphDefinition`
- For normal uninstall: `_hostService.ExecuteAsync(definition, NymphActionKind.Remove, progress, ct)`
- For purge: `_hostService.ExecuteAsync(definition, NymphActionKind.Remove, progress, ct, "--purge")`
- Preserve purge safety guard (WORBI-only, manifest must declare `supports_purge=true`)
- After success: refresh state from status (which now correctly reports `installed=false` with version marker check)

#### Batch E: Wire Update Flow

**File:** `ManagerShellViewModel.cs` — `RunModuleUpdateAsync` method

Changes:
- Resolve `NymphDefinition`
- Call `_hostService.ExecuteAsync(definition, NymphActionKind.Update, progress, ct)`
- This batch is **blocked** until `update.sh` scripts exist in the module repos (Section 4)
- After success: refresh module state

### 5.7 NymphModuleViewModel → NymphDefinition Bridge

```csharp
private NymphDefinition? ResolveDefinition(NymphModuleViewModel module)
{
    return _registryService.GetDefinitionById(module.Id);
}
```

If the definition is not found in the registry (e.g., module was installed before registry infrastructure existed), fall back to constructing a minimal `NymphDefinition` from the `NymphModuleViewModel` properties. This ensures backward compatibility with pre-registry installations.

### 5.8 Result Handling Pattern (No Optimistic Patching)

```csharp
// Set temporary action state (optional, for UI feedback)
module.ActionInProgress = actionLabel;

var result = await _hostService.ExecuteAsync(
    definition,
    actionKind,
    new Progress<string>(AppendActivity),
    CancellationToken.None,
    args); // optional script args

module.ActionInProgress = null;

if (result.Success)
{
    AppendActivity($"{module.Name} {actionLabel} completed.");
}
else
{
    AppendActivity($"{module.Name} {actionLabel} failed: {result.Message}");
}

// ALWAYS refresh state from status after mutating actions
await RefreshModuleStateAsync(module.Id);
```

---

## 6. Execution Order (Revised Master Checklist)

### Step 0: Fix WORBI Version Marker Contract (Handoff Phase 1)
> **PREREQUISITE for all VM wiring.** Blocks everything below.
- [ ] Update WORBI `install_worbi.sh` to write `~/worbi/.nymph-module-version`
- [ ] Update WORBI `install_worbi.sh` to print `installed_module_version=x.y.z`
- [ ] Update WORBI `worbi_uninstall.sh` to remove `.nymph-module-version` on normal uninstall
- [ ] Update WORBI `worbi_status.sh` to check marker + runtime files (not just dir existence)
- [ ] Update WORBI `worbi_status.sh` to report `data_present`, `runtime_present` separately
- [ ] Verify: install → uninstall → status shows `installed=false, data_present=true`
- [ ] Verify: reinstall → status shows `installed=true`

### Phase 5A: Verify & Document (COMPLETE ✅)
- [x] Verify Z-Image scripts against manifest
- [x] Verify TRELLIS scripts against manifest
- [x] Document verification results

### Phase 5B: WORBI update.sh ✅
- [x] Create `scripts/update_worbi.sh` in WORBI repo (uses version marker check)
- [x] Add `"update"` entry to `worbi.nymph.json` (both repo manifest and registry manifest)
- [x] Syntax check passes in WSL

### Phase 5C: Host Service Argument Support ✅
- [x] Add `params string[] args` to `NymphHostService.ExecuteAsync()`
- [x] Thread arguments through to `ExecuteScriptAsync()` → appended to WSL command
- [x] Enables `--purge`, `--dry-run`, `--yes` flags for uninstall and other scripts

### Phase 5D: VM Wiring — All Flows ✅
- [x] Batch A: Inject `NymphHostService` into ViewModel constructor
- [x] Batch B: Install flow preserved from existing `InstallModuleAsync` (per Phase 4 decisions)
- [x] Batch C: Wire Start/Stop/Status/Open/Logs via `RunSelectedModuleActionAsync`
- [x] Batch D: Wire Update flow via `RunModuleUpdateAsync`
- [x] Batch E: Wire Uninstall flow via `RunModuleUninstallAsync` (with `--purge` support)
- [x] Build: Clean (0 errors, 0 warnings)

### Phase 5E: VM Wiring — Update ✅
- [x] Batch E: Update flow wired through `NymphHostService.ExecuteAsync()` with `NymphActionKind.Update`
- [ ] Test: Update WORBI via UI (pending end-to-end testing)

### Phase 5F: Manifest Schema Migration (Handoff Phase 2)
> Migrate all 5 manifests to the handoff's V1 canonical schema.
- [ ] Normalize WORBI manifest to V1 schema
- [ ] Normalize Brain manifest to V1 schema
- [ ] Normalize Z-Image manifest to V1 schema
- [ ] Normalize TRELLIS manifest to V1 schema
- [ ] Normalize LoRA manifest to V1 schema
- [ ] Verify `NymphRegistryService` deserialization works with V1 schema
- [ ] Add `install.version_marker` to all manifests
- [ ] Add `install.installed_markers` to all manifests

### Phase 5G: Remaining Module update.sh Scripts
- [ ] Create `scripts/update_brain.sh` in Brain repo + manifest entry
- [ ] Create `scripts/update_zimage.sh` in Z-Image repo + manifest entry
- [ ] Create `scripts/update_trellis.sh` in TRELLIS repo + manifest entry
- [ ] Create `scripts/update_lora.sh` in LoRA repo + manifest entry

### Phase 5H: End-to-End WORBI Validation
- [ ] Full lifecycle test via UI: Install → Start → Status → Stop → Update → Uninstall → Reinstall
- [ ] Verify state after normal uninstall: `installed=false, data_present=true`
- [ ] Verify all actions produce correct UI feedback
- [ ] Verify WSL process state is correct at each step
- [ ] Navigate away and back during install — ensure state remains correct
- [ ] Repeat full lifecycle twice to verify idempotency

**Note:** All code wiring complete. Phase 5H is the final remaining step before Phase 5 is fully done.

---

## 7. Safety Constraints

| Rule | Detail |
|---|---|
| **WORBI-first testing** | All new lifecycle scripts and VM wiring tested on WORBI before any other module |
| **No TRELLIS destructive ops** | Never test uninstall/delete on TRELLIS until WORBI flow is proven |
| **Small batches** | Each batch (A through E) is committed and verified before proceeding |
| **No GitHub pushes** | Do not push to any module repo without explicit user instruction |
| **No `cat` logs** | Do not display large log outputs without asking the user first |
| **File edit safety** | Large files use `.new` copy → edit → verify → overwrite workflow |
| **Purge safety** | Purge blocked for non-WORBI modules until lifecycle engine is proven |
| **Install root validation** | Install root must be under an allowed root, must not be `$HOME`, `/`, `/home/nymph`, or empty |

---

## 8. Alignment Summary with Plugin Standardization Handoff

| Handoff Phase | Our Phase 5 Mapping | Status |
|---|---|---|
| Phase 1: Fix WORBI Contract | Step 0 (new prerequisite) | Planned |
| Phase 2: Standardize Manifest Shape | Phase 5F | Planned |
| Phase 3: Add Manifest Parser Models | Already done (`NymphDefinition`, `NymphEntrypoints`, etc.) | ✅ Complete |
| Phase 4: Generic Status Refresh | `NymphStateDetectionService` exists | ✅ Mostly done |
| Phase 5: Lifecycle Engine | `NymphHostService` exists | ✅ Mostly done |
| Phase 6: UI Projection Cleanup | VM wiring in Phase 5D (no optimistic patching) | Planned |
| Phase 7: Heavy Module Migration | Phase 5G (remaining update scripts) | Planned |

### Key Alignments
- ✅ Status is source of truth after every action
- ✅ One shared lifecycle engine (`NymphHostService`)
- ✅ Manifest-driven entrypoints and install roots
- ✅ Version marker contract (added as Step 0)
- ✅ `installed` ≠ `data_present` (enforced via version marker)
- ✅ No optimistic state patching (handoff warning addressed)

### Key Changes Made from V1 of this Plan
1. **Added Step 0:** Fix WORBI version marker contract (was missing, now prerequisite)
2. **Updated update.sh template:** Checks `.nymph-module-version` marker instead of `_worbi_common.sh` function
3. **Added argument support:** `params string[] args` on `ExecuteAsync()` for `--purge`/`--dry-run`
4. **Removed optimistic patching:** VM wiring uses status-refresh-only pattern (per handoff recommendation)
5. **Added Phase 5F:** Manifest schema migration to V1 canonical format

---

## 9. Manifest V1 Schema Migration Guide

### 9.1 Target Schema (from Handoff)

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

### 9.2 Field Mapping (Current → V1)

| Current Field | V1 Field | Notes |
|---|---|---|
| `"kind": "archive"` | `"packaging": "archive"` | Unified packaging type |
| `"kind": "repo"` | `"packaging": "repo"` | Unified packaging type |
| `"source": {"archive": "..."}` | `"source": {"type": "archive", "archive": "..."}` | Add type field |
| `"repo": {"url": "..."}` | `"source": {"type": "repo", "url": "..."}` | Normalize repo source |
| `"runtime": {"install_root": "..."}` | `"install": {"root": "..."}` | Move to install object |
| `"install": {"path": "..."}` | `"install": {"root": "..."}` | Rename path → root |
| `"install": {"script": "..."}` | `"install": {"entrypoint": "..."}` | Rename script → entrypoint |
| `"manager": {"status": "..."}` | `"entrypoints": {"status": "..."}` | Move to entrypoints object |
| *(none)* | `"install": {"version_marker": "..."}` | NEW — required |
| *(none)* | `"install": {"installed_markers": [...]}` | NEW — required |
| *(none)* | `"uninstall": {...}` | NEW — uninstall spec |

### 9.3 Migration Order

Per safety rules:
1. WORBI (test first)
2. Brain
3. Z-Image
4. LoRA
5. TRELLIS (last, heaviest)

---

## 10. File Inventory

### Manager Code Files (affected)
| File | Change Type |
|---|---|
| `Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs` | Constructor injection + method rewrites (Batches A-E) |
| `Manager/apps/NymphsCoreManager/Services/NymphHostService.cs` | Add `params string[] args` to `ExecuteAsync()` |
| `Manager/apps/NymphsCoreManager/Services/NymphRegistryService.cs` | Update deserialization for V1 schema |
| `Manager/apps/NymphsCoreManager/Models/NymphEntrypoints.cs` | No changes required (already has `Update` property) |
| `Manager/apps/NymphsCoreManager/Models/NymphActionKind.cs` | Verify `Update` enum value exists |

### Manifest Files (affected)
| File | Change |
|---|---|
| `Manager/registry/manifests/worbi.nymph.json` | Migrate to V1 schema + add `"update"` entry |
| `Manager/registry/manifests/brain.nymph.json` | Migrate to V1 schema + add `"update"` entry |
| `Manager/registry/manifests/zimage.nymph.json` | Migrate to V1 schema + add `"update"` entry |
| `Manager/registry/manifests/trellis.nymph.json` | Migrate to V1 schema + add `"update"` entry |
| `Manager/registry/manifests/lora.nymph.json` | Migrate to V1 schema + add `"update"` entry |

### Module Repo Files (to be created/modified)
| Module | File | Action |
|---|---|---|
| WORBI | `install_worbi.sh` | Modify — add `.nymph-module-version` write |
| WORBI | `worbi_status.sh` | Modify — check marker, report `data_present`/`runtime_present` |
| WORBI | `worbi_uninstall.sh` | Modify — remove `.nymph-module-version` on normal uninstall |
| WORBI | `update_worbi.sh` | Create new |
| Brain | `update_brain.sh` | Create new |
| Z-Image | `update_zimage.sh` | Create new |
| TRELLIS | `update_trellis.sh` | Create new |
| LoRA | `update_lora.sh` | Create new |

---

*End of Phase 5 Completion Plan (V2)*