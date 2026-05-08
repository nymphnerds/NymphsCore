namespace NymphsCoreManager.Models;

/// <summary>
/// Result of comparing a local manifest version against a remote manifest version.
/// </summary>
public sealed record NymphVersionComparison(
    string ModuleId,
    string LocalVersion,
    string RemoteVersion,
    bool HasUpdate,
    string Detail);