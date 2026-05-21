namespace NymphsCoreManager.Models;

public sealed record ManagerManifestInfo(
    string Id,
    string Name,
    string Version,
    string ArtifactUrl,
    string SourceUrl);
