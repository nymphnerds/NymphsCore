using System;
using System.Collections.Generic;

namespace NymphsCoreManager.Models;

public sealed record NymphStatusSnapshot(
    string ModuleId,
    bool IsInstalled,
    bool IsRunning,
    string Version,
    string State,
    string Detail,
    string InstallRoot,
    string Health,
    IReadOnlyDictionary<string, string> Values)
{
    public static NymphStatusSnapshot FromStatusOutput(string moduleId, string? output)
    {
        var values = ParseKeyValueLines(output);
        var installed = ParseBool(GetValue(values, "installed")) ?? false;
        var running = ParseBool(GetValue(values, "running")) ?? false;
        var version = GetValue(values, "version") ?? (installed ? "unknown" : "not-installed");
        var state = GetValue(values, "state") ?? (running ? "running" : installed ? "installed" : "available");
        var detail = GetValue(values, "detail") ?? (installed ? "Installed." : "Not installed.");
        var installRoot = GetValue(values, "install_root") ?? "";
        var health = GetValue(values, "health") ?? "unknown";

        return new NymphStatusSnapshot(
            moduleId,
            installed,
            running,
            version,
            state,
            detail,
            installRoot,
            health,
            values);
    }

    public string? Get(string key)
    {
        return GetValue(Values, key);
    }

    private static IReadOnlyDictionary<string, string> ParseKeyValueLines(string? output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output))
        {
            return values;
        }

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            values[parts[0]] = parts[1];
        }

        return values;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("running", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("stopped", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}
