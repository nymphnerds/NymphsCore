using System.Diagnostics;
using System.IO;
using System.Net.Http;
using NymphsCoreManager.Models;

namespace NymphsCoreManager.Services;

public sealed class NymphHostService
{
    private readonly ProcessRunner _processRunner;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// WSL distribution used for runtime actions (Section 10: WSL Rules).
    /// </summary>
    private const string WslDistro = "NymphsCore";
    private const string WslUser = "nymph";

    public NymphHostService(ProcessRunner processRunner, HttpClient httpClient)
    {
        _processRunner = processRunner;
        _httpClient = httpClient;
    }

    // ---------------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Executes a lifecycle action against a Nymph module's entrypoint script.
    /// </summary>
    /// <param name="definition">Module definition from registry.</param>
    /// <param name="action">Lifecycle action to execute.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="args">Optional script arguments (e.g., "--purge", "--dry-run", "--yes").</param>
    public async Task<NymphActionResult> ExecuteAsync(
        NymphDefinition definition,
        NymphActionKind action,
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        params string[] args)
    {
        var entrypoint = ResolveEntrypoint(definition, action);

        if (entrypoint is null)
        {
            return NymphActionResult.FailureResult(
                action, definition.Id,
                $"No entrypoint script configured for action '{action}' on module '{definition.Id}'.",
                exitCode: -1);
        }

        return action switch
        {
            NymphActionKind.Open => await ExecuteOpenAsync(definition, entrypoint, progress, ct).ConfigureAwait(false),
            NymphActionKind.Configure => NymphActionResult.NotImplementedResult(action, definition.Id, "Configure action not yet implemented"),
            _ => await ExecuteScriptAsync(definition, action, entrypoint, progress, ct, args).ConfigureAwait(false)
        };
    }

    // ---------------------------------------------------------------------------
    // Script Execution
    // ---------------------------------------------------------------------------

    private async Task<NymphActionResult> ExecuteScriptAsync(
        NymphDefinition definition,
        NymphActionKind action,
        string entrypoint,
        IProgress<string>? progress,
        CancellationToken ct,
        string[] args)
    {
        // Determine the working directory (install root)
        var installRoot = ResolveInstallRoot(definition);

        // If script is remote (HTTP URL), download it first
        string localScriptPath = entrypoint;
        bool downloaded = false;

        if (entrypoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            entrypoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            localScriptPath = await DownloadScriptAsync(entrypoint, installRoot, ct).ConfigureAwait(false);
            downloaded = true;
        }

        // Build the WSL command: wsl.exe -d NymphsCore --user nymph bash /path/to/script [args...]
        var wslArgs = new List<string>
        {
            "-d", WslDistro,
            "--user", WslUser,
            "bash", localScriptPath
        };

        // Append optional script arguments (e.g., "--purge", "--dry-run", "--yes")
        if (args != null && args.Length > 0)
        {
            wslArgs.AddRange(args);
        }

        progress?.Report($"[host] Running '{action}' for '{definition.Id}' via WSL ({WslDistro})");

        CommandResult result;
        try
        {
            result = await _processRunner.RunAsync(
                "wsl.exe",
                wslArgs,
                Path.IsPathRooted(installRoot) ? installRoot : GetHomeDirectory(),
                progress,
                null,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return NymphActionResult.FailureResult(action, definition.Id, "Action cancelled by user.", exitCode: -2);
        }
        catch (Exception ex)
        {
            return NymphActionResult.FailureResult(action, definition.Id, $"Process execution failed: {ex.Message}", exitCode: -1);
        }
        finally
        {
            // Clean up downloaded temp script
            if (downloaded && File.Exists(localScriptPath))
            {
                try { File.Delete(localScriptPath); } catch { /* best-effort cleanup */ }
            }
        }

        return result.ExitCode == 0
            ? NymphActionResult.SuccessResult(action, definition.Id, result.CombinedOutput)
            : NymphActionResult.FailureResult(action, definition.Id, $"Script exited with code {result.ExitCode}.", result.CombinedOutput, result.ExitCode);
    }

    private async Task<NymphActionResult> ExecuteOpenAsync(
        NymphDefinition definition,
        string entrypoint,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        // "open" typically prints a URL; run the entrypoint script to get it
        var installRoot = ResolveInstallRoot(definition);
        string localScriptPath = entrypoint;
        bool downloaded = false;

        if (entrypoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            entrypoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            localScriptPath = await DownloadScriptAsync(entrypoint, installRoot, ct).ConfigureAwait(false);
            downloaded = true;
        }

        var wslArgs = new List<string>
        {
            "-d", WslDistro,
            "--user", WslUser,
            "bash", localScriptPath
        };

        try
        {
            var result = await _processRunner.RunAsync(
                "wsl.exe",
                wslArgs,
                Path.IsPathRooted(installRoot) ? installRoot : GetHomeDirectory(),
                progress,
                null,
                ct).ConfigureAwait(false);

            // Try to extract a URL from the output
            var output = result.CombinedOutput.Trim();
            var url = ExtractUrl(output);

            if (url is not null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { /* browser launch is best-effort */ }

                return NymphActionResult.SuccessResult(NymphActionKind.Open, definition.Id, $"Opened {url}");
            }

            return result.ExitCode == 0
                ? NymphActionResult.SuccessResult(NymphActionKind.Open, definition.Id, output)
                : NymphActionResult.FailureResult(NymphActionKind.Open, definition.Id, $"open.sh exited with code {result.ExitCode}.", output, result.ExitCode);
        }
        finally
        {
            if (downloaded && File.Exists(localScriptPath))
            {
                try { File.Delete(localScriptPath); } catch { }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Remote Script Download
    // ---------------------------------------------------------------------------

    private async Task<string> DownloadScriptAsync(string url, string installRoot, CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.sh");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);

        // Make executable (Windows temp path may be accessed from WSL)
        try
        {
            var ci = new FileInfo(tempFile);
            ci.IsReadOnly = false;
        }
        catch { /* best-effort */ }

        return tempFile;
    }

    // ---------------------------------------------------------------------------
    // Resolution Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Maps NymphActionKind to the corresponding entrypoint script path from the manifest.
    /// </summary>
    private static string? ResolveEntrypoint(NymphDefinition definition, NymphActionKind action)
    {
        var ep = definition.Entrypoints;
        return action switch
        {
            NymphActionKind.Install => ep?.Install,
            NymphActionKind.Update => ep?.Update,
            NymphActionKind.Status => ep?.Status,
            NymphActionKind.Start => ep?.Start,
            NymphActionKind.Stop => ep?.Stop,
            NymphActionKind.Open => ep?.Open,
            NymphActionKind.Logs => ep?.Logs,
            NymphActionKind.Remove => ep?.Uninstall,
            _ => null
        };
    }

    /// <summary>
    /// Resolves the module's install root directory.
    /// </summary>
    private static string ResolveInstallRoot(NymphDefinition definition)
    {
        var root = definition.Runtime?.InstallRoot;
        if (string.IsNullOrWhiteSpace(root))
        {
            // Fallback: use module id as a subdirectory under ~
            return $"~/Nymphs-{definition.Id}";
        }

        // Expand ~/ to user's home directory
        if (root.StartsWith("~/"))
        {
            return Path.Combine(GetHomeDirectory(), root.Substring(2));
        }

        return root;
    }

    private static string GetHomeDirectory()
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Extracts the first HTTP(S) URL from output text.
    /// </summary>
    private static string? ExtractUrl(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        // Simple heuristic: find first token starting with http:// or https://
        foreach (var line in output.Split('\n', '\r'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Trim trailing punctuation that might not be part of the URL
                return trimmed.Trim('.', ',', ')');
            }
        }

        return null;
    }
}