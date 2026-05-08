# Phase 5 Remaining Steps — Completion Roadmap

> **Status**: IN PROGRESS (2026-05-09)
> **Created**: 2026-05-09
> **Branch**: `rauty`
> **Related**: `NYMPH_ADDON_PACKAGING_MASTER_PLAN.md` (Phase 5)
> **Related**: `NYMPH_PHASE_5_COMPLETION_PLAN.md` (V2 plan)
> **Related**: `NYMPH_PHASE_5D_VM_WIRING_NEXT_STEPS.md` (VM wiring details)
> **Safety Rule**: All destructive actions tested on WORBI first. Never on TRELLIS/Z-Image/LoRA/Brain.

---

## 1. Context

Phase 5 code wiring is complete (Steps 0, 5A–5E). This document tracks the remaining validation, migration, and script-creation work needed to declare Phase 5 fully done.

### Already Completed

| Sub-phase | Description | Status |
|-----------|-------------|--------|
| Step 0 | WORBI version marker scripts modified | ✅ Scripts done |
| 5A | Z-Image & TRELLIS manifest verification | ✅ Complete |
| 5B | WORBI `update_worbi.sh` created | ✅ Complete |
| 5C | `NymphHostService` argument support | ✅ Complete |
| 5D | VM wiring batches A–E | ✅ Complete |
| 5E | Update flow wiring | ✅ Code done |
| Build | Clean compilation | ✅ 0 errors, 0 warnings |

### Remaining

| Sub-phase | Description | Priority |
|-----------|-------------|----------|
| **Step 0 Verify** | WORBI version marker contract verification in WSL | 🔴 P0 |
| **5H** | End-to-end WORBI UI lifecycle validation | 🔴 P0 |
| **5F** | Manifest V1 schema migration (all 5 manifests) | 🟡 P1 |
| **5G** | Remaining update scripts (Brain, Z-Image, TRELLIS, LoRA) | 🟡 P1 |

---

## 2. Build Command

```powershell
dotnet build "Code/NymphsCore/Manager/apps/NymphsCoreManager/NymphsCoreManager.csproj" --no-restore
```

Run after any code changes to verify the project compiles cleanly.

---

## 3. WSL Environment

| Distro | Purpose |
|--------|---------|
| `NymphsCore_Lite` | Development/source WSL checkout |
| `NymphsCore` | Real managed runtime WSL used by Manager (target for lifecycle tests) |

Runtime lifecycle tests target:
```bash
wsl.exe -d NymphsCore --user nymph -- ...
```

---

## 4. Step 0 Verify: WORBI Version Marker Contract

> **Prerequisite for Step 5H.** Must confirm the WORBI marker scripts work correctly in WSL before testing the full UI lifecycle.

### 4.1 Verify Install Writes Marker

```bash
# From Windows CMD/Powershell, run in NymphsCore WSL distro as user nymph
wsl.exe -d NymphsCore --user nymph bash -c 'bash /home/nymph/NymphsModules/worbi/scripts/install_worbi.sh'
wsl.exe -d NymphsCore --user nymph bash -c 'cat ~/worbi/.nymph-module-version'
```

**Expected**: Version number printed (e.g., `6.2.50`).

### 4.2 Verify Status Reports Installed

```bash
wsl.exe -d NymphsCore --user nymph bash -c 'bash /home/nymph/NymphsModules/worbi/scripts/worbi_status.sh'
```

**Expected output includes**:
```
installed=true
runtime_present=true
data_present=true
version=6.2.50
```

### 4.3 Verify Normal Uninstall Removes Marker

```bash
wsl.exe -d NymphsCore --user nymph bash -c 'bash /home/nymph/NymphsModules/worbi/scripts/worbi_uninstall.sh --yes'
wsl.exe -d NymphsCore --user nymph bash -c 'test -f ~/worbi/.nymph-module-version && echo "MARKER EXISTS" || echo "MARKER GONE"'
```

**Expected**: `MARKER GONE`

### 4.4 Verify Status After Uninstall

```bash
wsl.exe -d NymphsCore --user nymph bash -c 'bash /home/nymph/NymphsModules/worbi/scripts/worbi_status.sh'
```

**Expected output**:
```
installed=false
data_present=true
runtime_present=false
version=not-installed
running=false
```

### 4.5 Verify Reinstall

```bash
wsl.exe -d NymphsCore --user nymph bash -c 'bash /home/nymph/NymphsModules/worbi/scripts/install_worbi.sh'
wsl.exe -d NymphsCore --user nymph bash -c 'bash /home/nymph/NymphsModules/worbi/scripts/worbi_status.sh'
```

**Expected**: `installed=true` again.

### Checklist

- [ ] Step 0.1: Install WORBI → `.nymph-module-version` written with version
- [ ] Step 0.2: Status reports `installed=true`, `runtime_present=true`
- [ ] Step 0.3: Normal uninstall → marker removed
- [ ] Step 0.4: Status after uninstall → `installed=false`, `data_present=true`, `runtime_present=false`
- [ ] Step 0.5: Reinstall → `installed=true` again

---

## 5. Phase 5H: End-to-End WORBI UI Validation

> **Requires Step 0 Verify to pass first.** Tests the full WORBI lifecycle through the Manager UI.

### 5.1 Build & Launch

```powershell
dotnet build "Code/NymphsCore/Manager/apps/NymphsCoreManager/NymphsCoreManager.csproj" --no-restore
```

Launch the Manager application and navigate to the Home page.

### 5.2 WORBI Install via UI

1. Click WORBI card in "Available Nymphs" section
2. Click "Install" button
3. Verify install progress appears in logs/feedback area
4. Verify WORBI card moves to "Your Nymphs" (installed) section after completion
5. Verify state badge shows "Installed" or similar

### 5.3 WORBI Start via UI

1. On WORBI detail page, click "Start"
2. Verify start progress in logs
3. Verify state badge changes to "Running"
4. Verify WSL process is running: `wsl.exe -d NymphsCore --user nymph ps aux | grep worbi`

### 5.4 WORBI Status via UI

1. Click "Status" action
2. Verify status output appears
3. Verify state badge reflects running state

### 5.5 WORBI Open via UI

1. Click "Open" action
2. Verify browser opens to WORBI URL (http://localhost:8082)

### 5.6 WORBI Logs via UI

1. Click "Logs" action
2. Verify logs panel fills with WORBI log output

### 5.7 WORBI Stop via UI

1. Click "Stop" action
2. Verify stop progress in logs
3. Verify state badge changes to "Installed" (not running)
4. Verify WSL process stopped

### 5.8 WORBI Update via UI

1. Click "Update" button (if available)
2. Verify update progress in logs
3. Verify version marker updated after completion
4. Verify state badge still shows "Installed"

### 5.9 WORBI Uninstall (Normal) via UI

1. Click "Uninstall" button
2. Confirm in dialog
3. Verify uninstall progress in logs
4. Verify WORBI card moves to "Available Nymphs" section
5. Verify state shows not installed but data present
6. Verify preserved data folders still exist: `wsl.exe -d NymphsCore --user nymph ls ~/worbi/`

### 5.10 WORBI Reinstall via UI

1. Click "Install" again on WORBI card
2. Verify install completes
3. Verify WORBI card moves back to "Your Nymphs"

### 5.11 Navigation Stress Test

1. Open WORBI page
2. Start install
3. Click Home during install
4. Return to WORBI page after completion
5. Verify state is correct

### 5.12 Idempotency Test

Repeat the full lifecycle (Install → Start → Stop → Uninstall → Reinstall) a second time.

### Checklist

- [ ] Step 5H.1: WORBI Install via UI — card moves to installed
- [ ] Step 5H.2: WORBI Start via UI — running state, process exists
- [ ] Step 5H.3: WORBI Status via UI — status output correct
- [ ] Step 5H.4: WORBI Open via UI — browser opens
- [ ] Step 5H.5: WORBI Logs via UI — logs appear
- [ ] Step 5H.6: WORBI Stop via UI — stopped state, process gone
- [ ] Step 5H.7: WORBI Update via UI — version updated
- [ ] Step 5H.8: WORBI Uninstall (normal) via UI — data preserved, not installed
- [ ] Step 5H.9: WORBI Reinstall via UI — installed again
- [ ] Step 5H.10: Navigation stress test — state remains correct
- [ ] Step 5H.11: Idempotency — repeat full lifecycle, no failures

---

## 6. Phase 5F: Manifest V1 Schema Migration

> Migrate all 5 bundled manifests to the canonical V1 schema from the handoff document.

### 6.1 Target Schema

```json
{
  "manifest_version": 1,
  "id": "<module-id>",
  "name": "<Module Name>",
  "short_name": "<ABBR>",
  "version": "<semver>",
  "description": "<description>",
  "category": "<category>",
  "packaging": "<archive|repo|hybrid>",
  "source": {
    "type": "<archive|repo|path>",
    "url": "<git-url>",
    "ref": "<branch>"
  },
  "install": {
    "root": "$HOME/<install-dir>",
    "entrypoint": "scripts/install_<module>.sh",
    "version_marker": "$HOME/<install-dir>/.nymph-module-version",
    "installed_markers": [
      "$HOME/<install-dir>/.nymph-module-version"
    ]
  },
  "entrypoints": {
    "status": "scripts/<module>_status.sh",
    "start": "scripts/<module>_start.sh",
    "stop": "scripts/<module>_stop.sh",
    "open": "scripts/<module>_open.sh",
    "logs": "scripts/<module>_logs.sh",
    "install": "scripts/install_<module>.sh",
    "update": "scripts/update_<module>.sh",
    "uninstall": "scripts/<module>_uninstall.sh"
  },
  "uninstall": {
    "entrypoint": "scripts/<module>_uninstall.sh",
    "preserve_by_default": [],
    "supports_purge": false,
    "purge_allowed": false
  },
  "runtime": {
    "urls": [],
    "logs_dir": "$HOME/<install-dir>/logs"
  },
  "ui": {
    "page": "custom",
    "page_kind": "<module-id>",
    "sort_order": 0,
    "standard_lifecycle_rail": true
  }
}
```

### 6.2 Migration Order

Per safety rules:
1. WORBI (test first, already has working scripts)
2. Brain
3. Z-Image
4. LoRA
5. TRELLIS (last, heaviest)

### 6.3 C# Deserialization Verification

After migrating each manifest, verify `NymphRegistryService` can load it:
- Check `NymphDefinition.cs` deserialization handles V1 fields
- May need to add/adjust JSON property name mappings
- Test: `LoadBundledRegistry()` loads all 5 without errors

### Checklist

- [ ] Step 5F.1: Migrate WORBI manifest to V1 schema
- [ ] Step 5F.2: Migrate Brain manifest to V1 schema
- [ ] Step 5F.3: Migrate Z-Image manifest to V1 schema
- [ ] Step 5F.4: Migrate LoRA manifest to V1 schema
- [ ] Step 5F.5: Migrate TRELLIS manifest to V1 schema
- [ ] Step 5F.6: Verify `NymphRegistryService` deserialization works with V1
- [ ] Step 5F.7: Build succeeds after all migrations

---

## 7. Phase 5G: Remaining Update Scripts

> Create `update.sh` scripts for the 4 remaining modules. Use the WORBI `update_worbi.sh` as the reference template.

### 7.1 Update Script Contract

Every `update.sh` script must:
- Check `.nymph-module-version` for installed state (not directory existence)
- Exit non-zero if not installed
- Stop service if running
- Pull latest / re-apply install steps
- Preserve user data
- Restart if was running
- Print `installed_module_version=x.y.z` on success

### 7.2 Script Creation Order

1. Brain — `scripts/update_brain.sh`
2. Z-Image — `scripts/update_zimage.sh`
3. LoRA — `scripts/update_lora.sh`
4. TRELLIS — `scripts/update_trellis.sh` (last, heaviest)

### 7.3 Manifest Entries

After creating each script, add the `"update"` entry to the corresponding `*.nymph.json` manifest and the `"update"` capability.

### Checklist

- [ ] Step 5G.1: Create `update_brain.sh` in Brain repo
- [ ] Step 5G.2: Add `update` entry to `brain.nymph.json`
- [ ] Step 5G.3: Create `update_zimage.sh` in Z-Image repo
- [ ] Step 5G.4: Add `update` entry to `zimage.nymph.json`
- [ ] Step 5G.5: Create `update_lora.sh` in LoRA repo
- [ ] Step 5G.6: Add `update` entry to `lora.nymph.json`
- [ ] Step 5G.7: Create `update_trellis.sh` in TRELLIS repo
- [ ] Step 5G.8: Add `update` entry to `trellis.nymph.json`
- [ ] Step 5G.9: Syntax check all 4 scripts in WSL

---

## 8. Safety Rules

| Rule | Detail |
|------|--------|
| **WORBI-first testing** | All new lifecycle scripts and VM wiring tested on WORBI before any other module |
| **No TRELLIS destructive ops** | Never test uninstall/delete on TRELLIS until WORBI flow is proven |
| **No GitHub pushes** | Do not push to any module repo without explicit user instruction |
| **No `cat` logs** | Do not display large log outputs without asking the user first |
| **File edit safety** | Large files use `.new` copy → edit → verify → overwrite workflow |
| **Purge safety** | Purge blocked for non-WORBI modules until lifecycle engine is proven |
| **Install root validation** | Install root must not be `$HOME`, `/`, `/home/nymph`, or empty |
| **Build after changes** | Run build command after every code change |

---

## 9. Master Completion Checklist

### Step 0: WORBI Version Marker Verification
- [ ] Install writes marker
- [ ] Status reports installed
- [ ] Uninstall removes marker
- [ ] Status after uninstall correct
- [ ] Reinstall works

### Phase 5H: E2E WORBI UI Validation
- [ ] Install via UI
- [ ] Start via UI
- [ ] Status via UI
- [ ] Open via UI
- [ ] Logs via UI
- [ ] Stop via UI
- [ ] Update via UI
- [ ] Uninstall (normal) via UI
- [ ] Reinstall via UI
- [ ] Navigation stress test
- [ ] Idempotency test

### Phase 5F: Manifest V1 Migration
- [ ] WORBI manifest
- [ ] Brain manifest
- [ ] Z-Image manifest
- [ ] LoRA manifest
- [ ] TRELLIS manifest
- [ ] Deserialization verification
- [ ] Build verification

### Phase 5G: Remaining Update Scripts
- [ ] Brain update script
- [ ] Z-Image update script
- [ ] LoRA update script
- [ ] TRELLIS update script
- [ ] Manifest entries
- [ ] Syntax checks

---

## 10. File Inventory

### Manager Code Files
| File | Role |
|------|------|
| `Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs` | Main VM — actions wired to HostService |
| `Manager/apps/NymphsCoreManager/Services/NymphHostService.cs` | Host service — WSL script execution |
| `Manager/apps/NymphsCoreManager/Services/NymphRegistryService.cs` | Registry — loads manifests |
| `Manager/apps/NymphsCoreManager/Services/NymphStateDetectionService.cs` | State detection — parses status |
| `Manager/apps/NymphsCoreManager/Models/NymphDefinition.cs` | Manifest model |
| `Manager/apps/NymphsCoreManager/Models/NymphEntrypoints.cs` | Entrypoint paths |

### Manifest Files
| File | Module |
|------|--------|
| `Manager/registry/manifests/worbi.nymph.json` | WORBI |
| `Manager/registry/manifests/brain.nymph.json` | Brain |
| `Manager/registry/manifests/zimage.nymph.json` | Z-Image |
| `Manager/registry/manifests/trellis.nymph.json` | TRELLIS |
| `Manager/registry/manifests/lora.nymph.json` | LoRA |

### Module Repo Script Files
| Module | Install | Status | Start | Stop | Update | Uninstall |
|--------|---------|--------|-------|------|--------|-----------|
| WORBI | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Brain | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| Z-Image | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| TRELLIS | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| LoRA | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |

---

*End of Phase 5 Remaining Steps document.*