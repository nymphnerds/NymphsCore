using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NymphsCoreManager.Models;

namespace NymphsCoreManager.Services;

public sealed class InstallerWorkflowService
{
    public const string ManagedDistroName = "NymphsCore";
    public const string ManagedLinuxUser = "nymph";
    public const string WslAvailabilityCheckKey = "wsl_availability";
    public const string ExistingWslDistrosCheckKey = "existing_wsl_distros";
    public const string GuideUrl = "https://github.com/nymphnerds/NymphsCore/blob/modular/docs/GETTING_STARTED.md";
    public const string ReadmeUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/Manager/README.md";
    public const string SourceRepoUrl = "https://github.com/nymphnerds/NymphsCore";
    public const string FootprintDocUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/docs/FOOTPRINT.md";
    public const string AddonGuideUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/docs/BLENDER_ADDON_USER_GUIDE.md";
    private const string NymphModuleRegistryUrl = "https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json";

    private readonly ProcessRunner _processRunner = new();
    private readonly HttpClient _moduleRegistryHttpClient = new();

    public InstallerWorkflowService()
    {
        RepositoryRoot = ResolveRepositoryRoot();
        ScriptsDirectory = Path.Combine(RepositoryRoot, "scripts");
        PayloadDirectory = ResolvePayloadDirectory();
        BaseTarPath = ResolveBaseTarPath();
        LogFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NymphsCore");
    }

    public string RepositoryRoot { get; }

    public string ScriptsDirectory { get; }

    public string PayloadDirectory { get; }

    public string BaseTarPath { get; }

    public bool BaseTarAvailable => File.Exists(BaseTarPath);

    public string LogFolderPath { get; }

    public string WslConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

    public string CreateInstallSessionLogPath()
    {
        Directory.CreateDirectory(LogFolderPath);
        return Path.Combine(
            LogFolderPath,
            $"installer-run-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    public IReadOnlyList<DriveChoice> GetAvailableDrives()
    {
        return DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .Select(drive => new DriveChoice(drive.RootDirectory.FullName, drive.AvailableFreeSpace))
            .OrderBy(drive => drive.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<SystemCheckItem>> RunSystemChecksAsync(CancellationToken cancellationToken)
    {
        var items = new List<SystemCheckItem>
        {
            CheckAdministratorStatus(),
            CheckDriveAvailability(),
            await CheckWslAvailabilityAsync(cancellationToken).ConfigureAwait(false),
            await CheckExistingWslDistrosAsync(cancellationToken).ConfigureAwait(false),
            await CheckNvidiaStatusAsync(cancellationToken).ConfigureAwait(false),
        };

        return items;
    }

    public async Task ImportBaseDistroAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var importScript = RequireScript("import_base_distro.ps1");
        ReportImportSettings(progress, settings);

        var result = await RunImportScriptAsync(
            importScript,
            settings,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        if (!settings.RepairExistingDistro &&
            result.CombinedOutput.Contains("Wsl/Service/RegisterDistro/0x8000000d", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackDistroName = await GetNextAvailableManagedDistroAliasAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(fallbackDistroName, settings.DistroName, StringComparison.OrdinalIgnoreCase))
            {
                settings.DistroName = fallbackDistroName;
                settings.InstallLocation = BuildAliasedInstallLocation(settings.InstallLocation, fallbackDistroName);
                progress.Report($"Managed distro name appears stuck in WSL. Retrying import as '{fallbackDistroName}'.");
                ReportImportSettings(progress, settings);

                result = await RunImportScriptAsync(
                    importScript,
                    settings,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException("Base distro import failed.");
    }

    public async Task UninstallBaseRuntimeAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report($"Uninstalling managed WSL distro '{settings.DistroName}'.");

        try
        {
            progress.Report($"Terminating '{settings.DistroName}' if it is running.");
            await _processRunner.RunAsync(
                "wsl.exe",
                ["--terminate", settings.DistroName],
                Environment.SystemDirectory,
                progress: null,
                environmentVariables: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Terminate can fail when the distro is already stopped; unregister below is the real operation.
        }

        progress.Report($"Unregistering '{settings.DistroName}'.");
        var unregisterResult = await _processRunner.RunAsync(
            "wsl.exe",
            ["--unregister", settings.DistroName],
            Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (unregisterResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to unregister '{settings.DistroName}'.");
        }

        if (string.IsNullOrWhiteSpace(settings.InstallLocation) || !Directory.Exists(settings.InstallLocation))
        {
            progress.Report("Managed runtime folder already removed.");
            return;
        }

        progress.Report($"Deleting runtime folder: {settings.InstallLocation}");
        await _processRunner.RunAsync(
            "wsl.exe",
            ["--shutdown"],
            Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                Directory.Delete(settings.InstallLocation, recursive: true);
                progress.Report("Managed runtime folder deleted.");
                return;
            }
            catch when (attempt < 6)
            {
                await Task.Delay(500 * attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        Directory.Delete(settings.InstallLocation, recursive: true);
        progress.Report("Managed runtime folder deleted.");
    }

    public WslConfigValues GetRecommendedWslConfig()
    {
        var totalMemoryGb = (int)Math.Floor(GetTotalPhysicalMemoryBytes() / (double)(1024L * 1024L * 1024L));
        var logicalProcessors = Environment.ProcessorCount;

        int memoryGb;
        if (totalMemoryGb >= 96)
        {
            memoryGb = 48;
        }
        else if (totalMemoryGb >= 64)
        {
            memoryGb = 32;
        }
        else if (totalMemoryGb >= 48)
        {
            memoryGb = 24;
        }
        else if (totalMemoryGb >= 32)
        {
            memoryGb = 16;
        }
        else if (totalMemoryGb >= 24)
        {
            memoryGb = 12;
        }
        else
        {
            memoryGb = Math.Max(8, totalMemoryGb - 4);
        }

        var processors = Math.Max(4, Math.Min(16, logicalProcessors - 2));
        var swapGb = Math.Max(8, Math.Min(16, memoryGb / 2));

        return new WslConfigValues(memoryGb, processors, swapGb);
    }

    public int GetDetectedGpuVramMb()
    {
        try
        {
            var result = _processRunner.RunAsync(
                fileName: "nvidia-smi.exe",
                arguments:
                [
                    "--query-gpu=memory.total",
                    "--format=csv,noheader,nounits",
                ],
                workingDirectory: Environment.SystemDirectory,
                progress: null,
                environmentVariables: null,
                cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

            if (result.ExitCode == 0)
            {
                var output = result.CombinedOutput.Trim();
                var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && int.TryParse(lines[0].Trim(), out var vramMb))
                {
                    return vramMb;
                }
            }
        }
        catch
        {
            // Fall back to 0 if detection fails
        }

        return 0;
    }

    public WslConfigFileState GetCurrentWslConfig()
    {
        var path = WslConfigPath;
        if (!File.Exists(path))
        {
            return new WslConfigFileState(path, exists: false, memoryGb: null, processors: null, swapGb: null);
        }

        var lines = File.ReadAllLines(path);
        var memoryGb = ParseIniSizeGb(lines, "wsl2", "memory");
        var processors = ParseIniInt(lines, "wsl2", "processors");
        var swapGb = ParseIniSizeGb(lines, "wsl2", "swap");
        return new WslConfigFileState(path, exists: true, memoryGb, processors, swapGb);
    }

    public async Task ApplyWslConfigAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (settings.WslConfigMode == WslConfigMode.KeepExisting && File.Exists(WslConfigPath))
        {
            progress.Report("WSL resource settings: keeping the current .wslconfig values.");
            return;
        }

        var config = settings.WslConfigMode switch
        {
            WslConfigMode.Custom => new WslConfigValues(
                settings.WslMemoryGb,
                settings.WslProcessors,
                settings.WslSwapGb),
            _ => GetRecommendedWslConfig(),
        };

        var updatedContent = BuildUpdatedWslConfigContent(config);
        var existingContent = File.Exists(WslConfigPath)
            ? File.ReadAllText(WslConfigPath)
            : string.Empty;

        if (string.Equals(existingContent, updatedContent, StringComparison.Ordinal))
        {
            progress.Report("WSL resource settings: .wslconfig already matches the selected values.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(WslConfigPath)!);

        if (File.Exists(WslConfigPath))
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(WslConfigPath)!,
                $".wslconfig.backup-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Copy(WslConfigPath, backupPath, overwrite: true);
            progress.Report($"WSL resource settings: backed up the current .wslconfig to {backupPath}.");
        }

        File.WriteAllText(WslConfigPath, updatedContent, Encoding.ASCII);
        progress.Report($"WSL resource settings: wrote memory={config.MemoryGb}GB, processors={config.Processors}, swap={config.SwapGb}GB to {WslConfigPath}.");

        var wslStatus = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments: ["--status"],
            workingDirectory: Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (wslStatus.ExitCode == 0)
        {
            progress.Report("WSL resource settings: shutting down WSL so the new values can apply.");
            var shutdownResult = await _processRunner.RunAsync(
                fileName: "wsl.exe",
                arguments: ["--shutdown"],
                workingDirectory: Environment.SystemDirectory,
                progress: null,
                environmentVariables: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (shutdownResult.ExitCode != 0)
            {
                throw new InvalidOperationException("Failed to shut down WSL after writing .wslconfig.");
            }
        }
        else
        {
            progress.Report("WSL resource settings: WSL is not ready yet, so the new .wslconfig will apply after WSL is available.");
        }
    }

    public async Task RunRuntimeSetupAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var runtimeScript = RequireScript("run_finalize_in_distro.ps1");
        var arguments = new List<string>
        {
            "-DistroName", settings.DistroName,
            "-LinuxUser", settings.LinuxUser,
            "-SkipBackendEnvs",
            "-SkipModels",
            "-SkipVerify",
        };

        progress.Report("Runtime setup: preparing the shared base shell inside the managed distro.");
        progress.Report("Module backend environments are installed by module-owned entrypoints after the module is installed.");

        var githubToken = ResolveGitHubTokenFromEnvironment();
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            progress.Report($"GitHub token: provided for authenticated GitHub operations ({githubToken.Length} chars after trimming).");
        }

        var result = await RunPowerShellScriptAsync(
            runtimeScript,
            arguments,
            progress,
            BuildInstallerEnvironment(),
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Runtime setup failed.");
        }
    }

    public async Task<ShellRuntimeMonitorSnapshot> GetShellRuntimeMonitorAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        // Sidebar runtime monitor: leave this path stable unless there is a concrete bug to fix.
        // It targets the managed NymphsCore distro on purpose, and GPU telemetry intentionally reuses
        // the older monitor_query.sh flow that already proved reliable in the previous Brain-page monitor.
        try
        {
            var osReleaseText = await TryRunWslTextAsync(settings, ["cat", "/etc/os-release"], cancellationToken).ConfigureAwait(false);
            var kernelText = await TryRunWslTextAsync(settings, ["uname", "-r"], cancellationToken).ConfigureAwait(false);
            var uptimeText = await TryRunWslTextAsync(settings, ["cat", "/proc/uptime"], cancellationToken).ConfigureAwait(false);
            var cpuStat1Text = await TryRunWslTextAsync(settings, ["cat", "/proc/stat"], cancellationToken).ConfigureAwait(false);
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            var cpuStat2Text = await TryRunWslTextAsync(settings, ["cat", "/proc/stat"], cancellationToken).ConfigureAwait(false);
            var memInfoText = await TryRunWslTextAsync(settings, ["cat", "/proc/meminfo"], cancellationToken).ConfigureAwait(false);
            var diskText = await TryRunWslTextAsync(settings, ["df", "-Pk", "/"], cancellationToken).ConfigureAwait(false);
            var gpuTelemetry = await GetManagedRuntimeGpuTelemetryAsync(settings, cancellationToken).ConfigureAwait(false);

            var prettyName = NormalizeRuntimeDistribution(ParsePrettyName(osReleaseText));
            var kernel = string.IsNullOrWhiteSpace(kernelText) ? "-" : kernelText.Trim();
            var uptime = FormatRuntimeUptime(uptimeText);

            var (cpuTotal1, cpuIdle1) = ParseCpuStat(cpuStat1Text);
            var (cpuTotal2, cpuIdle2) = ParseCpuStat(cpuStat2Text);
            var cpuPercent = ComputeCpuPercent(cpuTotal1, cpuIdle1, cpuTotal2, cpuIdle2);

            var (memoryUsedKb, memoryTotalKb, memoryPercent) = ParseMemoryInfo(memInfoText);
            var (diskUsedKb, diskTotalKb, diskPercent) = ParseDiskUsage(diskText);
            var windowsDiskUsageLabel = GetWindowsDiskUsageLabel(settings.InstallLocation);
            var brainTelemetry = await GetShellBrainTelemetryAsync(settings, cancellationToken).ConfigureAwait(false);

            var isAvailable = !string.Equals(kernel, "-", StringComparison.Ordinal);

            return new ShellRuntimeMonitorSnapshot(
                isAvailable,
                $"WSL: {prettyName}",
                $"Kernel: {kernel}",
                $"Uptime: {uptime}",
                cpuPercent,
                memoryTotalKb > 0
                    ? $"{FormatRuntimeGb(memoryUsedKb, decimals: 1)} / {FormatRuntimeGb(memoryTotalKb, decimals: 1)} GB"
                    : "- / -",
                memoryPercent,
                diskTotalKb > 0
                    ? $"{FormatRuntimeGb(diskUsedKb, decimals: 0)} / {FormatRuntimeGb(diskTotalKb, decimals: 0)} GB"
                    : "- / -",
                diskPercent,
                windowsDiskUsageLabel,
                gpuTelemetry.GpuVram,
                gpuTelemetry.GpuTemp,
                brainTelemetry.BrainLlmStateLabel,
                brainTelemetry.BrainModelLabel,
                brainTelemetry.BrainContextLabel,
                brainTelemetry.BrainTokensPerSecondLabel);
        }
        catch
        {
            return ShellRuntimeMonitorSnapshot.Offline;
        }
    }

    private async Task<(string BrainLlmStateLabel, string BrainModelLabel, string BrainContextLabel, string BrainTokensPerSecondLabel)> GetShellBrainTelemetryAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await GetNymphsBrainMonitorAsync(settings, cancellationToken).ConfigureAwait(false);
            if (!snapshot.IsRunning)
            {
                return ("LLM: Offline", "Model: -", "Context: -", "TPS: -");
            }

            return (
                "LLM: Running",
                $"Model: {ValueOrDash(snapshot.Model)}",
                $"Context: {ValueOrDash(snapshot.Context)}",
                $"TPS: {ValueOrDash(snapshot.TokensPerSecond)}");
        }
        catch
        {
            return ("LLM: Unknown", "Model: -", "Context: -", "TPS: -");
        }
    }

    public async Task<BrainMonitorSnapshot> GetNymphsBrainMonitorAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var monitorScript = Path.Combine(ScriptsDirectory, "monitor_query.sh");
        var wslMonitorScriptPath = File.Exists(monitorScript)
            ? ConvertWindowsPathToWsl(monitorScript) ?? string.Empty
            : string.Empty;
        var installedMonitorScriptPath = $"{settings.BrainInstallRoot}/scripts/monitor_query.sh";

        if (!string.IsNullOrWhiteSpace(wslMonitorScriptPath))
        {
            var installCommand = new StringBuilder()
                .Append("mkdir -p ")
                .Append(ToBashSingleQuoted($"{settings.BrainInstallRoot}/scripts"))
                .Append("; install -m 755 ")
                .Append(ToBashSingleQuoted(wslMonitorScriptPath))
                .Append(' ')
                .Append(ToBashSingleQuoted(installedMonitorScriptPath))
                .ToString();

            await RunWslBashAsync(settings, installCommand, progress: null, cancellationToken)
                .ConfigureAwait(false);
        }

        var pid = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "pid", cancellationToken)
            .ConfigureAwait(false);
        var modelsJson = await QueryNymphsBrainModelsEndpointAsync(settings, cancellationToken)
            .ConfigureAwait(false);
        var isRunning = !string.IsNullOrWhiteSpace(pid) || !string.IsNullOrWhiteSpace(modelsJson);

        if (!isRunning)
        {
            return BrainMonitorSnapshot.Offline;
        }

        var model = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "model", cancellationToken)
            .ConfigureAwait(false);
        var context = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "context", cancellationToken)
            .ConfigureAwait(false);
        var vram = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "gpu-vram", cancellationToken)
            .ConfigureAwait(false);
        var temp = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "gpu-temp", cancellationToken)
            .ConfigureAwait(false);
        var tps = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "tps", cancellationToken)
            .ConfigureAwait(false);

        var snapshot = new BrainMonitorSnapshot(
            true,
            ValueOrDash(model),
            ValueOrDash(context) == "-" ? "Unavailable" : ValueOrDash(context),
            ValueOrDash(vram),
            ValueOrDash(temp),
            ValueOrDash(tps) == "-" ? "Waiting" : ValueOrDash(tps));

        if (!snapshot.IsRunning || (snapshot.GpuVram != "-" && snapshot.GpuTemp != "-"))
        {
            return snapshot;
        }

        var gpuSnapshot = await GetWindowsGpuTelemetryAsync(cancellationToken).ConfigureAwait(false);
        return snapshot with
        {
            GpuVram = gpuSnapshot.GpuVram,
            GpuTemp = gpuSnapshot.GpuTemp,
        };
    }

    private async Task<string> QueryNymphsBrainMonitorValueAsync(
        InstallSettings settings,
        string monitorScriptPath,
        string query,
        CancellationToken cancellationToken)
    {
        var result = await RunWslCommandAsync(
            settings,
            [monitorScriptPath, query],
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0 ? result.CombinedOutput.Trim() : string.Empty;
    }

    private async Task<string> QueryNymphsBrainModelsEndpointAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var result = await RunWslCommandAsync(
            settings,
            [
                "curl",
                "--silent",
                "--show-error",
                "--fail",
                "--connect-timeout",
                "2",
                "--max-time",
                "5",
                "http://127.0.0.1:8000/v1/models",
            ],
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0 ? result.CombinedOutput.Trim() : string.Empty;
    }

    private async Task<(string GpuVram, string GpuTemp)> GetManagedRuntimeGpuTelemetryAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var monitorScript = Path.Combine(ScriptsDirectory, "monitor_query.sh");
        var wslMonitorScriptPath = File.Exists(monitorScript)
            ? ConvertWindowsPathToWsl(monitorScript) ?? string.Empty
            : string.Empty;
        var installedMonitorScriptPath = $"{settings.BrainInstallRoot}/scripts/monitor_query.sh";

        if (!string.IsNullOrWhiteSpace(wslMonitorScriptPath))
        {
            var installCommand = new StringBuilder()
                .Append("mkdir -p ")
                .Append(ToBashSingleQuoted($"{settings.BrainInstallRoot}/scripts"))
                .Append("; install -m 755 ")
                .Append(ToBashSingleQuoted(wslMonitorScriptPath))
                .Append(' ')
                .Append(ToBashSingleQuoted(installedMonitorScriptPath))
                .ToString();

            await RunWslBashAsync(settings, installCommand, progress: null, cancellationToken)
                .ConfigureAwait(false);
        }

        var gpuVram = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "gpu-vram", cancellationToken)
            .ConfigureAwait(false);
        var gpuTemp = await QueryNymphsBrainMonitorValueAsync(settings, installedMonitorScriptPath, "gpu-temp", cancellationToken)
            .ConfigureAwait(false);

        gpuVram = ValueOrDash(gpuVram);
        gpuTemp = ValueOrDash(gpuTemp);

        if (gpuVram != "-" && gpuTemp != "-")
        {
            return (gpuVram, gpuTemp);
        }

        return await GetWindowsGpuTelemetryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string GpuVram, string GpuTemp)> GetWindowsGpuTelemetryAsync(CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            "nvidia-smi.exe",
            @"C:\Windows\System32\nvidia-smi.exe",
            @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var result = await _processRunner.RunAsync(
                    fileName: candidate,
                    arguments:
                    [
                        "--query-gpu=memory.used,memory.total,temperature.gpu",
                        "--format=csv,noheader,nounits",
                    ],
                    workingDirectory: Environment.SystemDirectory,
                    progress: null,
                    environmentVariables: null,
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    continue;
                }

                var firstLine = result.CombinedOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    continue;
                }

                var parts = firstLine
                    .Split(',', StringSplitOptions.TrimEntries)
                    .ToArray();
                if (parts.Length < 3 ||
                    !double.TryParse(parts[0], out var usedMb) ||
                    !double.TryParse(parts[1], out var totalMb))
                {
                    continue;
                }

                var temp = string.IsNullOrWhiteSpace(parts[2]) ? "Unavailable" : $"{parts[2]}C";
                return ($"{usedMb / 1024:0} GB/{totalMb / 1024:0} GB", temp);
            }
            catch
            {
                // Try the next known NVIDIA path.
            }
        }

        return ("Unavailable", "Unavailable");
    }

    private static string ToWindowsWslPath(InstallSettings settings, string linuxPath)
    {
        var normalizedLinuxPath = linuxPath.Replace('\\', '/');
        if (!normalizedLinuxPath.StartsWith("/", StringComparison.Ordinal))
        {
            normalizedLinuxPath = "/" + normalizedLinuxPath;
        }

        return $@"\\wsl.localhost\{settings.DistroName}{normalizedLinuxPath.Replace('/', '\\')}";
    }

    private Task<CommandResult> RunWslBashAsync(
        InstallSettings settings,
        string bashCommand,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return RunWslCommandAsync(
            settings,
            ["/bin/bash", "-lc", bashCommand],
            progress,
            cancellationToken);
    }

    private Task<CommandResult> RunWslCommandAsync(
        InstallSettings settings,
        IEnumerable<string> commandArguments,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-d",
            settings.DistroName,
            "--user",
            settings.LinuxUser,
            "--",
        };
        arguments.AddRange(commandArguments);

        return _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken);
    }

    private static string ValueOrDash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Trim();
        return normalized is "—" or "–" ? "-" : normalized;
    }

    private static string ValueOrDash(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Trim();
        return normalized is "—" or "–" ? "-" : normalized;
    }

    private static int ParseRuntimeInt(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static long ParseRuntimeLong(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var rawValue) && long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0L;
    }

    private async Task<string> TryRunWslTextAsync(
        InstallSettings settings,
        IEnumerable<string> commandArguments,
        CancellationToken cancellationToken)
    {
        var result = await RunWslCommandAsync(
            settings,
            commandArguments,
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0
            ? result.CombinedOutput.Trim()
            : string.Empty;
    }

    private static string ParsePrettyName(string? osReleaseText)
    {
        if (string.IsNullOrWhiteSpace(osReleaseText))
        {
            return "Linux";
        }

        foreach (var line in osReleaseText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("PRETTY_NAME=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line["PRETTY_NAME=".Length..].Trim().Trim('"');
            return string.IsNullOrWhiteSpace(value) ? "Linux" : value;
        }

        return "Linux";
    }

    private static string FormatRuntimeUptime(string? uptimeText)
    {
        if (string.IsNullOrWhiteSpace(uptimeText))
        {
            return "-";
        }

        var firstPart = uptimeText.Split('.', 2)[0].Trim();
        if (!long.TryParse(firstPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uptimeSeconds) || uptimeSeconds < 0)
        {
            return "-";
        }

        var days = uptimeSeconds / 86400;
        var hours = (uptimeSeconds % 86400) / 3600;
        var minutes = (uptimeSeconds % 3600) / 60;

        if (days > 0)
        {
            return $"{days}d {hours}h {minutes}m";
        }

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{minutes}m";
    }

    private static (long Total, long Idle) ParseCpuStat(string? cpuStatText)
    {
        if (string.IsNullOrWhiteSpace(cpuStatText))
        {
            return (0L, 0L);
        }

        var line = cpuStatText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(item => item.StartsWith("cpu ", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(line))
        {
            return (0L, 0L);
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return (0L, 0L);
        }

        var values = parts.Skip(1)
            .Select(part => long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0L)
            .ToArray();

        var total = values.Sum();
        var idle = values.Length > 4 ? values[3] + values[4] : values[3];
        return (total, idle);
    }

    private static int ComputeCpuPercent(long total1, long idle1, long total2, long idle2)
    {
        var totalDelta = total2 - total1;
        var idleDelta = idle2 - idle1;

        if (totalDelta <= 0)
        {
            return 0;
        }

        var busyDelta = Math.Max(0L, totalDelta - idleDelta);
        return (int)Math.Clamp(
            (int)Math.Round(100d * busyDelta / totalDelta, MidpointRounding.AwayFromZero),
            0,
            100);
    }

    private static (long UsedKb, long TotalKb, int Percent) ParseMemoryInfo(string? memInfoText)
    {
        if (string.IsNullOrWhiteSpace(memInfoText))
        {
            return (0L, 0L, 0);
        }

        long totalKb = 0;
        long availableKb = 0;

        foreach (var line in memInfoText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
            {
                totalKb = ParseFirstLongToken(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
            {
                availableKb = ParseFirstLongToken(line);
            }
        }

        if (totalKb <= 0)
        {
            return (0L, 0L, 0);
        }

        var usedKb = Math.Max(0L, totalKb - availableKb);
        var percent = (int)Math.Clamp(
            (int)Math.Round(100d * usedKb / totalKb, MidpointRounding.AwayFromZero),
            0,
            100);

        return (usedKb, totalKb, percent);
    }

    private static (long UsedKb, long TotalKb, int Percent) ParseDiskUsage(string? diskText)
    {
        if (string.IsNullOrWhiteSpace(diskText))
        {
            return (0L, 0L, 0);
        }

        var line = diskText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(line))
        {
            return (0L, 0L, 0);
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return (0L, 0L, 0);
        }

        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalKb) ||
            !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var usedKb) ||
            totalKb <= 0)
        {
            return (0L, 0L, 0);
        }

        var percent = (int)Math.Clamp(
            (int)Math.Round(100d * usedKb / totalKb, MidpointRounding.AwayFromZero),
            0,
            100);

        return (usedKb, totalKb, percent);
    }

    private static long ParseFirstLongToken(string line)
    {
        foreach (var token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0L;
    }

    private static string NormalizeRuntimeDistribution(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Linux";
        }

        var match = Regex.Match(value, @"Ubuntu\s+\d+\.\d+", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Value;
        }

        return value.Replace(" GNU/Linux", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static string FormatRuntimeGb(long kilobytes, int decimals)
    {
        if (kilobytes <= 0)
        {
            return "0";
        }

        var gigabytes = kilobytes / 1024d / 1024d;
        return gigabytes.ToString(decimals == 0 ? "0" : $"0.{new string('0', decimals)}", CultureInfo.InvariantCulture);
    }

    private static string GetWindowsDiskUsageLabel(string? installLocation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installLocation))
            {
                return "- / -";
            }

            var root = Path.GetPathRoot(installLocation);
            if (string.IsNullOrWhiteSpace(root))
            {
                return "- / -";
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return "- / -";
            }

            var usedBytes = Math.Max(0L, drive.TotalSize - drive.AvailableFreeSpace);
            return $"{FormatBytesAsGb(usedBytes, decimals: 0)} / {FormatBytesAsGb(drive.TotalSize, decimals: 0)} GB";
        }
        catch
        {
            return "- / -";
        }
    }

    private static string FormatBytesAsGb(long bytes, int decimals)
    {
        if (bytes <= 0)
        {
            return "0";
        }

        var gigabytes = bytes / 1024d / 1024d / 1024d;
        return gigabytes.ToString(decimals == 0 ? "0" : $"0.{new string('0', decimals)}", CultureInfo.InvariantCulture);
    }

    public void OpenNymphsBrainModelManager(InstallSettings settings)
    {
        var modelManagerPath = $"{settings.BrainInstallRoot}/bin/lms-model";

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wt.exe",
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("new-tab");
            startInfo.ArgumentList.Add("--title");
            startInfo.ArgumentList.Add("Nymphs-Brain Model Manager");
            startInfo.ArgumentList.Add("wsl.exe");
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(settings.DistroName);
            startInfo.ArgumentList.Add("--user");
            startInfo.ArgumentList.Add(settings.LinuxUser);
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(modelManagerPath);
            System.Diagnostics.Process.Start(startInfo);
        }
        catch
        {
            var fallbackCommand =
                "start \"Nymphs-Brain Model Manager\" wsl.exe " +
                $"-d {QuoteWindowsCommandArgument(settings.DistroName)} " +
                $"--user {QuoteWindowsCommandArgument(settings.LinuxUser)} " +
                "-- " +
                QuoteWindowsCommandArgument(modelManagerPath);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {fallbackCommand}",
                UseShellExecute = false,
            });
        }
    }

    private static void OpenWslFolder(InstallSettings settings, string linuxPath)
    {
        OpenWslPath(settings, linuxPath);
    }

    private static void OpenWslPath(InstallSettings settings, string linuxPath)
    {
        var explorerPath = ToWindowsWslPath(settings, linuxPath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = explorerPath,
            UseShellExecute = true,
        });
    }

    public async Task RunSystemDependenciesOnlyAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var runtimeScript = RequireScript("run_finalize_in_distro.ps1");
        var arguments = new List<string>
        {
            "-DistroName", settings.DistroName,
            "-LinuxUser", settings.LinuxUser,
            "-SystemOnly",
        };

        progress.Report("System dependencies: checking base Linux packages needed by optional modules.");
        progress.Report("System dependencies note: this may run apt briefly if the existing distro was created from an older base image.");

        var result = await RunPowerShellScriptAsync(
            runtimeScript,
            arguments,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("System dependency setup failed.");
        }
    }

    public async Task<IReadOnlyList<NymphModuleUpdateInfo>> CheckNymphModuleRegistryUpdatesAsync(
        InstallSettings settings,
        IEnumerable<string> installedModuleIds,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var installedIds = installedModuleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        progress.Report("Checking Nymphs registry for module updates...");

        using var registryDocument = await FetchJsonDocumentAsync(NymphModuleRegistryUrl, cancellationToken).ConfigureAwait(false);
        var results = new List<NymphModuleUpdateInfo>();

        foreach (var moduleElement in registryDocument.RootElement.GetProperty("modules").EnumerateArray())
        {
            var moduleId = GetJsonString(moduleElement, "id")?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(moduleId) || !installedIds.Contains(moduleId))
            {
                continue;
            }

            var manifestUrl = GetJsonString(moduleElement, "manifest_url")?.Trim();
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                results.Add(new NymphModuleUpdateInfo(moduleId, null, null, false, "Registry entry has no manifest URL."));
                continue;
            }

            using var remoteManifest = await FetchJsonDocumentAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            var remoteVersion = GetJsonString(remoteManifest.RootElement, "version");
            var installedVersion = ReadInstalledModuleVersion(settings, moduleId);
            var hasUpdate = IsRemoteVersionNewer(installedVersion, remoteVersion);
            var detail = hasUpdate
                ? $"{moduleId}: update available {installedVersion ?? "unknown"} -> {remoteVersion ?? "unknown"}"
                : $"{moduleId}: current ({installedVersion ?? "unknown installed"}, remote {remoteVersion ?? "unknown"})";

            progress.Report(detail);
            results.Add(new NymphModuleUpdateInfo(moduleId, installedVersion, remoteVersion, hasUpdate, detail));
        }

        return results;
    }

    public string? GetInstalledNymphModuleVersion(InstallSettings settings, string moduleId)
    {
        return ReadInstalledModuleVersion(settings, moduleId);
    }

    public InstalledNymphModuleUiInfo? GetInstalledNymphModuleUiInfo(InstallSettings settings, string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);
        var markerPath = ToWindowsWslPath(settings, $"{installRoot}/.nymph-module-version");
        if (!File.Exists(markerPath))
        {
            return null;
        }

        var manifestPath = ToWindowsWslPath(settings, $"{installRoot}/nymph.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("ui", out var uiElement) ||
            uiElement.ValueKind != JsonValueKind.Object ||
            !uiElement.TryGetProperty("manager_ui", out var managerUiElement) ||
            managerUiElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = GetJsonString(managerUiElement, "type")?.Trim();
        if (!string.Equals(type, "local_html", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var entrypoint = (GetJsonString(managerUiElement, "entrypoint") ?? string.Empty)
            .Trim()
            .Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(entrypoint) ||
            entrypoint.StartsWith("/", StringComparison.Ordinal) ||
            entrypoint.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            return null;
        }

        var windowsPath = ToWindowsWslPath(settings, $"{installRoot}/{entrypoint}");
        if (!File.Exists(windowsPath))
        {
            return null;
        }

        var title = GetJsonString(managerUiElement, "title")?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Module UI";
        }

        return new InstalledNymphModuleUiInfo(normalizedModuleId, title, entrypoint, windowsPath);
    }

    public async Task<NymphModuleManifestInfo?> GetNymphModuleManifestInfoAsync(
        string moduleId,
        CancellationToken cancellationToken)
    {
        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();

        using var registryDocument = await FetchJsonDocumentAsync(NymphModuleRegistryUrl, cancellationToken).ConfigureAwait(false);
        foreach (var moduleElement in registryDocument.RootElement.GetProperty("modules").EnumerateArray())
        {
            var registryModuleId = GetJsonString(moduleElement, "id")?.Trim().ToLowerInvariant();
            if (!string.Equals(registryModuleId, normalizedModuleId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestUrl = GetJsonString(moduleElement, "manifest_url")?.Trim();
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                return null;
            }

            using var manifestDocument = await FetchJsonDocumentAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            return BuildNymphModuleManifestInfo(moduleElement, manifestDocument.RootElement, manifestUrl);
        }

        return null;
    }

    public async Task<IReadOnlyList<NymphModuleManifestInfo>> GetNymphModuleRegistryManifestInfosAsync(
        CancellationToken cancellationToken)
    {
        using var registryDocument = await FetchJsonDocumentAsync(NymphModuleRegistryUrl, cancellationToken).ConfigureAwait(false);
        var modules = new List<NymphModuleManifestInfo>();

        foreach (var moduleElement in registryDocument.RootElement.GetProperty("modules").EnumerateArray())
        {
            if (moduleElement.TryGetProperty("trusted", out var trustedElement) &&
                trustedElement.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            var manifestUrl = GetJsonString(moduleElement, "manifest_url")?.Trim();
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                continue;
            }

            using var manifestDocument = await FetchJsonDocumentAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            modules.Add(BuildNymphModuleManifestInfo(moduleElement, manifestDocument.RootElement, manifestUrl));
        }

        return modules
            .OrderBy(module => module.SortOrder)
            .ThenBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static NymphModuleManifestInfo BuildNymphModuleManifestInfo(
        JsonElement registryElement,
        JsonElement manifestRoot,
        string manifestUrl)
    {
        var id = GetJsonString(manifestRoot, "id") ?? GetJsonString(registryElement, "id") ?? "";
        var name = GetJsonString(manifestRoot, "name") ?? GetJsonString(registryElement, "name") ?? id;
        var shortName = GetJsonString(manifestRoot, "short_name") ?? GetJsonString(registryElement, "short_name") ?? BuildShortName(id, name);
        var category = GetJsonString(manifestRoot, "category") ?? GetJsonString(registryElement, "category") ?? "";
        var kind = GetJsonString(manifestRoot, "packaging") ?? GetJsonString(manifestRoot, "kind") ?? GetJsonString(registryElement, "packaging") ?? "";
        var sourceSummary = "";
        var repositoryUrl = "";
        if (manifestRoot.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.Object)
        {
            sourceSummary = GetJsonString(sourceElement, "archive")
                ?? GetJsonString(sourceElement, "repo")
                ?? GetJsonString(sourceElement, "url")
                ?? "";
            repositoryUrl = GetJsonString(sourceElement, "repo")
                ?? GetJsonString(sourceElement, "repository")
                ?? "";
        }

        if (string.IsNullOrWhiteSpace(sourceSummary) &&
            manifestRoot.TryGetProperty("repo", out var repoElement) &&
            repoElement.ValueKind == JsonValueKind.Object)
        {
            sourceSummary = GetJsonString(repoElement, "url") ?? "";
        }

        if (string.IsNullOrWhiteSpace(repositoryUrl) &&
            manifestRoot.TryGetProperty("repo", out var repositoryElement) &&
            repositoryElement.ValueKind == JsonValueKind.Object)
        {
            repositoryUrl = GetJsonString(repositoryElement, "url") ?? "";
        }

        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            repositoryUrl = GetJsonString(registryElement, "repo_url")
                ?? GetJsonString(registryElement, "repository_url")
                ?? BuildGitHubRepositoryUrlFromManifestUrl(manifestUrl);
        }

        var installRoot = "";
        if (manifestRoot.TryGetProperty("install", out var installElement) && installElement.ValueKind == JsonValueKind.Object)
        {
            installRoot = GetJsonString(installElement, "root") ?? GetJsonString(installElement, "path") ?? "";
        }

        if (string.IsNullOrWhiteSpace(installRoot) &&
            manifestRoot.TryGetProperty("runtime", out var runtimeElement) &&
            runtimeElement.ValueKind == JsonValueKind.Object)
        {
            installRoot = GetJsonString(runtimeElement, "install_root") ?? "";
        }

        if (string.IsNullOrWhiteSpace(installRoot))
        {
            installRoot = GetJsonString(registryElement, "install_root") ?? "";
        }

        var entrypointActions = ReadObjectPropertyNames(manifestRoot, "entrypoints");
        if (entrypointActions.Count == 0)
        {
            entrypointActions = ReadObjectPropertyNames(manifestRoot, "manager")
                .Where(action => !string.Equals(action, "page", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var capabilities = BuildCapabilities(entrypointActions);
        var sortOrder = 1000;
        if (manifestRoot.TryGetProperty("ui", out var uiElement) &&
            uiElement.ValueKind == JsonValueKind.Object &&
            uiElement.TryGetProperty("sort_order", out var sortElement) &&
            sortElement.TryGetInt32(out var parsedSortOrder))
        {
            sortOrder = parsedSortOrder;
        }
        else if (registryElement.TryGetProperty("sort_order", out var registrySortElement) &&
                 registrySortElement.TryGetInt32(out var parsedRegistrySortOrder))
        {
            sortOrder = parsedRegistrySortOrder;
        }

        return new NymphModuleManifestInfo(
            Id: id,
            Name: name,
            ShortName: shortName,
            Category: category,
            Kind: kind,
            Version: GetJsonString(manifestRoot, "version") ?? "",
            Description: GetJsonString(manifestRoot, "description") ?? GetJsonString(registryElement, "summary") ?? "",
            ManifestUrl: manifestUrl,
            RepositoryUrl: repositoryUrl,
            SourceSummary: sourceSummary,
            InstallRoot: installRoot,
            Capabilities: capabilities,
            DevCapabilities: ["check-upstream", "test-upstream", "package"],
            SortOrder: sortOrder);
    }

    private static string BuildGitHubRepositoryUrlFromManifestUrl(string manifestUrl)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri))
        {
            return "";
        }

        if (!string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        var parts = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return "";
        }

        return $"https://github.com/{parts[0]}/{parts[1]}";
    }

    private static IReadOnlyList<string> ReadObjectPropertyNames(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return element.EnumerateObject()
            .Select(property => property.Name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
    }

    private static IReadOnlyList<string> BuildCapabilities(IReadOnlyList<string> actions)
    {
        var actionSet = actions
            .Where(action => !string.Equals(action, "install", StringComparison.OrdinalIgnoreCase))
            .Where(action => !string.Equals(action, "uninstall", StringComparison.OrdinalIgnoreCase))
            .Where(action => !string.Equals(action, "update", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ordered = new List<string>();
        foreach (var standardAction in new[] { "status", "start", "stop", "open", "logs" })
        {
            if (actionSet.Remove(standardAction))
            {
                ordered.Add(standardAction);
            }
        }

        ordered.AddRange(actionSet.OrderBy(action => action, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    private static string BuildShortName(string id, string name)
    {
        var source = string.IsNullOrWhiteSpace(id) ? name : id;
        var letters = new string(source.Where(char.IsLetterOrDigit).Take(2).ToArray());
        return string.IsNullOrWhiteSpace(letters)
            ? "NY"
            : letters.ToUpperInvariant();
    }

    public async Task<string> RunNymphModuleUninstallAsync(
        InstallSettings settings,
        string moduleId,
        bool purge,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new ArgumentException("Module id is required.", nameof(moduleId));
        }

        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var stagedUninstallScriptPath = $"/tmp/nymphs-manager-uninstall-{normalizedModuleId}.sh";
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);

        var scriptArguments = new List<string>
        {
            "--module",
            normalizedModuleId,
            "--yes",
        };

        if (purge)
        {
            scriptArguments.Add("--purge");
        }

        progress.Report(purge
            ? $"Deleting module '{moduleId}' from the managed distro..."
            : $"Uninstalling module '{moduleId}' from the managed distro...");

        var result = await RunPackagedManagerScriptAsync(
            settings,
            "uninstall_nymph_module.sh",
            stagedUninstallScriptPath,
            scriptArguments,
            "uninstall",
            progress,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0 && !await WslPathExistsAsync(settings, installRoot, cancellationToken).ConfigureAwait(false))
        {
            progress.Report($"uninstall_exit_was_nonzero_but_install_root_is_absent={installRoot}");
            return result.CombinedOutput;
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"Module uninstall failed for '{moduleId}' with exit code {result.ExitCode}."
                : $"Module uninstall failed for '{moduleId}' with exit code {result.ExitCode}.\n\n{result.CombinedOutput.Trim()}";
            throw new InvalidOperationException(detail);
        }

        return result.CombinedOutput;
    }

    private static string GetNymphModuleInstallRoot(InstallSettings settings, string normalizedModuleId)
    {
        var homePath = $"/home/{settings.LinuxUser}";
        var cachedManifestPath = ToWindowsWslPath(
            settings,
            $"{homePath}/.cache/nymphs-modules/repos/{normalizedModuleId}/nymph.json");

        try
        {
            if (File.Exists(cachedManifestPath))
            {
                using var manifest = JsonDocument.Parse(File.ReadAllText(cachedManifestPath));
                var manifestRoot = manifest.RootElement;
                var installRoot = "";
                if (manifestRoot.TryGetProperty("install", out var installElement) &&
                    installElement.ValueKind == JsonValueKind.Object)
                {
                    installRoot = GetJsonString(installElement, "root") ?? GetJsonString(installElement, "path") ?? "";
                }

                if (string.IsNullOrWhiteSpace(installRoot) &&
                    manifestRoot.TryGetProperty("runtime", out var runtimeElement) &&
                    runtimeElement.ValueKind == JsonValueKind.Object)
                {
                    installRoot = GetJsonString(runtimeElement, "install_root") ?? "";
                }

                if (IsSafeModuleInstallRoot(homePath, installRoot))
                {
                    return installRoot.TrimEnd('/');
                }
            }
        }
        catch
        {
            // Fall back to the generic module folder if the cached manifest is unavailable or invalid.
        }

        return $"{homePath}/{normalizedModuleId}";
    }

    private static bool IsSafeModuleInstallRoot(string homePath, string? installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            return false;
        }

        var normalized = installRoot.Trim().Replace('\\', '/').TrimEnd('/');
        return normalized.StartsWith($"{homePath}/", StringComparison.Ordinal) &&
               !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private async Task<bool> WslPathExistsAsync(
        InstallSettings settings,
        string linuxPath,
        CancellationToken cancellationToken)
    {
        var result = await RunWslCommandAsync(
            settings,
            ["test", "-e", linuxPath],
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    public async Task RunNymphModuleInstallFromRegistryAsync(
        InstallSettings settings,
        string moduleId,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new ArgumentException("Module id is required.", nameof(moduleId));
        }

        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var stagedInstallScriptPath = $"/tmp/nymphs-manager-install-{normalizedModuleId}.sh";
        var scriptArguments = new List<string>
        {
            "--module",
            normalizedModuleId,
        };

        progress.Report($"Installing module '{moduleId}' from the Nymphs registry...");

        var result = await RunPackagedManagerScriptAsync(
            settings,
            "install_nymph_module_from_registry.sh",
            stagedInstallScriptPath,
            scriptArguments,
            "install",
            progress,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"Module registry install failed for '{moduleId}' with exit code {result.ExitCode}."
                : $"Module registry install failed for '{moduleId}' with exit code {result.ExitCode}.\n\n{result.CombinedOutput.Trim()}";
            throw new InvalidOperationException(detail);
        }
    }

    private async Task<CommandResult> RunPackagedManagerScriptAsync(
        InstallSettings settings,
        string scriptName,
        string stagedScriptPath,
        IReadOnlyList<string> scriptArguments,
        string label,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        await RunWslCommandAsync(
            settings,
            ["rm", "-f", stagedScriptPath],
            progress: null,
            cancellationToken).ConfigureAwait(false);

        var localScriptPath = GetPackagedManagerScriptPath(scriptName);
        if (string.IsNullOrWhiteSpace(localScriptPath))
        {
            throw new FileNotFoundException(
                $"Packaged Manager script '{scriptName}' was not found in '{ScriptsDirectory}'. Rebuild the Manager package.",
                Path.Combine(ScriptsDirectory, scriptName));
        }

        progress.Report($"staging_{label}_script_from_manager={localScriptPath}");
        var encodedScript = Convert.ToBase64String(File.ReadAllBytes(localScriptPath));
        var stageCommand =
            "set -euo pipefail; " +
            $"mkdir -p $(dirname {ToBashSingleQuoted(stagedScriptPath)}); " +
            $"printf %s {ToBashSingleQuoted(encodedScript)} | base64 -d > {ToBashSingleQuoted(stagedScriptPath)}; " +
            $"chmod +x {ToBashSingleQuoted(stagedScriptPath)}";
        var stageResult = await RunWslBashAsync(settings, stageCommand, progress, cancellationToken).ConfigureAwait(false);
        if (stageResult.ExitCode != 0)
        {
            return stageResult;
        }

        progress.Report($"staged_{label}_script={stagedScriptPath}");

        var runArguments = new List<string> { stagedScriptPath };
        runArguments.AddRange(scriptArguments);

        try
        {
            var commandArguments = new List<string> { "/bin/bash" };
            commandArguments.AddRange(runArguments);

            return await RunWslCommandAsync(
                settings,
                commandArguments,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await RunWslCommandAsync(
                settings,
                ["rm", "-f", stagedScriptPath],
                progress: null,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private string? GetPackagedManagerScriptPath(string scriptName)
    {
        scriptName = Path.GetFileName(scriptName);
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            return null;
        }

        var scriptPath = Path.Combine(ScriptsDirectory, scriptName);
        return File.Exists(scriptPath) ? scriptPath : null;
    }

    public async Task CancelActiveNymphModuleLifecyclesAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var homePath = $"/home/{settings.LinuxUser}";
        var actionRoot = $"{homePath}/.cache/nymphs-modules/actions";
        var bashCommand =
            "set +e; " +
            $"ACTION_ROOT={ToBashSingleQuoted(actionRoot)}; " +
            "collect_tree() { " +
            "  local root=\"$1\"; " +
            "  [[ \"$root\" =~ ^[0-9]+$ ]] || return 0; " +
            "  kill -0 \"$root\" >/dev/null 2>&1 || return 0; " +
            "  printf '%s\\n' \"$root\"; " +
            "  local child; " +
            "  for child in $(pgrep -P \"$root\" 2>/dev/null); do collect_tree \"$child\"; done; " +
            "}; " +
            "ROOTS=\"\"; " +
            "SELF_PID=$$; " +
            "if [[ -d \"$ACTION_ROOT\" ]]; then " +
            "  for state_file in \"$ACTION_ROOT\"/*.state; do " +
            "    [[ -f \"$state_file\" ]] || continue; " +
            "    pid=$(awk -F= '$1==\"pid\" {print $2; exit}' \"$state_file\" 2>/dev/null); " +
            "    [[ \"$pid\" =~ ^[0-9]+$ ]] && ROOTS=\"$ROOTS $pid\"; " +
            "  done; " +
            "fi; " +
            "while IFS= read -r pid; do ROOTS=\"$ROOTS $pid\"; done < <(ps -eo pid=,args= | awk -v self=\"$SELF_PID\" " +
            "'$1 != self && $0 !~ /awk/ && (/\\/tmp\\/nymphs-manager-(install|uninstall)-/ || /install_nymph_module_from_registry\\.sh/ || /uninstall_nymph_module\\.sh/ || /\\/\\.cache\\/nymphs-modules\\/repos\\/[^ ]+\\/scripts\\/(install|uninstall)_[^ ]+\\.sh/) {print $1}'); " +
            "PIDS=$(for root in $ROOTS; do collect_tree \"$root\"; done | awk '!seen[$0]++'); " +
            "if [[ -n \"$PIDS\" ]]; then " +
            "  count=$(printf '%s\\n' \"$PIDS\" | sed '/^$/d' | wc -l); " +
            "  echo stopping_active_module_lifecycle_jobs=$count; " +
            "  kill $PIDS >/dev/null 2>&1; " +
            "  sleep 0.5; " +
            "  for pid in $PIDS; do kill -0 \"$pid\" >/dev/null 2>&1 && kill -9 \"$pid\" >/dev/null 2>&1; done; " +
            "else " +
            "  echo stopping_active_module_lifecycle_jobs=0; " +
            "fi; " +
            "if [[ -d \"$ACTION_ROOT\" ]]; then " +
            "  for state_file in \"$ACTION_ROOT\"/*.state; do " +
            "    [[ -f \"$state_file\" ]] || continue; " +
            "    pid=$(awk -F= '$1==\"pid\" {print $2; exit}' \"$state_file\" 2>/dev/null); " +
            "    if [[ -z \"$pid\" ]] || ! kill -0 \"$pid\" >/dev/null 2>&1; then rm -f \"$state_file\"; fi; " +
            "  done; " +
            "fi";

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Active module lifecycle cleanup failed.");
        }
    }

    public async Task<string> RunNymphModuleActionAsync(
        InstallSettings settings,
        string moduleId,
        string action,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        return await RunNymphModuleActionAsync(
            settings,
            moduleId,
            action,
            Array.Empty<string>(),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> RunNymphModuleActionAsync(
        InstallSettings settings,
        string moduleId,
        string action,
        IReadOnlyList<string> actionArguments,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new ArgumentException("Module id is required.", nameof(moduleId));
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(normalizedAction, "^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException($"Unsupported module action: {action}", nameof(action));
        }

        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var homePath = $"/home/{settings.LinuxUser}";
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);
        var cacheRepo = $"{homePath}/.cache/nymphs-modules/repos/{normalizedModuleId}";
        var manifestPath = $"{cacheRepo}/nymph.json";
        var installedManifestPath = $"{installRoot}/nymph.json";
        var versionMarkerPath = $"{installRoot}/.nymph-module-version";
        var localBinEntrypoint = $"{homePath}/.local/bin/{normalizedModuleId}-{normalizedAction}";
        var installRootBinEntrypoint = $"{installRoot}/bin/{normalizedModuleId}-{normalizedAction}";
        var isStatusAction = string.Equals(normalizedAction, "status", StringComparison.OrdinalIgnoreCase);
        var commandTimeoutPrefix = isStatusAction ? "timeout 6s " : string.Empty;
        var actionArgumentSuffix = BuildSafeModuleActionArgumentSuffix(actionArguments);

        if (isStatusAction)
        {
            var activeLifecycleStatus = await GetActiveModuleLifecycleStatusAsync(
                settings,
                normalizedModuleId,
                homePath,
                installRoot,
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(activeLifecycleStatus))
            {
                return activeLifecycleStatus;
            }
        }

        var entrypointReader =
            "import json, os, sys\n" +
            "if not os.path.isfile(sys.argv[1]):\n" +
            "    raise SystemExit(0)\n" +
            "with open(sys.argv[1], 'r', encoding='utf-8') as handle:\n" +
            "    manifest = json.load(handle)\n" +
            "action = os.environ['MODULE_ACTION']\n" +
            "entrypoint = str(manifest.get('entrypoints', {}).get(action, '')).strip()\n" +
            "if not entrypoint:\n" +
            "    entrypoint = str(manifest.get('manager', {}).get(action, '')).strip()\n" +
            "if entrypoint and (entrypoint.startswith('/') or '..' in entrypoint.split('/')):\n" +
            "    raise SystemExit('unsafe module entrypoint')\n" +
            "print(entrypoint)\n";
        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted(homePath)}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "ENTRYPOINT=\"\"; " +
            $"if [[ -f {ToBashSingleQuoted(manifestPath)} ]]; then " +
            $"ENTRYPOINT=$(MODULE_ACTION={ToBashSingleQuoted(normalizedAction)} python3 -c {ToBashSingleQuoted(entrypointReader)} {ToBashSingleQuoted(manifestPath)} 2>/dev/null || true); " +
            $"elif [[ -f {ToBashSingleQuoted(installedManifestPath)} ]]; then " +
            $"ENTRYPOINT=$(MODULE_ACTION={ToBashSingleQuoted(normalizedAction)} python3 -c {ToBashSingleQuoted(entrypointReader)} {ToBashSingleQuoted(installedManifestPath)} 2>/dev/null || true); " +
            "fi; " +
            $"if [[ -n \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(cacheRepo)}/\"$ENTRYPOINT\" ]]; then " +
            $"  {commandTimeoutPrefix}bash {ToBashSingleQuoted(cacheRepo)}/\"$ENTRYPOINT\"{actionArgumentSuffix}; " +
            $"elif [[ -n \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(installRoot)}/\"$ENTRYPOINT\" ]]; then " +
            $"  {commandTimeoutPrefix}bash {ToBashSingleQuoted(installRoot)}/\"$ENTRYPOINT\"{actionArgumentSuffix}; " +
            $"elif [[ {(isStatusAction ? $"-f {ToBashSingleQuoted(versionMarkerPath)} && " : string.Empty)}-x {ToBashSingleQuoted(localBinEntrypoint)} ]]; then " +
            $"  {commandTimeoutPrefix}{ToBashSingleQuoted(localBinEntrypoint)}{actionArgumentSuffix}; " +
            $"elif [[ {(isStatusAction ? $"-f {ToBashSingleQuoted(versionMarkerPath)} && " : string.Empty)}-x {ToBashSingleQuoted(installRootBinEntrypoint)} ]]; then " +
            $"  {commandTimeoutPrefix}{ToBashSingleQuoted(installRootBinEntrypoint)}{actionArgumentSuffix}; " +
            "else " +
            (isStatusAction
                ? $"  echo id={ToBashSingleQuoted(normalizedModuleId)}; echo installed=false; echo running=false; echo version=not-installed; echo state=available; echo health=unknown; echo install_root={ToBashSingleQuoted(installRoot)}; echo detail={ToBashSingleQuoted("Module is available from the registry, but is not installed yet.")}; exit 0; "
                : $"  echo {ToBashSingleQuoted($"Module action is not available: {normalizedModuleId}/{normalizedAction}")} >&2; exit 5; ") +
            "fi";

        progress.Report($"Running module action '{normalizedAction}' for '{normalizedModuleId}'...");

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"Module action '{normalizedAction}' failed for '{normalizedModuleId}' with exit code {result.ExitCode}."
                : $"Module action '{normalizedAction}' failed for '{normalizedModuleId}' with exit code {result.ExitCode}.\n\n{result.CombinedOutput.Trim()}";
            throw new InvalidOperationException(detail);
        }

        return result.CombinedOutput.Trim();
    }

    private static string BuildSafeModuleActionArgumentSuffix(IReadOnlyList<string> actionArguments)
    {
        if (actionArguments.Count == 0)
        {
            return string.Empty;
        }

        var safeArguments = new List<string>();
        foreach (var argument in actionArguments)
        {
            if (string.IsNullOrWhiteSpace(argument) ||
                argument.Length > 200 ||
                argument.Any(char.IsControl))
            {
                throw new ArgumentException("Unsafe module action argument.");
            }

            safeArguments.Add(ToBashSingleQuoted(argument.Trim()));
        }

        return " " + string.Join(" ", safeArguments);
    }

    private async Task<string?> GetActiveModuleLifecycleStatusAsync(
        InstallSettings settings,
        string normalizedModuleId,
        string homePath,
        string installRoot,
        CancellationToken cancellationToken)
    {
        var actionStateFile = $"{homePath}/.cache/nymphs-modules/actions/{normalizedModuleId}.state";
        var versionMarkerPath = $"{installRoot}/.nymph-module-version";
        var bashCommand =
            "set -euo pipefail; " +
            $"STATE_FILE={ToBashSingleQuoted(actionStateFile)}; " +
            $"VERSION_MARKER={ToBashSingleQuoted(versionMarkerPath)}; " +
            $"MODULE_ID={ToBashSingleQuoted(normalizedModuleId)}; " +
            "if [[ ! -f \"$STATE_FILE\" ]]; then " +
            "  found=$(ps -eo pid=,args= | awk -v self=\"$$\" -v module=\"$MODULE_ID\" '$1 != self && $0 !~ /awk/ { pid=$1; line=$0; if (index(line, \"/tmp/nymphs-manager-install-\" module \".sh\") || index(line, \"/repos/\" module \"/scripts/install_\")) { print pid \" install\"; exit } if (index(line, \"/tmp/nymphs-manager-uninstall-\" module \".sh\") || index(line, \"/repos/\" module \"/scripts/uninstall_\")) { print pid \" uninstall\"; exit } }'); " +
            "  pid=${found%% *}; action=${found##* }; " +
            "  if [[ -n \"$pid\" ]] && kill -0 \"$pid\" >/dev/null 2>&1; then " +
            "    state=installing; detail=\"Module lifecycle action is still running.\"; " +
            "    [[ \"$action\" == uninstall ]] && state=uninstalling; " +
            "    installed=false; version=in-progress; " +
            "    if [[ -f \"$VERSION_MARKER\" ]]; then installed=true; version=$(cat \"$VERSION_MARKER\" 2>/dev/null || printf unknown); fi; " +
            "    echo id=$MODULE_ID; " +
            "    echo installed=$installed; " +
            "    echo running=false; " +
            "    echo version=$version; " +
            "    echo state=$state; " +
            "    echo health=busy; " +
            "    echo action=$action; " +
            "    echo action_pid=$pid; " +
            "    echo detail=$detail; " +
            "    exit 0; " +
            "  fi; " +
            "  exit 0; " +
            "fi; " +
            "module=\"\"; action=\"\"; status=\"\"; pid=\"\"; detail=\"\"; started_at=\"\"; " +
            "while IFS='=' read -r key value; do " +
            "  case \"$key\" in " +
            "    module) module=\"$value\" ;; " +
            "    action) action=\"$value\" ;; " +
            "    status) status=\"$value\" ;; " +
            "    pid) pid=\"$value\" ;; " +
            "    detail) detail=\"$value\" ;; " +
            "    started_at) started_at=\"$value\" ;; " +
            "  esac; " +
            "done < \"$STATE_FILE\"; " +
            "if [[ -n \"$pid\" ]] && kill -0 \"$pid\" >/dev/null 2>&1; then " +
            "  state=\"$action\"; " +
            "  case \"$action\" in install) state=installing ;; update) state=updating ;; uninstall) state=uninstalling ;; delete) state=deleting ;; esac; " +
            "  installed=false; version=in-progress; " +
            "  if [[ -f \"$VERSION_MARKER\" ]]; then installed=true; version=$(cat \"$VERSION_MARKER\" 2>/dev/null || printf unknown); fi; " +
            "  echo id=${module:-" + ToBashSingleQuoted(normalizedModuleId) + "}; " +
            "  echo installed=$installed; " +
            "  echo running=false; " +
            "  echo version=$version; " +
            "  echo state=${state:-module-action}; " +
            "  echo health=busy; " +
            "  echo action=${action:-module-action}; " +
            "  echo action_pid=$pid; " +
            "  echo action_started_at=$started_at; " +
            "  echo detail=${detail:-Module lifecycle action is still running.}; " +
            "  exit 0; " +
            "fi; " +
            "rm -f \"$STATE_FILE\"; " +
            "exit 0";

        var result = await RunWslBashAsync(settings, bashCommand, progress: null, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.CombinedOutput)
            ? result.CombinedOutput.Trim()
            : null;
    }

    public async Task BootstrapWslAsync(
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("WSL setup: checking whether Windows WSL is already available.");

        var status = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments: ["--status"],
            workingDirectory: Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (status.ExitCode == 0)
        {
            progress.Report("WSL setup: WSL is already available on this machine.");
            return;
        }

        progress.Report("WSL setup: installing Windows WSL support without adding a separate Linux distro.");

        var installResult = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments: ["--install", "--no-distribution"],
            workingDirectory: Environment.SystemDirectory,
            progress: progress,
            environmentVariables: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (installResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(installResult.CombinedOutput)
                    ? "WSL setup failed."
                    : $"WSL setup failed. {installResult.CombinedOutput.Trim()}");
        }

        progress.Report("WSL setup command completed. If Windows asks for a restart, restart Windows and rerun the installer.");
    }

    public async Task<bool> ManagedDistroExistsAsync(CancellationToken cancellationToken)
    {
        return !string.IsNullOrWhiteSpace(await GetExistingManagedDistroNameAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<string?> GetExistingManagedDistroNameAsync(CancellationToken cancellationToken)
    {
        var distros = await GetWslDistroNamesAsync(cancellationToken).ConfigureAwait(false);
        return FindManagedDistroName(distros);
    }

    public void OpenLogFolder()
    {
        Directory.CreateDirectory(LogFolderPath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = LogFolderPath,
            UseShellExecute = true,
        });
    }

    public void OpenReadme()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = ReadmeUrl,
            UseShellExecute = true,
        });
    }

    public void OpenSourceRepo()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = SourceRepoUrl,
            UseShellExecute = true,
        });
    }

    public void OpenGuide()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = GuideUrl,
            UseShellExecute = true,
        });
    }

    public void OpenFootprintDoc()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = FootprintDocUrl,
            UseShellExecute = true,
        });
    }

    public void OpenAddonGuide()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = AddonGuideUrl,
            UseShellExecute = true,
        });
    }

    private SystemCheckItem CheckAdministratorStatus()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

#if DEBUG
        if (!isAdmin)
        {
            return new SystemCheckItem(
                "Administrator access",
                "The real manager is expected to run elevated.",
                CheckState.Warning,
                "This Debug build is running without elevation so dotnet run works. The Release manager will request administrator access automatically.");
        }
#endif

        return new SystemCheckItem(
            "Administrator access",
            "The manager app is expected to run elevated.",
            isAdmin ? CheckState.Pass : CheckState.Fail,
            isAdmin ? "Administrator privileges are active." : "Restart the app as Administrator.");
    }

    private async Task<SystemCheckItem> CheckBaseTarPresenceAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(BaseTarPath))
        {
            var fileInfo = new FileInfo(BaseTarPath);
            var sizeGiB = fileInfo.Length / (1024d * 1024d * 1024d);
            return new SystemCheckItem(
                "Base distro package",
                "Checks whether a prebuilt base distro package is available.",
                CheckState.Pass,
                $"Found {fileInfo.Name} at {fileInfo.FullName} ({sizeGiB:F1} GB).");
        }

        try
        {
            if (await ManagedDistroExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return new SystemCheckItem(
                    "Base distro package",
                    "Checks whether a prebuilt base distro package is available.",
                    CheckState.Warning,
                    $"Base distro tar was not found, but an existing managed {ManagedDistroName} distro was detected. Repair/continue can reuse the existing distro without replacing it.");
            }
        }
        catch
        {
            // Fall through to the no-tar bootstrap guidance if WSL inspection is unavailable.
        }

        return new SystemCheckItem(
            "Base distro package",
            "Checks whether a prebuilt base distro package is available.",
            CheckState.Warning,
            "Base distro tar was not found. The manager can bootstrap a fresh Ubuntu base locally instead. " +
            $"If you later place NymphsCore.tar at {BaseTarPath}, the installer will use it as a faster prebuilt path.");
    }

    private SystemCheckItem CheckDriveAvailability()
    {
        var drives = GetAvailableDrives();
        if (drives.Count == 0)
        {
            return new SystemCheckItem(
                "Install drives",
                "Checks that at least one fixed Windows drive is available.",
                CheckState.Fail,
                "No fixed drives were detected.");
        }

        var bestDrive = drives.OrderByDescending(drive => drive.FreeBytes).First();
        return new SystemCheckItem(
            "Install drives",
            "Checks that Windows install targets are available.",
            CheckState.Pass,
            $"Detected {drives.Count} fixed drive(s). Largest free space: {bestDrive.DisplayLabel}.");
    }

    private async Task<SystemCheckItem> CheckExistingWslDistrosAsync(CancellationToken cancellationToken)
    {
        const string title = "Existing WSL distros";
        const string description = "Warns if this machine already has one or more WSL distros installed.";

        try
        {
            var distros = await GetWslDistroNamesAsync(cancellationToken).ConfigureAwait(false);
            if (distros.Count == 0)
            {
                return new SystemCheckItem(
                    title,
                    description,
                    CheckState.Pass,
                    "No existing WSL distros were detected.",
                    key: ExistingWslDistrosCheckKey);
            }

            var distroList = string.Join(", ", distros);
            var existingManagedDistroName = FindManagedDistroName(distros);
            var details = !string.IsNullOrWhiteSpace(existingManagedDistroName)
                ? $"Detected existing distro(s): {distroList}. Existing managed {existingManagedDistroName} distro found. Rerunning the installer will reuse it for repair/continue instead of unregistering it."
                : $"Detected existing distro(s): {distroList}. This installer uses a separate managed {ManagedDistroName} distro and does not automatically remove your existing WSL distros.";

            return new SystemCheckItem(title, description, CheckState.Warning, details, key: ExistingWslDistrosCheckKey);
        }
        catch (Exception ex)
        {
            return new SystemCheckItem(title, description, CheckState.Warning, ex.Message, key: ExistingWslDistrosCheckKey);
        }
    }

    private async Task<SystemCheckItem> CheckWslAvailabilityAsync(CancellationToken cancellationToken)
    {
        const string title = "WSL availability";
        const string description = "Checks that WSL is installed and ready for this installer.";

        try
        {
            var result = await _processRunner.RunAsync(
                fileName: "wsl.exe",
                arguments: ["--status"],
                workingDirectory: Environment.SystemDirectory,
                progress: null,
                environmentVariables: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                var details = "Windows WSL is not ready on this machine yet. Use Set Up WSL to install Windows WSL support first.";
                if (!string.IsNullOrWhiteSpace(result.CombinedOutput))
                {
                    details += " " + result.CombinedOutput.Trim();
                }

                return new SystemCheckItem(
                    title,
                    description,
                    CheckState.Fail,
                    details,
                    key: WslAvailabilityCheckKey);
            }

            var lines = result.CombinedOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.Contains("WSL1 is not supported", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Contains("Please enable the \"Windows Subsystem for Linux\" optional component", StringComparison.OrdinalIgnoreCase))
                .ToList();

            string? defaultDistro = null;
            string? defaultVersion = null;

            foreach (var line in lines)
            {
                var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    continue;
                }

                if (parts[0].Equals("Default Distribution", StringComparison.OrdinalIgnoreCase))
                {
                    defaultDistro = parts[1];
                }
                else if (parts[0].Equals("Default Version", StringComparison.OrdinalIgnoreCase))
                {
                    defaultVersion = parts[1];
                }
            }

            var summaryParts = new List<string> { "WSL is installed and responding." };
            if (!string.IsNullOrWhiteSpace(defaultVersion))
            {
                summaryParts.Add($"Default version: WSL {defaultVersion}.");
            }
            if (!string.IsNullOrWhiteSpace(defaultDistro))
            {
                summaryParts.Add($"Default distro: {defaultDistro}.");
            }

            return new SystemCheckItem(title, description, CheckState.Pass, string.Join(" ", summaryParts), key: WslAvailabilityCheckKey);
        }
        catch (Exception ex)
        {
            return new SystemCheckItem(title, description, CheckState.Fail, ex.Message, key: WslAvailabilityCheckKey);
        }
    }

    private async Task<SystemCheckItem> CheckCommandAsync(
        string title,
        string description,
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                fileName,
                arguments,
                Environment.SystemDirectory,
                progress: null,
                environmentVariables: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                var details = string.IsNullOrWhiteSpace(result.CombinedOutput)
                    ? "Command completed successfully."
                    : result.CombinedOutput.Trim();

                return new SystemCheckItem(title, description, CheckState.Pass, details);
            }

            return new SystemCheckItem(
                title,
                description,
                CheckState.Fail,
                string.IsNullOrWhiteSpace(result.CombinedOutput)
                    ? $"Command exited with code {result.ExitCode}."
                    : result.CombinedOutput.Trim());
        }
        catch (Exception ex)
        {
            return new SystemCheckItem(title, description, CheckState.Fail, ex.Message);
        }
    }

    private async Task<SystemCheckItem> CheckNvidiaStatusAsync(CancellationToken cancellationToken)
    {
        const string title = "NVIDIA driver visibility";
        const string description = "Checks that the Windows NVIDIA runtime tooling is available.";

        try
        {
            var gpuList = await _processRunner.RunAsync(
                fileName: "nvidia-smi.exe",
                arguments:
                [
                    "--query-gpu=name,driver_version",
                    "--format=csv,noheader",
                ],
                workingDirectory: Environment.SystemDirectory,
                progress: null,
                environmentVariables: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (gpuList.ExitCode != 0)
            {
                return new SystemCheckItem(
                    title,
                    description,
                    CheckState.Fail,
                    string.IsNullOrWhiteSpace(gpuList.CombinedOutput)
                        ? "nvidia-smi exited with an error."
                        : gpuList.CombinedOutput.Trim());
            }

            var lines = gpuList.CombinedOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (lines.Count == 0)
            {
                return new SystemCheckItem(
                    title,
                    description,
                    CheckState.Fail,
                    "nvidia-smi ran but did not report any GPUs.");
            }

            var firstGpu = lines[0];
            var gpuCount = lines.Count;
            var details = gpuCount == 1
                ? $"Detected GPU: {firstGpu}"
                : $"Detected {gpuCount} NVIDIA GPUs. First GPU: {firstGpu}";

            return new SystemCheckItem(title, description, CheckState.Pass, details);
        }
        catch (Exception ex)
        {
            return new SystemCheckItem(title, description, CheckState.Fail, ex.Message);
        }
    }

    private async Task<CommandResult> RunPowerShellScriptAsync(
        string scriptPath,
        IEnumerable<string> scriptArguments,
        IProgress<string> progress,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", scriptPath,
        };
        arguments.AddRange(scriptArguments);

        return await _processRunner.RunAsync(
            fileName: "powershell.exe",
            arguments: arguments,
            workingDirectory: RepositoryRoot,
            progress: progress,
            environmentVariables: environmentVariables,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string?>? BuildInstallerEnvironment()
    {
        Dictionary<string, string?>? environmentVariables = null;

        var githubToken = ResolveGitHubTokenFromEnvironment();
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            environmentVariables ??= new Dictionary<string, string?>();
            environmentVariables["NYMPHS3D_GITHUB_TOKEN"] = githubToken;
        }

        return environmentVariables;
    }

    private static string? ResolveGitHubTokenFromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable("NYMPHS3D_GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        }

        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    private async Task<IReadOnlyList<string>> GetWslDistroNamesAsync(CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments: ["-l", "-q"],
            workingDirectory: Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.CombinedOutput)
                    ? "Could not determine whether existing WSL distros are present."
                    : result.CombinedOutput.Trim());
        }

        return result.CombinedOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<JsonDocument> FetchJsonDocumentAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _moduleRegistryHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? ReadInstalledModuleVersion(InstallSettings settings, string moduleId)
    {
        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);
        var markerCandidates = new[]
        {
            ToWindowsWslPath(settings, $"{installRoot}/.nymph-module-version"),
            ToWindowsWslPath(settings, $"/home/{settings.LinuxUser}/{normalizedModuleId}/.nymph-module-version"),
        };

        foreach (var markerPath in markerCandidates)
        {
            try
            {
                if (!File.Exists(markerPath))
                {
                    continue;
                }

                var markerVersion = File.ReadLines(markerPath).FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(markerVersion))
                {
                    return markerVersion;
                }
            }
            catch
            {
                // Try the next candidate. A missing or unreadable marker should not break update checks.
            }
        }

        var manifestCandidates = new[]
        {
            ToWindowsWslPath(settings, $"/home/{settings.LinuxUser}/.cache/nymphs-modules/{normalizedModuleId}.nymph.json"),
            ToWindowsWslPath(settings, $"/home/{settings.LinuxUser}/.cache/nymphs-modules/repos/{normalizedModuleId}/nymph.json"),
            ToWindowsWslPath(settings, $"{installRoot}/nymph.json"),
            ToWindowsWslPath(settings, $"/home/{settings.LinuxUser}/{normalizedModuleId}/nymph.json"),
        };

        foreach (var manifestPath in manifestCandidates)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                return GetJsonString(document.RootElement, "version");
            }
            catch
            {
                // Try the next candidate. A malformed local manifest should not break the whole update check.
            }
        }

        return null;
    }

    private static bool IsRemoteVersionNewer(string? installedVersion, string? remoteVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion) || string.IsNullOrWhiteSpace(remoteVersion))
        {
            return false;
        }

        var installedParts = ParseVersionParts(installedVersion);
        var remoteParts = ParseVersionParts(remoteVersion);
        var partCount = Math.Max(installedParts.Count, remoteParts.Count);

        for (var index = 0; index < partCount; index++)
        {
            var installedPart = index < installedParts.Count ? installedParts[index] : 0;
            var remotePart = index < remoteParts.Count ? remoteParts[index] : 0;
            if (remotePart > installedPart)
            {
                return true;
            }

            if (remotePart < installedPart)
            {
                return false;
            }
        }

        return false;
    }

    private static IReadOnlyList<int> ParseVersionParts(string version)
    {
        return Regex.Matches(version, @"\d+")
            .Select(match => int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .ToList();
    }

    private string RequireScript(string scriptName)
    {
        var scriptPath = Path.Combine(ScriptsDirectory, scriptName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Required script not found: {scriptPath}");
        }

        return scriptPath;
    }

    private static string ToBashSingleQuoted(string value)
    {
        return "'" + value.Replace("'", "'\\''") + "'";
    }

    private static string QuoteWindowsCommandArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string? ConvertWindowsPathToWsl(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return null;
        }

        var normalized = windowsPath.Replace('\\', '/');
        var uncPrefixes = new[] { "//wsl.localhost/", "//wsl$/" };
        foreach (var prefix in uncPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var pathStart = normalized.IndexOf('/', prefix.Length);
                if (pathStart <= prefix.Length || pathStart + 1 >= normalized.Length)
                {
                    return null;
                }

                var sourceDistroName = normalized[prefix.Length..pathStart];
                if (!string.Equals(sourceDistroName, ManagedDistroName, StringComparison.OrdinalIgnoreCase))
                {
                    // Do not silently turn a dev/source distro UNC path into a target-runtime path.
                    // Example: \\wsl.localhost\NymphsCore_Lite\...\script.sh is not executable
                    // inside the managed NymphsCore runtime distro.
                    return null;
                }

                return "/" + normalized[(pathStart + 1)..];
            }
        }

        if (normalized.Length >= 3 &&
            char.IsLetter(normalized[0]) &&
            normalized[1] == ':' &&
            normalized[2] == '/')
        {
            return $"/mnt/{char.ToLowerInvariant(normalized[0])}/{normalized[3..]}";
        }

        return null;
    }

    private static string ResolveRepositoryRoot()
    {
        var candidates = new List<string>();
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
        {
            candidates.Add(current);
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        candidates.Add(Directory.GetCurrentDirectory());

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(candidate, "scripts", "import_base_distro.ps1")))
            {
                return candidate;
            }
        }

        return AppContext.BaseDirectory;
    }

    private string ResolvePayloadDirectory()
    {
        var localPayload = Path.Combine(AppContext.BaseDirectory, "payload");
        if (Directory.Exists(localPayload))
        {
            return localPayload;
        }

        var repoPayload = Path.Combine(RepositoryRoot, "payload");
        if (Directory.Exists(repoPayload))
        {
            return repoPayload;
        }

        return localPayload;
    }

    private string ResolveBaseTarPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "NymphsCore.tar"),
            Path.Combine(PayloadDirectory, "NymphsCore.tar"),
            Path.Combine(RepositoryRoot, "payload", "NymphsCore.tar"),
            @"D:\WSL\NymphsCore.tar",
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static bool IsCanonicalManagedDistro(string distroName)
    {
        return string.Equals(distroName, ManagedDistroName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindManagedDistroName(IEnumerable<string> distros)
    {
        return distros
            .Where(IsCanonicalManagedDistro)
            .FirstOrDefault();
    }

    private async Task<string> GetNextAvailableManagedDistroAliasAsync(CancellationToken cancellationToken)
    {
        var distros = await GetWslDistroNamesAsync(cancellationToken).ConfigureAwait(false);
        var suffix = 2;
        while (distros.Any(name => string.Equals(name, ManagedDistroName + suffix, StringComparison.OrdinalIgnoreCase)))
        {
            suffix++;
        }

        return ManagedDistroName + suffix;
    }

    private static string BuildAliasedInstallLocation(string currentInstallLocation, string distroName)
    {
        if (string.IsNullOrWhiteSpace(currentInstallLocation))
        {
            return currentInstallLocation;
        }

        var trimmed = currentInstallLocation.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return currentInstallLocation;
        }

        return Path.Combine(parent, distroName);
    }

    private static void ReportImportSettings(IProgress<string> progress, InstallSettings settings)
    {
        if (settings.RepairExistingDistro)
        {
            progress.Report($"Existing managed distro detected: {settings.DistroName}");
            progress.Report("Reusing the existing managed distro for repair/continue.");
        }
        else
        {
            if (File.Exists(settings.TarPath))
            {
                progress.Report($"Using base tar: {settings.TarPath}");
            }
            else
            {
                progress.Report("Bootstrapping a fresh Ubuntu base locally.");
            }
            progress.Report($"Import target: {settings.DistroName} -> {settings.InstallLocation}");
        }

        progress.Report($"Linux user: {settings.LinuxUser}");
    }

    private async Task<CommandResult> RunImportScriptAsync(
        string importScript,
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-TarPath", settings.TarPath,
            "-DistroName", settings.DistroName,
            "-InstallLocation", settings.InstallLocation,
            "-LinuxUser", settings.LinuxUser,
        };

        if (settings.RepairExistingDistro)
        {
            arguments.Add("-RepairExisting");
        }
        else
        {
            arguments.Add("-Force");
        }

        return await RunPowerShellScriptAsync(
            importScript,
            arguments,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);
    }

    private string BuildUpdatedWslConfigContent(WslConfigValues config)
    {
        var lines = File.Exists(WslConfigPath)
            ? File.ReadAllLines(WslConfigPath).ToList()
            : new List<string>();

        UpsertIniKey(lines, "wsl2", "memory", $"{config.MemoryGb}GB");
        UpsertIniKey(lines, "wsl2", "processors", config.Processors.ToString());
        UpsertIniKey(lines, "wsl2", "swap", $"{config.SwapGb}GB");

        var content = string.Join(Environment.NewLine, lines).TrimEnd();
        return content + Environment.NewLine;
    }

    private static void UpsertIniKey(List<string> lines, string sectionName, string key, string value)
    {
        var sectionHeader = $"[{sectionName}]";
        var sectionStart = -1;
        var sectionEnd = lines.Count;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(trimmed, sectionHeader, StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i;
                continue;
            }

            if (sectionStart >= 0)
            {
                sectionEnd = i;
                break;
            }
        }

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add(sectionHeader);
            lines.Add($"{key}={value}");
            return;
        }

        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var existingKey = trimmed[..equalsIndex].Trim();
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                return;
            }
        }

        lines.Insert(sectionEnd, $"{key}={value}");
    }

    private static int? ParseIniInt(IReadOnlyList<string> lines, string sectionName, string key)
    {
        var raw = GetIniValue(lines, sectionName, key);
        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        return null;
    }

    private static int? ParseIniSizeGb(IReadOnlyList<string> lines, string sectionName, string key)
    {
        var raw = GetIniValue(lines, sectionName, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^2];
        }
        else if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^2];
            if (int.TryParse(trimmed, out var mb) && mb > 0)
            {
                return Math.Max(1, (int)Math.Ceiling(mb / 1024d));
            }
        }

        if (int.TryParse(trimmed, out var gb) && gb > 0)
        {
            return gb;
        }

        return null;
    }

    private static string? GetIniValue(IReadOnlyList<string> lines, string sectionName, string key)
    {
        var inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                inSection = string.Equals(trimmed, $"[{sectionName}]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection || trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var existingKey = trimmed[..equalsIndex].Trim();
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[(equalsIndex + 1)..].Trim();
            }
        }

        return null;
    }

    private static ulong GetTotalPhysicalMemoryBytes()
    {
        var memoryStatus = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(memoryStatus))
        {
            throw new InvalidOperationException("Could not read total physical memory from Windows.");
        }

        return memoryStatus.TotalPhys;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        }

        public uint Length;

        public uint MemoryLoad;

        public ulong TotalPhys;

        public ulong AvailPhys;

        public ulong TotalPageFile;

        public ulong AvailPageFile;

        public ulong TotalVirtual;

        public ulong AvailVirtual;

        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);
}
