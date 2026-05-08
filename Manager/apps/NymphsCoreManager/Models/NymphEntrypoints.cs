using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphEntrypoints
{
    [JsonPropertyName("install")]
    public string? Install { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("start")]
    public string? Start { get; init; }

    [JsonPropertyName("stop")]
    public string? Stop { get; init; }

    [JsonPropertyName("open")]
    public string? Open { get; init; }

    [JsonPropertyName("logs")]
    public string? Logs { get; init; }

    [JsonPropertyName("update")]
    public string? Update { get; init; }

    [JsonPropertyName("uninstall")]
    public string? Uninstall { get; init; }
}
