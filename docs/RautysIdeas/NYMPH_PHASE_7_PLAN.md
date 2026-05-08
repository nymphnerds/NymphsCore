# Phase 7: Online Registry — Plan & Assessment

> **Status**: Assessment Complete — Ready to Implement
> **Date**: 2026-05-08
> **Depends on**: Phase 2 (Registry Infrastructure), Phase 5E (Manager Shell), Phase 6 (Module Repos)

---

## 1. Executive Summary

Phase 7 enables the Manager to discover and install Nymph updates from the online registry. The backend infrastructure (Phase 2) and ViewModel wiring (Phase 5E) already implemented ~80% of the required functionality. The remaining work focuses on **UI polish** and **home page update badges**.

---

## 2. What Already Exists

### 2.1 `NymphRegistryService` (Backend — Complete)
| Method | Status | Notes |
|--------|--------|-------|
| `FetchRemoteRegistryAsync()` | ✅ Done | Fetches online `nymphs.json` from GitHub |
| `FetchRemoteManifestAsync(url)` | ✅ Done | Downloads single manifest from raw GitHub URL |
| `CheckForUpdatesAsync()` | ✅ Done | Batch version check for all local definitions |
| `CompareVersion(moduleId, local, remote)` | ✅ Done | Semver comparison logic |
| `ConstructRemoteManifestUrl(def)` | ✅ Done | Converts `git@github.com:...` → raw.githubusercontent URL |
| `DefaultRegistryUrl` | ✅ Done | `https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json` |

### 2.2 `ManagerShellViewModel` (ViewModel — Complete)
| Feature | Status | Notes |
|---------|--------|-------|
| `CheckForUpdatesCommand` / `CheckForUpdatesAsync()` | ✅ Done | Calls `WorkflowService.CheckNymphModuleRegistryUpdatesAsync()` |
| `UpdateModuleCommand` / `UpdateModuleAsync(module)` | ✅ Done | Reruns install flow from updated manifest |
| `UpdateSummary` property | ✅ Done | Text summary, but **NOT bound in XAML** |
| `ApplyModuleUpdateResults(results)` | ✅ Done | Iterates results, calls `module.ApplyUpdateState()` |
| `ClearModuleUpdateAfterSuccessfulInstall()` | ✅ Done | Clears `HasUpdate` after successful install/update |
| `_updateModuleCommand.RaiseCanExecuteChanged()` | ✅ Done | Re-enables update button after state changes |

### 2.3 `NymphModuleViewModel` (Model — Complete)
| Property | Status | Notes |
|----------|--------|-------|
| `HasUpdate` | ✅ Done | Boolean, drives `UpdateModuleCommand` CanExecute |
| `RemoteVersionLabel` | ✅ Done | Shows remote version on module page |
| `UpdateDetail` | ✅ Done | Human-readable update message |
| `ApplyUpdateState(localVer, remoteVer, hasUpdate, detail)` | ✅ Done | Sets all three properties above |

### 2.4 XAML — Module Page (Complete)
| Element | Status | Binding |
|---------|--------|---------|
| "Update Module" button | ✅ Done | `Command="{Binding UpdateModuleCommand}"`, `Visibility="{Binding DisplayedModule.HasUpdate, Converter={StaticResource BoolToVisibilityConverter}}"` |
| Remote version TextBlock | ✅ Done | `Text="{Binding DisplayedModule.RemoteVersionLabel, StringFormat=Remote: {0}}"` |
| "Check for Updates" toolbar button | ✅ Done | `Command="{Binding CheckForUpdatesCommand}"` |

---

## 3. What Is Missing

### 3.1 Home Page Update Badges (Gap #1 — High Priority)
**Problem:** The `InstalledHomeModuleCardTemplate` and `AvailableHomeModuleCardTemplate` DataTemplates in `MainWindow.xaml` do not display any update-available indicator on the home page module cards. The user must navigate into each module page to see if an update is available.

**Solution:** Add a small badge/icon to each installed module card on the home page that appears when `HasUpdate == true`.

### 3.2 UpdateSummary Not Bound (Gap #2 — Medium Priority)
**Problem:** The `UpdateSummary` property on `ManagerShellViewModel` is updated after every "Check for Updates" run, but it is not bound to any visible UI element in `MainWindow.xaml`.

**Solution:** Bind `UpdateSummary` to a TextBlock in the toolbar area or home page header.

### 3.3 Online Registry File (Gap #3 — Infrastructure)
**Problem:** The `nymphs-registry` repository at `github.com/nymphnerds/nymphs-registry` may not exist yet. The `nymphs.json` master catalog file needs to be created with entries for all 5 modules.

**Solution:** Create the registry file locally, then publish to GitHub.

---

## 4. Implementation Plan

### Step 1: Create Online Registry File
**Files:** `Code/NymphsCore/Manager/registry/nymphs.json`

```json
{
  "registry_version": 1,
  "modules": [
    {
      "id": "worbi",
      "name": "WORBI (Workflow Orchestrator)",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/worbi/main/nymph.json"
    },
    {
      "id": "brain",
      "name": "Nymphs Brain",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/brain/main/nymph.json"
    },
    {
      "id": "zimage",
      "name": "Z-Image",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/zimage/main/nymph.json"
    },
    {
      "id": "trellis",
      "name": "Trellis (Managed Runtime)",
      "channel": "stable",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/trellis/main/nymph.json"
    },
    {
      "id": "lora",
      "name": "LoRA / AI Toolkit",
      "channel": "experimental",
      "manifest_url": "https://raw.githubusercontent.com/nymphnerds/lora/main/nymph.json"
    }
  ]
}
```

### Step 2: Add Update Badge to Home Page Module Cards
**File:** `MainWindow.xaml` — `InstalledHomeModuleCardTemplate` DataTemplate

Add a visibility-bound badge element:
```xml
<TextBlock Text="UPDATE" 
           Style="{StaticResource UpdateBadgeStyle}"
           Visibility="{Binding HasUpdate, Converter={StaticResource BoolToVisibilityConverter}}" />
```

Create `UpdateBadgeStyle`:
```xml
<Style x:Key="UpdateBadgeStyle" TargetType="TextBlock">
  <Setter Property="Foreground" Value="#FFB800"/>
  <Setter Property="FontWeight" Value="Bold"/>
  <Setter Property="FontSize" Value="9"/>
  <Setter Property="Margin" Value="0,0,0,4"/>
</Style>
```

### Step 3: Bind UpdateSummary in UI
**File:** `MainWindow.xaml` — Toolbar area or home page header

```xml
<TextBlock Text="{Binding UpdateSummary}" 
           Foreground="#9AB8B4" 
           FontSize="12"
           Margin="0,4,0,0"
           Visibility="{Binding UpdateSummary, Converter={StaticResource NonEmptyToVisibilityConverter}}"/>
```

If `NonEmptyToVisibilityConverter` doesn't exist, create it or use a simple approach:
```xml
<TextBlock Text="{Binding UpdateSummary}" 
           Foreground="#9AB8B4" 
           FontSize="12"
           Margin="0,4,0,0"/>
```

### Step 4: Verify End-to-End Flow
1. Launch Manager
2. Click "Check for Updates" button
3. Verify `UpdateSummary` text appears
4. Verify home page module cards show UPDATE badges for modules with available updates
5. Click into a module with update → verify "Update Module" button is visible
6. Click "Update Module" → verify update flow executes

---

## 5. Dependencies

| Dependency | Status |
|------------|--------|
| Phase 2: Registry Infrastructure | ✅ Complete |
| Phase 5E: Manager Shell (ViewModel wiring) | ✅ Complete |
| Phase 6: Module Repos (nymph.json files) | ✅ Complete |
| `NymphModuleUpdateInfo` model | ✅ Exists (used by `ApplyModuleUpdateResults`) |
| `WorkflowService.CheckNymphModuleRegistryUpdatesAsync()` | ✅ Exists |

---

## 6. Files to Modify

| File | Change |
|------|--------|
| `Code/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml` | Add update badge to home module card templates, bind `UpdateSummary` |
| `Code/NymphsCore/Manager/registry/nymphs.json` | Create (or confirm exists) — master registry catalog |

## 7. Files Already Complete (No Changes Needed)

| File | Why |
|------|-----|
| `NymphRegistryService.cs` | Remote fetch, manifest download, version comparison |
| `ManagerShellViewModel.cs` | CheckForUpdatesCommand, UpdateModuleCommand, ApplyModuleUpdateResults |
| `NymphModuleViewModel.cs` | HasUpdate, RemoteVersionLabel, UpdateDetail, ApplyUpdateState |
| All 5 module `nymph.json` files | Phase 6 complete |

---

## 8. Success Criteria

- [ ] Manager can fetch the online registry and display update summary
- [ ] Home page module cards show UPDATE badges when updates are available
- [ ] Module detail page shows "Update Module" button when update is available
- [ ] Clicking "Update Module" successfully re-runs the install flow
- [ ] After successful update, badge is cleared and version reflects new version

---

## 9. Phase 7.5: Registry Publishing (Post-Implementation)

After Phase 7 is verified locally, publish the `nymphs-registry` repository:
1. Create `github.com/nymphnerds/nymphs-registry` repo
2. Push `nymphs.json` to `main` branch
3. Verify the raw URL is accessible: `https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json`
4. Update all module repos with correct `source.repo` URLs
5. End-to-end test: Manager → Check for Updates → discovers modules from online registry