namespace NymphsCoreManager.Models;

public sealed record ManagerReleaseInfo(
    string Version,
    string ArtifactUrl,
    string ArtifactHash,
    IReadOnlyList<string> ReleaseNotes);
