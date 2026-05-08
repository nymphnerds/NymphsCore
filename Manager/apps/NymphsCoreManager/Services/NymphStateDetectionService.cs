using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NymphsCoreManager.Models;

namespace NymphsCoreManager.Services;

/// <summary>
/// Detects runtime state of Nymph modules by calling their status entrypoints
/// and parsing the output. Manifest-driven replacement for per-module ApplyXxxState methods.
/// </summary>
public sealed class NymphStateDetectionService
{
    private readonly InstallerWorkflowService _workflowService;
    private readonly InstallSettings _settings;
    private readonly ConcurrentDictionary<string, NymphModuleState> _cache = new();

    public NymphStateDetectionService(
        InstallerWorkflowService workflowService,
        InstallSettings settings)
    {
        _workflowService = workflowService;
        _settings = settings;
    }

    /// <summary>
    /// Detects state for a single module using its manifest entrypoint (status action).
    /// Falls back to filesystem-only detection if the status call fails.
    /// </summary>
    public async Task<NymphModuleState> DetectStateAsync(
        NymphDefinition definition,
        string installPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Call the module's status entrypoint via the existing workflow service
            var statusOutput = await _workflowService.RunNymphModuleActionAsync(
                _settings,
                definition.Id,
                "status",
                new Progress<string>(s => { }),
                cancellationToken).ConfigureAwait(false);

            var state = ParseStatusOutput(definition, statusOutput, installPath);
            _cache[definition.Id] = state;
            return state;
        }
        catch
        {
            // Status script failed or not available - fall back to filesystem detection
            var state = DetectStateFromFilesystem(definition, installPath);
            _cache[definition.Id] = state;
            return state;
        }
    }

    /// <summary>
    /// Detects state for all modules in parallel.
    /// </summary>
    public async Task<Dictionary<string, NymphModuleState>> DetectAllAsync(
        IReadOnlyList<NymphDefinition> definitions,
        Dictionary<string, string> installPaths,
        CancellationToken cancellationToken = default)
    {
        var tasks = definitions.Select(def =>
        {
            var path = installPaths.TryGetValue(def.Id, out var p) ? p : string.Empty;
            return DetectStateAsync(def, path, cancellationToken);
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return definitions.Zip(results, (def, state) => (def.Id, state))
            .ToDictionary(x => x.Id, x => x.state);
    }

    /// <summary>
    /// Gets cached state for a module (null if not yet detected).
    /// </summary>
    public NymphModuleState? GetCachedState(string moduleId) =>
        _cache.TryGetValue(moduleId, out var state) ? state : null;

    /// <summary>
    /// Clears the state cache.
    /// </summary>
    public void ClearCache() => _cache.Clear();

    /// <summary>
    /// Parses status script output to determine runtime state.
    /// Handles key=value format (WORBI), "key: value" format (Brain), and generic patterns.
    /// </summary>
    private static NymphModuleState ParseStatusOutput(
        NymphDefinition definition,
        string statusOutput,
        string installPath)
    {
        var values = ParseKeyValueLines(statusOutput);

        // Determine installed state from script output or filesystem
        var installed = ResolveInstalledState(values, installPath);
        var running = ResolveRunningState(values, installPath);

        // Map to runtime state enum
        var runtimeState = (installed, running) switch
        {
            (true, true) => NymphRuntimeState.Running,
            (true, false) => NymphRuntimeState.Stopped,
            (false, false) => NymphRuntimeState.Unknown,
            (false, true) => NymphRuntimeState.Degraded, // unusual but possible
        };

        // Build human-readable status text
        var statusText = BuildStatusText(definition, values, installed, running);

        return new NymphModuleState(runtimeState, statusText);
    }

    /// <summary>
    /// Detects state purely from filesystem (no status script available).
    /// </summary>
    private static NymphModuleState DetectStateFromFilesystem(
        NymphDefinition definition,
        string installPath)
    {
        var installed = Directory.Exists(installPath);
        var runtimeState = installed ? NymphRuntimeState.Stopped : NymphRuntimeState.Unknown;
        var statusText = installed
            ? "Module files detected but status script not available."
            : $"{definition.Name} is available as an optional Nymph.";

        return new NymphModuleState(runtimeState, statusText);
    }

    // -- State resolution helpers --

    private static bool ResolveInstalledState(Dictionary<string, string> values, string installPath)
    {
        // Check explicit "installed" key from status output
        if (values.TryGetValue("installed", out var val))
            return ParseBool(val, false);

        // Fall back to filesystem check
        return Directory.Exists(installPath);
    }

    private static bool ResolveRunningState(Dictionary<string, string> values, string installPath)
    {
        // 1. Explicit "running" key (WORBI style: running=true)
        if (values.TryGetValue("running", out var val) && ParseBool(val, false))
            return true;

        // 2. Backend key (WORBI style: backend=running)
        if (values.TryGetValue("backend", out var backend))
        {
            var b = backend.ToLowerInvariant();
            if (b.Contains("running") || b == "responding")
                return true;
        }

        // 3. Llama-server / LLM server key (Brain style: llama-server: running)
        if (values.TryGetValue("llama-server", out var llm) ||
            values.TryGetValue("llm server", out llm))
        {
            if (llm?.Equals("running", StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        // 4. Health key (WORBI style: health=ok)
        if (values.TryGetValue("health", out var health))
        {
            if (health?.Equals("ok", StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        // 5. Check for PID files in logs directory (last resort)
        var logsDir = Path.Combine(installPath, "logs");
        if (Directory.Exists(logsDir))
        {
            foreach (var pidFile in Directory.EnumerateFiles(logsDir, "*.pid"))
            {
                if (IsPidAlive(pidFile))
                    return true;
            }
        }

        return false;
    }

    private static string BuildStatusText(
        NymphDefinition definition,
        Dictionary<string, string> values,
        bool installed,
        bool running)
    {
        if (!installed)
            return $"{definition.Name} is available as an optional Nymph.";

        // Use "detail" key from status output if available
        if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("detail")))
            return values["detail"]!;

        var parts = new List<string>();
        if (running)
            parts.Add($"{definition.Name} is running.");
        else
            parts.Add($"{definition.Name} is installed but not running.");

        if (!string.IsNullOrWhiteSpace(values.GetValueOrDefault("version")))
            parts.Add($"Version: {values["version"]}");

        return string.Join(" ", parts);
    }

    // -- Parsing helpers --

    internal static Dictionary<string, string> ParseKeyValueLines(string? output)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output))
            return result;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Try key=value first (most common format)
            var eqIdx = line.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = line[..eqIdx].Trim().ToLowerInvariant();
                var val = line[(eqIdx + 1)..].Trim();
                result[key] = val;
                continue;
            }

            // Try "key: value" format (Brain status output)
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                var key = line[..colonIdx].Trim().ToLowerInvariant();
                var val = line[(colonIdx + 1)..].Trim();
                result[key] = val;
            }
        }

        return result;
    }

    private static bool ParseBool(string value, bool @default)
    {
        if (bool.TryParse(value, out var result))
            return result;

        return value.ToLowerInvariant() switch
        {
            "yes" or "true" or "on" or "running" or "ok" => true,
            "no" or "false" or "off" or "stopped" or "failed" => false,
            _ => @default
        };
    }

    private static bool IsPidAlive(string pidFile)
    {
        try
        {
            if (!File.Exists(pidFile))
                return false;

            var pidStr = File.ReadAllText(pidFile).Trim();
            if (!int.TryParse(pidStr, out var pid))
                return false;

            // On Windows, check WSL process; on Linux, check directly
            if (OperatingSystem.IsWindows())
            {
                // Check if PID exists in WSL
                var psi = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = $"-e ps -p {pid} -o pid=",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit(3000);
                var output = proc.StandardOutput.ReadToEnd().Trim();
                return !string.IsNullOrEmpty(output);
            }
            else
            {
                return Process.GetProcessById(pid) != null;
            }
        }
        catch
        {
            return false;
        }
    }
}