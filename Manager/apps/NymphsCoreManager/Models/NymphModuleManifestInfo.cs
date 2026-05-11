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
    string RepositoryUrl,
    string SourceSummary,
    string InstallRoot,
    string ManagerUiTitle,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> DevCapabilities,
    int SortOrder);
