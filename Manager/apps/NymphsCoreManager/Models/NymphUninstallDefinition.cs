using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphUninstallDefinition
{
    [JsonPropertyName("supports_purge")]
    public bool SupportsPurge { get; init; }

    [JsonPropertyName("requires_confirmation")]
    public bool RequiresConfirmation { get; init; } = true;

    [JsonPropertyName("dry_run_arg")]
    public string? DryRunArg { get; init; }

    [JsonPropertyName("confirm_arg")]
    public string? ConfirmArg { get; init; }

    [JsonPropertyName("purge_arg")]
    public string? PurgeArg { get; init; }

    [JsonPropertyName("preserve_by_default")]
    public string[]? PreserveByDefault { get; init; }

    [JsonPropertyName("removes_by_default")]
    public string[]? RemovesByDefault { get; init; }
}