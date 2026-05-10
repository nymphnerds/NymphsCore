namespace NymphsCoreManager.Models;

public sealed record NymphModuleManifestInfo(
    string Id,
    string Name,
    string ShortName,
    string Category,
    string Kind,
    string Version,
    string Description,
    string ManifestUrl,
    string SourceSummary,
    string InstallRoot,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> DevCapabilities,
    int SortOrder);
