using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using NymphsCoreManager.Models;

namespace NymphsCoreManager.Services;

public sealed class NymphRegistryService
{
    private readonly ConcurrentDictionary<string, NymphDefinition> _definitions = new();
    private readonly string _registryDirectory;
    private readonly ILogger _logger;

    public NymphRegistryService(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _registryDirectory = ResolveRegistryDirectory();
    }

    // -- Registry entry models

    private sealed record NymphRegistryFile(
        int RegistryVersion,
        NymphRegistryEntry[] Modules)
    {
        [System.Text.Json.Serialization.JsonPropertyName("registry_version")]
        public int RegistryVersion { get; init; } = RegistryVersion;

        [System.Text.Json.Serialization.JsonPropertyName("modules")]
        public NymphRegistryEntry[] Modules { get; init; } = Modules;
    }

    /// <summary>
    /// Single entry from the online registry (nymphs.json).
    /// </summary>
    public sealed record NymphRegistryEntry(
        string Id,
        string Name,
        string Channel)
    {
        [System.Text.Json.Serialization.JsonPropertyName("manifest_url")]
        public string? ManifestUrl { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("manifest_path")]
        public string? ManifestPath { get; init; }
    }

    // -- Public API

    public IReadOnlyList<NymphDefinition> GetAllDefinitions() => _definitions.Values.ToList();

    public NymphDefinition? GetDefinition(string id) =>
        _definitions.TryGetValue(id, out var def) ? def : null;

    /// <summary>
    /// Fetches the remote registry and returns the list of entries.
    /// </summary>
    public async Task<IReadOnlyList<NymphRegistryEntry>> FetchRemoteRegistryAsync(
        string? registryUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = registryUrl ?? DefaultRegistryUrl;
        _logger.Info($"Fetching remote registry from: {url}");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var json = await client.GetStringAsync(url, cancellationToken);
            var registry = JsonSerializer.Deserialize<NymphRegistryFile>(json, JsonOptions);
            if (registry == null)
            {
                _logger.Warn("Failed to parse remote registry");
                return Array.Empty<NymphRegistryEntry>();
            }

            _logger.Info($"Remote registry loaded: {registry.Modules.Length} modules");
            return registry.Modules;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Remote registry fetch failed: {ex.Message}");
            return Array.Empty<NymphRegistryEntry>();
        }
    }

    /// <summary>
    /// Downloads a manifest from a remote URL and parses it into a NymphDefinition.
    /// </summary>
    public async Task<NymphDefinition?> FetchRemoteManifestAsync(
        string manifestUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var json = await client.GetStringAsync(manifestUrl, cancellationToken);
            var definition = JsonSerializer.Deserialize<NymphDefinition>(json, JsonOptions);
            if (definition == null)
            {
                _logger.Warn($"Failed to parse remote manifest: {manifestUrl}");
                return null;
            }

            _logger.Info($"Fetched remote manifest: {definition.Id} v{definition.Version}");
            return definition;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Remote manifest fetch failed ({manifestUrl}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Compares a local version against a remote version and returns a comparison result.
    /// </summary>
    public static NymphVersionComparison CompareVersion(
        string moduleId,
        string? localVersion,
        string? remoteVersion)
    {
        if (string.IsNullOrWhiteSpace(remoteVersion))
            return new NymphVersionComparison(
                moduleId, localVersion ?? "unknown", "unknown", false, "Remote version not available.");

        if (string.IsNullOrWhiteSpace(localVersion))
            return new NymphVersionComparison(
                moduleId, "not installed", remoteVersion ?? "unknown", true,
                $"New version available: {remoteVersion}.");

        // Simple semantic version comparison
        var localParts = ParseVersion(localVersion);
        var remoteParts = ParseVersion(remoteVersion);

        var hasUpdate = CompareVersionTuple(remoteParts, localParts) > 0;
        var detail = hasUpdate
            ? $"Update available: {localVersion} → {remoteVersion}"
            : $"Already on latest version ({localVersion}).";

        return new NymphVersionComparison(
            moduleId, localVersion, remoteVersion, hasUpdate, detail);
    }

    /// <summary>
    /// Checks for updates on all local definitions by fetching remote manifests.
    /// </summary>
    public async Task<IReadOnlyList<NymphVersionComparison>> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        var comparisons = new List<NymphVersionComparison>();

        foreach (var def in GetAllDefinitions())
        {
            // Try to fetch the remote manifest to get the latest version
            // Use the module's source repo to construct a manifest URL
            var remoteUrl = ConstructRemoteManifestUrl(def);
            if (string.IsNullOrEmpty(remoteUrl))
            {
                comparisons.Add(new NymphVersionComparison(
                    def.Id, def.Version, "unknown", false, "No remote manifest URL available."));
                continue;
            }

            var remoteDef = await FetchRemoteManifestAsync(remoteUrl, cancellationToken);
            comparisons.Add(CompareVersion(def.Id, def.Version, remoteDef?.Version));
        }

        return comparisons;
    }

    /// <summary>
    /// Constructs a remote manifest URL from a module definition's source repo.
    /// </summary>
    private static string? ConstructRemoteManifestUrl(NymphDefinition def)
    {
        var repo = def.Source?.Repo;
        if (string.IsNullOrEmpty(repo))
            return null;

        // Convert git@github.com:user/repo.git -> https://raw.githubusercontent.com/user/repo/main/nymph.json
        // Or https://github.com/user/repo -> https://raw.githubusercontent.com/user/repo/main/nymph.json
        var ownerRepo = ExtractOwnerRepo(repo);
        if (ownerRepo == null)
            return null;

        return $"https://raw.githubusercontent.com/{ownerRepo}/main/nymph.json";
    }

    private static string? ExtractOwnerRepo(string repo)
    {
        // git@github.com:user/repo.git -> user/repo
        var match = Regex.Match(repo, @"github\.com[:/](.+)/(.+?)(?:\.git)?$");
        if (match.Success)
            return $"{match.Groups[1].Value}/{match.Groups[2].Value}";

        // Already in owner/repo format
        if (Regex.IsMatch(repo, @"^[a-zA-Z0-9_-]+/[a-zA-Z0-9_-]+$"))
            return repo;

        return null;
    }

    /// <summary>
    /// Compares two version tuples. Returns positive if a > b, negative if a < b, zero if equal.
    /// </summary>
    private static int CompareVersionTuple(
        (int Major, int Minor, int Patch) a,
        (int Major, int Minor, int Patch) b)
    {
        if (a.Major != b.Major) return a.Major - b.Major;
        if (a.Minor != b.Minor) return a.Minor - b.Minor;
        return a.Patch - b.Patch;
    }

    /// <summary>
    /// Parses a version string into a comparable tuple (major, minor, patch).
    /// </summary>
    private static (int Major, int Minor, int Patch) ParseVersion(string version)
    {
        var parts = version.TrimStart('v', 'V').Split('.');
        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
        return (major, minor, patch);
    }

    public async Task LoadBundledRegistryAsync()
    {
        var registryPath = Path.Combine(_registryDirectory, "nymphs.json");
        if (!File.Exists(registryPath))
        {
            _logger.Warn($"Bundled registry not found at: {registryPath}");
            return;
        }

        var json = await File.ReadAllTextAsync(registryPath);
        var registry = JsonSerializer.Deserialize<NymphRegistryFile>(json, JsonOptions);
        if (registry == null)
        {
            _logger.Warn("Failed to parse bundled registry");
            return;
        }

        foreach (var entry in registry.Modules)
        {
            if (!string.IsNullOrEmpty(entry.ManifestPath))
            {
                var manifestPath = Path.Combine(_registryDirectory, entry.ManifestPath);
                await LoadManifestAsync(manifestPath);
            }
            else if (!string.IsNullOrEmpty(entry.ManifestUrl))
            {
                // Remote manifests are not loaded in bundled mode
                _logger.Info($"Skipping remote manifest for {entry.Id} (bundled mode)");
            }
        }
    }

    private async Task LoadManifestAsync(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Warn($"Manifest file not found: {path}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var definition = JsonSerializer.Deserialize<NymphDefinition>(json, JsonOptions);
            if (definition == null)
            {
                _logger.Warn($"Failed to parse manifest: {path}");
                return;
            }

            _definitions[definition.Id] = definition;
            _logger.Info($"Loaded manifest: {definition.Id} v{definition.Version}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Error loading manifest {path}: {ex.Message}");
        }
    }

    // -- Path resolution

    private static string ResolveRegistryDirectory()
    {
        // Walk up from the assembly directory to find Manager/registry/
        var basePath = AppContext.BaseDirectory;
        var current = basePath;

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "registry");
            if (Directory.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // Fallback: assume running from source tree
        // App runs from Manager/apps/NymphsCoreManager/bin/..., registry is at Manager/registry
        return Path.Combine(basePath, "../../registry");
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // -- Constants

    private const string DefaultRegistryUrl =
        "https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json";

    // -- Logging

    public interface ILogger
    {
        void Info(string message);
        void Warn(string message);
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();
        public void Info(string _) { }
        public void Warn(string _) { }
    }
}