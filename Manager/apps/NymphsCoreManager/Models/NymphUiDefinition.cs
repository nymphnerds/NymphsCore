using System.Text.Json.Serialization;

namespace NymphsCoreManager.Models;

public sealed record NymphUiDefinition
{
    [JsonPropertyName("show_tab_when_installed")]
    public bool ShowTabWhenInstalled { get; init; } = true;

    [JsonPropertyName("tab_label")]
    public string? TabLabel { get; init; }

    [JsonPropertyName("install_label")]
    public string? InstallLabel { get; init; }
}