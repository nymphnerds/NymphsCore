namespace NymphsCoreManager.Models;

/// <summary>
/// Result of a state detection run for a single Nymph module.
/// </summary>
public sealed record NymphModuleState(
    NymphRuntimeState RuntimeState,
    string StatusText);
