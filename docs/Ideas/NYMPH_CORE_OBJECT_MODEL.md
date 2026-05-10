# Nymph Core Object Model

**Generated**: 2026-05-05  
**Branch Context**: `modular`  
**Purpose**: Concrete Manager-side C# object model for turning `NymphsCore` into a modular host for installable `Nymphs`.

---

## 1. Goal

The Manager needs a small core object model that can:

- load Nymph definitions
- detect installed state
- expose runtime state separately from static manifest data
- orchestrate generic lifecycle actions
- drive dynamic tabs/pages in the UI

This object model should live under the existing structure:

- `Models/`
- `Services/`

and should fit the current naming style already used in `NymphsCoreManager`.

---

## 2. Recommended Core Types

### Models

- `NymphDefinition`
- `NymphSourceDefinition`
- `NymphEntrypoints`
- `NymphUiDefinition`
- `NymphRuntimeDefinition`
- `NymphState`
- `NymphActionKind`
- `NymphInstallState`
- `NymphRuntimeState`
- `NymphDependency`
- `NymphActionResult`

### Services

- `NymphRegistryService`
- `NymphStateDetectorService`
- `NymphHostService`

Optional later:

- `NymphDependencyResolver`
- `NymphManifestLoader`

---

## 3. Static Definition vs Live State

Keep this split strict:

- `NymphDefinition` = static manifest data
- `NymphState` = live/detected/current state

This is important because the Manager should not treat:

- installed
- not installed
- outdated
- running
- stopped

as package-definition data.

---

## 4. Models

## 4.1 `NymphDefinition`

This is the in-memory form of `nymph.json`.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphDefinition(
    int ManifestVersion,
    string Id,
    string Name,
    string Kind,
    string Description,
    string? Category,
    string? Version,
    NymphSourceDefinition Source,
    NymphEntrypoints Entrypoints,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<NymphDependency> Dependencies,
    NymphUiDefinition? Ui,
    NymphRuntimeDefinition? Runtime,
    string? UpdateChannel);
```

Purpose:

- drives listing
- drives UI labels
- tells the Manager what entrypoints exist
- describes the source/package kind

---

## 4.2 `NymphSourceDefinition`

Represents where the Nymph comes from.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphSourceDefinition(
    string Kind,
    string? Repo,
    string? Ref,
    string? Pin,
    string? Archive,
    string? Format,
    string? Path);
```

Notes:

- `Kind` should align with the manifest kind:
  - `script`
  - `repo`
  - `archive`
  - `hybrid`

---

## 4.3 `NymphEntrypoints`

This is the real Manager contract.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphEntrypoints(
    string? Install,
    string? Update,
    string? Status,
    string? Start,
    string? Stop,
    string? Remove,
    string? Open,
    string? Logs,
    string? Configure);
```

Notes:

- do not reduce this to just `install_script`
- the Manager needs a full lifecycle surface

---

## 4.4 `NymphUiDefinition`

Small presentation hints only.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphUiDefinition(
    bool ShowTabWhenInstalled,
    string? TabLabel,
    string? InstallLabel,
    string? IconKey);
```

Notes:

- keep this lightweight
- this is not where layout logic should live

---

## 4.5 `NymphRuntimeDefinition`

Useful for logs, URLs, and install roots.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphRuntimeDefinition(
    string? InstallRoot,
    string? LogsDirectory,
    string? FrontendUrl,
    string? BackendUrl);
```

---

## 4.6 `NymphDependency`

Simple v1 dependency model.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphDependency(
    string Id,
    bool IsOptional = false);
```

For v1, keep it minimal.

Later you can add:

- version constraints
- dependency reason text
- dependency kinds

---

## 4.7 `NymphState`

This is the live/current state of a Nymph.

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphState(
    string NymphId,
    NymphInstallState InstallState,
    NymphRuntimeState RuntimeState,
    string? InstalledVersion,
    string? AvailableVersion,
    bool UpdateAvailable,
    string? StatusSummary,
    DateTimeOffset CheckedAt);
```

This should come from:

- live detection
- cached state
- status scripts

not from the static manifest itself.

---

## 4.8 `NymphInstallState`

```csharp
namespace NymphsCoreManager.Models;

public enum NymphInstallState
{
    Unknown = 0,
    NotInstalled = 1,
    Installed = 2,
    Broken = 3
}
```

---

## 4.9 `NymphRuntimeState`

```csharp
namespace NymphsCoreManager.Models;

public enum NymphRuntimeState
{
    Unknown = 0,
    NotApplicable = 1,
    Stopped = 2,
    Starting = 3,
    Running = 4,
    Degraded = 5,
    Failed = 6
}
```

This lets the Manager distinguish:

- installed but not running
- running healthy
- partially alive / degraded

---

## 4.10 `NymphActionKind`

```csharp
namespace NymphsCoreManager.Models;

public enum NymphActionKind
{
    Install = 0,
    Update = 1,
    Status = 2,
    Start = 3,
    Stop = 4,
    Remove = 5,
    Open = 6,
    Logs = 7,
    Configure = 8
}
```

Useful for generic dispatch, UI binding, and logging.

---

## 4.11 `NymphActionResult`

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphActionResult(
    string NymphId,
    NymphActionKind Action,
    bool Success,
    int ExitCode,
    string Summary,
    string Output);
```

This is the generic action result the Manager can log and display.

It pairs naturally with the existing:

- `CommandResult`

---

## 5. Services

## 5.1 `NymphRegistryService`

This service owns discovery and in-memory Nymph definitions.

```csharp
namespace NymphsCoreManager.Services;

public sealed class NymphRegistryService
{
    public IReadOnlyList<NymphDefinition> GetAllDefinitions();
    public NymphDefinition? GetDefinition(string id);
    public IReadOnlyList<NymphDefinition> GetInstalledCandidates();
    public Task ReloadAsync(CancellationToken cancellationToken);
}
```

Responsibilities:

- load manifest files
- validate basic manifest shape
- expose the current definition set

It should not own process execution.

---

## 5.2 `NymphStateDetectorService`

This service owns live state detection.

```csharp
namespace NymphsCoreManager.Services;

public sealed class NymphStateDetectorService
{
    public Task<NymphState> DetectStateAsync(
        NymphDefinition definition,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<NymphState>> DetectAllStatesAsync(
        IEnumerable<NymphDefinition> definitions,
        CancellationToken cancellationToken);
}
```

Responsibilities:

- check installed paths/files
- optionally invoke status entrypoints
- evaluate version/update state
- return live `NymphState`

This is where “installed / running / outdated / broken” belongs.

---

## 5.3 `NymphHostService`

This is the action dispatcher.

```csharp
namespace NymphsCoreManager.Services;

public sealed class NymphHostService
{
    public Task<NymphActionResult> ExecuteAsync(
        NymphDefinition definition,
        NymphActionKind action,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
```

Responsibilities:

- map generic actions to entrypoint scripts
- invoke the right wrapper script
- return a unified action result

This service should build on the existing:

- `ProcessRunner`

---

## 6. Recommended Relationship Between Services

Suggested flow:

1. `NymphRegistryService` loads manifests
2. `NymphStateDetectorService` resolves current states
3. `NymphHostService` runs actions against selected Nymphs
4. the ViewModel binds to:
   - definitions
   - states
   - action results

This keeps responsibilities separated and avoids one giant service doing everything.

---

## 7. Recommended UI Binding Shape

The UI should not bind directly to raw manifest JSON.

Instead, bind to a merged view model shape like:

```csharp
namespace NymphsCoreManager.Models;

public sealed record NymphSurfaceModel(
    NymphDefinition Definition,
    NymphState State);
```

That gives the UI:

- static definition data
- live runtime/install state

without mixing them together improperly.

---

## 8. Suggested File Placement

### Models

- `Models/NymphDefinition.cs`
- `Models/NymphSourceDefinition.cs`
- `Models/NymphEntrypoints.cs`
- `Models/NymphUiDefinition.cs`
- `Models/NymphRuntimeDefinition.cs`
- `Models/NymphDependency.cs`
- `Models/NymphState.cs`
- `Models/NymphInstallState.cs`
- `Models/NymphRuntimeState.cs`
- `Models/NymphActionKind.cs`
- `Models/NymphActionResult.cs`
- `Models/NymphSurfaceModel.cs`

### Services

- `Services/NymphRegistryService.cs`
- `Services/NymphStateDetectorService.cs`
- `Services/NymphHostService.cs`

Optional later:

- `Services/NymphDependencyResolver.cs`
- `Services/NymphManifestLoader.cs`

---

## 9. Implementation Order

Recommended implementation order:

1. `NymphDefinition` and related models
2. `NymphRegistryService`
3. `NymphState` + `NymphStateDetectorService`
4. `NymphHostService`
5. `NymphSurfaceModel`
6. UI/tab generation refactor

This order helps because:

- definitions come first
- live state comes second
- actions come third
- UI follows the real data model

---

## 10. Recommendation

The most important thing to keep clean is this:

- `NymphDefinition` says what a Nymph is
- `NymphState` says what condition it is currently in
- `NymphHostService` says what the Manager can do to it

If those three pieces stay separate, the Manager can be stripped down to a true modular core without turning into another pile of special cases.
