using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NymphsCoreManager.Services;

public sealed class SharedSecretsService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NymphsCore");

    private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "shared-secrets.json");

    public SharedSecrets Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new SharedSecrets();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<SharedSecrets>(json) ?? new SharedSecrets();
        }
        catch
        {
            return new SharedSecrets();
        }
    }

    public void Save(SharedSecrets secrets)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(
            secrets,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        File.WriteAllText(ConfigPath, json + Environment.NewLine);
    }

    public string ConfigFilePath => ConfigPath;
}

public sealed class SharedSecrets
{
    [JsonPropertyName("huggingface_token")]
    public string HuggingFaceToken { get; set; } = string.Empty;

    [JsonPropertyName("openrouter_api_key")]
    public string OpenRouterApiKey { get; set; } = string.Empty;
}
