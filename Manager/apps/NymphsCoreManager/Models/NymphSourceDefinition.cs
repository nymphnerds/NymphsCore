using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphSourceDefinition
{
    [JsonPropertyName("repo")]
    public string? Repo { get; init; }

    [JsonPropertyName("ref")]
    public string? Ref { get; init; }

    [JsonPropertyName("archive")]
    public string? Archive { get; init; }

    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("local_path")]
    public string? LocalPath { get; init; }

    /// <summary>
    /// Returns true if this source is a git repo (has Repo URL).
    /// </summary>
    public bool IsRepo => !string.IsNullOrEmpty(Repo);

    /// <summary>
    /// Returns true if this source is an archive (has Archive path).
    /// </summary>
    public bool IsArchive => !string.IsNullOrEmpty(Archive);

    /// <summary>
    /// Returns true if this source is a local filesystem path.
    /// </summary>
    public bool IsLocal => !string.IsNullOrEmpty(LocalPath);
}