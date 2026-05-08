using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphDependency
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("min_version")]
    public string? MinVersion { get; init; }
}