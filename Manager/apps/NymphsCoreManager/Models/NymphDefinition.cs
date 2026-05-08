using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphDefinition(
    string Id,
    string Name,
    string Kind,
    string Category,
    string Description,
    NymphSourceDefinition Source,
    NymphEntrypoints Entrypoints)
{
    [JsonPropertyName("manifest_version")]
    public int ManifestVersion { get; init; } = 1;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("capabilities")]
    public string[]? Capabilities { get; init; }

    [JsonPropertyName("dependencies")]
    public NymphDependency[]? Dependencies { get; init; }

    [JsonPropertyName("ui")]
    public NymphUiDefinition? Ui { get; init; }

    [JsonPropertyName("runtime")]
    public NymphRuntimeDefinition? Runtime { get; init; }

    [JsonPropertyName("update_policy")]
    public object? UpdatePolicy { get; init; }

    [JsonPropertyName("uninstall")]
    public NymphUninstallDefinition? Uninstall { get; init; }
}
