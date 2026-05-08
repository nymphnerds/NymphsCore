# Nymph Phase 6: External Module Repos — Local Verification Plan

**Generated**: 2026-05-09
**Branch**: `rauty`
**Mode**: Local-only (no GitHub pushes)
**Prerequisites**: Phases 0-5 complete

---

## Objective

Verify and standardize all 5 module repos locally so each:
1. Contains a valid `nymph.json` at the repo root
2. Ships all required lifecycle scripts
3. Manifest matches the unified V1 schema
4. Manifest `entrypoints` match actual script filenames

---

## Module Inventory

| # | Module | ID | Kind | Category | Local Repo | Install Root |
|---|--------|----|------|----------|------------|--------------|
| 1 | Brain | `brain` | `repo` | `service` | TBD — locate | `~/Nymphs-Brain` |
| 2 | Z-Image Turbo | `zimage` | `repo` | `runtime` | TBD — locate | `~/Z-Image` |
| 3 | TRELLIS.2 | `trellis` | `repo` | `runtime` | TBD — locate | `~/TRELLIS.2` |
| 4 | LoRA / AI Toolkit | `lora` | `hybrid` | `trainer` | TBD — locate | `~/ZImage-Trainer` |
| 5 | WORBI | `worbi` | `archive` | `tool` | `Code/worbi/` ✓ | `~/worbi` |

**Registry**: `Code/NymphsCore/Manager/registry/nymphs.json`
**Bundled Manifests**: `Code/NymphsCore/Manager/registry/manifests/*.nymph.json`

---

## Known Local State (Discovered 2026-05-09)

### WORBI (`Code/worbi/`) — Already Exists
```
nymph.json                          ✓ Present, V1 schema
README.md                           ✓
packages/worbi-6.2.49.tar.gz        ✓ Archive present
scripts/install_worbi.sh            ✓
scripts/installer_from_package.sh   ✓
scripts/update_worbi.sh             ✓
scripts/worbi_status.sh             ✓
scripts/worbi_start.sh              ✓
scripts/worbi_stop.sh               ✓
scripts/worbi_open.sh               ✓
scripts/worbi_logs.sh               ✓
scripts/worbi_uninstall.sh          ✓
```

### Other Modules (Brain, Z-Image, TRELLIS, LoRA)
- **NOT found as Windows repos** — need to be created
- WSL contains runtime installs (not git repos):
  - `/home/nymph/Nymphs-Brain/` — runtime, has `scripts/monitor_query.sh` only
  - `/home/nymph/Z-Image/` — runtime, has Python scripts only
  - `/home/nymph/TRELLIS.2/` — runtime, has Python scripts only
  - `ZImage-Trainer` — not found in WSL either
- Module repos must be scaffolded at `Code/brain/`, `Code/zimage/`, `Code/trellis/`, `Code/lora/`

---

## Unified V1 Schema Reference

The canonical manifest format all modules must conform to:

```json
{
  "manifest_version": 1,
  "id": "<module-id>",
  "name": "<Module Name>",
  "kind": "<repo|archive|hybrid|script>",
  "category": "<service|runtime|tool|trainer|frontend|bridge>",
  "version": "<semver>",
  "description": "<short description>",
  "source": {
    "repo": "git@github.com:nymphnerds/<module>.git",
    "ref": "main"
  },
  "entrypoints": {
    "install": "scripts/install_<module>.sh",
    "status": "scripts/<module>_status.sh",
    "start": "scripts/<module>_start.sh",
    "stop": "scripts/<module>_stop.sh",
    "open": "scripts/<module>_open.sh",
    "logs": "scripts/<module>_logs.sh",
    "update": "scripts/update_<module>.sh",
    "uninstall": "scripts/<module>_uninstall.sh"
  },
  "capabilities": ["install", "status", "start", "stop", "open", "logs", "update", "uninstall"],
  "dependencies": [],
  "ui": {
    "show_tab_when_installed": true,
    "tab_label": "<Module Name>",
    "page_kind": "<module-id>",
    "install_label": "Install <Module Name>",
    "sort_order": <number>
  },
  "runtime": {
    "install_root": "~/<install-dir>",
    "logs_dir": "~/<install-dir>/logs"
  },
  "update_policy": {
    "channel": "<stable|test|pinned>"
  }
}
```

### Required Fields
- `manifest_version`, `id`, `name`, `kind`, `description`, `source`, `entrypoints`

### Optional Fields
- `category`, `version`, `capabilities`, `dependencies`, `ui`, `runtime`, `update_policy`, `uninstall`

---

## Execution Steps

### Step 1: Verify WORBI Repo (Safest First)

**Status**: ✅ Repo exists at `Code/worbi/`

**Tasks**:
- [ ] Compare `Code/worbi/nymph.json` (repo manifest) with `Manager/registry/manifests/worbi.nymph.json` (bundled manifest)
- [ ] Identify differences — repo version has richer fields (uninstall block, runtime URLs), bundled version is simpler
- [ ] Decide which is canonical (repo `nymph.json` should be the source of truth)
- [ ] Sync bundled manifest to match repo manifest if needed
- [ ] Verify all 8 entrypoint scripts exist and are executable
- [ ] Verify `nymph.json` is valid JSON, passes V1 schema
- [ ] Issues found during verification:
  - [ ] (none yet)

**Issues Found**:
- Repo manifest (`Code/worbi/nymph.json`) uses `"source": {"archive": "...", "format": "tar.gz"}` (archive-based)
- Bundled manifest (`Manager/registry/manifests/worbi.nymph.json`) uses `"source": {"repo": "git@...", "ref": "main"}` (repo-based)
- These are two different source models for the same module — need to reconcile

---

### Step 2: Locate & Verify Brain Repo

**Status**: 🔍 Repo location TBD

**Tasks**:
- [ ] Locate Brain repo locally (check WSL, check `Code/` subdirectories)
- [ ] If not found locally, note that it needs to be cloned
- [ ] Verify `nymph.json` exists at repo root
- [ ] Sync manifest from/to bundled version
- [ ] Verify all lifecycle scripts present:
  - `install_brain.sh`, `brain_status.sh`, `brain_start.sh`, `brain_stop.sh`
  - `brain_open.sh`, `brain_logs.sh`, `brain_uninstall.sh`, `update_brain.sh`
- [ ] Verify manifest entrypoints match actual script names
- [ ] Validate V1 schema compliance

---

### Step 3: Locate & Verify Z-Image Repo

**Status**: 🔍 Repo location TBD

**Tasks**:
- [ ] Locate Z-Image repo locally
- [ ] Verify `nymph.json` exists
- [ ] Sync manifest
- [ ] Verify lifecycle scripts
- [ ] Validate V1 schema

---

### Step 4: Locate & Verify TRELLIS Repo

**Status**: 🔍 Repo location TBD

**Tasks**:
- [ ] Locate TRELLIS repo locally
- [ ] Verify `nymph.json` exists
- [ ] Sync manifest
- [ ] Verify lifecycle scripts
- [ ] Validate V1 schema

---

### Step 5: Locate & Verify LoRA Repo

**Status**: 🔍 Repo location TBD

**Tasks**:
- [ ] Locate LoRA repo locally
- [ ] Verify `nymph.json` exists
- [ ] Sync manifest
- [ ] Verify lifecycle scripts
- [ ] Validate V1 schema

---

### Step 6: Registry Verification

**Status**: Pending

**Tasks**:
- [ ] Review `Manager/registry/nymphs.json` — ensure all 5 modules listed
- [ ] Verify each entry has correct `id`, `name`, and `manifest_path`
- [ ] Ensure no stale/missing entries

---

## Manifest Sync Strategy

### Two Manifest Locations
Each module has manifests in two places:

| Location | File | Purpose |
|----------|------|---------|
| **Repo root** | `<repo>/nymph.json` | Source of truth, ships with module |
| **Bundled registry** | `Manager/registry/manifests/<id>.nymph.json` | Local fallback for Manager |

### Sync Direction
**Repo `nymph.json` → Bundled manifest** (repo is canonical)

The bundled manifest in the registry should be a copy of the repo manifest, possibly with minor adjustments for local-only paths.

### Key Differences to Reconcile
1. **Source block**: Repo uses actual source (archive/repo URL). Bundled may use local paths.
2. **Version**: Repo has actual version. Bundled may have placeholder version.
3. **Rich fields**: Repo manifest may have extra fields (uninstall, runtime URLs) not in bundled.

---

## Discovery Questions

1. Where do Brain, Z-Image, TRELLIS, and LoRA repos live locally?
2. Should the bundled registry manifests be exact copies of repo manifests, or can they differ for local testing?
3. Are the module repos expected to be at `Code/brain/`, `Code/zimage/`, etc., or somewhere else?

---

## Success Criteria

Phase 6 is complete when:
- [ ] All 5 module repos are located and accessible
- [ ] Each repo contains valid `nymph.json` at root
- [ ] Each `nymph.json` conforms to V1 schema
- [ ] Each manifest `entrypoints` matches actual script files
- [ ] Bundled registry manifests are in sync with repo manifests
- [ ] `Manager/registry/nymphs.json` correctly references all 5 modules

---

## Completion Log

(To be updated as each step completes)

### Step 1: WORBI (2026-05-09) ✅ COMPLETE
- [x] Repo exists at `Code/worbi/`
- [x] All 8 lifecycle scripts present
- [x] `nymph.json` valid V1 schema
- [x] Bundled manifest synced from repo manifest (repo is canonical)
- [x] Fixed `NymphSourceDefinition.cs` — `Archive` changed from `object?` to `string?`, added `Format`
- [x] Added `NymphUninstallDefinition.cs` — C# model for uninstall block
- Issues:
  - Repo manifest uses archive-based source, original bundled used repo-based source → resolved by syncing bundled to match repo

### Step 2-5: Other Modules (Brain, Z-Image, TRELLIS, LoRA)
- [x] Discovered: WSL dirs are runtime installs, NOT git repos
- [ ] Scaffold `Code/brain/` with `nymph.json` + lifecycle scripts
- [ ] Scaffold `Code/zimage/` with `nymph.json` + lifecycle scripts
- [ ] Scaffold `Code/trellis/` with `nymph.json` + lifecycle scripts
- [ ] Scaffold `Code/lora/` with `nymph.json` + lifecycle scripts

### Step 6: Registry ✅ VERIFIED
- [x] All 5 modules present in `nymphs.json`
- [x] IDs, names, channels, manifest_paths correct
- [x] WORBI channel corrected from `test` → sync needed? (left as-is for now)

---

## Notes

- **Safety**: WORBI first, then lighter modules, TRELLIS last (heaviest recovery cost)
- **Local only**: No GitHub pushes during this phase
- **Manifest sync**: Repo `nymph.json` is canonical; bundled manifests are derivatives