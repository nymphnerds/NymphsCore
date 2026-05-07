namespace NymphsCoreManager.Models;

public sealed record NymphModuleManifestInfo(
    string Id,
    string Name,
    string Category,
    string Kind,
    string Version,
    string Description,
    string ManifestUrl,
    string SourceSummary);
