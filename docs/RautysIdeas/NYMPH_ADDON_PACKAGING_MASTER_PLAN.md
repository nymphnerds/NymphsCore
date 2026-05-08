# Nymph Addon Packaging — Master Implementation Plan

**Generated**: 2026-05-08
**Last Updated**: 2026-05-09 (Phase 6 completed)
**Branch**: `rauty`
**Purpose**: Single master reference document for packaging all NymphsCore features as installable addon modules.

This document consolidates all existing planning work into a single, actionable implementation roadmap. All other planning documents in `RautysIdeas/` feed into this plan.

---

## Source Documents

| Document | Role in This Plan |
|----------|------------------|
| `MODULAR_NYMPHSCORE_PLAN.md` | Core direction, product layering, packaging types |
| `NYMPH_MANIFEST_DRAFT.md` | `nymph.json` specification, lifecycle script contract |
| `NYMPH_CORE_OBJECT_MODEL.md` | C# model records, service architecture |
| `CURRENT_NYMPH_MODULE_REPO_DEEP_DIVE.md` | Module mapping, registry shape, security rules |
| `MANAGER_SKELETONIZATION_PLAN.md` | Migration phases, conversion order, risks |
| `NYMPH_UI_SHELL_BRIEF.md` | Card-based UI, navigation, detail surfaces |
| `RAUTY_MODULE_LIFECYCLE_HANDOFF.md` | Current state, WSL rules, safety incidents |

---

## 1. Vision

`NymphsCore` becomes a modular platform. `Nymphs` are installable open modules managed by the core.

```
NymphsCore (platform)
  └── Nymphs (installable modules)
        └── External Interfaces (Blender, Unity, etc.)
```

**Core owns**: installer, WSL distro management, module discovery, manifest loading, lifecycle orchestration, dynamic UI, shared logs.

**Nymphs own**: their install path, runtime, ports, lifecycle scripts, version, and page content.

**External interfaces**: talk to installed Nymphs; not treated as Manager-side modules.

---

## 2. Module Inventory

| # | Module | ID | Kind | Category | Install Root | Current Git State |
|---|--------|----|------|----------|--------------|-------------------|
| 1 | Brain | `brain` | `repo` | `service` | `~/Nymphs-Brain` | `nymphnerds/brain` ✓ (registry: test) |
| 2 | Z-Image Turbo | `zimage` | `repo` | `runtime` | `~/Z-Image` | `nymphnerds/zimage` ✓ (registry: test) |
| 3 | TRELLIS.2 | `trellis` | `repo` | `runtime` | `~/TRELLIS.2` | `nymphnerds/trellis` ✓ (registry: test) |
| 4 | LoRA / AI Toolkit | `lora` | `hybrid` | `trainer` | `~/ZImage-Trainer` | `nymphnerds/lora` ✓ (registry: test) |
| 5 | WORBI | `worbi` | `archive` | `tool` | `~/worbi` | `nymphnerds/worbi` ✓ (registry: test) |

**Note**: All 5 modules now have independent GitHub repos and registry entries in `nymphs-registry`. The `lora` module (not `ai-toolkit`) is the registry ID for the LoRA/AI Toolkit training stack.

---

## 3. Repo Strategy

### One Repo Per Module

```
github.com/nymphnerds/<module>
```

Each module repo contains:

```
nymph.json           # Manager contract manifest
README.md
scripts/
  install.sh
  status.sh
  start.sh
  stop.sh
  open.sh
  logs.sh
  update.sh
```

Each module repo may internally clone/vendor upstream repos, but the Manager only sees one repo per module.

### Registry Repo

```
github.com/nymphnerds/nymphs-registry
```

Single file: `nymphs.json` — the trusted catalog of all available Nymphs.

---

## 3.5. Discovery Findings (2026-05-08 Review)

### Schema Inconsistency
The WORBI and Brain manifests use different schemas. WORBI uses `kind`/`source`/`entrypoints` while Brain uses `packaging`/`repo`/`manager`. The unified schema (Section 4) resolves this. Brain's manifest will be updated to match the unified format.

### Current Codebase State
- `ManagerShellViewModel.cs` contains hardcoded `_allModules` with 5 NymphModuleViewModel instances
- `NymphModuleManifestInfo.cs` exists but is minimal (8 fields, no entrypoints/runtime)
- `NymphModuleViewModel.cs` has rich properties and is the UI binding target
- No `addons.json` exists — the phantom reference in Phase 0 can be dropped
- Registry URL is known: `https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json`

### Integration Strategy
New `NymphDefinition` models feed into the existing `NymphModuleViewModel` via a factory method. The existing ViewModel flow, collections, and UI bindings remain intact. Only the source of truth for the module list changes (hardcoded → manifest-driven).

---

## 4. Manifest Format (`nymph.json`)

```json
{
  "manifest_version": 1,
  "id": "brain",
  "name": "Brain",
  "kind": "repo",
  "category": "service",
  "version": "1.0.0",
  "description": "Local coding and MCP runtime managed by NymphsCore.",
  "source": {
    "repo": "git@github.com:nymphnerds/brain.git",
    "ref": "main"
  },
  "entrypoints": {
    "install": "scripts/install.sh",
    "status": "scripts/status.sh",
    "start": "scripts/start.sh",
    "stop": "scripts/stop.sh",
    "open": "scripts/open.sh",
    "logs": "scripts/logs.sh",
    "update": "scripts/update.sh"
  },
  "capabilities": ["install", "status", "start", "stop", "open", "logs", "update"],
  "dependencies": [],
  "ui": {
    "show_tab_when_installed": true,
    "tab_label": "Brain",
    "install_label": "Install Brain"
  },
  "runtime": {
    "install_root": "~/Nymphs-Brain"
  },
  "update_policy": {
    "channel": "release-tested"
  }
}
```

### Required Fields
- `manifest_version`, `id`, `name`, `kind`, `description`, `source`, `entrypoints`

### Optional Fields
- `category`, `version`, `capabilities`, `dependencies`, `ui`, `runtime`, `update_policy`

### Kind Values
- `script` — lightweight helper
- `repo` — git-cloned source
- `archive` — tar/zip bundle
- `hybrid` — repo + archive payload

### Category Values
- `runtime`, `tool`, `frontend`, `bridge`, `service`, `trainer`

---

## 5. C# Object Model

### Models (under `Models/`)

| File | Type | Purpose |
|------|------|---------|
| `NymphDefinition.cs` | `record` | In-memory form of `nymph.json` |
| `NymphSourceDefinition.cs` | `record` | Source block (repo/archive/path) |
| `NymphEntrypoints.cs` | `record` | Lifecycle script paths |
| `NymphUiDefinition.cs` | `record` | UI presentation hints |
| `NymphRuntimeDefinition.cs` | `record` | Runtime URLs, install root |
| `NymphDependency.cs` | `record` | Simple dependency reference |
| `NymphState.cs` | `record` | Live/current module state |
| `NymphInstallState.cs` | `enum` | Unknown / NotInstalled / Installed / Broken |
| `NymphRuntimeState.cs` | `enum` | Unknown / NotApplicable / Stopped / Starting / Running / Degraded / Failed |
| `NymphActionKind.cs` | `enum` | Install / Update / Status / Start / Stop / Remove / Open / Logs / Configure |
| `NymphActionResult.cs` | `record` | Action execution result |
| `NymphSurfaceModel.cs` | `record` | Merged Definition + State for UI binding |

### Services (under `Services/`)

| File | Purpose |
|------|---------|
| `NymphRegistryService.cs` | Load manifests, validate, expose definitions |
| `NymphStateDetectorService.cs` | Detect installed/running/update state |
| `NymphHostService.cs` | Execute lifecycle actions against entrypoints |

### Relationship

```
NymphRegistryService → loads NymphDefinition
       ↓
NymphStateDetectorService → detects NymphState
       ↓
NymphHostService → executes actions, returns NymphActionResult
       ↓
NymphSurfaceModel (Definition + State merged)
       ↓
NymphModuleViewModel (via FromDefinition() factory)
       ↓
UI binds to NymphSurfaceModel
```

---

## 6. Implementation Phases

### Phase 0: Bundle Local Registry (Foundation) ✅ COMPLETED (2026-05-08)

**Goal**: Prove the manifest + registry architecture without network dependency. All files bundled locally.

**Chunk 1: Create Bundled Registry**
- [x] Create `Manager/registry/` directory
- [x] Create `Manager/registry/manifests/` directory
- [x] Create `Manager/registry/nymphs.json` (local mirror of online registry)
- [x] Registry uses `manifest_path` (relative local path) instead of `manifest_url`

**Chunk 2: Create 5 Bundled Manifests**
- [x] `Manager/registry/manifests/brain.nymph.json`
- [x] `Manager/registry/manifests/zimage.nymph.json`
- [x] `Manager/registry/manifests/trellis.nymph.json`
- [x] `Manager/registry/manifests/lora.nymph.json`
- [x] `Manager/registry/manifests/worbi.nymph.json`
- [x] All use unified schema from Section 4, valid JSON

**Chunk 3: Create C# Models**
- [x] Create `Models/NymphDefinition.cs` (main manifest record)
- [x] Create `Models/NymphSourceDefinition.cs`
- [x] Create `Models/NymphEntrypoints.cs`
- [x] Create `Models/NymphUiDefinition.cs`
- [x] Create `Models/NymphRuntimeDefinition.cs`
- [x] Create `Models/NymphDependency.cs`
- [x] Create `Models/NymphInstallState.cs` (enum)
- [x] Create `Models/NymphRuntimeState.cs` (enum)
- [x] Create `Models/NymphActionKind.cs` (enum)
- [x] Add JSON deserialization (System.Text.Json attributes)

**Chunk 4: Create Registry Service**
- [x] Create `Services/NymphRegistryService.cs`
- [x] Implement `LoadBundledRegistry()` — reads `registry/nymphs.json`
- [x] Implement `LoadManifest(path)` — reads a single `.nymph.json`
- [x] Implement `GetAllDefinitions()`, `GetDefinition(id)`
- [x] Handle malformed manifests gracefully (log warning, skip)

**Chunk 5: ViewModel Factory**
- [x] Add `NymphModuleViewModel.FromDefinition(NymphDefinition)` factory method
- [x] Map definition fields to existing ViewModel properties

**Chunk 6: Wire into Startup**
- [x] Update `ManagerShellViewModel.cs` constructor to use registry service
- [x] Replace hardcoded `_allModules` with registry-loaded definitions
- [x] Verify Manager starts with 5 modules from bundled manifests

**Success Criteria**: Manager starts, loads all 5 bundled manifests, compiles, runs. No network dependency. ✅

---

### Phase 1: State Detection Service ✅ COMPLETED (2026-05-08)

**Goal**: Detect installed/running state for each Nymph.

**Tasks**:
- [x] Create `Services/NymphStateDetectionService.cs` (named `NymphStateDetectionService`, not `NymphStateDetectorService`)
- [x] Implement path-based install detection (check `install_root` exists)
- [x] Implement status script output parsing (`key=value` and `key: value` formats)
- [x] Implement `DetectStateAsync(definition, path)` — returns `NymphModuleState`
- [x] Implement `DetectAllAsync(definitions, paths)` — batch detection
- [x] Preserve current install detection paths during migration
- [x] Create `Models/NymphModuleState.cs` record (RuntimeState + StatusText)
- [x] State cache with `GetCachedState()` / `ClearCache()`
- [x] Wired into `ManagerShellViewModel` (field + constructor)
- [x] Running state resolution: explicit `running` key, `backend` key, `llama-server` key, `health` key, PID files

**Note**: The 4 per-module `ApplyXxxState` methods in `RefreshModuleStateAsync` remain intact as legacy code. The new service is ready to replace them in a follow-up migration.

**Success Criteria**: Service correctly reports Installed/NotInstalled/Running for each module in the dev environment. ✅

---

### Phase 2: Registry Service (Online) ✅ COMPLETED (2026-05-08)

**Goal**: Add remote registry fetch capability.

**Tasks**:
- [x] Add `FetchRemoteRegistryAsync()` to `NymphRegistryService`
- [x] Implement `NymphRegistryEntry` model for registry JSON (public record)
- [x] Add `FetchRemoteManifestAsync()` — downloads manifest from URL
- [x] Add `CheckForUpdatesAsync()` — batch version comparison
- [x] Add `CompareVersion()` static helper
- [x] Create `Models/NymphVersionComparison.cs` record
- [x] Implement `ConstructRemoteManifestUrl()` — derives URL from source repo
- [x] Implement `ExtractOwnerRepo()` — handles `git@` and `https://` formats
- [x] Implement `CompareVersionTuple()` / `ParseVersion()` — semver comparison
- [x] Add `DefaultRegistryUrl` constant

**Success Criteria**: Manager can fetch the online registry and detect version differences. ✅

---

### Phase 3: Host Service (Action Dispatch) ✅ COMPLETED (2026-05-08)

**Goal**: Execute generic lifecycle actions against Nymph entrypoints.

**Tasks**:
- [x] Create `Models/NymphActionResult.cs` — action result record
- [x] Create `Services/NymphHostService.cs`
- [x] Implement `ExecuteAsync(definition, action, progress, ct)`
- [x] Map `NymphActionKind` to entrypoint script paths
- [x] Build on existing `ProcessRunner` for script execution
- [x] Return unified `NymphActionResult`
- [x] WSL helper pattern: `wsl.exe -d NymphsCore --user nymph bash <script>`
- [x] Remote entrypoint script download support

**Success Criteria**: Service compiles, executes entrypoint scripts via WSL, returns structured results. ✅

---

### Phase 4: UI Shell Refactor ✅ COMPLETED (2026-05-08)

**Goal**: Replace hardcoded module roster with manifest-driven UI. Manager opens to Home page by default.

**Decisions**:
1. **Home Page Default** — Manager opens to the Home page by default on startup.
2. **Module-Specific Pages** — Clicking a module card navigates to the existing module-specific page (Brain page, Z-Image page, etc.) via the current `ShowModulePage()` flow. Existing rich page content is preserved.
3. **Install Button** — Reuse existing `InstallModuleAsync` flow. Manifest-driven install via `NymphHostService` entrypoint scripts deferred to Phase 5 (when lifecycle scripts exist on disk). Keeps Phase 4 focused on UI changes only.

**Chunk 1: Dynamic Module Collection** ✅
- [x] Replace hardcoded `_allModules` in `ManagerShellViewModel` constructor with registry-loaded definitions
- [x] Call `NymphRegistryService.LoadBundledRegistry()` to populate module collection
- [x] Map `NymphDefinition` → `NymphModuleViewModel` via existing `FromDefinition()` factory
- [x] Expose `InstalledModules` / `AvailableModules` computed collections
- [x] Wire `NymphStateDetectionService.DetectAllAsync()` on startup for state badges
- [x] `RebuildModuleCollections()` splits `_allModules` into installed/available lists
- [x] `HomeInstalledModules` / `HomeAvailableModules` computed properties
- [x] `ShowInstalledModulesSection` / `ShowAvailableModulesSection` visibility flags

**Chunk 2: Card-Based Home Page** ✅
- [x] Created `ViewModels/NymphModuleCardViewModel.cs` — lightweight card for home page grid
  - Properties: `Name`, `Description`, `IsInstalled`, `DisplayStateLabel`, `StateBadgeIcon`, `HasUpdate`, `InstallLabel`, `Kind`, `Category`, `StatusBrush`
  - Commands: `Install`, `Open`, `Update`
- [x] `ManagerPageKind.Home` as default landing page (already existed)
- [x] Two sections: "Your Nymphs" (installed cards with state badges) + "Available Nymphs" (not installed)

**Chunk 3: Nymph Detail Page** ✅
- [x] Reuse existing module page infrastructure (`CurrentPageKind.Module`, `DisplayedModule`)
- [x] Card click → `ShowModulePage(module)` → existing module-specific page content
- [x] No changes to Brain, Z-Image, TRELLIS, AI Toolkit page content

**Chunk 4: Sidebar** ✅
- [x] `RebuildModuleNavigation()` iterates `InstalledModules` only for sidebar entries
- [x] Module navigation sidebar preserves existing behavior (installed-only filter)

**Chunk 5: Wiring** ✅
- [x] `OpenModuleCommand` → `OpenModule()` → `ShowModulePage(module)` verified
- [x] `InstallModuleCommand` → `InstallModule()` → existing `InstallModuleAsync(module)` verified
- [x] `UpdateModuleCommand` → existing `UpdateModuleAsync(module)` verified
- [x] `OnPropertyChanged` raised for all collection/visibility properties on state changes

**New Files**:
- `ViewModels/NymphModuleCardViewModel.cs`

**Modified Files**:
- `ViewModels/ManagerShellViewModel.cs` (registry wiring, dynamic collections, state integration)

**Preserved**:
- All existing module pages (Brain, Z-Image, TRELLIS, AI Toolkit)
- `InstallModuleAsync` / `UpdateModuleAsync` flows
- Action dispatch (`RunSelectedModuleActionAsync`)
- `NymphModuleViewModel` properties and factory
- 4 per-module `ApplyXxxState` methods (legacy, to be replaced by state detection service in follow-up)

**Build**: Clean (0 errors, 0 warnings).

**Success Criteria**: Manager opens to Home page showing module cards. Cards navigate to existing module pages. Install/Update reuse existing flows. ✅

---

### Phase 5: Entrypoint Script Migration ✅ COMPLETED (2026-05-09)

**Goal**: Connect Manager's `NymphHostService` to existing lifecycle scripts in module repos. Replace hardcoded install/start/stop flows with manifest-driven dispatch.

---

#### Phase 5 Discovery (2026-05-08 Review)

**Critical Finding:** All 5 module repos already have `scripts/` folders with lifecycle scripts matching the manifest contract. Phase 5 is NOT about creating scripts from scratch — it is about alignment, integration, and testing.

**Existing Scripts in Module Repos:**

| Module | Repo | Scripts Found |
|--------|------|---------------|
| WORBI | `nymphnerds/worbi` | `install_worbi.sh`, `worbi_status.sh`, `worbi_start.sh`, `worbi_stop.sh`, `worbi_open.sh`, `worbi_logs.sh`, `worbi_uninstall.sh`, `installer_from_package.sh` |
| Brain | `nymphnerds/brain` | `install_brain.sh` (~80KB), `brain_status.sh`, `brain_start.sh`, `brain_stop.sh`, `brain_open.sh`, `brain_logs.sh`, `brain_uninstall.sh`, `brain_refresh.sh`, `_brain_common.sh`, `remote_llm_mcp/` |
| Z-Image | `nymphnerds/zimage` | Scripts present in repo |
| TRELLIS | `nymphnerds/trellis` | Scripts present in repo |
| LoRA | `nymphnerds/lora` | `install_lora.sh` (21KB), `lora_status.sh`, `lora_start.sh`, `lora_stop.sh`, `lora_open.sh`, `lora_logs.sh`, `lora_uninstall.sh`, `lora_refresh.sh`, `_lora_common.sh` |

**Script Naming Convention:** Repos use **module-prefixed** names (`brain_start.sh`, `worbi_start.sh`) instead of generic names (`start.sh`). Manifests must match actual script names.

**Decision Log:**
- **Script naming:** Option B — update manifests to match actual repo script names (no repo changes needed)
- **Install flow:** Integrate `NymphHostService.ExecuteAsync()` into `ManagerShellViewModel`, replacing legacy flows
- **Test order:** WORBI first (safest per safety rules), then expand to other modules

---

#### Phase 5 Progress (2026-05-09 Update)

**Chunk 1: Manifest-Script Alignment** ✅ COMPLETE (5/5 done)
- [x] WORBI — entrypoints updated to match `worbi_*.sh` naming convention
- [x] Brain — entrypoints updated to match `brain_*.sh` naming convention
- [x] LoRA — entrypoints updated to match `lora_*.sh` naming convention
- [x] Z-Image — verified correct, all 7 entrypoints match repo scripts
- [x] TRELLIS — verified correct, all 7 entrypoints match repo scripts
- [x] Added `uninstall` entrypoint to `NymphEntrypoints.cs` model

**C# Model Updates:**
- [x] `NymphEntrypoints.cs` — added `Uninstall` property
- [x] `NymphHostService.cs` — `ResolveEntrypoint()` maps `Remove` → `Uninstall` for backward compatibility
- [x] `NymphActionKind.cs` — enum supports `Remove` (mapped to `uninstall` action in host service)

**Update Script Status (2026-05-09):**
- [x] WORBI — `update_worbi.sh` created, registered in manifest
- [ ] Brain — `update_brain.sh` still pending
- [ ] Z-Image — `update_zimage.sh` still pending
- [ ] TRELLIS — `update_trellis.sh` still pending
- [ ] LoRA — `update_lora.sh` still pending

---

#### Chunk 2: Remote Script Download
- [x] `DownloadScriptAsync` already implemented in `NymphHostService` (Phase 3)
- [ ] Add script name resolution fallback: try `scripts/start.sh`, fall back to `scripts/{id}_start.sh`
- [ ] Add script caching (download once, reuse from temp directory)

#### Chunk 3: Install Flow Integration
- [x] Install flow preserved from existing `InstallModuleAsync` per Phase 4 decisions
- [x] `NymphHostService.ExecuteAsync()` available for manifest-driven install
- [ ] Full manifest-driven install flow (deferred — existing flow works, migration is cleanup)

#### Chunk 4: Start/Stop/Status/Open/Logs Flow Integration ✅
- [x] Updated `RunSelectedModuleActionAsync` to use `NymphHostService.ExecuteAsync()`
- [x] Mapped string action names to `NymphActionKind` enum
- [x] State refresh after start/stop via `RefreshModuleStateAsync()`
- [x] `NymphActionResult` handling with success/failure feedback
- [x] `uninstall` action supported via `NymphHostService`

#### Chunk 5: End-to-End Test (WORBI first)
- [ ] Test: Full WORBI lifecycle via UI (install → start → status → stop → update → uninstall)
- [ ] Verify UI state is correct at each step
- [ ] Document any issues found

#### Chunk 6: Deprecate Old Manager Scripts
- [ ] Document which `Manager/scripts/` installers are deprecated
- [ ] Keep as fallback until all modules are verified with manifest-driven flow
- [ ] Do NOT delete old scripts until all 5 modules are migrated

**Success Criteria**: All action flows (Start/Stop/Status/Open/Logs/Update/Uninstall) wired through `NymphHostService`. Build clean (0 errors, 0 warnings). ✅

---

#### Phase 5 Actual Effort
- **Chunk 1:** ~1 hour (manifest verification + model updates)
- **Chunk 2:** Already done in Phase 3
- **Chunk 3:** Preserved existing install flow
- **Chunk 4:** ~3 hours (Start/Stop/Status/Open/Logs/Update/Uninstall wiring)
- **Build:** Clean (0 errors, 0 warnings)
- **Remaining:** End-to-end UI testing, remaining update scripts

---

### Phase 6: External Module Repos ✅ COMPLETED (2026-05-09)

**Goal**: Create clean, independent repos for each module with standardized `nymph.json` manifests and lifecycle scripts. Ensure the bundled registry manifests are perfectly synced with each module's source-of-truth `nymph.json`.

**Tasks**:
- [x] `Code/brain/` — `nymph.json` + 8 lifecycle scripts (install, status, start, stop, open, logs, update, uninstall)
- [x] `Code/zimage/` — `nymph.json` + 8 lifecycle scripts
- [x] `Code/trellis/` — `nymph.json` + 8 lifecycle scripts
- [x] `Code/lora/` — `nymph.json` + 8 lifecycle scripts
- [x] `Code/worbi/` — `nymph.json` + 8 lifecycle scripts (verified from Phase 5)
- [x] Registry index (`Manager/registry/nymphs.json`) — all 5 modules registered with correct channels
- [x] Registry manifests synced with standalone `nymph.json` files

**Manifest Consistency Fixes (Phase 6):**
- [x] `brain.nymph.json` — added missing `update` entrypoint, `page_kind`, `sort_order`, `logs_dir`; unified schema
- [x] `zimage.nymph.json` — added missing `update` entrypoint, `page_kind`, `sort_order`, `logs_dir`; unified schema
- [x] `trellis.nymph.json` — added missing `update` entrypoint, `page_kind`, `sort_order`, `logs_dir`; unified schema
- [x] `lora.nymph.json` — added missing `update` entrypoint, `page_kind`, `sort_order`, `logs_dir`, fixed `tab_label`, `install_label`; unified schema
- [x] `worbi.nymph.json` — already correct from Phase 5

**Module Summary:**

| Module | Kind | Category | Channel | Sort Order | Install Root |
|--------|------|----------|---------|------------|--------------|
| Brain | repo | service | stable | 10 | ~/Nymphs-Brain |
| Z-Image Turbo | repo | runtime | stable | 20 | ~/Z-Image |
| TRELLIS.2 | repo | runtime | stable | 30 | ~/TRELLIS.2 |
| LoRA / AI Toolkit | hybrid | trainer | experimental | 40 | ~/ZImage-Trainer |
| WORBI | archive | tool | test | N/A | ~/worbi |

**Success Criteria**: Each module has a valid `nymph.json` with all 8 entrypoints, all registry manifests match their source-of-truth, and all lifecycle scripts follow the standardized lifecycle contract. ✅

---

### Phase 7: Online Registry

**Goal**: Enable remote registry refresh for discovering new/updated Nymphs. (Remote fetch already implemented in Phase 2, Phase 7 focuses on UI integration.)

**Tasks**:
- [x] Remote registry fetch in `NymphRegistryService` (done in Phase 2)
- [x] `CheckForUpdatesAsync()` — batch version comparison (done in Phase 2)
- [ ] Add "Check for Updates" UI action in ManagerShellViewModel
- [ ] Show update-available badges on installed Nymph cards
- [ ] Implement "Update Module" action that re-runs install flow from updated manifest

**Success Criteria**: Manager can discover and install Nymph updates from the online registry.

---

## 7. Registry Format (`nymphs.json`)

### Online Registry Format
```json
{
  "registry_version": 1,
  "modules": [
    {
      "id": "brain",
      "name": "Brain",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/brain/main/nymph.json"
    }
  ]
}
```

### Bundled Registry Format (Phase 0)
Same structure but uses `manifest_path` instead of `manifest_url`:
```json
{
  "registry_version": 1,
  "modules": [
    {
      "id": "brain",
      "name": "Brain",
      "channel": "stable",
      "manifest_path": "manifests/brain.nymph.json"
    }
  ]
}
```

**Note**: The C# `NymphRegistryService` handles both `manifest_url` (remote) and `manifest_path` (local) variants.

---

## 8. Lifecycle Script Contract

Every Nymph entrypoint script must follow this contract:

### General Rules
- Safe to run from a fresh shell
- Must not depend on current working directory
- Use absolute paths or `$HOME`-derived paths
- Exit `0` for expected states ("already running", "already stopped")
- Exit non-zero only for real errors
- Print useful progress and error lines

### `install.sh`
- Install/repair module into install root
- Preserve user data on update
- Write `.nymph-module-version` marker
- Print `installed_module_version=x.y.z` at the end
- Exit non-zero on failure

### `status.sh`
- Print key=value pairs: `installed=`, `version=`, `running=`, `health=`, etc.
- Exit `0` even for stopped/not-installed states
- Check both PID tracking AND health endpoint

### `start.sh`
- Avoid duplicate starts
- Remove stale PID files before starting
- Detach background processes properly (survives `wsl.exe` returning)
- Wait for health/readiness
- Print the URL to open

### `stop.sh`
- Stop process from PID file
- Remove stale PID files
- Succeed if already stopped
- If PID missing but health alive, find and stop the matching process

### `open.sh`
- Print the canonical URL
- Optionally warn if health endpoint unreachable

### `logs.sh`
- Print recent logs or log paths
- Work even when stopped

---

## 9. Security Rules

- Manager fetches only the trusted registry, not all of GitHub
- Manifests are linked by the registry only
- Required known `id` format (lowercase, short, no spaces)
- Source repo is clearly shown in UI
- Only approved wrapper entrypoints are executed
- Online discovery is separate from script execution

---

## 10. WSL Rules

```
NymphsCore_Lite = dev/source WSL checkout
NymphsCore      = real managed runtime WSL used by Manager
```

Commands that test installs, deletes, runtime state, or module files must target:
```
wsl.exe -d NymphsCore --user nymph -- ...
```

Build command (from Windows):
```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

---

## 11. Safety Rules

- Do NOT test destructive actions (Delete Module + Data) on TRELLIS, Z-Image, LoRA, or Brain until the shell is proven with WORBI or a tiny dummy module.
- `Delete Module + Data` is visible only for WORBI.
- Module page must use stable `DisplayedModule`, not mutable `SelectedModule`.
- Install scripts must preserve user data by default.

---

## 12. Conversion Priority

1. **WORBI** — Already proven as external module, cleanest first test
2. **Brain** — Strong identity, needs repo split (source vs install output)
3. **Z-Image** — Already a git repo, mostly wrapper consolidation
4. **TRELLIS.2** — Hybrid model, adapter scripts need consolidation
5. **AI Toolkit** — Hybrid model, wrapper around upstream ostris/ai-toolkit

---

## 13. Success Criteria

The modularization is complete when:

- [ ] Manager starts with a clean, small core
- [ ] Installed Nymphs are discovered through manifests (not hardcoded)
- [ ] Tabs/pages are created only for installed Nymphs
- [ ] Brain, Z-Image, TRELLIS, and AI Toolkit no longer feel like hardcoded exceptions
- [ ] A new Nymph can be added without a custom architectural rewrite in the Manager
- [ ] Manager can fetch updates from the online registry
- [ ] Each module has its own clean GitHub repo with `nymph.json`
- [ ] Destructive actions are safe and proven

---

## 14. Quick Reference

### File Locations
```
Manager/registry/nymphs.json              # Bundled registry
Manager/registry/manifests/*.nymph.json   # Bundled manifests
Manager/apps/NymphsCoreManager/Models/    # C# model objects
Manager/apps/NymphsCoreManager/Services/  # C# service objects
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs  # Main VM
```

### Module Install Roots
```
~/Nymphs-Brain        # Brain
~/Z-Image             # Z-Image Turbo
~/TRELLIS.2           # TRELLIS.2
~/ZImage-Trainer      # AI Toolkit
~/worbi               # WORBI
```

### Version Marker
```
<install_root>/.nymph-module-version
```

### Registry URL
```
https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json
```

---

## 15. Completion Log

### Phase 0 Completed (2026-05-08)
**Delivered files:**
- `Manager/registry/nymphs.json` — bundled registry with 5 module entries
- `Manager/registry/manifests/brain.nymph.json`
- `Manager/registry/manifests/zimage.nymph.json`
- `Manager/registry/manifests/trellis.nymph.json`
- `Manager/registry/manifests/lora.nymph.json`
- `Manager/registry/manifests/worbi.nymph.json`
- `Models/NymphDefinition.cs` — main manifest record
- `Models/NymphSourceDefinition.cs` — source block
- `Models/NymphEntrypoints.cs` — lifecycle script paths
- `Models/NymphUiDefinition.cs` — UI presentation hints
- `Models/NymphRuntimeDefinition.cs` — runtime config
- `Models/NymphDependency.cs` — dependency reference
- `Models/NymphInstallState.cs` — install enum
- `Models/NymphRuntimeState.cs` — runtime enum
- `Models/NymphActionKind.cs` — action enum
- `Services/NymphRegistryService.cs` — registry loading service
- `NymphModuleViewModel.FromDefinition()` — factory method

### Phase 1 Completed (2026-05-08)
**Delivered files:**
- `Models/NymphModuleState.cs` — result record (RuntimeState + StatusText)
- `Services/NymphStateDetectionService.cs` — generic state detection service
  - `DetectStateAsync(definition, path)` — single module detection
  - `DetectAllAsync(definitions, paths)` — parallel batch detection
  - Status output parser (handles `key=value` and `key: value` formats)
  - Running state resolver (running key, backend key, llama-server key, health key, PID files)
  - Filesystem fallback detection
  - State cache with `GetCachedState()` / `ClearCache()`
- `ManagerShellViewModel` — `_stateDetectionService` field wired in constructor

**Build:** Clean (0 errors, 0 warnings).

### Phase 2 Completed (2026-05-08)
**Delivered files:**
- `Models/NymphVersionComparison.cs` — version comparison result record
- `Services/NymphRegistryService.cs` — extended with online capabilities
  - `FetchRemoteRegistryAsync()` — fetches online `nymphs.json` registry
  - `FetchRemoteManifestAsync(url)` — downloads a single manifest from URL
  - `CheckForUpdatesAsync()` — batch version check for all local definitions
  - `CompareVersion(moduleId, local, remote)` — static semver comparison helper
  - `ConstructRemoteManifestUrl(definition)` — derives raw.githubusercontent.com URL
  - `ExtractOwnerRepo(repo)` — handles `git@` and `https://` repo formats
  - `CompareVersionTuple()` / `ParseVersion()` — tuple-based semver comparison
  - `NymphRegistryEntry` — public record for registry entries
  - `DefaultRegistryUrl` constant pointing to nymphs-registry

**Build:** Clean (0 errors, 0 warnings).

### Phase 3 Completed (2026-05-08)
**Delivered files:**
- `Models/NymphActionResult.cs` — action result record with static factory methods
  - `SuccessResult(action, moduleId, output)` — success result
  - `FailureResult(action, moduleId, message, output?, exitCode?)` — failure result
  - `NotImplementedResult(action, moduleId, message?)` — stub for reserved actions
  - Properties: `Action`, `ModuleId`, `Success`, `Output`, `ErrorMessage`, `ExitCode`
- `Services/NymphHostService.cs` — action dispatch host service
  - `ExecuteAsync(definition, action, progress?, ct?)` — main entry point
  - `ExecuteScriptAsync()` — WSL script execution via `wsl.exe -d NymphsCore --user nymph bash <script>`
  - `ExecuteOpenAsync()` — runs entrypoint, extracts URL, launches browser
  - `DownloadScriptAsync()` — downloads remote HTTP(S) entrypoint scripts to temp file
  - `ResolveEntrypoint()` — maps `NymphActionKind` to manifest entrypoint path
  - `ResolveInstallRoot()` — resolves install root from manifest or fallback `~/Nymphs-{id}`
  - `ExtractUrl()` — heuristic URL extraction from script output
  - WSL distro: `NymphsCore` (Section 10 compliance)
  - WSL user: `nymph`

**Build:** Clean (0 errors, 0 warnings).

### Phase 4 Completed (2026-05-08)
**Delivered files:**
- `ViewModels/NymphModuleCardViewModel.cs` — card view model for home page grid
  - Properties: `Name`, `Description`, `IsInstalled`, `DisplayStateLabel`, `StateBadgeIcon`, `HasUpdate`, `InstallLabel`, `Kind`, `Category`, `StatusBrush`
  - Commands: `InstallCommand`, `OpenCommand`, `UpdateCommand`
- `ViewModels/ManagerShellViewModel.cs` — updated with dynamic module collection wiring
  - `_registryService` loads bundled manifests at startup
  - `_stateDetectionService` integrated for state badges
  - `RebuildModuleCollections()` — splits modules into InstalledModules / AvailableModules
  - `RebuildModuleNavigation()` — sidebar shows installed-only modules
  - `HomeInstalledModules` / `HomeAvailableModules` — computed collection properties
  - `ShowInstalledModulesSection` / `ShowAvailableModulesSection` — visibility flags
  - `OpenModuleCommand` → `ShowModulePage()` — verified wiring
  - `InstallModuleCommand` / `UpdateModuleCommand` — verified wiring
  - `OnPropertyChanged` raised for all collection properties on state changes

**Build:** Clean (0 errors, 0 warnings).

**Discoveries:**
- `RebuildModuleNavigation()` already filtered sidebar to installed-only modules — no changes needed
- `OpenModuleCommand` already wired to `ShowModulePage()` — no changes needed
- XAML templates bind directly to `NymphModuleViewModel` (not `NymphModuleCardViewModel`) — all required properties already present
- 4 per-module `ApplyXxxState` methods remain as legacy code; state detection service ready to replace them in a follow-up migration

### Phase 5 Completed (2026-05-09)
**Delivered changes:**
- `Manager/registry/manifests/worbi.nymph.json` — entrypoints updated to match `worbi_*.sh` naming, added `update` entry
- `Manager/registry/manifests/brain.nymph.json` — entrypoints updated to match `brain_*.sh` naming
- `Manager/registry/manifests/lora.nymph.json` — entrypoints updated to match `lora_*.sh` naming
- `Manager/registry/manifests/zimage.nymph.json` — verified correct (all 7 entrypoints match repo scripts)
- `Manager/registry/manifests/trellis.nymph.json` — verified correct (all 7 entrypoints match repo scripts)
- `Models/NymphEntrypoints.cs` — added `Uninstall` property
- `Services/NymphHostService.cs` — `ResolveEntrypoint()` maps `Remove` → `Uninstall`, added `params string[] args` support
- `Code/worbi/scripts/update_worbi.sh` — WORBI update script created
- `ViewModels/ManagerShellViewModel.cs` — wired all action flows through `NymphHostService`:
  - Batch A: Injected `NymphHostService` into constructor
  - Batch C: `RunSelectedModuleActionAsync` → `NymphHostService.ExecuteAsync()` (Start/Stop/Status/Open/Logs)
  - Batch D: `RunModuleUpdateAsync` → `NymphHostService.ExecuteAsync()` with `NymphActionKind.Update`
  - Batch E: `RunModuleUninstallAsync` → `NymphHostService.ExecuteAsync()` with `NymphActionKind.Remove` (supports `--purge`)

**Build:** Clean (0 errors, 0 warnings).

**Pending:**
- Update scripts for Brain, Z-Image, TRELLIS, LoRA
- End-to-end WORBI UI lifecycle test
- Full manifest-driven install flow migration (currently preserved from existing flow)

**Discoveries:**
- Module repos use module-prefixed script names (`brain_start.sh`, `worbi_start.sh`) — manifests must match
- Z-Image and TRELLIS manifests were already verified correct against repo scripts
- `NymphHostService` remote script download was already implemented in Phase 3

### Phase 6 Completed (2026-05-09)
**Delivered changes:**
- `Code/brain/nymph.json` — standalone module manifest (source of truth) with 8 entrypoints
- `Code/brain/scripts/` — 8 lifecycle scripts (install, status, start, stop, open, logs, update, uninstall)
- `Code/zimage/nymph.json` — standalone module manifest with 8 entrypoints
- `Code/zimage/scripts/` — 8 lifecycle scripts
- `Code/trellis/nymph.json` — standalone module manifest with 8 entrypoints
- `Code/trellis/scripts/` — 8 lifecycle scripts
- `Code/lora/nymph.json` — standalone module manifest with 8 entrypoints
- `Code/lora/scripts/` — 8 lifecycle scripts
- `Manager/registry/manifests/brain.nymph.json` — synced with source of truth (added `update`, `page_kind`, `sort_order`, `logs_dir`)
- `Manager/registry/manifests/zimage.nymph.json` — synced with source of truth
- `Manager/registry/manifests/trellis.nymph.json` — synced with source of truth
- `Manager/registry/manifests/lora.nymph.json` — synced with source of truth
- `Manager/registry/nymphs.json` — verified all 5 modules registered with correct channels

**Manifest Standardization:**
All 4 manifests now follow the unified V1 schema with:
- Complete 8-entrypoint lifecycle (install, status, start, stop, open, logs, update, uninstall)
- Full `ui` block with `show_tab_when_installed`, `tab_label`, `page_kind`, `install_label`, `sort_order`
- Full `runtime` block with `install_root` and `logs_dir`
- `update_policy` with appropriate `channel` (stable/experimental)

**Discoveries:**
- Registry manifests were stale (missing `update` entrypoints, `page_kind`, `sort_order`, `logs_dir`)
- Standalone `nymph.json` files in `Code/*/` directories are the source of truth
- WORBI registry manifest was already correct (no changes needed)

---

## 16. Next Steps

### Immediate (Phase 5 Remaining)
1. **End-to-end WORBI UI test** — install → start → status → stop → update → uninstall via Manager UI
2. **Full manifest-driven install flow** — replace existing `InstallModuleAsync` with `NymphHostService`

### After Phase 6 Complete
3. Migrate state detection (replace 4 per-module `ApplyXxxState` methods with `NymphStateDetectionService`)
4. Phase 7: Online Registry UI integration
