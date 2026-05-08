# NymphsCore Phase 5D: VM Wiring Next Steps

> Created: 2026-05-09 | Status: COMPLETE (2026-05-09)
> Companion to: `NYMPH_ADDON_PACKAGING_MASTER_PLAN.md`

---

## 1. Completion Summary (Steps 0 through 5C)

### Step 0: WORBI Version Marker Contract
- **`installer_from_package.sh`**: Writes `~/worbi/.nymph-module-version` on successful install.
- **`worbi_status.sh`**: Checks marker + runtime files. Reports `installed=true/false`, `data_present`, `runtime_present` separately.
- **`worbi_uninstall.sh`**: Removes `.nymph-module-version` on normal uninstall (not purge).
- **Verified**: Syntax checks pass, status output correct in WSL `NymphsCore` distro.

### Phase 5B: WORBI update.sh
- **Created**: `Code/worbi/scripts/update_worbi.sh` — archive-aware update script.
  - Checks `.nymph-module-version` for installed state.
  - Preserves `data/`, `projects/`, `config/`, `logs/`.
  - Stops if running → re-installs → restores data → restarts if was running.
- **Updated manifests**:
  - `Code/worbi/nymph.json` (repo manifest) — added `"update"` entry + `"update"` capability.
  - `Code/NymphsCore/Manager/registry/manifests/worbi.nymph.json` (registry manifest) — added `"update"` entry + `"update"` capability.
- **Verified**: Syntax check passes in WSL.

### Phase 5C: Host Service Argument Support
- **Modified**: `NymphHostService.ExecuteAsync()` — added `params string[] args`.
- **Threaded** `args` through to `ExecuteScriptAsync()` → appended to WSL command arguments.
- **Enables**: `--purge`, `--dry-run`, `--yes` flags for uninstall and other scripts.
- **Build**: 0 warnings, 0 errors.

---

## 2. Phase 5D: VM Wiring — Overview

**Goal**: Wire the action commands in `ManagerShellViewModel` to use `NymphHostService` + `NymphRegistryService` instead of the legacy `InstallerWorkflowService`.

**Why it matters**: The current VM routes all Install/Start/Stop/Status/Update/Open/Logs/Uninstall actions through `InstallerWorkflowService.RunNymphModuleActionAsync()`, which uses hardcoded module ID → command mappings. The new architecture uses:
1. **`NymphRegistryService`** — resolves manifest entrypoints per module.
2. **`NymphHostService`** — executes lifecycle scripts via WSL with proper argument forwarding.
3. **`NymphStateDetectionService`** — detects real-time module state after every action.

**Current VM structure** (key fields from line ~14-24):
```csharp
private readonly InstallerWorkflowService _workflowService;
private readonly ISystemCheckRunner _systemCheckRunner;
private readonly NymphRegistryService _registryService;       // exists but NOT used for actions
private readonly NymphStateDetectionService _stateDetection;  // exists, used for status parsing
private readonly ApplicationSettings _settings;
```

**Missing field**:
```csharp
private readonly NymphHostService _hostService;  // NEEDS TO BE ADDED
```

---

## 3. Target Architecture Diagram

```
User clicks button (XAML)
    → RelayCommand (e.g., _installModuleCommand)
        → ManagerShellViewModel method (e.g., InstallModule())
            → Resolve NymphDefinition from _registryService
                → NymphHostService.ExecuteAsync(definition, action, progress, ct, args...)
                    → ResolveEntrypoint() → gets script path from manifest
                        → wsl.exe -d NymphsCore --user nymph bash /path/to/script [args]
                            → Script runs in WSL
                                → Result captured (stdout, stderr, exit code)
                                    → NymphActionResult returned
                                        → ApplyImmediateModuleInstallResult() / RefreshModuleStateAsync()
                                            → Status is truth after every action
                                                → UI updates (collections, commands, feedback)
```

---

## 4. Execution Batches (A through E)

Each batch is a small, atomic change that compiles independently.

---

### Batch A: Inject NymphHostService into VM

**Files to modify**:
- `Code/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs`

**Changes**:
1. Add field: `private readonly NymphHostService _hostService;`
2. Update constructor to accept `NymphHostService hostService` parameter.
3. Assign: `_hostService = hostService;`
4. Register in DI: Check `App.xaml.cs` or `Program.cs` for service registration. If `NymphHostService` is not registered, add:
   ```csharp
   services.AddSingleton<NymphHostService>();
   ```

**Verification**:
- Build succeeds (0 errors).
- No behavior change yet (existing code still uses `_workflowService`).

---

### Batch B: Wire Install Flow

**Files to modify**:
- `ManagerShellViewModel.cs` — `InstallModule()` method and related helpers.

**Current flow** (what exists now):
```csharp
private async Task RunModuleInstallAsync(NymphModuleViewModel module)
{
    // ...
    var installOutput = await _workflowService.RunNymphModuleActionAsync(
        _settings, module.Id, "install", progress, ct);
    // ...
}
```

**Target flow**:
```csharp
private async Task RunModuleInstallAsync(NymphModuleViewModel module)
{
    // ...
    var definition = _registryService.GetDefinition(module.Id);
    if (definition is null) { /* error */ return; }

    var result = await _hostService.ExecuteAsync(
        definition,
        NymphActionKind.Install,
        progress,
        ct);

    if (result.Success)
    {
        ApplyImmediateModuleInstallResult(module, isInstalled: true, result.Output);
        await RefreshModuleStateAsync();
    }
    else
    {
        SetModuleActionFeedback($"{module.Name}: install failed", result.Output);
    }
    // ...
}
```

**Key considerations**:
- `RunModuleInstallAsync` currently handles both fresh installs and refreshes. Preserve both paths.
- After install, always call `RefreshModuleStateAsync()` to re-detect state.
- Update progress reporter format to match `NymphHostService` output (prefixes with `[host]`).

**Verification**:
- Build succeeds.
- Manual test: Install WORBI from UI → check `~/worbi/.nymph-module-version` exists.

---

### Batch C: Wire Start / Stop / Status / Open / Logs Flows

**Files to modify**:
- `ManagerShellViewModel.cs` — `RunSelectedModuleAction()` method (~line 1700+).

**Current flow**:
```csharp
private async Task RunSelectedModuleAction(NymphModuleViewModel module, string actionName)
{
    // ...
    var output = await _workflowService.RunNymphModuleActionAsync(
        _settings, module.Id, actionName.ToLower(), progress, ct);
    // ...
}
```

**Target flow**:
```csharp
private async Task RunSelectedModuleAction(NymphModuleViewModel module, string actionName)
{
    var definition = _registryService.GetDefinition(module.Id);
    var action = MapStringToActionKind(actionName); // "start" -> NymphActionKind.Start
    var result = await _hostService.ExecuteAsync(definition, action, progress, ct);

    switch (action)
    {
        case NymphActionKind.Status:
            var state = _stateDetection.ParseStatusOutput(result.Output);
            module.ApplyState(/* from parsed state */);
            break;
        case NymphActionKind.Open:
            if (!result.Success) /* handle no URL */
            break;
        // Other actions just report feedback
        default:
            SetModuleActionFeedback($"{module.Name}: {actionName} finished", result.Output);
            break;
    }

    await RefreshModuleStateAsync();
}
```

**String-to-ActionKind mapping** (new private helper):
```csharp
private static NymphActionKind MapStringToActionKind(string actionName) =>
    actionName.ToLowerInvariant() switch
    {
        "start" => NymphActionKind.Start,
        "stop" => NymphActionKind.Stop,
        "status" => NymphActionKind.Status,
        "open" => NymphActionKind.Open,
        "logs" => NymphActionKind.Logs,
        "configure" => NymphActionKind.Configure,
        _ => throw new ArgumentException($"Unknown action: {actionName}")
    };
```

**Verification**:
- Build succeeds.
- Manual test: Start WORBI → Status WORBI → Stop WORBI → verify state updates in UI.

---

### Batch D: Wire Update Flow

**Files to modify**:
- `ManagerShellViewModel.cs` — `UpdateModule()` method.

**Current flow**:
```csharp
private async Task RunModuleUpdateAsync(NymphModuleViewModel module)
{
    // ...
    var updateOutput = await _workflowService.RunNymphModuleActionAsync(
        _settings, module.Id, "update", progress, ct);
    // ...
}
```

**Target flow**:
```csharp
private async Task RunModuleUpdateAsync(NymphModuleViewModel module)
{
    // ...
    var definition = _registryService.GetDefinition(module.Id);
    var result = await _hostService.ExecuteAsync(definition, NymphActionKind.Update, progress, ct);

    if (result.Success)
    {
        var installedVersion = ExtractInstalledModuleVersion(result.Output.Split('\n', '\r'));
        ApplyImmediateModuleInstallResult(module, isInstalled: true, result.Output, installedVersion);
        ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
        await RefreshModuleStateAsync();
    }
    // ...
}
```

**Key considerations**:
- Update scripts may report new version in output (e.g., `installed_module_version=1.2.3`).
- After update, clear the "update available" badge.
- WORBI is the first module with `update.sh` — test on WORBI first.

**Verification**:
- Build succeeds.
- Manual test: Trigger WORBI update from UI → verify version marker updates.

---

### Batch E: Wire Uninstall Flow (with Purge Support)

**Files to modify**:
- `ManagerShellViewModel.cs` — `RunModuleUninstallAsync()` method (~line 1758).

**Current flow**:
```csharp
private async Task RunModuleUninstallAsync(NymphModuleViewModel module, bool purge)
{
    // ...
    var uninstallOutput = await _workflowService.RunNymphModuleUninstallAsync(
        _settings, module.Id, purge, progress, ct);
    // ...
}
```

**Target flow**:
```csharp
private async Task RunModuleUninstallAsync(NymphModuleViewModel module, bool purge)
{
    // ... PURGE SAFETY GUARDS (unchanged) ...
    // Purge only allowed for WORBI:
    if (purge && !string.Equals(module.Id, "worbi", StringComparison.OrdinalIgnoreCase))
    {
        // ... existing block ...
        return;
    }

    var definition = _registryService.GetDefinition(module.Id);
    var args = purge ? new[] { "--purge" } : Array.Empty<string>();
    var result = await _hostService.ExecuteAsync(definition, NymphActionKind.Remove, progress, ct, args);

    if (result.Success)
    {
        ApplyImmediateModuleInstallResult(module, isInstalled: false, result.Output);
        await RefreshModuleStateAsync();
    }
    // ...
}
```

**Key considerations**:
- `NymphActionKind.Remove` maps to `uninstall.sh` entrypoint.
- The `--purge` flag is passed as a script argument (Phase 5C enables this).
- Purge safety guards remain unchanged (WORBI-only).
- After uninstall, navigate away from module page if it was displayed.

**Verification**:
- Build succeeds.
- Manual test: Uninstall WORBI (non-purge) → verify data preserved. Install again. Uninstall with purge → verify data deleted.

---

## 5. "Status is Truth" Lifecycle Rule

After **every** action (Install, Start, Stop, Update, Uninstall), the VM must:
1. Call `RefreshModuleStateAsync()` to re-detect state from the distro.
2. Update `NymphModuleViewModel` state properties.
3. Rebuild module collections (`RebuildModuleCollections()`).
4. Raise `CanExecuteChanged` on all affected commands.

**Existing code already does this** — the wiring batches must preserve this pattern.

---

## 6. Purge Safety Guards

| Rule | Implementation |
|------|----------------|
| Purge only on WORBI | `if (purge && id != "worbi") { block; return; }` |
| Confirmation dialog | `MessageBox.Show()` with Yes/No before any uninstall |
| Non-purge preserves data | `uninstall.sh` without `--purge` moves data to backup |
| Purge deletes everything | `uninstall.sh --purge` removes install + data |

---

## 7. Execution Checklist

- [x] **Batch A**: Inject `NymphHostService` into VM constructor
  - [x] Add field
  - [x] Update constructor signature
  - [x] Register in DI container
  - [x] Build verify
- [x] **Batch B**: Wire Install flow
  - [x] Install flow preserved from existing `InstallModuleAsync` (per Phase 4 decisions)
  - [x] `NymphHostService.ExecuteAsync()` available for future manifest-driven install migration
- [x] **Batch C**: Wire Start/Stop/Status/Open/Logs
  - [x] Replace action dispatch in `RunSelectedModuleActionAsync`
  - [x] Add action kind mapping
  - [x] State refresh after actions via `RefreshModuleStateAsync()`
  - [x] Build verify
- [x] **Batch D**: Wire Update flow
  - [x] Replace update dispatch in `RunModuleUpdateAsync`
  - [x] Handle version extraction from output
  - [x] Build verify
- [x] **Batch E**: Wire Uninstall flow
  - [x] Replace uninstall dispatch in `RunModuleUninstallAsync` with `NymphActionKind.Remove`
  - [x] Pass `--purge` arg when `purge=true`
  - [x] Preserve purge safety guards (WORBI-only)
  - [x] Build verify
- [ ] **End-to-end test**: Full WORBI lifecycle
  - [ ] Install → Start → Status → Stop → Update → Uninstall → Install again
  - [ ] Verify UI state is correct at each step
  - [ ] Verify no regression in non-WORBI modules

**Build:** Clean (0 errors, 0 warnings) ✅

---

## 8. Risk & Edge Cases

| Risk | Mitigation |
|------|-----------|
| `NymphHostService` not registered in DI | Check `App.xaml.cs` before Batch A; add registration if missing |
| Module not in registry (null definition) | Guard with null check + error feedback |
| Script path not in manifest | `ResolveEntrypoint` returns null → `NymphActionResult.FailureResult` |
| WSL command fails (distro not running) | `ProcessRunner` throws → caught → failure result |
| Progress reporter format mismatch | Ensure `[host]` prefix is handled or stripped in VM |
| Cancel mid-action | `CancellationToken` threaded through → `OperationCanceledException` → failure result with exit code -2 |
| State detection after action returns stale data | Add small delay or retry in `RefreshModuleStateAsync` if needed |
| Non-WORBI modules don't have `update.sh` yet | Guard Update button: only enable if `definition.Entrypoints.Update != null` |

---

## 9. Dependencies Between Batches

```
Batch A (DI Injection)
    ↓ (required by all)
Batch B (Install)
    ↓
Batch C (Start/Stop/Status/Open/Logs)
    ↓
Batch D (Update)
    ↓
Batch E (Uninstall)
    ↓
End-to-end WORBI test
```

**No batch can be skipped.** Each builds on the previous.

---

## 10. Files Involved Summary

| File | Role |
|------|------|
| `ManagerShellViewModel.cs` | Main VM — all wiring happens here |
| `NymphHostService.cs` | Host service — already has `params string[] args` support |
| `NymphRegistryService.cs` | Registry — provides `NymphDefinition` per module ID |
| `NymphStateDetectionService.cs` | State detection — parses status output |
| `NymphActionKind.cs` | Enum — maps string actions to types |
| `NymphActionResult.cs` | Result model — success/failure/output |
| `NymphModuleViewModel.cs` | Module VM — state properties updated after actions |
| `App.xaml.cs` | DI registration — may need `NymphHostService` registration |

---

## 11. Completion Notes

All batches A-E completed on 2026-05-09. Build verified clean (0 errors, 0 warnings).

**What was done:**
- Injected `NymphHostService` into `ManagerShellViewModel` constructor
- Wired `RunSelectedModuleActionAsync` → `NymphHostService.ExecuteAsync()` for Start/Stop/Status/Open/Logs
- Wired `RunModuleUpdateAsync` → `NymphHostService.ExecuteAsync()` with `NymphActionKind.Update`
- Wired `RunModuleUninstallAsync` → `NymphHostService.ExecuteAsync()` with `NymphActionKind.Remove` (supports `--purge`)
- Install flow preserved from existing `InstallModuleAsync` (per Phase 4 decisions)

**Remaining:**
- End-to-end WORBI UI lifecycle test (Phase 5H)
- Update scripts for Brain, Z-Image, TRELLIS, LoRA (Phase 5G)
- Full manifest-driven install flow migration (deferred cleanup)

---

*Document ends.*