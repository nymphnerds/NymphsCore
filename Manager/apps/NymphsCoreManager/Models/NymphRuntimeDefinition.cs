using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphRuntimeDefinition
{
    [JsonPropertyName("install_root")]
    public string? InstallRoot { get; init; }

    [JsonPropertyName("health_url")]
    public string? HealthUrl { get; init; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; init; }

    [JsonPropertyName("port")]
    public int? Port { get; init; }
}