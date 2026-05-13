using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Net.Sockets;
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
    private readonly HttpClient _aiToolkitHttpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:8675"),
    };
    private readonly HttpClient _moduleRegistryHttpClient = new();
    private static readonly Regex TrainerYamlPresetIdRegex = new(@"^\s*#\s*nymphs_preset_id:\s*(?<value>[^\r\n#]+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlStepsRegex = new(@"^\s*steps:\s*(?<value>\d+)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlLearningRateRegex = new(@"^\s*lr:\s*(?<value>[^\s#]+)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlRankRegex = new(@"^\s*linear:\s*(?<value>\d+)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlContentStyleRegex = new(@"^\s*content_or_style:\s*[""']?(?<value>[^""'\r\n#]+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlLowVramRegex = new(@"^\s*low_vram:\s*(?<value>true|false)\s*$", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex TrainerYamlSaveEveryRegex = new(@"^\s*save_every:\s*(?<value>\d+)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlMaxSavesRegex = new(@"^\s*max_step_saves_to_keep:\s*(?<value>\d+)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlAdapterPathRegex = new(@"^\s*assistant_lora_path:\s*[""']?(?<value>[^""'\r\n#]+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TrainerYamlSamplePromptRegex = new(@"^\s*-\s*prompt:\s*(?<value>.+?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private sealed record TrustedModuleSourceInfo(string RepositoryUrl, string Branch);

    public InstallerWorkflowService()
    {
        _moduleRegistryHttpClient.Timeout = TimeSpan.FromSeconds(8);
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
        var wslAvailabilityTask = RunSystemCheckWithTimeoutAsync(
            CheckWslAvailabilityAsync,
            TimeSpan.FromSeconds(4),
            new SystemCheckItem(
                "WSL availability",
                "Checks that WSL is installed and ready for this installer.",
                CheckState.Fail,
                "WSL did not respond within 4 seconds. Startup is continuing; refresh again once WSL has settled.",
                key: WslAvailabilityCheckKey),
            cancellationToken);

        var distroTask = RunSystemCheckWithTimeoutAsync(
            CheckExistingWslDistrosAsync,
            TimeSpan.FromSeconds(4),
            new SystemCheckItem(
                "Existing WSL distros",
                "Warns if this machine already has one or more WSL distros installed.",
                CheckState.Warning,
                "WSL distro listing did not respond within 4 seconds. Startup is continuing.",
                key: ExistingWslDistrosCheckKey),
            cancellationToken);

        var nvidiaTask = RunSystemCheckWithTimeoutAsync(
            CheckNvidiaStatusAsync,
            TimeSpan.FromSeconds(3),
            new SystemCheckItem(
                "NVIDIA driver visibility",
                "Checks that the Windows NVIDIA runtime tooling is available.",
                CheckState.Warning,
                "nvidia-smi did not respond within 3 seconds. GPU status can be checked again after startup."),
            cancellationToken);

        await Task.WhenAll(wslAvailabilityTask, distroTask, nvidiaTask).ConfigureAwait(false);

        var items = new List<SystemCheckItem>
        {
            CheckAdministratorStatus(),
            CheckDriveAvailability(),
            wslAvailabilityTask.Result,
            distroTask.Result,
            nvidiaTask.Result,
        };

        return items;
    }

    private static async Task<SystemCheckItem> RunSystemCheckWithTimeoutAsync(
        Func<CancellationToken, Task<SystemCheckItem>> checkFactory,
        TimeSpan timeout,
        SystemCheckItem timeoutResult,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            return await checkFactory(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return timeoutResult;
        }
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
            "-SkipVerify",
        };

        progress.Report("Runtime setup: preparing the required runtime environments inside the managed distro");
        progress.Report("Runtime setup note: this stage can sit on one line for a long time during apt work, CUDA setup, Python environment creation, or Flash Attention builds. On some machines the Flash Attention step can take hours. That does not mean the installer is frozen.");

        if (!settings.PrefetchModelsNow)
        {
            arguments.Add("-SkipModels");
            progress.Report("Model prefetch: off for now. Models can download later on first server start.");
        }
        else
        {
            progress.Report("Model prefetch: required models will be downloaded now.");
            if (string.IsNullOrWhiteSpace(settings.HuggingFaceToken))
            {
                progress.Report("Hugging Face token: none provided. Public downloads can still work, but they may be slower or more rate-limited.");
            }
            else
            {
                progress.Report($"Hugging Face token: provided for installer-time model downloads ({settings.HuggingFaceToken.Length} chars after trimming).");
            }
        }

        var environmentVariables = BuildInstallerEnvironment(settings);
        var githubToken = ResolveGitHubTokenFromEnvironment();
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            progress.Report($"GitHub token: provided for private backend repo clones ({githubToken.Length} chars after trimming).");
        }

        var result = await RunPowerShellScriptAsync(
            runtimeScript,
            arguments,
            progress,
            environmentVariables,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Runtime setup failed.");
        }
    }

    public async Task RunNymphsBrainInstallAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (!settings.InstallNymphsBrain)
        {
            progress.Report("Nymphs-Brain: skipped. Experimental local LLM stack was not selected.");
            return;
        }

        var brainScript = RequireScript("install_nymphs_brain.sh");
        var wslBrainScriptPath = ConvertWindowsPathToWsl(brainScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {brainScript}");

        var scriptArguments = new List<string>
        {
            "--install-root", settings.BrainInstallRoot,
            "--quiet",
        };

        if (settings.DownloadBrainModelNow)
        {
            scriptArguments.Add("--download-model");
        }

        if (!string.IsNullOrWhiteSpace(settings.OpenRouterApiKey))
        {
            scriptArguments.Add("--openrouter-api-key");
            scriptArguments.Add(settings.OpenRouterApiKey);
        }

        // Detect actual GPU VRAM from Windows (not WSL) for correct LLM recommendations
        var gpuVramMb = GetDetectedGpuVramMb();

        var bashCommandBuilder = new StringBuilder()
            .Append("set -euo pipefail; ")
            .Append($"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; ")
            .Append($"export USER={ToBashSingleQuoted(settings.LinuxUser)}; ")
            .Append($"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; ")
            .Append("export NYMPHS3D_RUNTIME_ROOT=\"$HOME\"; ")
            .Append($"export NYMPHS3D_GPU_VRAM_MB=\"{gpuVramMb}\"; ")
            .Append("bash ")
            .Append(ToBashSingleQuoted(wslBrainScriptPath))
            .Append(' ')
            .Append(string.Join(" ", scriptArguments.Select(ToBashSingleQuoted)));

        var bashCommand = bashCommandBuilder.ToString();

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report($"Nymphs-Brain: installing experimental local LLM stack to {settings.BrainInstallRoot}.");
        progress.Report(gpuVramMb > 0
            ? $"Nymphs-Brain: detected {gpuVramMb} MB GPU VRAM from Windows for model recommendations."
            : "Nymphs-Brain: GPU VRAM detection failed, using WSL fallback.");
        progress.Report("Nymphs-Brain: tools will be installed now. Use the Brain page Manage Models action after install to choose the local model and optional remote llm-wrapper model.");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Nymphs-Brain install failed.");
        }
    }

#if LEGACY_MANAGER_MODULE_TOOLS
    public async Task RunZImageTrainerInstallAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var trainerScript = RequireScript("install_zimage_trainer_aitk.sh");
        var wslTrainerScriptPath = ConvertWindowsPathToWsl(trainerScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {trainerScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export ZIMAGE_TRAINER_ROOT=\"$HOME/ZImage-Trainer\"; " +
            "export ZIMAGE_DATASET_ROOT=\"$HOME/ZImage-Trainer/datasets\"; " +
            "export ZIMAGE_LORA_ROOT=\"$HOME/ZImage-Trainer/loras\"; " +
            $"bash {ToBashSingleQuoted(wslTrainerScriptPath)}";

        progress.Report("Z-Image Trainer: installing AI Toolkit sidecar in an isolated venv.");
        progress.Report("Z-Image Trainer: default method is Z-Image Turbo LoRA training with the Turbo training adapter.");

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Z-Image Trainer install failed.");
        }

        await SyncZImageTrainerSupportFilesAsync(settings, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ZImageTrainerStatus> GetZImageTrainerStatusAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        string? selectedJobRef = null)
    {
        var statusScript = RequireScript("zimage_trainer_status.sh");
        var wslStatusScriptPath = ConvertWindowsPathToWsl(statusScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {statusScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export ZIMAGE_TRAINER_ROOT=\"$HOME/ZImage-Trainer\"; " +
            "export ZIMAGE_DATASET_ROOT=\"$HOME/ZImage-Trainer/datasets\"; " +
            "export ZIMAGE_LORA_ROOT=\"$HOME/ZImage-Trainer/loras\"; " +
            $"bash {ToBashSingleQuoted(wslStatusScriptPath)}";

        var result = await RunWslBashAsync(settings, bashCommand, progress: null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Z-Image Trainer status check failed.");
        }

        var status = ParseZImageTrainerStatus(result.CombinedOutput);
        return await EnrichZImageTrainerStatusFromAiToolkitAsync(
            settings,
            status,
            selectedJobRef,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> GetZImageTrainerDatasetNamesAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var datasetsWindowsPath = ToWindowsWslPath(settings, $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets");
        if (string.IsNullOrWhiteSpace(datasetsWindowsPath) || !Directory.Exists(datasetsWindowsPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var datasetNames = Directory.GetDirectories(datasetsWindowsPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(datasetNames);
    }

    public async Task<ZImageTrainerJobSettings?> GetZImageTrainerJobSettingsAsync(
        InstallSettings settings,
        string loraName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var officialUiJobConfig = await TryGetOfficialUiJobConfigJsonAsync(
            settings,
            normalizedLora,
            cancellationToken).ConfigureAwait(false);
        var officialUiSettings = ParseZImageTrainerJobSettingsFromOfficialUiJobConfig(normalizedLora, officialUiJobConfig);
        if (officialUiSettings is not null)
        {
            return officialUiSettings;
        }

        return TryReadZImageTrainerJobSettingsFromYaml(settings, normalizedLora);
    }

#endif

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

    private static ZImageTrainerJobSettings? TryReadZImageTrainerJobSettingsFromYaml(
        InstallSettings settings,
        string normalizedLora)
    {
        var jobWindowsPath = ToWindowsWslPath(settings, $"/home/{settings.LinuxUser}/ZImage-Trainer/jobs/{normalizedLora}.yaml");
        if (string.IsNullOrWhiteSpace(jobWindowsPath) || !File.Exists(jobWindowsPath))
        {
            return null;
        }

        var yaml = File.ReadAllText(jobWindowsPath);
        var steps = ParseMatchInt(TrainerYamlStepsRegex, yaml);
        var learningRate = ParseMatchString(TrainerYamlLearningRateRegex, yaml) ?? "1e-4";
        var rank = ParseMatchInt(TrainerYamlRankRegex, yaml);
        var contentOrStyle = ParseMatchString(TrainerYamlContentStyleRegex, yaml) ?? "content";
        var lowVram = ParseMatchBool(TrainerYamlLowVramRegex, yaml);
        var saveEvery = ParseMatchInt(TrainerYamlSaveEveryRegex, yaml);
        var maxSaves = ParseMatchInt(TrainerYamlMaxSavesRegex, yaml);
        var adapterPath = ParseMatchString(TrainerYamlAdapterPathRegex, yaml) ?? string.Empty;
        var samplePrompt = NormalizeTrainerSamplePrompt(ParseMatchString(TrainerYamlSamplePromptRegex, yaml));
        var adapterVersion = adapterPath.Contains("_v2", StringComparison.OrdinalIgnoreCase) ? "v2" : "v1";
        var presetId = ParseMatchString(TrainerYamlPresetIdRegex, yaml);
        if (string.IsNullOrWhiteSpace(presetId))
        {
            presetId = InferTrainerPresetId(contentOrStyle);
        }

        return new ZImageTrainerJobSettings(
            normalizedLora,
            presetId,
            steps > 0 ? steps : 3000,
            string.IsNullOrWhiteSpace(learningRate) ? "1e-4" : learningRate,
            rank > 0 ? rank : 16,
            ComputeCheckpointCount(steps > 0 ? steps : 3000, saveEvery, maxSaves),
            lowVram,
            adapterVersion,
            contentOrStyle,
            samplePrompt);
    }

    private async Task<string?> TryGetOfficialUiJobConfigJsonAsync(
        InstallSettings settings,
        string loraName,
        CancellationToken cancellationToken)
    {
        var uiDbPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/ai-toolkit/aitk_db.db";
        var bashCommand =
            "set -euo pipefail; " +
            $"UI_DB_PATH={ToBashSingleQuoted(uiDbPath)} " +
            $"JOB_NAME={ToBashSingleQuoted(loraName)} " +
            "python3 - <<'PYEOF'\n" +
            "import base64\n" +
            "import os\n" +
            "import sqlite3\n" +
            "db_path = os.environ['UI_DB_PATH']\n" +
            "job_name = os.environ['JOB_NAME']\n" +
            "con = sqlite3.connect(db_path, timeout=5.0)\n" +
            "try:\n" +
            "    cur = con.cursor()\n" +
            "    cur.execute(\"SELECT job_config FROM Job WHERE name = ? AND job_type = 'train' ORDER BY updated_at DESC LIMIT 1\", (job_name,))\n" +
            "    row = cur.fetchone()\n" +
            "    if row and row[0]:\n" +
            "        print(base64.b64encode(str(row[0]).encode('utf-8')).decode('ascii'), end='')\n" +
            "finally:\n" +
            "    con.close()\n" +
            "PYEOF";

        var result = await RunWslBashAsync(settings, bashCommand, progress: null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var encodedConfig = result.CombinedOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(encodedConfig))
        {
            return null;
        }

        try
        {
            var jsonBytes = Convert.FromBase64String(encodedConfig);
            return Encoding.UTF8.GetString(jsonBytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static ZImageTrainerJobSettings? ParseZImageTrainerJobSettingsFromOfficialUiJobConfig(
        string normalizedLora,
        string? jobConfigJson)
    {
        if (string.IsNullOrWhiteSpace(jobConfigJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(jobConfigJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("config", out var config))
            {
                return null;
            }

            if (!config.TryGetProperty("process", out var processes) ||
                processes.ValueKind != JsonValueKind.Array ||
                processes.GetArrayLength() == 0)
            {
                return null;
            }

            var process = processes[0];
            var train = TryGetObjectProperty(process, "train");
            var network = TryGetObjectProperty(process, "network");
            var model = TryGetObjectProperty(process, "model");
            var meta = TryGetObjectProperty(root, "meta");
            var nymphs = TryGetObjectProperty(meta, "nymphs");

            var contentOrStyle = GetStringProperty(train, "content_or_style") ?? "content";
            var presetId = GetStringProperty(nymphs, "preset_id");
            if (string.IsNullOrWhiteSpace(presetId))
            {
                presetId = InferTrainerPresetId(contentOrStyle);
            }

            var learningRate = GetStringProperty(train, "lr") ?? "1e-4";
            var rank = GetIntProperty(network, "linear");
            var lowVram = GetBoolProperty(model, "low_vram");
            var save = TryGetObjectProperty(process, "save");
            var sample = TryGetObjectProperty(process, "sample");
            var saveEvery = GetIntProperty(save, "save_every");
            var maxSaves = GetIntProperty(save, "max_step_saves_to_keep");
            var adapterPath = GetStringProperty(model, "assistant_lora_path") ?? string.Empty;
            var adapterVersion = adapterPath.Contains("_v2", StringComparison.OrdinalIgnoreCase) ? "v2" : "v1";
            var steps = GetIntProperty(train, "steps") > 0 ? GetIntProperty(train, "steps") : 3000;
            var samplePrompt = ResolveTrainerSamplePromptFromJson(sample);

            return new ZImageTrainerJobSettings(
                normalizedLora,
                presetId,
                steps,
                string.IsNullOrWhiteSpace(learningRate) ? "1e-4" : learningRate,
                rank > 0 ? rank : 16,
                ComputeCheckpointCount(steps, saveEvery, maxSaves),
                lowVram,
                adapterVersion,
                contentOrStyle,
                samplePrompt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task StartZImageTrainerOfficialUiAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(
            settings,
            progress,
            cancellationToken,
            allowLaunchIfClosed: true).ConfigureAwait(false);
    }

    public async Task StartZImageTrainerGradioUiAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await StopZImageTrainerUiScriptAsync(
            settings,
            "ztrain-stop-gradio-ui",
            "Stopping stale Gradio UI process...",
            progress,
            cancellationToken).ConfigureAwait(false);
        await RunZImageTrainerUiScriptAsync(
            settings,
            "ztrain-start-gradio-ui",
            "Starting simple AI Toolkit Gradio UI...",
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task StopZImageTrainerOfficialUiAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await StopZImageTrainerUiScriptAsync(
            settings,
            "ztrain-stop-official-ui",
            "Stopping AI Toolkit process...",
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task KillZImageTrainerOfficialUiAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Killing AI Toolkit process...");
        var bashCommand =
            "set +e; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "UI_PORT=8675; " +
            "is_alive() { " +
            "  if ss -ltn 2>/dev/null | grep -q ':8675 '; then return 0; fi; " +
            "  if pgrep -u \"$(id -u)\" -f '[n]ext-server' >/dev/null 2>&1; then return 0; fi; " +
            "  if pgrep -u \"$(id -u)\" -f '[n]ext start --port 8675' >/dev/null 2>&1; then return 0; fi; " +
            "  if pgrep -u \"$(id -u)\" -f 'dist/cron/[w]orker.js' >/dev/null 2>&1; then return 0; fi; " +
            "  if pgrep -u \"$(id -u)\" -f 'node_modules/.bin/[c]oncurrently' >/dev/null 2>&1; then return 0; fi; " +
            "  return 1; " +
            "}; " +
            "pkill -9 -u \"$(id -u)\" -f 'node_modules/.bin/[c]oncurrently' >/dev/null 2>&1 || true; " +
            "pkill -9 -u \"$(id -u)\" -f 'dist/cron/[w]orker.js' >/dev/null 2>&1 || true; " +
            "pkill -9 -u \"$(id -u)\" -f '[n]ext start --port 8675' >/dev/null 2>&1 || true; " +
            "pkill -9 -u \"$(id -u)\" -f '[n]ext-server' >/dev/null 2>&1 || true; " +
            "for _ in {1..16}; do " +
            "  if ! is_alive; then " +
            "    echo 'AI Toolkit server stopped.'; exit 0; " +
            "  fi; " +
            "  pkill -9 -u \"$(id -u)\" -f 'node_modules/.bin/[c]oncurrently' >/dev/null 2>&1 || true; " +
            "  pkill -9 -u \"$(id -u)\" -f 'dist/cron/[w]orker.js' >/dev/null 2>&1 || true; " +
            "  pkill -9 -u \"$(id -u)\" -f '[n]ext start --port 8675' >/dev/null 2>&1 || true; " +
            "  pkill -9 -u \"$(id -u)\" -f '[n]ext-server' >/dev/null 2>&1 || true; " +
            "  sleep 0.25; " +
            "done; " +
            "exit 1";

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (!await WaitForAiToolkitUiToStopAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Killing AI Toolkit failed.");
            }
        }
        else
        {
            progress?.Report("AI Toolkit server stopped.");
            return;
        }
    }

    public async Task StopZImageTrainerGradioUiAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await StopZImageTrainerUiScriptAsync(
            settings,
            "ztrain-stop-gradio-ui",
            "Stopping Gradio UI process...",
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RunZImageTrainerUiScriptAsync(
        InstallSettings settings,
        string scriptName,
        string activityLabel,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var scriptPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/{scriptName}";
        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"if [[ ! -x {ToBashSingleQuoted(scriptPath)} ]]; then echo 'Trainer UI launcher missing. Repair Trainer first.'; exit 1; fi; " +
            $"{ToBashSingleQuoted(scriptPath)}";

        progress?.Report(activityLabel);
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{activityLabel.TrimEnd('.')} failed.");
        }
    }

    private async Task StopZImageTrainerUiScriptAsync(
        InstallSettings settings,
        string scriptName,
        string activityLabel,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var scriptPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/{scriptName}";
        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"if [[ ! -x {ToBashSingleQuoted(scriptPath)} ]]; then exit 0; fi; " +
            $"{ToBashSingleQuoted(scriptPath)}";

        progress?.Report(activityLabel);
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{activityLabel.TrimEnd('.')} failed.");
        }
    }

    public async Task CreateZImageTrainerJobAsync(
        InstallSettings settings,
        string datasetName,
        string loraName,
        string presetId,
        string adapterVersion,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        await CreateZImageTrainerJobAsync(
            settings,
            datasetName,
            loraName,
            presetId,
            adapterVersion,
            null,
            null,
            null,
            null,
            null,
            null,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateZImageTrainerJobAsync(
        InstallSettings settings,
        string datasetName,
        string loraName,
        string presetId,
        string adapterVersion,
        int? stepsOverride,
        string? learningRateOverride,
        int? rankOverride,
        bool? lowVramOverride,
        string? samplePromptOverride,
        int? checkpointCountOverride,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var normalizedDataset = NormalizeTrainerName(datasetName, "dataset");
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var datasetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets/{normalizedDataset}";
        var jobPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/jobs/{normalizedLora}.yaml";
        var loraOutputPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/loras/{normalizedLora}";
        var loraMetadataPath = $"{loraOutputPath}/nymphs_lora.json";
        var metadataPath = $"{datasetPath}/metadata.csv";
        await SyncZImageTrainerSupportFilesAsync(settings, progress, cancellationToken).ConfigureAwait(false);
        var normalizedAdapterVersion = NormalizeZImageTrainerAdapterVersion(adapterVersion);
        var adapterPath = await EnsureZImageTrainerTrainingAdapterAsync(settings, normalizedAdapterVersion, progress, cancellationToken).ConfigureAwait(false);
        var metadataStatus = await PrepareZImageTrainerMetadataAsync(
            settings,
            normalizedDataset,
            progress,
            cancellationToken).ConfigureAwait(false);
        SyncTrainerCaptionTextFiles(
            ToWindowsWslPath(settings, datasetPath),
            ToWindowsWslPath(settings, metadataPath));

        var jobPreset = ApplyZImageTrainerOverrides(
            ResolveZImageTrainerPreset(presetId),
            stepsOverride,
            learningRateOverride,
            rankOverride,
            lowVramOverride,
            samplePromptOverride,
            checkpointCountOverride);
        var loraMetadataJson = BuildZImageLoraMetadataJson(loraName, normalizedLora, jobPreset);
        var jobScript = BuildZImageTrainerConfig(settings.LinuxUser, normalizedDataset, normalizedLora, adapterPath, jobPreset);
        var jobConfigJson = BuildZImageTrainerConfigJson(settings.LinuxUser, normalizedDataset, normalizedLora, adapterPath, jobPreset);
        const string jobHeredocMarker = "__NYMPHS_ZIMAGE_CONFIG__";
        const string loraMetadataHeredocMarker = "__NYMPHS_ZIMAGE_LORA_METADATA__";

        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted(datasetPath)} {ToBashSingleQuoted($"/home/{settings.LinuxUser}/ZImage-Trainer/jobs")} {ToBashSingleQuoted($"/home/{settings.LinuxUser}/ZImage-Trainer/loras")} {ToBashSingleQuoted(loraOutputPath)}; " +
            $"cat > {ToBashSingleQuoted(jobPath)} <<'{jobHeredocMarker}'\n" +
            $"{jobScript}\n" +
            $"{jobHeredocMarker}\n" +
            $"cat > {ToBashSingleQuoted(loraMetadataPath)} <<'{loraMetadataHeredocMarker}'\n" +
            $"{loraMetadataJson}\n" +
            $"{loraMetadataHeredocMarker}\n" +
            $"echo 'Created training config: {jobPath}'; " +
            $"echo 'Dataset folder: {datasetPath}'; " +
            $"echo 'Metadata file: {metadataPath}'; " +
            $"echo 'LoRA metadata: {loraMetadataPath}'; " +
            $"echo 'Images found: {metadataStatus.ImageCount}'; " +
            $"echo 'Captions still blank: {metadataStatus.MissingCaptionCount}'; " +
            $"echo 'Caption text files mirrored beside the images for AI Toolkit.'; " +
            $"echo 'Training adapter: {normalizedAdapterVersion}'; " +
            $"echo 'Adapter path: {adapterPath}'";

        progress.Report(
            $"Creating Z-Image Trainer config '{normalizedLora}' for dataset '{normalizedDataset}' using preset {jobPreset.Label} and adapter {normalizedAdapterVersion}.");
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Z-Image Trainer config creation failed.");
        }

        await TryRegisterZImageTrainerJobInOfficialUiAsync(
            settings,
            normalizedLora,
            jobConfigJson,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RunZImageTrainerJobAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        await EnsureAiToolkitApiReadyAsync(settings, progress, cancellationToken).ConfigureAwait(false);

        var officialUiJobId = await FindOfficialUiJobIdAsync(
            settings,
            normalizedLora,
            progress,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(officialUiJobId))
        {
            throw new InvalidOperationException(
                "Could not link the training run to an AI Toolkit job. Recreate the job or repair Trainer before starting training.");
        }

        await QueueOfficialUiJobAsync(
            settings,
            officialUiJobId,
            normalizedLora,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task StartZImageTrainerQueueAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(settings, progress, cancellationToken, allowLaunchIfClosed: false).ConfigureAwait(false);

        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var jobId = await FindOfficialUiJobIdAsync(settings, normalizedLora, progress, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new InvalidOperationException("No AI Toolkit job was found for the selected LoRA.");
        }

        var job = await GetAiToolkitJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        var gpuIds = job is null ? null : GetStringProperty(job.RootElement, "gpu_ids");
        if (string.IsNullOrWhiteSpace(gpuIds))
        {
            throw new InvalidOperationException("AI Toolkit job does not have a valid queue assignment.");
        }

        await SendAiToolkitApiRequestAsync(
            HttpMethod.Get,
            $"/api/queue/{Uri.EscapeDataString(gpuIds)}/start",
            null,
            cancellationToken).ConfigureAwait(false);
        progress?.Report($"Started AI Toolkit queue {gpuIds}.");
    }

    public async Task StopZImageTrainerQueueAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(settings, progress, cancellationToken, allowLaunchIfClosed: false).ConfigureAwait(false);

        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var jobId = await FindOfficialUiJobIdAsync(settings, normalizedLora, progress, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new InvalidOperationException("No AI Toolkit job was found for the selected LoRA.");
        }

        var job = await GetAiToolkitJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        var gpuIds = job is null ? null : GetStringProperty(job.RootElement, "gpu_ids");
        if (string.IsNullOrWhiteSpace(gpuIds))
        {
            throw new InvalidOperationException("AI Toolkit job does not have a valid queue assignment.");
        }

        await SendAiToolkitApiRequestAsync(
            HttpMethod.Get,
            $"/api/queue/{Uri.EscapeDataString(gpuIds)}/stop",
            null,
            cancellationToken).ConfigureAwait(false);
        progress?.Report($"Stopped AI Toolkit queue {gpuIds}.");
    }

    public async Task DeleteZImageTrainerJobAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(settings, progress, cancellationToken, allowLaunchIfClosed: false).ConfigureAwait(false);

        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var jobId = await FindOfficialUiJobIdAsync(settings, normalizedLora, progress, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new InvalidOperationException("No AI Toolkit job was found for the selected LoRA.");
        }

        await SendAiToolkitApiRequestAsync(
            HttpMethod.Get,
            $"/api/jobs/{Uri.EscapeDataString(jobId)}/delete",
            null,
            cancellationToken).ConfigureAwait(false);
        progress?.Report($"Deleted AI Toolkit job '{normalizedLora}'.");
    }

    public async Task<string?> GetZImageTrainerJobIdAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(settings, progress, cancellationToken, allowLaunchIfClosed: false).ConfigureAwait(false);
        return await FindOfficialUiJobIdAsync(
            settings,
            NormalizeTrainerName(loraName, "LoRA"),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> TryGetZImageTrainerJobLogAsync(
        InstallSettings settings,
        string loraName,
        CancellationToken cancellationToken)
    {
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");

        try
        {
            if (!await IsAiToolkitApiHealthyAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            using var job = await TryGetAiToolkitJobByRefAsync(normalizedLora, cancellationToken).ConfigureAwait(false);
            var jobId = job is null ? null : GetStringProperty(job.RootElement, "id");
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return null;
            }

            var responseJson = await SendAiToolkitApiRequestAsync(
                HttpMethod.Get,
                $"/api/jobs/{Uri.EscapeDataString(jobId)}/log",
                null,
                cancellationToken).ConfigureAwait(false);

            using var response = JsonDocument.Parse(responseJson);
            return GetStringProperty(response.RootElement, "log");
        }
        catch
        {
            return null;
        }
    }

    public bool DoesZImageTrainerFinalCheckpointExist(
        InstallSettings settings,
        string loraName)
    {
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var finalCheckpointLinuxPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/loras/{normalizedLora}/{normalizedLora}.safetensors";
        var finalCheckpointWindowsPath = ToWindowsWslPath(settings, finalCheckpointLinuxPath);
        return File.Exists(finalCheckpointWindowsPath);
    }

    public async Task<bool> StopZImageTrainerJobAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(
            settings,
            progress,
            cancellationToken,
            allowLaunchIfClosed: false).ConfigureAwait(false);

        var activeJob = await GetAiToolkitActiveTrainJobAsync(cancellationToken).ConfigureAwait(false);
        if (activeJob is null)
        {
            progress?.Report("No active Z-Image Trainer job was found.");
            return false;
        }

        var jobId = GetStringProperty(activeJob.RootElement, "id");
        var status = GetStringProperty(activeJob.RootElement, "status") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(jobId))
        {
            progress?.Report("No active Z-Image Trainer job was found.");
            return false;
        }

        progress?.Report("Requesting stop for the active AI Toolkit job...");
        if (string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase))
        {
            await SendAiToolkitApiRequestAsync(HttpMethod.Get, $"/api/jobs/{Uri.EscapeDataString(jobId)}/mark_stopped", null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await SendAiToolkitApiRequestAsync(HttpMethod.Get, $"/api/jobs/{Uri.EscapeDataString(jobId)}/stop", null, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("Stop requested for the active AI Toolkit job.");
        return true;
    }

    public async Task EnsureZImageTrainerPicturesFolderAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var datasetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets/{normalizedLora}";
        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted(datasetPath)}; " +
            $"echo 'Pictures folder ready: {datasetPath}'";

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Could not create Z-Image Trainer pictures folder.");
        }
    }

    public async Task<ZImageTrainerMetadataStatus> PrepareZImageTrainerMetadataAsync(
        InstallSettings settings,
        string datasetName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedDataset = NormalizeTrainerName(datasetName, "dataset");
        var datasetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets/{normalizedDataset}";
        var metadataPath = $"{datasetPath}/metadata.csv";
        var bootstrapCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted(datasetPath)}; " +
            $"touch {ToBashSingleQuoted(metadataPath)}";

        var bootstrapResult = await RunWslBashAsync(settings, bootstrapCommand, progress: null, cancellationToken).ConfigureAwait(false);
        if (bootstrapResult.ExitCode != 0)
        {
            throw new InvalidOperationException("Could not prepare the captions file.");
        }

        var datasetWindowsPath = ToWindowsWslPath(settings, datasetPath);
        var metadataWindowsPath = ToWindowsWslPath(settings, metadataPath);
        Directory.CreateDirectory(datasetWindowsPath);

        var existingCaptions = ReadTrainerMetadata(metadataWindowsPath);
        var rows = Directory.EnumerateFiles(datasetWindowsPath, "*", SearchOption.TopDirectoryOnly)
            .Where(static path => IsSupportedTrainerImageFile(Path.GetExtension(path)))
            .Select(Path.GetFileName)
            .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
            .OrderBy(static fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .Select(fileName =>
            {
                existingCaptions.TryGetValue(fileName!, out var caption);
                return new KeyValuePair<string, string>(fileName!, caption ?? string.Empty);
            })
            .ToList();

        WriteTrainerMetadata(metadataWindowsPath, rows);

        var missingCaptionCount = rows.Count(row => string.IsNullOrWhiteSpace(row.Value));
        progress?.Report(
            $"Captions file ready: {metadataPath} ({rows.Count} image(s), {missingCaptionCount} caption(s) still blank).");

        return new ZImageTrainerMetadataStatus(rows.Count, missingCaptionCount, datasetPath, metadataPath);
    }


    private async Task<string> EnsureZImageTrainerTrainingAdapterAsync(
        InstallSettings settings,
        string adapterVersion,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedAdapterVersion = NormalizeZImageTrainerAdapterVersion(adapterVersion);
        var adapterRepoDir = $"/home/{settings.LinuxUser}/ZImage-Trainer/adapters/zimage_turbo_training_adapter";
        var adapterPathFile = $"{adapterRepoDir}/selected_adapter_path_{normalizedAdapterVersion}.txt";
        var trainerPython = $"/home/{settings.LinuxUser}/ZImage-Trainer/ai-toolkit/venv/bin/python";
        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted(adapterRepoDir)}; " +
            $"if [[ ! -x {ToBashSingleQuoted(trainerPython)} ]]; then echo 'Trainer Python missing: {trainerPython}. Repair Trainer first.' >&2; exit 1; fi; " +
            $"if [[ -f {ToBashSingleQuoted(adapterPathFile)} ]]; then " +
            $"  IFS= read -r SELECTED_ADAPTER < {ToBashSingleQuoted(adapterPathFile)} || true; " +
            "  SELECTED_ADAPTER=\"${SELECTED_ADAPTER%$'\\r'}\"; " +
            "  if [[ -n \"$SELECTED_ADAPTER\" && -f \"$SELECTED_ADAPTER\" ]]; then " +
            $"    echo \"Turbo training adapter {normalizedAdapterVersion} ready: $SELECTED_ADAPTER\"; " +
            "    exit 0; " +
            "  fi; " +
            "fi; " +
            $"ADAPTER_REPO_DIR={ToBashSingleQuoted(adapterRepoDir)} ADAPTER_PATH_FILE={ToBashSingleQuoted(adapterPathFile)} ADAPTER_VERSION={ToBashSingleQuoted(normalizedAdapterVersion)} {ToBashSingleQuoted(trainerPython)} - <<'PYEOF'\n" +
            "import os\n" +
            "from pathlib import Path\n" +
            "from huggingface_hub import snapshot_download\n" +
            "adapter_dir = Path(os.environ['ADAPTER_REPO_DIR'])\n" +
            "path_file = Path(os.environ['ADAPTER_PATH_FILE'])\n" +
            "adapter_version = os.environ.get('ADAPTER_VERSION', 'v1').strip().lower()\n" +
            "adapter_dir.mkdir(parents=True, exist_ok=True)\n" +
            "snapshot_download(\n" +
            "    repo_id='ostris/zimage_turbo_training_adapter',\n" +
            "    local_dir=str(adapter_dir),\n" +
            "    allow_patterns=['*.safetensors', '*.bin', '*.pt', '*.pth', '*.ckpt'],\n" +
            ")\n" +
            "version_marker = f'_{adapter_version}'\n" +
            "matching_candidates = [\n" +
            "        path for path in adapter_dir.rglob('*')\n" +
            "        if path.is_file() and path.suffix.lower() in {'.safetensors', '.bin', '.pt', '.pth', '.ckpt'}\n" +
            "]\n" +
            "matching_candidates = [path for path in matching_candidates if version_marker in path.name.lower()]\n" +
            "candidates = sorted(\n" +
            "    matching_candidates,\n" +
            "    key=lambda path: (\n" +
            "        0 if path.suffix.lower() == '.safetensors' else 1,\n" +
            "        0 if path.parent == adapter_dir else 1,\n" +
            "        len(path.name),\n" +
            "        str(path).lower(),\n" +
            "    ),\n" +
            ")\n" +
            "if not candidates:\n" +
            "    raise SystemExit(f'No adapter weight file matching {adapter_version} was downloaded for ostris/zimage_turbo_training_adapter')\n" +
            "selected = candidates[0].resolve()\n" +
            "path_file.write_text(str(selected) + '\\n', encoding='utf-8')\n" +
            "print(f'Turbo training adapter {adapter_version} ready: {selected}', flush=True)\n" +
            "PYEOF";

        progress?.Report($"Ensuring Turbo training adapter {normalizedAdapterVersion} is ready for the trainer.");
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Could not prepare the Turbo training adapter.");
        }

        var selectedLine = result.CombinedOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.StartsWith($"Turbo training adapter {normalizedAdapterVersion}", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(selectedLine))
        {
            var separatorIndex = selectedLine.IndexOf(':');
            if (separatorIndex >= 0)
            {
                var selectedPath = selectedLine[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    return selectedPath;
                }
            }
        }

        return $"{adapterRepoDir}/zimage_turbo_training_adapter_{normalizedAdapterVersion}.safetensors";
    }

    private static string NormalizeZImageTrainerAdapterVersion(string? adapterVersion)
    {
        return string.Equals(adapterVersion, "v2", StringComparison.OrdinalIgnoreCase)
            ? "v2"
            : "v1";
    }

    public async Task<ZImageTrainerMetadataStatus> DraftZImageTrainerCaptionsAsync(
        InstallSettings settings,
        string datasetName,
        string trainingType,
        string captionMode,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var normalizedDataset = NormalizeTrainerName(datasetName, "dataset");
        var normalizedTrainingType = string.Equals(trainingType, "style", StringComparison.OrdinalIgnoreCase)
            ? "style"
            : "character";
        var normalizedCaptionMode = string.Equals(captionMode, "overwrite_all", StringComparison.OrdinalIgnoreCase)
            ? "overwrite_all"
            : "fill_blanks";

        await SyncZImageTrainerSupportFilesAsync(settings, progress, cancellationToken).ConfigureAwait(false);

        var metadataStatus = await PrepareZImageTrainerMetadataAsync(
            settings,
            normalizedDataset,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (metadataStatus.ImageCount == 0)
        {
            throw new InvalidOperationException("Add training pictures first.");
        }

        var captionScriptPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/zimage-caption-brain.sh";
        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export BRAIN_INSTALL_ROOT={ToBashSingleQuoted(settings.BrainInstallRoot)}; " +
            $"export ZIMAGE_DATASET_DIR={ToBashSingleQuoted(metadataStatus.DatasetPath)}; " +
            $"export ZIMAGE_METADATA_PATH={ToBashSingleQuoted(metadataStatus.MetadataPath)}; " +
            $"export ZIMAGE_CAPTION_MODE={ToBashSingleQuoted(normalizedCaptionMode)}; " +
            $"export ZIMAGE_TRAINING_FOCUS={ToBashSingleQuoted(normalizedTrainingType)}; " +
            $"bash {ToBashSingleQuoted(captionScriptPath)}";

        progress.Report(
            $"Caption Brain: drafting captions for {metadataStatus.ImageCount} image(s) using {normalizedTrainingType} guidance.");

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Caption Brain drafting failed.");
        }

        return await PrepareZImageTrainerMetadataAsync(
            settings,
            normalizedDataset,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncZImageTrainerSupportFilesAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var captionBrainScript = RequireScript("zimage_caption_brain.sh");
        var wslCaptionBrainScriptPath = ConvertWindowsPathToWsl(captionBrainScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {captionBrainScript}");
        var captionBrainPythonScript = RequireScript("zimage_caption_brain.py");
        var wslCaptionBrainPythonScriptPath = ConvertWindowsPathToWsl(captionBrainPythonScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {captionBrainPythonScript}");
        var captionShellTargetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/zimage-caption-brain.sh";
        var captionPythonTargetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/zimage-caption-brain.py";
        var trainerPythonPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/ai-toolkit/venv/bin/python";
        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted($"/home/{settings.LinuxUser}/ZImage-Trainer/bin")}; " +
            $"install -m 755 {ToBashSingleQuoted(wslCaptionBrainScriptPath)} {ToBashSingleQuoted(captionShellTargetPath)}; " +
            $"install -m 755 {ToBashSingleQuoted(wslCaptionBrainPythonScriptPath)} {ToBashSingleQuoted(captionPythonTargetPath)}; " +
            $"if ! {ToBashSingleQuoted(trainerPythonPath)} -c {ToBashSingleQuoted("import PIL")} >/dev/null 2>&1; then " +
            $"  {ToBashSingleQuoted(trainerPythonPath)} -m pip install Pillow; " +
            "fi; " +
            $"echo 'Caption Brain helper synced: {captionShellTargetPath}'; " +
            $"echo 'Caption Brain client synced: {captionPythonTargetPath}'; " +
            "echo 'Caption Brain image dependency ready: Pillow'";

        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Could not sync managed Z-Image trainer support files.");
        }
    }

    public async Task<string> GetNymphsBrainStatusAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var result = await RunNymphsBrainCommandAsync(
            settings,
            "brain-status",
            progress: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Nymphs-Brain status check failed.");
        }

        return result.CombinedOutput;
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

        if (result.ExitCode != 0)
        {
            return string.Empty;
        }

        return result.CombinedOutput.Trim();
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
                if (parts.Length < 3
                    || !double.TryParse(parts[0], out var usedMb)
                    || !double.TryParse(parts[1], out var totalMb))
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

    public async Task RunNymphsBrainToolAsync(
        InstallSettings settings,
        string toolName,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedNymphsBrainTool(toolName))
        {
            throw new ArgumentException($"Unsupported Nymphs-Brain tool: {toolName}", nameof(toolName));
        }

        progress.Report($"Nymphs-Brain: running {toolName}...");
        var result = await RunNymphsBrainCommandAsync(
            settings,
            toolName,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            if (toolName == "mcp-start"
                && await IsNymphsBrainMcpProcessRunningAsync(settings, cancellationToken).ConfigureAwait(false))
            {
                progress.Report("Nymphs-Brain MCP gateway process is running. Continuing after mcp-start timeout.");
                return;
            }

            throw new InvalidOperationException($"Nymphs-Brain {toolName} failed.");
        }
    }

    private async Task<bool> IsNymphsBrainMcpProcessRunningAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var bashCommand =
            "set +e; " +
            $"INSTALL_ROOT={ToBashSingleQuoted(settings.BrainInstallRoot)}; " +
            """
            PID_FILE="${INSTALL_ROOT}/mcp/logs/mcp-proxy.pid"
            if [[ -f "${PID_FILE}" ]]; then
              pid="$(cat "${PID_FILE}" 2>/dev/null || true)"
              if [[ "${pid}" =~ ^[0-9]+$ ]] && kill -0 "${pid}" >/dev/null 2>&1; then
                exit 0
              fi
            fi
            ps -eo pid=,ppid=,args= | awk -v self="$$" '$1 != self && $2 != self && $0 !~ /[b]ash -lc/ && $0 ~ /mcp-proxy/ { found=1 } END { exit(found ? 0 : 1) }'
            """;

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments:
            [
                "-d", settings.DistroName,
                "--user", settings.LinuxUser,
                "--",
                "/bin/bash", "-lc", bashCommand,
            ],
            workingDirectory: Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    public async Task StopNymphsBrainServicesAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        try
        {
            progress.Report("Stopping Nymphs-Brain services...");

            foreach (var tool in new[] { "open-webui-stop", "mcp-stop", "lms-stop" })
            {
                var toolPath = $"{settings.BrainInstallRoot}/bin/{tool}";
                progress.Report($"Running {tool}...");
                var result = await RunWslCommandAsync(
                    settings,
                    [toolPath],
                    progress,
                    timeout.Token).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    progress.Report($"{tool}: warning exit code {result.ExitCode}.");
                }
            }

            var stillListening = await RunWslBashAsync(
                settings,
                "ss -ltnp 2>/dev/null | grep -E ':(8000|8100|8081)' || true",
                progress: null,
                timeout.Token).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stillListening.CombinedOutput))
            {
                progress.Report("ERROR: Nymphs-Brain ports are still listening:");
                progress.Report(stillListening.CombinedOutput.Trim());
                throw new InvalidOperationException("Nymphs-Brain stop failed.");
            }

            progress.Report("Nymphs-Brain stop completed.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Nymphs-Brain stop timed out after 45 seconds.");
        }
    }

    private static BrainMonitorSnapshot ParseBrainMonitorSnapshot(string output)
    {
        var values = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var isRunning = values.TryGetValue("state", out var state)
            && string.Equals(state, "running", StringComparison.OrdinalIgnoreCase);

        var context = ValueOrDash(values, "context");
        var tps = ValueOrDash(values, "tps");

        return new BrainMonitorSnapshot(
            isRunning,
            ValueOrDash(values, "model"),
            isRunning && context == "-" ? "Unavailable" : context,
            ValueOrDash(values, "vram"),
            ValueOrDash(values, "temp"),
            isRunning && tps == "-" ? "Waiting" : tps);
    }

#if LEGACY_MANAGER_MODULE_TOOLS
    private static ZImageTrainerStatus ParseZImageTrainerStatus(string output)
    {
        var values = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var installState = ValueOrDash(values.GetValueOrDefault("ZIMAGE_TRAINER_INSTALLED"));
        var repoExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_REPO"));
        var venvExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_VENV"));
        var datasetRootExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_DATASET_ROOT"));
        var outputRootExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_OUTPUT_ROOT"));
        var running = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_RUNNING"));
        var activeState = ValueOrDash(values.GetValueOrDefault("ZIMAGE_TRAINER_ACTIVE_STATE")).ToLowerInvariant();
        var loraCount = ParseNonNegativeInt(values.GetValueOrDefault("ZIMAGE_TRAINER_LORAS_FOUND"));
        var datasetCount = ParseNonNegativeInt(values.GetValueOrDefault("ZIMAGE_TRAINER_DATASETS_FOUND"));
        var uiNodeExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_UI_NODE"));
        var uiBuildExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_UI_BUILD"));
        var uiDbExists = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_UI_DB"));
        var uiRunning = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_UI_RUNNING"));
        var queueWorkerRunning = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_QUEUE_WORKER_RUNNING"));
        var queueRunning = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_QUEUE_RUNNING"));
        var gradioRunning = IsYes(values.GetValueOrDefault("ZIMAGE_TRAINER_GRADIO_RUNNING"));
        var activeInfo = ValueOrDash(values.GetValueOrDefault("ZIMAGE_TRAINER_ACTIVE_INFO"));

        var detail = installState switch
        {
            "installed" => $"Installed. {loraCount} LoRA file(s), {datasetCount} dataset folder(s).",
            "partial" => "Partial install. Repair needed.",
            _ => "Not installed.",
        };

        if (running)
        {
            detail += activeState switch
            {
                "queued" when queueRunning => " Training queued.",
                "queued" => " Training queued, but the AI Toolkit queue is stopped.",
                "running" when !string.IsNullOrWhiteSpace(activeInfo) && !string.Equals(activeInfo, "-", StringComparison.Ordinal) => $" {activeInfo}.",
                "running" => " Training running.",
                _ => " Trainer activity detected.",
            };
        }

        if (uiRunning)
        {
            detail += " AI Toolkit running.";
        }
        else if (uiNodeExists && uiBuildExists && uiDbExists && installState == "installed")
        {
            detail += " AI Toolkit ready.";
        }

        if (gradioRunning)
        {
            detail += " Gradio running.";
        }
        else if (installState == "installed")
        {
            detail += " Gradio ready.";
        }

        if (installState == "installed" && !queueWorkerRunning)
        {
            detail += " Queue worker not running.";
        }

        return new ZImageTrainerStatus(
            installState,
            repoExists,
            venvExists,
            datasetRootExists,
            outputRootExists,
            running,
            loraCount,
            datasetCount,
            uiRunning,
            queueWorkerRunning,
            queueRunning,
            gradioRunning,
            activeState,
            activeInfo,
            detail);
    }

#endif

    private static bool IsYes(string? value) =>
        string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);

    private static int ParseNonNegativeInt(string? value) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;

    private static int ParseMatchInt(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success && int.TryParse(match.Groups["value"].Value, out var parsed) ? parsed : 0;
    }

    private static bool ParseMatchBool(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success && bool.TryParse(match.Groups["value"].Value, out var parsed) && parsed;
    }

    private static string? ParseMatchString(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static JsonElement? TryGetObjectProperty(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? value
            : null;
    }

    private static JsonElement? TryGetObjectProperty(JsonElement? element, string propertyName)
    {
        return element.HasValue && element.Value.ValueKind == JsonValueKind.Object && element.Value.TryGetProperty(propertyName, out var value)
            ? value
            : null;
    }

    private static string? GetStringProperty(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => null,
        };
    }

    private static int GetIntProperty(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0,
        };
    }

    private static bool GetBoolProperty(JsonElement? element, string propertyName)
    {
        if (!element.HasValue ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false,
        };
    }

    private static string InferTrainerPresetId(string? contentOrStyle)
    {
        return (contentOrStyle ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "content" => "strong_style",
            "style" => "style",
            "balanced" => "baseline",
            _ => "baseline",
        };
    }

    private static string NormalizeTrainerName(string value, string label)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"Enter a {label} name.");
        }

        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                builder.Append('_');
            }
        }

        var normalized = builder.ToString().Trim('_', '-');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"Enter a {label} name using letters, numbers, spaces, dashes, or underscores.");
        }

        return normalized;
    }

    public sealed record ZImageTrainerMetadataStatus(
        int ImageCount,
        int MissingCaptionCount,
        string DatasetPath,
        string MetadataPath);

    public sealed record ZImageTrainerJobSettings(
        string LoraName,
        string PresetId,
        int Steps,
        string LearningRate,
        int Rank,
        int CheckpointCount,
        bool LowVram,
        string AdapterVersion,
        string ContentOrStyle,
        string SamplePrompt);

    private sealed record ZImageTrainerPreset(
        string Id,
        string Label,
        string TypeLabel,
        string AmountLabel,
        int Steps,
        int SaveEvery,
        int MaxStepSavesToKeep,
        int SampleEvery,
        bool DisableSampling,
        bool CacheTextEmbeddings,
        string LearningRate,
        int Rank,
        int Resolution,
        string ContentOrStyle,
        bool LowVram,
        int GuidanceScale,
        int SampleSteps,
        string SamplePrompt);

    private static ZImageTrainerPreset ResolveZImageTrainerPreset(string presetId)
    {
        var normalizedPreset = (presetId ?? string.Empty).Trim().ToLowerInvariant();

        return normalizedPreset switch
        {
            "fast_test" => new ZImageTrainerPreset(
                "fast_test",
                "Fast Test",
                "Turbo",
                "Quick Check",
                500,
                0,
                0,
                0,
                true,
                true,
                "1e-4",
                8,
                512,
                "balanced",
                false,
                1,
                8,
                string.Empty),
            "strong_style" or "style_high_noise" => new ZImageTrainerPreset(
                "strong_style",
                "Strong Style",
                "Style",
                "High Noise",
                5000,
                1250,
                4,
                250,
                false,
                true,
                "1e-4",
                16,
                1024,
                "content",
                false,
                1,
                8,
                string.Empty),
            "style" or "style_balanced" or "style_light" => new ZImageTrainerPreset(
                "style",
                "Style",
                "Style",
                "Balanced",
                3000,
                750,
                4,
                250,
                false,
                true,
                "1e-4",
                16,
                1024,
                "balanced",
                false,
                1,
                8,
                string.Empty),
            "basic_turbo" or "baseline" or _ => new ZImageTrainerPreset(
                "baseline",
                "Baseline",
                "Turbo",
                "Baseline",
                3000,
                750,
                4,
                250,
                false,
                true,
                "1e-4",
                16,
                1024,
                "balanced",
                false,
                1,
                8,
                string.Empty),
        };
    }

    private static ZImageTrainerPreset ApplyZImageTrainerOverrides(
        ZImageTrainerPreset preset,
        int? stepsOverride,
        string? learningRateOverride,
        int? rankOverride,
        bool? lowVramOverride,
        string? samplePromptOverride,
        int? checkpointCountOverride)
    {
        var resolvedSteps = stepsOverride is > 0 ? stepsOverride.Value : preset.Steps;
        var resolvedRank = rankOverride is > 0 ? rankOverride.Value : preset.Rank;
        var resolvedLearningRate = string.IsNullOrWhiteSpace(learningRateOverride)
            ? preset.LearningRate
            : learningRateOverride.Trim();
        var resolvedLowVram = lowVramOverride ?? preset.LowVram;
        var resolvedSamplePrompt = string.IsNullOrWhiteSpace(samplePromptOverride)
            ? preset.SamplePrompt
            : samplePromptOverride.Trim();
        var resolvedCheckpointCount = checkpointCountOverride is >= 0
            ? checkpointCountOverride.Value
            : preset.MaxStepSavesToKeep;
        var resolvedSaveEvery = ComputeSaveEvery(resolvedSteps, resolvedCheckpointCount);

        return preset with
        {
            Steps = resolvedSteps,
            LearningRate = resolvedLearningRate,
            Rank = resolvedRank,
            LowVram = resolvedLowVram,
            SamplePrompt = resolvedSamplePrompt,
            SaveEvery = resolvedSaveEvery,
            MaxStepSavesToKeep = resolvedCheckpointCount,
        };
    }

    private static int ComputeSaveEvery(int steps, int checkpointCount)
    {
        if (steps <= 0 || checkpointCount <= 0)
        {
            return 0;
        }

        return Math.Max(1, steps / checkpointCount);
    }

    private static int ComputeCheckpointCount(int steps, int saveEvery, int maxSaves)
    {
        if (steps <= 0 || saveEvery <= 0 || maxSaves <= 0)
        {
            return 0;
        }

        var derivedCount = Math.Max(1, (int)Math.Round(steps / (double)saveEvery, MidpointRounding.AwayFromZero));
        return Math.Max(1, Math.Min(maxSaves, derivedCount));
    }

    private static string NormalizeTrainerSamplePrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var trimmed = prompt.Trim();
        if ((trimmed.StartsWith("'") && trimmed.EndsWith("'")) ||
            (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed.Replace("''", "'").Trim();
    }

    private static string ResolveTrainerSamplePromptFromJson(JsonElement? sample)
    {
        if (!sample.HasValue || sample.Value.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (!sample.Value.TryGetProperty("samples", out var samples) ||
            samples.ValueKind != JsonValueKind.Array ||
            samples.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstSample = samples[0];
        return GetStringProperty(firstSample, "prompt") ?? string.Empty;
    }

    private static string BuildZImageTrainerConfig(string linuxUser, string datasetName, string loraName, string adapterPath, ZImageTrainerPreset preset)
    {
        return $$"""
---
# nymphs_preset_id: {{preset.Id}}
job: extension
config:
  name: '{{EscapeYamlSingleQuoted(loraName)}}'
  process:
    - type: 'sd_trainer'
      training_folder: '/home/{{linuxUser}}/ZImage-Trainer/loras'
      sqlite_db_path: '/home/{{linuxUser}}/ZImage-Trainer/ai-toolkit/aitk_db.db'
      device: cuda:0
      network:
        type: "lora"
        linear: {{preset.Rank}}
        linear_alpha: {{preset.Rank}}
      save:
        dtype: "bf16"
        save_every: {{preset.SaveEvery}}
        max_step_saves_to_keep: {{preset.MaxStepSavesToKeep}}
      logging:
        log_every: 1
        use_ui_logger: true
      datasets:
        - folder_path: '/home/{{linuxUser}}/ZImage-Trainer/datasets/{{EscapeYamlSingleQuoted(datasetName)}}'
          caption_ext: "txt"
          caption_dropout_rate: 0.05
          cache_latents_to_disk: false
          resolution: [ {{preset.Resolution}} ]
      train:
        batch_size: 1
        steps: {{preset.Steps}}
        gradient_accumulation: 1
        train_unet: true
        train_text_encoder: false
        gradient_checkpointing: true
        noise_scheduler: "flowmatch"
        timestep_type: "weighted"
        content_or_style: "{{preset.ContentOrStyle}}"
        optimizer: "adamw8bit"
        optimizer_params:
          weight_decay: 0.0001
        unload_text_encoder: false
        cache_text_embeddings: {{preset.CacheTextEmbeddings.ToString().ToLowerInvariant()}}
        lr: {{preset.LearningRate}}
        ema_config:
          use_ema: false
          ema_decay: 0.99
        skip_first_sample: true
        force_first_sample: false
        disable_sampling: {{preset.DisableSampling.ToString().ToLowerInvariant()}}
        dtype: "bf16"
        diff_output_preservation: false
        diff_output_preservation_multiplier: 1
        diff_output_preservation_class: "person"
        switch_boundary_every: 1
        loss_type: "mse"
      model:
        name_or_path: '/home/{{linuxUser}}/ZImage-Trainer/models/Tongyi-MAI/Z-Image-Turbo'
        quantize: false
        qtype: "qfloat8"
        quantize_te: false
        qtype_te: "qfloat8"
        arch: "zimage:turbo"
        low_vram: {{preset.LowVram.ToString().ToLowerInvariant()}}
        model_kwargs: {}
        layer_offloading: false
        layer_offloading_text_encoder_percent: 1
        layer_offloading_transformer_percent: 1
        assistant_lora_path: '{{EscapeYamlSingleQuoted(adapterPath)}}'
      sample:
        sampler: "flowmatch"
        sample_every: {{preset.SampleEvery}}
        width: {{preset.Resolution}}
        height: {{preset.Resolution}}
        samples:
          - prompt: '{{EscapeYamlSingleQuoted(preset.SamplePrompt)}}'
        neg: ""
        seed: 42
        walk_seed: true
        guidance_scale: {{preset.GuidanceScale}}
        sample_steps: {{preset.SampleSteps}}
        num_frames: 1
        fps: 1
meta:
  name: "[name]"
  version: '1.0'
  nymphs:
    preset_id: '{{EscapeYamlSingleQuoted(preset.Id)}}'
""";
    }

    private static string BuildZImageTrainerConfigJson(string linuxUser, string datasetName, string loraName, string adapterPath, ZImageTrainerPreset preset)
    {
        var learningRateValue = ParseLearningRateValue(preset.LearningRate);
        var jobConfig = new
        {
            job = "extension",
            config = new
            {
                name = loraName,
                process = new object[]
                {
                    new
                    {
                        type = "sd_trainer",
                        training_folder = $"/home/{linuxUser}/ZImage-Trainer/loras",
                        sqlite_db_path = $"/home/{linuxUser}/ZImage-Trainer/ai-toolkit/aitk_db.db",
                        device = "cuda:0",
                        network = new
                        {
                            type = "lora",
                            linear = preset.Rank,
                            linear_alpha = preset.Rank,
                        },
                        save = new
                        {
                            dtype = "bf16",
                            save_every = preset.SaveEvery,
                            max_step_saves_to_keep = preset.MaxStepSavesToKeep,
                        },
                        logging = new
                        {
                            log_every = 1,
                            use_ui_logger = true,
                        },
                        datasets = new object[]
                        {
                            new
                            {
                                folder_path = $"/home/{linuxUser}/ZImage-Trainer/datasets/{datasetName}",
                                caption_ext = "txt",
                                caption_dropout_rate = 0.05,
                                cache_latents_to_disk = false,
                                resolution = new[] { preset.Resolution },
                            },
                        },
                        train = new
                        {
                            batch_size = 1,
                            steps = preset.Steps,
                            gradient_accumulation = 1,
                            train_unet = true,
                            train_text_encoder = false,
                            gradient_checkpointing = true,
                            noise_scheduler = "flowmatch",
                            timestep_type = "weighted",
                            content_or_style = preset.ContentOrStyle,
                            optimizer = "adamw8bit",
                            optimizer_params = new
                            {
                                weight_decay = 0.0001,
                            },
                            unload_text_encoder = false,
                            cache_text_embeddings = preset.CacheTextEmbeddings,
                            lr = learningRateValue,
                            ema_config = new
                            {
                                use_ema = false,
                                ema_decay = 0.99,
                            },
                            skip_first_sample = true,
                            force_first_sample = false,
                            disable_sampling = preset.DisableSampling,
                            dtype = "bf16",
                            diff_output_preservation = false,
                            diff_output_preservation_multiplier = 1,
                            diff_output_preservation_class = "person",
                            switch_boundary_every = 1,
                            loss_type = "mse",
                        },
                        model = new
                        {
                            name_or_path = $"/home/{linuxUser}/ZImage-Trainer/models/Tongyi-MAI/Z-Image-Turbo",
                            quantize = false,
                            qtype = "qfloat8",
                            quantize_te = false,
                            qtype_te = "qfloat8",
                            arch = "zimage:turbo",
                            low_vram = preset.LowVram,
                            model_kwargs = new { },
                            layer_offloading = false,
                            layer_offloading_text_encoder_percent = 1,
                            layer_offloading_transformer_percent = 1,
                            assistant_lora_path = adapterPath,
                        },
                        sample = new
                        {
                            sampler = "flowmatch",
                            sample_every = preset.SampleEvery,
                            width = preset.Resolution,
                            height = preset.Resolution,
                            samples = new object[]
                            {
                                new
                                {
                                    prompt = preset.SamplePrompt,
                                },
                            },
                            neg = string.Empty,
                            seed = 42,
                            walk_seed = true,
                            guidance_scale = preset.GuidanceScale,
                            sample_steps = preset.SampleSteps,
                            num_frames = 1,
                            fps = 1,
                        },
                    },
                },
            },
            meta = new
            {
                name = "[name]",
                version = "1.0",
                nymphs = new
                {
                    preset_id = preset.Id,
                },
            },
        };

        return JsonSerializer.Serialize(jobConfig);
    }

    private static string BuildZImageLoraMetadataJson(string rawLoraName, string normalizedLoraName, ZImageTrainerPreset preset)
    {
        var displayName = string.IsNullOrWhiteSpace(rawLoraName) ? normalizedLoraName : rawLoraName.Trim();
        var activationText = BuildDefaultZImageLoraActivationText(displayName, preset);
        var metadata = new
        {
            schema_version = 1,
            source = "nymphs_manager",
            display_name = displayName,
            activation_text = activationText,
            auto_use_trigger = !string.IsNullOrWhiteSpace(activationText),
            lora_type = string.Equals(preset.ContentOrStyle, "style", StringComparison.OrdinalIgnoreCase) ? "style" : "character",
            notes = "Manager default: use the LoRA name itself as activation text.",
        };

        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    private static string BuildDefaultZImageLoraActivationText(string displayName, ZImageTrainerPreset preset)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return trimmed;
    }

    private static double ParseLearningRateValue(string? learningRate)
    {
        if (!string.IsNullOrWhiteSpace(learningRate) &&
            double.TryParse(learningRate.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return 1e-4;
    }

    private async Task TryRegisterZImageTrainerJobInOfficialUiAsync(
        InstallSettings settings,
        string loraName,
        string jobConfigJson,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await EnsureAiToolkitApiReadyAsync(settings, progress, cancellationToken).ConfigureAwait(false);
        await UpsertAiToolkitJobViaApiAsync(loraName, jobConfigJson, progress, cancellationToken).ConfigureAwait(false);
        progress?.Report($"Registered '{loraName}' in the AI Toolkit jobs list.");
    }

    private async Task<string?> FindOfficialUiJobIdAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var job = await TryGetAiToolkitJobByRefAsync(loraName, cancellationToken).ConfigureAwait(false);
        var jobId = job is null ? null : GetStringProperty(job.RootElement, "id");

        if (string.IsNullOrWhiteSpace(jobId))
        {
            progress?.Report($"Warning: could not look up the AI Toolkit job for '{loraName}'.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(jobId))
        {
            progress?.Report($"Linked trainer run '{loraName}' to AI Toolkit job {jobId}.");
        }
        else
        {
            progress?.Report($"Warning: no AI Toolkit job ID was found for '{loraName}'.");
        }

        return string.IsNullOrWhiteSpace(jobId) ? null : jobId;
    }

    private async Task QueueOfficialUiJobAsync(
        InstallSettings settings,
        string jobId,
        string loraName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var jobBeforeQueue = await GetAiToolkitJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
        var gpuIds = jobBeforeQueue is null ? null : GetStringProperty(jobBeforeQueue.RootElement, "gpu_ids");
        if (string.IsNullOrWhiteSpace(gpuIds))
        {
            throw new InvalidOperationException("AI Toolkit job does not have a valid GPU/queue assignment.");
        }

        await SendAiToolkitApiRequestAsync(HttpMethod.Get, $"/api/jobs/{Uri.EscapeDataString(jobId)}/start", null, cancellationToken).ConfigureAwait(false);

        var queuedJob = await WaitForAiToolkitJobLiveStateAsync(jobId, cancellationToken).ConfigureAwait(false);
        var status = queuedJob is null ? null : GetStringProperty(queuedJob.RootElement, "status");
        if (queuedJob is null ||
            (!string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "running", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("AI Toolkit did not keep the job in a queued or running state.");
        }

        progress?.Report($"Queued '{loraName}' in the AI Toolkit queue on GPU target {gpuIds}.");
    }

    private async Task EnsureAiToolkitApiReadyAsync(
        InstallSettings settings,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        bool allowLaunchIfClosed = true)
    {
        if (await IsAiToolkitApiHealthyAsync(cancellationToken).ConfigureAwait(false))
        {
            await EnsureAiToolkitSettingsConfiguredAsync(settings, cancellationToken).ConfigureAwait(false);
            progress?.Report("AI Toolkit is already running.");
            return;
        }

        if (await IsAiToolkitUiRespondingAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "AI Toolkit is already open but not healthy. Use Kill AI Toolkit, then open it again.");
        }

        if (!allowLaunchIfClosed)
        {
            throw new InvalidOperationException(
                "AI Toolkit is not running. Open AI Toolkit first, then try again.");
        }

        await RunZImageTrainerUiScriptAsync(
            settings,
            "ztrain-start-official-ui",
            "Starting AI Toolkit...",
            progress,
            cancellationToken).ConfigureAwait(false);

        await WaitForAiToolkitApiHealthyAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAiToolkitSettingsConfiguredAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureAiToolkitSettingsConfiguredAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var desiredTrainingFolder = $"/home/{settings.LinuxUser}/ZImage-Trainer/loras";
        var desiredDatasetsFolder = $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets";

        var settingsJson = await SendAiToolkitApiRequestAsync(HttpMethod.Get, "/api/settings", null, cancellationToken).ConfigureAwait(false);
        using var response = JsonDocument.Parse(settingsJson);
        var currentTrainingFolder = GetStringProperty(response.RootElement, "TRAINING_FOLDER") ?? string.Empty;
        var currentDatasetsFolder = GetStringProperty(response.RootElement, "DATASETS_FOLDER") ?? string.Empty;
        var currentToken = GetStringProperty(response.RootElement, "HF_TOKEN") ?? string.Empty;

        if (string.Equals(currentTrainingFolder, desiredTrainingFolder, StringComparison.Ordinal) &&
            string.Equals(currentDatasetsFolder, desiredDatasetsFolder, StringComparison.Ordinal))
        {
            return;
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            HF_TOKEN = currentToken,
            TRAINING_FOLDER = desiredTrainingFolder,
            DATASETS_FOLDER = desiredDatasetsFolder,
        });

        await SendAiToolkitApiRequestAsync(HttpMethod.Post, "/api/settings", payloadJson, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForAiToolkitApiHealthyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsAiToolkitApiHealthyAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("AI Toolkit API did not become reachable on localhost:8675.");
    }

    private async Task<bool> IsAiToolkitApiHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var queueResponse = await _aiToolkitHttpClient.GetAsync("/api/queue", cancellationToken).ConfigureAwait(false);
            if (!queueResponse.IsSuccessStatusCode)
            {
                return false;
            }

            using var jobsResponse = await _aiToolkitHttpClient.GetAsync("/api/jobs?job_type=train", cancellationToken).ConfigureAwait(false);
            return jobsResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

#if LEGACY_MANAGER_MODULE_TOOLS
    private async Task<ZImageTrainerStatus> EnrichZImageTrainerStatusFromAiToolkitAsync(
        InstallSettings settings,
        ZImageTrainerStatus status,
        string? selectedJobRef,
        CancellationToken cancellationToken)
    {
        var normalizedRef = string.IsNullOrWhiteSpace(selectedJobRef)
            ? string.Empty
            : NormalizeTrainerName(selectedJobRef, "LoRA");

        if (string.IsNullOrWhiteSpace(normalizedRef))
        {
            return status;
        }

        if (!status.OfficialUiRunning || !await IsAiToolkitApiHealthyAsync(cancellationToken).ConfigureAwait(false))
        {
            return status;
        }

        try
        {
            await EnsureAiToolkitSettingsConfiguredAsync(settings, cancellationToken).ConfigureAwait(false);

            using var jobDoc = await TryGetAiToolkitJobByRefAsync(normalizedRef, cancellationToken).ConfigureAwait(false);
            var selectedJobExists = jobDoc is not null;
            var selectedJobName = selectedJobExists ? (GetStringProperty(jobDoc!.RootElement, "name") ?? normalizedRef) : normalizedRef;
            var selectedJobState = selectedJobExists ? (GetStringProperty(jobDoc!.RootElement, "status") ?? string.Empty) : string.Empty;
            var selectedJobInfo = selectedJobExists ? (GetStringProperty(jobDoc!.RootElement, "info") ?? string.Empty) : string.Empty;

            var selectedDatasetName = normalizedRef;
            if (selectedJobExists)
            {
                var jobConfigJson = GetStringProperty(jobDoc!.RootElement, "job_config");
                var datasetFromConfig = TryGetDatasetNameFromJobConfig(jobConfigJson);
                if (!string.IsNullOrWhiteSpace(datasetFromConfig))
                {
                    selectedDatasetName = datasetFromConfig;
                }
            }

            var (datasetVisible, datasetImageCount) = await TryGetAiToolkitDatasetVisibilityAsync(
                selectedDatasetName,
                cancellationToken).ConfigureAwait(false);

            return status with
            {
                SelectedJobExists = selectedJobExists,
                SelectedJobName = selectedJobName,
                SelectedJobState = selectedJobState,
                SelectedJobInfo = selectedJobInfo,
                SelectedDatasetName = selectedDatasetName,
                SelectedDatasetVisible = datasetVisible,
                SelectedDatasetImageCount = datasetImageCount,
            };
        }
        catch
        {
            return status;
        }
    }

#endif

    private async Task<(bool Visible, int ImageCount)> TryGetAiToolkitDatasetVisibilityAsync(
        string datasetName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(datasetName))
        {
            return (false, 0);
        }

        try
        {
            var listJson = await SendAiToolkitApiRequestAsync(HttpMethod.Get, "/api/datasets/list", null, cancellationToken).ConfigureAwait(false);
            using var listDoc = JsonDocument.Parse(listJson);
            if (listDoc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (false, 0);
            }

            var visible = listDoc.RootElement
                .EnumerateArray()
                .Any(element => string.Equals(element.GetString(), datasetName, StringComparison.OrdinalIgnoreCase));

            if (!visible)
            {
                return (false, 0);
            }

            var payloadJson = JsonSerializer.Serialize(new { datasetName });
            var imagesJson = await SendAiToolkitApiRequestAsync(HttpMethod.Post, "/api/datasets/listImages", payloadJson, cancellationToken).ConfigureAwait(false);
            using var imagesDoc = JsonDocument.Parse(imagesJson);
            var images = TryGetObjectProperty(imagesDoc.RootElement, "images");
            var count = images.HasValue && images.Value.ValueKind == JsonValueKind.Array
                ? images.Value.GetArrayLength()
                : 0;

            return (true, count);
        }
        catch
        {
            return (false, 0);
        }
    }

    private static string? TryGetDatasetNameFromJobConfig(string? jobConfigJson)
    {
        if (string.IsNullOrWhiteSpace(jobConfigJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jobConfigJson);
            var config = TryGetObjectProperty(doc.RootElement, "config");
            var process = TryGetObjectProperty(config, "process");
            if (!process.HasValue || process.Value.ValueKind != JsonValueKind.Array || process.Value.GetArrayLength() == 0)
            {
                return null;
            }

            var process0 = process.Value[0];
            var datasets = TryGetObjectProperty(process0, "datasets");
            if (!datasets.HasValue || datasets.Value.ValueKind != JsonValueKind.Array || datasets.Value.GetArrayLength() == 0)
            {
                return null;
            }

            var folderPath = GetStringProperty(datasets.Value[0], "folder_path");
            return string.IsNullOrWhiteSpace(folderPath)
                ? null
                : Path.GetFileName(folderPath.TrimEnd('/', '\\'));
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> IsAiToolkitUiRespondingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 8675, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForAiToolkitUiToStopAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            if (!await IsAiToolkitUiAliveInWslAsync(settings, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return !await IsAiToolkitUiAliveInWslAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsAiToolkitUiAliveInWslAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var bashCommand =
            "set +e; " +
            "if ss -ltn 2>/dev/null | grep -q ':8675 '; then echo alive; exit 0; fi; " +
            "if ps -eo pid=,ppid=,args= | " +
            "awk -v self=\"$$\" -v parent=\"$PPID\" ' " +
            "  $1 != self && $1 != parent && $2 != self && $2 != parent && " +
            "  ($0 ~ /[n]ext-server/ || " +
            "   $0 ~ /[n]ext start --port 8675/ || " +
            "   $0 ~ /dist\\/cron\\/worker\\.js/ || " +
            "   $0 ~ /node_modules\\/\\.bin\\/concurrently/ || " +
            "   $0 ~ /WORKER,UI/) { found=1 } END { exit(found ? 0 : 1) }'; " +
            "then echo alive; exit 0; fi; " +
            "echo stopped";

        var result = await RunWslBashAsync(settings, bashCommand, progress: null, cancellationToken).ConfigureAwait(false);
        return result.CombinedOutput.Contains("alive", StringComparison.OrdinalIgnoreCase);
    }


    private async Task<string> UpsertAiToolkitJobViaApiAsync(
        string loraName,
        string jobConfigJson,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var existingJob = await TryGetAiToolkitJobByRefAsync(loraName, cancellationToken).ConfigureAwait(false);
        var existingId = existingJob is null ? null : GetStringProperty(existingJob.RootElement, "id");

        using var jobConfig = JsonDocument.Parse(jobConfigJson);
        using var payloadStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payloadStream))
        {
            writer.WriteStartObject();
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                writer.WriteString("id", existingId);
            }

            writer.WriteString("name", loraName);
            writer.WriteString("gpu_ids", "0");
            writer.WriteString("job_type", "train");
            writer.WriteString("job_ref", loraName);
            writer.WritePropertyName("job_config");
            jobConfig.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        var payloadJson = Encoding.UTF8.GetString(payloadStream.ToArray());
        var responseJson = await SendAiToolkitApiRequestAsync(HttpMethod.Post, "/api/jobs", payloadJson, cancellationToken).ConfigureAwait(false);
        using var response = JsonDocument.Parse(responseJson);
        var jobId = GetStringProperty(response.RootElement, "id");
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new InvalidOperationException("AI Toolkit did not return a job ID after saving the job.");
        }

        progress?.Report($"Linked trainer run '{loraName}' to AI Toolkit job {jobId}.");
        return jobId;
    }

    private async Task<JsonDocument?> TryGetAiToolkitJobByRefAsync(string jobRef, CancellationToken cancellationToken)
    {
        var responseJson = await SendAiToolkitApiRequestAsync(
            HttpMethod.Get,
            $"/api/jobs?job_ref={Uri.EscapeDataString(jobRef)}",
            null,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(responseJson) || string.Equals(responseJson.Trim(), "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return JsonDocument.Parse(responseJson);
    }

    private async Task<JsonDocument?> GetAiToolkitJobByIdAsync(string jobId, CancellationToken cancellationToken)
    {
        var responseJson = await SendAiToolkitApiRequestAsync(
            HttpMethod.Get,
            $"/api/jobs?id={Uri.EscapeDataString(jobId)}",
            null,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(responseJson) || string.Equals(responseJson.Trim(), "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return JsonDocument.Parse(responseJson);
    }

    private async Task<JsonDocument?> GetAiToolkitActiveTrainJobAsync(CancellationToken cancellationToken)
    {
        var responseJson = await SendAiToolkitApiRequestAsync(
            HttpMethod.Get,
            "/api/jobs?job_type=train",
            null,
            cancellationToken).ConfigureAwait(false);

        using var response = JsonDocument.Parse(responseJson);
        if (!response.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? runningJob = null;
        JsonElement? queuedJob = null;
        foreach (var job in jobs.EnumerateArray())
        {
            var status = GetStringProperty(job, "status");
            if (runningJob is null && string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
            {
                runningJob = job.Clone();
                continue;
            }

            if (queuedJob is null && string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase))
            {
                queuedJob = job.Clone();
            }
        }

        if (runningJob is null && queuedJob is null)
        {
            return null;
        }

        var selectedJob = runningJob ?? queuedJob;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            selectedJob!.Value.WriteTo(writer);
        }

        return JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private async Task<JsonDocument?> WaitForAiToolkitJobLiveStateAsync(
        string jobId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        JsonDocument? lastSeenJob = null;

        while (DateTime.UtcNow < deadline)
        {
            lastSeenJob?.Dispose();
            lastSeenJob = await GetAiToolkitJobByIdAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (lastSeenJob is null)
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var status = GetStringProperty(lastSeenJob.RootElement, "status") ?? string.Empty;
            if (string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
            {
                return lastSeenJob;
            }

            if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                var info = GetStringProperty(lastSeenJob.RootElement, "info") ?? "Unknown AI Toolkit job failure.";
                lastSeenJob.Dispose();
                throw new InvalidOperationException($"AI Toolkit reported job state '{status}': {info}");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return lastSeenJob;
    }

    private async Task<string> SendAiToolkitApiRequestAsync(
        HttpMethod method,
        string requestPath,
        string? requestBodyJson,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, requestPath);
        if (!string.IsNullOrWhiteSpace(requestBodyJson))
        {
            request.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
        }

        using var response = await _aiToolkitHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI Toolkit API request to '{requestPath}' failed with {(int)response.StatusCode}: {responseBody}");
        }

        return responseBody;
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

    private static Dictionary<string, string> ReadTrainerMetadata(string metadataWindowsPath)
    {
        var captions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(metadataWindowsPath))
        {
            return captions;
        }

        foreach (var line in File.ReadLines(metadataWindowsPath))
        {
            if (string.IsNullOrWhiteSpace(line)
                || line.StartsWith("file_name,", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("image,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columns = ParseCsvLine(line);
            if (columns.Count == 0 || string.IsNullOrWhiteSpace(columns[0]))
            {
                continue;
            }

            captions[columns[0].Trim()] = columns.Count > 1 ? columns[1] : string.Empty;
        }

        return captions;
    }

    private static void WriteTrainerMetadata(string metadataWindowsPath, IEnumerable<KeyValuePair<string, string>> rows)
    {
        using var writer = new StreamWriter(metadataWindowsPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine("image,prompt");
        foreach (var row in rows)
        {
            writer.Write(EscapeCsvValue(row.Key));
            writer.Write(',');
            writer.WriteLine(EscapeCsvValue(row.Value));
        }
    }

    private static void SyncTrainerCaptionTextFiles(string datasetWindowsPath, string metadataWindowsPath)
    {
        var rows = ReadTrainerMetadata(metadataWindowsPath);
        foreach (var row in rows)
        {
            var imagePath = Path.Combine(datasetWindowsPath, row.Key);
            if (!File.Exists(imagePath))
            {
                continue;
            }

            var captionPath = Path.ChangeExtension(imagePath, ".txt");
            File.WriteAllText(
                captionPath,
                row.Value.Replace("\r", " ").Replace("\n", " ").Trim(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (ch == ',' && !insideQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }

    private static string EscapeCsvValue(string value)
    {
        var normalized = value.Replace("\r", " ").Replace("\n", " ");
        return "\"" + normalized.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeYamlSingleQuoted(string value)
    {
        return value.Replace("\r", " ").Replace("\n", " ").Replace("'", "''");
    }

    private static bool IsSupportedTrainerImageFile(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
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

    private async Task<CommandResult> RunWslCommandAsync(
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

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (!IsTransientWslServiceFailure(result))
        {
            return result;
        }

        progress?.Report($"WSL service error while talking to '{settings.DistroName}'. Restarting that distro and retrying once.");
        await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments: ["--terminate", settings.DistroName],
            workingDirectory: Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);

        return await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTransientWslServiceFailure(CommandResult result)
    {
        return result.ExitCode != 0 &&
               result.CombinedOutput.Contains("Wsl/Service/E_UNEXPECTED", StringComparison.OrdinalIgnoreCase);
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

#if LEGACY_MANAGER_MODULE_TOOLS
    public void OpenZImageTrainerDatasetsFolder(InstallSettings settings)
    {
        OpenWslFolder(settings, $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets");
    }

    public void OpenZImageTrainerPicturesFolder(InstallSettings settings, string loraName)
    {
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        OpenWslFolder(settings, $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets/{normalizedLora}");
    }

    public void OpenZImageTrainerMetadataFile(InstallSettings settings, string datasetName)
    {
        var normalizedDataset = NormalizeTrainerName(datasetName, "dataset");
        OpenWslPath(settings, $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets/{normalizedDataset}/metadata.csv");
    }

    public void OpenZImageTrainerJobsFolder(InstallSettings settings)
    {
        OpenWslFolder(settings, "/home/nymph/ZImage-Trainer/jobs");
    }

    public void OpenZImageTrainerLorasFolder(InstallSettings settings)
    {
        OpenWslFolder(settings, $"/home/{settings.LinuxUser}/ZImage-Trainer/loras");
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

    public void OpenNymphsBrainWebUi()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "http://localhost:8081",
            UseShellExecute = true,
        });
    }

    public void OpenZImageTrainerOfficialUi()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "http://localhost:8675/jobs",
            UseShellExecute = true,
        });
    }

    public void OpenZImageTrainerOfficialUiJob(string jobId)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = $"http://localhost:8675/jobs/{jobId}",
            UseShellExecute = true,
        });
    }

    public void OpenZImageTrainerGradioUi()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "http://localhost:7861",
            UseShellExecute = true,
        });
    }

#endif

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

#if LEGACY_MANAGER_MODULE_TOOLS
    public async Task RunModelPrefetchOnlyAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken,
        string backend = "all")
    {
        if (!string.Equals(settings.DistroName, ManagedDistroName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Model downloads are pinned to the canonical {ManagedDistroName} WSL distro. " +
                $"Current target was '{settings.DistroName}'. Open or repair the {ManagedDistroName} install before fetching models.");
        }

        var prefetchScript = RequireScript("prefetch_models.sh");
        var wslPrefetchScriptPath = ConvertWindowsPathToWsl(prefetchScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {prefetchScript}");
        var normalizedBackend = NormalizeModelPrefetchBackend(backend);

        var tokenExport = string.IsNullOrWhiteSpace(settings.HuggingFaceToken)
            ? string.Empty
            : $"export NYMPHS3D_HF_TOKEN={ToBashSingleQuoted(settings.HuggingFaceToken.Trim())}; ";
        var trellisQuant = NormalizeTrellisGgufPrefetchQuant(settings.TrellisGgufQuant);

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            tokenExport +
            "export NYMPHS3D_RUNTIME_ROOT=\"$HOME\"; " +
            "export NYMPHS3D_Z_IMAGE_DIR=\"$HOME/Z-Image\"; " +
            "export NYMPHS3D_N2D2_DIR=\"$NYMPHS3D_Z_IMAGE_DIR\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
            $"export TRELLIS_GGUF_QUANT={ToBashSingleQuoted(trellisQuant)}; " +
            $"bash {ToBashSingleQuoted(wslPrefetchScriptPath)} --backend {ToBashSingleQuoted(normalizedBackend)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report($"Model prefetch: downloading {FriendlyBackendLabel(normalizedBackend)} model weights only into WSL distro {ManagedDistroName}. TRELLIS GGUF quant: {trellisQuant}. Runtime repair is not part of this step.");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Model prefetch failed.");
        }
    }

    public async Task RunTrellisAdapterRepairAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var sourceApi = RequireScript(Path.Combine("trellis_adapter", "api_server_trellis_gguf.py"));
        var sourceCommon = RequireScript(Path.Combine("trellis_adapter", "trellis_gguf_common.py"));
        var wslSourceApi = ConvertWindowsPathToWsl(sourceApi)
            ?? throw new InvalidOperationException($"Could not convert adapter path for WSL: {sourceApi}");
        var wslSourceCommon = ConvertWindowsPathToWsl(sourceCommon)
            ?? throw new InvalidOperationException($"Could not convert adapter path for WSL: {sourceCommon}");
        var trellisRuntimeDir = $"/home/{settings.LinuxUser}/TRELLIS.2";
        var trellisScriptDir = $"{trellisRuntimeDir}/scripts";
        var trellisQuant = NormalizeTrellisGgufRuntimeQuant(settings.TrellisGgufQuant);

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export NYMPHS3D_RUNTIME_ROOT={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export NYMPHS3D_TRELLIS_DIR={ToBashSingleQuoted(trellisRuntimeDir)}; " +
            $"export TRELLIS_GGUF_QUANT={ToBashSingleQuoted(trellisQuant)}; " +
            $"mkdir -p {ToBashSingleQuoted(trellisScriptDir)}; " +
            $"install -m 644 {ToBashSingleQuoted(wslSourceApi)} {ToBashSingleQuoted($"{trellisScriptDir}/api_server_trellis_gguf.py")}; " +
            $"install -m 644 {ToBashSingleQuoted(wslSourceCommon)} {ToBashSingleQuoted($"{trellisScriptDir}/trellis_gguf_common.py")}; " +
            $"echo \"Managed TRELLIS GGUF adapter scripts repaired at {trellisScriptDir}.\"";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report("TRELLIS repair: syncing managed GGUF adapter scripts into the runtime.");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("TRELLIS adapter repair failed.");
        }
    }

    public async Task RunSmokeTestAsync(
        InstallSettings settings,
        string backend,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var smokeScript = RequireScript("smoke_test_server.sh");
        var wslSmokeScriptPath = ConvertWindowsPathToWsl(smokeScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {smokeScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export NYMPHS3D_RUNTIME_ROOT=\"$HOME\"; " +
            "export NYMPHS3D_Z_IMAGE_DIR=\"$HOME/Z-Image\"; " +
            "export NYMPHS3D_N2D2_DIR=\"$NYMPHS3D_Z_IMAGE_DIR\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
            $"export TRELLIS_GGUF_QUANT={ToBashSingleQuoted(NormalizeTrellisGgufRuntimeQuant(settings.TrellisGgufQuant))}; " +
            $"bash {ToBashSingleQuoted(wslSmokeScriptPath)} --backend {ToBashSingleQuoted(backend)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report($"Smoke test: running {FriendlyBackendLabel(backend)} startup check...");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{FriendlyBackendLabel(backend)} smoke test failed.");
        }
    }

#endif

    public async Task RunManagedRepoUpdateCheckAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var checkScript = RequireScript("check_managed_repo_updates.sh");
        var wslCheckScriptPath = ConvertWindowsPathToWsl(checkScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {checkScript}");
        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export NYMPHS3D_RUNTIME_ROOT=\"$HOME\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
            $"bash {ToBashSingleQuoted(wslCheckScriptPath)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report("Checking managed repo update state inside the existing distro...");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Managed repo update check failed.");
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

    public string? GetInstalledNymphModuleMarkerVersion(InstallSettings settings, string moduleId)
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
                return string.IsNullOrWhiteSpace(markerVersion) ? "unknown" : markerVersion;
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return null;
    }

    public async Task<string?> GetInstalledNymphModuleMarkerVersionAsync(
        InstallSettings settings,
        string moduleId,
        CancellationToken cancellationToken)
    {
        var directMarkerVersion = GetInstalledNymphModuleMarkerVersion(settings, moduleId);
        if (!string.IsNullOrWhiteSpace(directMarkerVersion))
        {
            return directMarkerVersion;
        }

        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);
        var fallbackRoot = $"/home/{settings.LinuxUser}/{normalizedModuleId}";
        var bashCommand =
            "set -euo pipefail; " +
            "for marker in " +
            $"{ToBashSingleQuoted($"{installRoot}/.nymph-module-version")} " +
            $"{ToBashSingleQuoted($"{fallbackRoot}/.nymph-module-version")}; do " +
            "  if [[ -f \"$marker\" ]]; then head -n 1 \"$marker\"; exit 0; fi; " +
            "done; " +
            "exit 0";

        var result = await RunWslBashAsync(settings, bashCommand, progress: null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var markerVersion = result.CombinedOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(markerVersion) ? null : markerVersion.Trim();
    }

    public async Task<IReadOnlyDictionary<string, NymphModuleMarkerProbe>> GetInstalledNymphModuleMarkerProbesAsync(
        InstallSettings settings,
        IEnumerable<NymphModuleManifestInfo> modules,
        CancellationToken cancellationToken)
    {
        var probeTargets = modules
            .Where(module => !string.IsNullOrWhiteSpace(module.Id))
            .SelectMany(module =>
            {
                var normalizedModuleId = module.Id.Trim().ToLowerInvariant();
                var resolvedRoots = new[]
                {
                    ResolveNymphModuleInstallRoot(settings, normalizedModuleId, module.InstallRoot),
                    GetNymphModuleInstallRoot(settings, normalizedModuleId),
                    $"/home/{settings.LinuxUser}/{normalizedModuleId}",
                };

                return resolvedRoots
                    .Where(root => !string.IsNullOrWhiteSpace(root))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(root => new
                    {
                        ModuleId = normalizedModuleId,
                        InstallRoot = root,
                    });
            })
            .GroupBy(module => $"{module.ModuleId}\t{module.InstallRoot}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (probeTargets.Count == 0)
        {
            return new Dictionary<string, NymphModuleMarkerProbe>(StringComparer.OrdinalIgnoreCase);
        }

        var targetLines = string.Join("\n", probeTargets.Select(target => $"{target.ModuleId}\t{target.InstallRoot}"));
        var bashCommand =
            "set -euo pipefail\n" +
            "while IFS=$'\\t' read -r module root; do\n" +
            "  [[ -n \"$module\" && -n \"$root\" ]] || continue\n" +
            "  marker=\"$root/.nymph-module-version\"\n" +
            "  marker_present=false\n" +
            "  root_present=false\n" +
            "  repair_present=false\n" +
            "  version=not-installed\n" +
            "  [[ -e \"$root\" ]] && root_present=true\n" +
            "  if [[ -f \"$root/nymph.json\" || -d \"$root/bin\" || -f \"$root/package.json\" || -d \"$root/.venv\" || -d \"$root/.venv-nunchaku\" || -d \"$root/dist\" || -d \"$root/server\" ]]; then\n" +
            "    repair_present=true\n" +
            "  fi\n" +
            "  if [[ -f \"$marker\" ]]; then\n" +
            "    marker_present=true\n" +
            "    repair_present=false\n" +
            "    version=\"$(head -n 1 \"$marker\" 2>/dev/null | tr -d '\\r' | awk '{$1=$1; print}' || true)\"\n" +
            "    [[ -n \"$version\" ]] || version=unknown\n" +
            "  fi\n" +
            "  printf 'id=%s marker_present=%s root_present=%s repair_present=%s version=%s install_root=%s\\n' \"$module\" \"$marker_present\" \"$root_present\" \"$repair_present\" \"$version\" \"$root\"\n" +
            "done <<'NYMPH_MODULE_MARKERS'\n" +
            targetLines + "\n" +
            "NYMPH_MODULE_MARKERS";

        var result = await RunWslBashAsync(settings, bashCommand, progress: null, cancellationToken).ConfigureAwait(false);
        var probes = new Dictionary<string, NymphModuleMarkerProbe>(StringComparer.OrdinalIgnoreCase);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.CombinedOutput)
                ? $"Fast module marker scan failed with exit code {result.ExitCode}."
                : $"Fast module marker scan failed with exit code {result.ExitCode}: {result.CombinedOutput.Trim()}";
            throw new InvalidOperationException(detail);
        }

        foreach (var line in result.CombinedOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var values = ParseKeyValueStatusLine(line);
            if (!values.TryGetValue("id", out var moduleId) || string.IsNullOrWhiteSpace(moduleId))
            {
                continue;
            }

            var currentProbe = new NymphModuleMarkerProbe(
                ModuleId: moduleId,
                MarkerPresent: values.TryGetValue("marker_present", out var markerPresent) && string.Equals(markerPresent, "true", StringComparison.OrdinalIgnoreCase),
                InstallRootPresent: values.TryGetValue("root_present", out var rootPresent) && string.Equals(rootPresent, "true", StringComparison.OrdinalIgnoreCase),
                RepairCandidatePresent: values.TryGetValue("repair_present", out var repairPresent) && string.Equals(repairPresent, "true", StringComparison.OrdinalIgnoreCase),
                Version: values.TryGetValue("version", out var version) && !string.IsNullOrWhiteSpace(version) ? version : "unknown",
                InstallRoot: values.TryGetValue("install_root", out var installRoot) ? installRoot : "");

            if (!probes.TryGetValue(moduleId, out var existingProbe) ||
                ShouldReplaceNymphModuleMarkerProbe(existingProbe, currentProbe))
            {
                probes[moduleId] = currentProbe;
            }
        }

        return probes;
    }

    private static bool ShouldReplaceNymphModuleMarkerProbe(NymphModuleMarkerProbe existingProbe, NymphModuleMarkerProbe candidateProbe)
    {
        if (candidateProbe.MarkerPresent != existingProbe.MarkerPresent)
        {
            return candidateProbe.MarkerPresent;
        }

        if (candidateProbe.RepairCandidatePresent != existingProbe.RepairCandidatePresent)
        {
            return candidateProbe.RepairCandidatePresent;
        }

        if (candidateProbe.InstallRootPresent != existingProbe.InstallRootPresent)
        {
            return candidateProbe.InstallRootPresent;
        }

        return false;
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
        var managerUiTitle = "";
        if (manifestRoot.TryGetProperty("ui", out var manifestUiElement) &&
            manifestUiElement.ValueKind == JsonValueKind.Object &&
            manifestUiElement.TryGetProperty("manager_ui", out var managerUiElement) &&
            managerUiElement.ValueKind == JsonValueKind.Object)
        {
            managerUiTitle = GetJsonString(managerUiElement, "title") ?? "";
        }

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
            OverviewDetail: BuildNymphModuleOverviewDetail(manifestRoot, registryElement),
            ManifestUrl: manifestUrl,
            RepositoryUrl: repositoryUrl,
            SourceSummary: sourceSummary,
            InstallRoot: installRoot,
            ManagerUiTitle: managerUiTitle,
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

    private async Task<TrustedModuleSourceInfo?> TryGetTrustedNymphModuleSourceInfoAsync(
        string moduleId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var registryDocument = await FetchJsonDocumentAsync(NymphModuleRegistryUrl, cancellationToken).ConfigureAwait(false);
            foreach (var moduleElement in registryDocument.RootElement.GetProperty("modules").EnumerateArray())
            {
                var registryModuleId = GetJsonString(moduleElement, "id")?.Trim().ToLowerInvariant();
                if (!string.Equals(registryModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (moduleElement.TryGetProperty("trusted", out var trustedElement) &&
                    trustedElement.ValueKind == JsonValueKind.False)
                {
                    return null;
                }

                var manifestUrl = GetJsonString(moduleElement, "manifest_url")?.Trim();
                if (string.IsNullOrWhiteSpace(manifestUrl))
                {
                    return null;
                }

                using var manifestDocument = await FetchJsonDocumentAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
                var repositoryUrl = ResolveNymphModuleRepositoryUrl(moduleElement, manifestDocument.RootElement, manifestUrl);
                var branch = ResolveNymphModuleRepositoryBranch(manifestDocument.RootElement, manifestUrl);
                return string.IsNullOrWhiteSpace(repositoryUrl) || string.IsNullOrWhiteSpace(branch)
                    ? null
                    : new TrustedModuleSourceInfo(repositoryUrl, branch);
            }
        }
        catch
        {
            // If the registry network read is unavailable, official modules still follow
            // the nymphnerds/<module-id> repo convention used by the registry.
        }

        return Regex.IsMatch(moduleId, "^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)
            ? new TrustedModuleSourceInfo($"https://github.com/nymphnerds/{moduleId}.git", "main")
            : null;
    }

    private static string ResolveNymphModuleRepositoryUrl(
        JsonElement registryElement,
        JsonElement manifestRoot,
        string manifestUrl)
    {
        var repositoryUrl = "";
        if (manifestRoot.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.Object)
        {
            repositoryUrl = GetJsonString(sourceElement, "repo")
                ?? GetJsonString(sourceElement, "repository")
                ?? "";
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

        return repositoryUrl.Trim();
    }

    private static string ResolveNymphModuleRepositoryBranch(JsonElement manifestRoot, string manifestUrl)
    {
        var branch = "";
        if (manifestRoot.TryGetProperty("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.Object)
        {
            branch = GetJsonString(sourceElement, "ref") ?? "";
        }

        if (string.IsNullOrWhiteSpace(branch) &&
            manifestRoot.TryGetProperty("repo", out var repoElement) &&
            repoElement.ValueKind == JsonValueKind.Object)
        {
            branch = GetJsonString(repoElement, "branch") ?? GetJsonString(repoElement, "ref") ?? "";
        }

        if (string.IsNullOrWhiteSpace(branch) &&
            Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                branch = parts[2];
            }
        }

        branch = branch.Trim();
        return Regex.IsMatch(branch, "^[A-Za-z0-9][A-Za-z0-9._/-]{0,119}$", RegexOptions.CultureInvariant)
            ? branch
            : "";
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

    private static string BuildNymphModuleOverviewDetail(JsonElement manifestRoot, JsonElement registryElement)
    {
        var registryLines = BuildOverviewBlockLines(registryElement);
        if (registryLines.Count > 0)
        {
            return string.Join("\n", registryLines);
        }

        return string.Join("\n", BuildOverviewBlockLines(manifestRoot));
    }

    private static List<string> BuildOverviewBlockLines(JsonElement root)
    {
        var lines = new List<string>();
        if (!root.TryGetProperty("overview", out var overview) || overview.ValueKind != JsonValueKind.Object)
        {
            return lines;
        }

        var body = GetJsonString(overview, "body")
            ?? GetJsonString(overview, "details")
            ?? GetJsonString(overview, "description");
        if (!string.IsNullOrWhiteSpace(body))
        {
            lines.Add(body.Trim());
        }

        AppendStringArrayOverviewLine(lines, overview, "works_with", "Works with");
        AppendStringArrayOverviewLine(lines, overview, "requirements", "Requirements");
        AppendStringArrayOverviewLine(lines, overview, "compatibility", "Compatibility");

        if (!overview.TryGetProperty("links", out var linksElement) ||
            linksElement.ValueKind != JsonValueKind.Array)
        {
            return lines;
        }

        foreach (var linkElement in linksElement.EnumerateArray())
        {
            if (linkElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var label = GetJsonString(linkElement, "label") ?? GetJsonString(linkElement, "name") ?? "Link";
            var url = GetJsonString(linkElement, "url") ?? GetJsonString(linkElement, "href") ?? "";
            if (!string.IsNullOrWhiteSpace(url))
            {
                lines.Add($"{label}: {url}");
            }
        }

        return lines;
    }

    private static void AppendStringArrayOverviewLine(List<string> lines, JsonElement overview, string propertyName, string label)
    {
        if (!overview.TryGetProperty(propertyName, out var arrayElement) ||
            arrayElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var values = arrayElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (values.Count > 0)
        {
            lines.Add($"{label}: {string.Join(", ", values)}");
        }
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
        return normalizedModuleId switch
        {
            "brain" => $"{homePath}/Nymphs-Brain",
            "zimage" => $"{homePath}/Z-Image",
            "trellis" => $"{homePath}/TRELLIS.2",
            "lora" or "ai-toolkit" => $"{homePath}/ZImage-Trainer",
            "worbi" => $"{homePath}/worbi",
            _ => $"{homePath}/{normalizedModuleId}",
        };
    }

    private static string ResolveNymphModuleInstallRoot(InstallSettings settings, string normalizedModuleId, string? manifestInstallRoot)
    {
        var homePath = $"/home/{settings.LinuxUser}";
        var normalized = string.IsNullOrWhiteSpace(manifestInstallRoot)
            ? GetNymphModuleInstallRoot(settings, normalizedModuleId)
            : manifestInstallRoot.Trim();

        if (normalized.StartsWith("$HOME/", StringComparison.Ordinal))
        {
            return $"{homePath}/{normalized["$HOME/".Length..]}";
        }

        if (normalized.Equals("$HOME", StringComparison.Ordinal))
        {
            return homePath;
        }

        if (normalized.StartsWith("~/", StringComparison.Ordinal))
        {
            return $"{homePath}/{normalized[2..]}";
        }

        if (normalized.Equals("~", StringComparison.Ordinal))
        {
            return homePath;
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return $"{homePath}/{normalized.TrimStart('/')}";
        }

        return normalized;
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

    public InstalledNymphModuleUiInfo? GetInstalledNymphModuleUiInfo(InstallSettings settings, string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);
        var installRootWindowsPath = ToWindowsWslPath(settings, installRoot);
        var manifestPath = Path.Combine(installRootWindowsPath, "nymph.json");
        var versionMarkerPath = Path.Combine(installRootWindowsPath, ".nymph-module-version");

        if (!File.Exists(versionMarkerPath))
        {
            return null;
        }

        var manifestSourcePath = manifestPath;
        if (!File.Exists(manifestSourcePath))
        {
            var cachedManifestPath = ToWindowsWslPath(
                settings,
                $"/home/{settings.LinuxUser}/.cache/nymphs-modules/{normalizedModuleId}.nymph.json");
            if (!File.Exists(cachedManifestPath))
            {
                return null;
            }

            manifestSourcePath = cachedManifestPath;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestSourcePath));
        var root = document.RootElement;
        JsonElement managerUiElement = default;
        var hasManagerUi = root.TryGetProperty("ui", out var uiElement) &&
                           uiElement.ValueKind == JsonValueKind.Object &&
                           uiElement.TryGetProperty("manager_ui", out managerUiElement) &&
                           managerUiElement.ValueKind == JsonValueKind.Object;
        if (!hasManagerUi)
        {
            if (!root.TryGetProperty("runtime", out var runtimeElement) ||
                runtimeElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var runtimeUrl = GetJsonString(runtimeElement, "frontend_url") ?? "";
            if (!Uri.TryCreate(runtimeUrl, UriKind.Absolute, out var runtimeUri) ||
                (!string.Equals(runtimeUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(runtimeUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return new InstalledNymphModuleUiInfo(
                normalizedModuleId,
                "local_url",
                GetJsonString(root, "name") ?? "Module UI",
                runtimeUrl,
                runtimeUrl);
        }

        var type = GetJsonString(managerUiElement, "type") ?? "";
        if (string.Equals(type, "local_url", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "url", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "local_web_url", StringComparison.OrdinalIgnoreCase))
        {
            var url = GetJsonString(managerUiElement, "url")
                ?? GetJsonString(managerUiElement, "href")
                ?? "";
            if (Uri.TryCreate(url, UriKind.Absolute, out var uiUri) &&
                (string.Equals(uiUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uiUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return new InstalledNymphModuleUiInfo(
                    normalizedModuleId,
                    "local_url",
                    GetJsonString(managerUiElement, "title") ?? "Module UI",
                    url,
                    url);
            }

            return null;
        }

        if (!string.Equals(type, "local_html", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "local_web_app", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var entrypoint = NormalizeSafeRelativeModulePath(GetJsonString(managerUiElement, "entrypoint"));
        if (string.IsNullOrWhiteSpace(entrypoint))
        {
            return null;
        }

        var candidatePath = Path.GetFullPath(Path.Combine(
            installRootWindowsPath,
            entrypoint.Replace('/', Path.DirectorySeparatorChar)));
        var installRootFullPath = Path.GetFullPath(installRootWindowsPath).TrimEnd(Path.DirectorySeparatorChar);
        if (!candidatePath.StartsWith(installRootFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(candidatePath))
        {
            return null;
        }

        return new InstalledNymphModuleUiInfo(
            normalizedModuleId,
            type,
            GetJsonString(managerUiElement, "title") ?? "Module UI",
            entrypoint,
            candidatePath);
    }

    public InstalledNymphModuleUiInfo? GetCachedInstalledNymphModuleUiInfo(InstallSettings settings, string moduleId)
    {
        var uiInfo = GetInstalledNymphModuleUiInfo(settings, moduleId);
        if (uiInfo is null)
        {
            return null;
        }

        if (string.Equals(uiInfo.Type, "local_url", StringComparison.OrdinalIgnoreCase))
        {
            return uiInfo;
        }

        try
        {
            var sourceFile = Path.GetFullPath(uiInfo.WindowsPath);
            var entrypointParts = uiInfo.Entrypoint
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var sourceRoot = ResolveModuleUiCopyRoot(sourceFile, entrypointParts);
            if (!Directory.Exists(sourceRoot))
            {
                return uiInfo;
            }

            var cacheRoot = Path.Combine(LogFolderPath, "ModuleUiCache", uiInfo.ModuleId);
            var cacheCopyRoot = BuildModuleUiCacheCopyRoot(cacheRoot, entrypointParts);
            var cachedEntrypoint = Path.Combine(
                cacheRoot,
                uiInfo.Entrypoint.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(cacheRoot);
            if (!File.Exists(cachedEntrypoint) || ModuleUiCacheNeedsRefresh(sourceRoot, cacheCopyRoot))
            {
                if (Directory.Exists(cacheCopyRoot))
                {
                    Directory.Delete(cacheCopyRoot, recursive: true);
                }

                CopyDirectory(sourceRoot, cacheCopyRoot);
            }

            return File.Exists(cachedEntrypoint)
                ? uiInfo with { WindowsPath = cachedEntrypoint }
                : uiInfo;
        }
        catch (Exception)
        {
            return uiInfo;
        }
    }

    private static string ResolveModuleUiCopyRoot(string sourceFile, IReadOnlyList<string> entrypointParts)
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile) ?? sourceFile;
        return entrypointParts.Count <= 1
            ? sourceDirectory
            : Path.Combine(GetAncestorDirectory(sourceDirectory, entrypointParts.Count - 1), entrypointParts[0]);
    }

    private static bool ModuleUiCacheNeedsRefresh(string sourceRoot, string cacheCopyRoot)
    {
        if (!Directory.Exists(cacheCopyRoot))
        {
            return true;
        }

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            var cachedFile = Path.Combine(cacheCopyRoot, relativePath);
            if (!File.Exists(cachedFile))
            {
                return true;
            }

            var sourceInfo = new FileInfo(sourceFile);
            var cachedInfo = new FileInfo(cachedFile);
            if (sourceInfo.LastWriteTimeUtc > cachedInfo.LastWriteTimeUtc)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetAncestorDirectory(string path, int levels)
    {
        var current = path;
        for (var i = 0; i < levels; i++)
        {
            current = Path.GetDirectoryName(current) ?? current;
        }

        return current;
    }

    private static string BuildModuleUiCacheCopyRoot(string cacheRoot, IReadOnlyList<string> entrypointParts)
    {
        return entrypointParts.Count <= 1
            ? cacheRoot
            : Path.Combine(cacheRoot, entrypointParts[0]);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectory);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static string? NormalizeSafeRelativeModulePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Trim().Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains("://", StringComparison.Ordinal) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            return null;
        }

        return normalized;
    }

    private static bool HasSafeModuleActionEntrypoint(
        InstallSettings settings,
        string manifestLinuxPath,
        string action)
    {
        try
        {
            var manifestWindowsPath = ToWindowsWslPath(settings, manifestLinuxPath);
            if (!File.Exists(manifestWindowsPath))
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifestWindowsPath));
            var root = document.RootElement;
            var entrypoint = "";
            if (root.TryGetProperty("entrypoints", out var entrypointsElement) &&
                entrypointsElement.ValueKind == JsonValueKind.Object)
            {
                entrypoint = GetJsonString(entrypointsElement, action) ?? "";
            }

            if (string.IsNullOrWhiteSpace(entrypoint) &&
                root.TryGetProperty("manager", out var managerElement) &&
                managerElement.ValueKind == JsonValueKind.Object)
            {
                entrypoint = GetJsonString(managerElement, action) ?? "";
            }

            return !string.IsNullOrWhiteSpace(NormalizeSafeRelativeModulePath(entrypoint));
        }
        catch
        {
            return false;
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
            [],
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

        var normalizedActionArguments = NormalizeModuleActionArguments(actionArguments);
        var quotedActionArguments = normalizedActionArguments.Count == 0
            ? string.Empty
            : " " + string.Join(" ", normalizedActionArguments.Select(ToBashSingleQuoted));
        var normalizedModuleId = moduleId.Trim().ToLowerInvariant();
        var homePath = $"/home/{settings.LinuxUser}";
        var installRoot = GetNymphModuleInstallRoot(settings, normalizedModuleId);
        var cacheRepo = $"{homePath}/.cache/nymphs-modules/repos/{normalizedModuleId}";
        var moduleWorkRoot = $"{homePath}/.cache/nymphs-modules";
        var manifestPath = $"{cacheRepo}/nymph.json";
        var installedManifestPath = $"{installRoot}/nymph.json";
        var versionMarkerPath = $"{installRoot}/.nymph-module-version";
        var localBinEntrypoint = $"{homePath}/.local/bin/{normalizedModuleId}-{normalizedAction}";
        var installRootBinEntrypoint = $"{installRoot}/bin/{normalizedModuleId}-{normalizedAction}";
        var conventionalEntrypoint = $"scripts/{normalizedModuleId.Replace('-', '_')}_{normalizedAction}.sh";
        var isStatusAction = string.Equals(normalizedAction, "status", StringComparison.OrdinalIgnoreCase);
        var commandTimeoutPrefix = isStatusAction ? "timeout 6s " : string.Empty;
        var unavailableStatus =
            $"echo id={ToBashSingleQuoted(normalizedModuleId)}; echo installed=false; echo running=false; echo version=not-installed; echo state=available; echo health=unknown; echo install_root={ToBashSingleQuoted(installRoot)}; echo detail={ToBashSingleQuoted("Module is available from the registry, but is not installed yet.")}; exit 0; ";
        var repairNeededStatus =
            $"echo id={ToBashSingleQuoted(normalizedModuleId)}; echo installed=false; echo running=false; echo runtime_present=true; echo data_present=true; echo version=not-installed; echo state=repair_needed; echo health=repair-needed; echo install_root={ToBashSingleQuoted(installRoot)}; echo detail={ToBashSingleQuoted("Existing module files were found, but the modular install marker is missing. Use Repair Module to finish or convert this install.")}; exit 0; ";
        var hasLocalActionEntrypoint = !isStatusAction &&
            (HasSafeModuleActionEntrypoint(settings, installedManifestPath, normalizedAction) ||
             HasSafeModuleActionEntrypoint(settings, manifestPath, normalizedAction));
        var trustedModuleSource = isStatusAction || hasLocalActionEntrypoint
            ? null
            : await TryGetTrustedNymphModuleSourceInfoAsync(normalizedModuleId, cancellationToken).ConfigureAwait(false);
        var moduleActionRepoUrl = trustedModuleSource?.RepositoryUrl ?? "";
        var moduleActionRepoBranch = trustedModuleSource?.Branch ?? "";
        if (!isStatusAction && string.IsNullOrWhiteSpace(moduleActionRepoUrl))
        {
            moduleActionRepoUrl = $"https://github.com/nymphnerds/{normalizedModuleId}.git";
        }

        if (!isStatusAction && string.IsNullOrWhiteSpace(moduleActionRepoBranch))
        {
            moduleActionRepoBranch = "main";
        }

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
            (isStatusAction
                ? $"if [[ ! -f {ToBashSingleQuoted(versionMarkerPath)} ]]; then if [[ -e {ToBashSingleQuoted(installRoot)} ]]; then {repairNeededStatus} else {unavailableStatus} fi; fi; "
                : string.Empty) +
            $"if [[ -f {ToBashSingleQuoted(installedManifestPath)} ]]; then " +
            $"ENTRYPOINT=$(MODULE_ACTION={ToBashSingleQuoted(normalizedAction)} python3 -c {ToBashSingleQuoted(entrypointReader)} {ToBashSingleQuoted(installedManifestPath)} 2>/dev/null || true); " +
            "fi; " +
            $"if [[ -z \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(manifestPath)} ]]; then " +
            $"ENTRYPOINT=$(MODULE_ACTION={ToBashSingleQuoted(normalizedAction)} python3 -c {ToBashSingleQuoted(entrypointReader)} {ToBashSingleQuoted(manifestPath)} 2>/dev/null || true); " +
            "fi; " +
            $"if [[ -z \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(cacheRepo)}/{ToBashSingleQuoted(conventionalEntrypoint)} ]]; then " +
            $"ENTRYPOINT={ToBashSingleQuoted(conventionalEntrypoint)}; " +
            "fi; " +
            (!isStatusAction
                ? "if [[ -z \"$ENTRYPOINT\" ]]; then " +
                  $"  mkdir -p {ToBashSingleQuoted(moduleWorkRoot)}/repos; " +
                  "  if command -v git >/dev/null 2>&1; then " +
                  $"        rm -rf {ToBashSingleQuoted(cacheRepo)}; " +
                  $"        git clone --depth 1 --branch {ToBashSingleQuoted(moduleActionRepoBranch)} {ToBashSingleQuoted(moduleActionRepoUrl)} {ToBashSingleQuoted(cacheRepo)} || echo module_action_repo_clone_failed={ToBashSingleQuoted(cacheRepo)} >&2; " +
                  "    else " +
                  "      echo module_action_refresh_tools_missing >&2; " +
                  "  fi; " +
                  $"  if [[ -f {ToBashSingleQuoted(manifestPath)} ]]; then " +
                  $"    ENTRYPOINT=$(MODULE_ACTION={ToBashSingleQuoted(normalizedAction)} python3 -c {ToBashSingleQuoted(entrypointReader)} {ToBashSingleQuoted(manifestPath)} 2>/dev/null || true); " +
                  "  fi; " +
                  $"  if [[ -z \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(cacheRepo)}/{ToBashSingleQuoted(conventionalEntrypoint)} ]]; then " +
                  $"    ENTRYPOINT={ToBashSingleQuoted(conventionalEntrypoint)}; " +
                  "  fi; " +
                  "fi; "
                : string.Empty) +
            $"if [[ -n \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(installRoot)}/\"$ENTRYPOINT\" ]]; then " +
            $"  {commandTimeoutPrefix}bash {ToBashSingleQuoted(installRoot)}/\"$ENTRYPOINT\"{quotedActionArguments}; " +
            $"elif [[ -n \"$ENTRYPOINT\" && -f {ToBashSingleQuoted(cacheRepo)}/\"$ENTRYPOINT\" ]]; then " +
            $"  {commandTimeoutPrefix}bash {ToBashSingleQuoted(cacheRepo)}/\"$ENTRYPOINT\"{quotedActionArguments}; " +
            $"elif [[ {(isStatusAction ? $"-f {ToBashSingleQuoted(versionMarkerPath)} && " : string.Empty)}-x {ToBashSingleQuoted(localBinEntrypoint)} ]]; then " +
            $"  {commandTimeoutPrefix}{ToBashSingleQuoted(localBinEntrypoint)}{quotedActionArguments}; " +
            $"elif [[ {(isStatusAction ? $"-f {ToBashSingleQuoted(versionMarkerPath)} && " : string.Empty)}-x {ToBashSingleQuoted(installRootBinEntrypoint)} ]]; then " +
            $"  {commandTimeoutPrefix}{ToBashSingleQuoted(installRootBinEntrypoint)}{quotedActionArguments}; " +
            "else " +
            (isStatusAction
                ? $"  {unavailableStatus}"
                : $"  echo {ToBashSingleQuoted($"Module action is not available: {normalizedModuleId}/{normalizedAction}")} >&2; " +
                  $"  echo checked_installed_manifest={ToBashSingleQuoted(installedManifestPath)} >&2; " +
                  $"  echo checked_cached_manifest={ToBashSingleQuoted(manifestPath)} >&2; " +
                  $"  echo checked_cache_repo={ToBashSingleQuoted(cacheRepo)} >&2; " +
                  "  exit 5; ") +
            "fi";

        if (!isStatusAction)
        {
            progress.Report($"Module action source '{normalizedModuleId}' -> {moduleActionRepoUrl}#{moduleActionRepoBranch}");
        }

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

    private static IReadOnlyList<string> NormalizeModuleActionArguments(IReadOnlyList<string> actionArguments)
    {
        if (actionArguments.Count == 0)
        {
            return [];
        }

        var normalized = new List<string>(actionArguments.Count);
        foreach (var argument in actionArguments)
        {
            var value = argument.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (value.Length > 256 ||
                value.Any(char.IsControl) ||
                !Regex.IsMatch(value, "^[A-Za-z0-9._=:/@+-]+$", RegexOptions.CultureInvariant))
            {
                throw new ArgumentException($"Unsupported module action argument: {argument}", nameof(actionArguments));
            }

            normalized.Add(value);
        }

        return normalized;
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

#if LEGACY_MANAGER_MODULE_TOOLS
    public async Task<IReadOnlyDictionary<string, RuntimeBackendStatus>> GetRuntimeBackendStatusesAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var statusScript = RequireScript("runtime_tools_status.sh");
        var wslStatusScriptPath = ConvertWindowsPathToWsl(statusScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {statusScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export NYMPHS3D_RUNTIME_ROOT=\"$HOME\"; " +
            "export NYMPHS3D_Z_IMAGE_DIR=\"$HOME/Z-Image\"; " +
            "export NYMPHS3D_N2D2_DIR=\"$NYMPHS3D_Z_IMAGE_DIR\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
            $"export TRELLIS_GGUF_QUANT={ToBashSingleQuoted(NormalizeTrellisGgufRuntimeQuant(settings.TrellisGgufQuant))}; " +
            $"bash {ToBashSingleQuoted(wslStatusScriptPath)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report("Checking runtime tool status inside the managed distro...");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Runtime tool status check failed.");
        }

        progress.Report("Managed runtime tool status checked.");

        var statuses = new Dictionary<string, RuntimeBackendStatus>(StringComparer.OrdinalIgnoreCase);
        var lines = result.CombinedOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (!TryParseRuntimeBackendStatus(line, out var status))
            {
                continue;
            }

            statuses[status.BackendId] = status;
        }

        return statuses;
    }

#endif

    public async Task CheckRuntimeDependencyUpdatesAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var checkerScript = RequireScript("check_runtime_dependency_updates.py");
        var wslCheckerScriptPath = ConvertWindowsPathToWsl(checkerScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {checkerScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"python3 {ToBashSingleQuoted(wslCheckerScriptPath)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report("Checking pinned runtime dependencies against upstream...");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode > 1)
        {
            throw new InvalidOperationException("Runtime dependency update check failed.");
        }

        progress.Report(
            result.ExitCode == 0
                ? "Runtime dependency pins are up to date."
                : "Runtime dependency updates are available. Test them before changing release pins.");
    }

    public async Task CheckInstalledRuntimeStateAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var checkerScript = RequireScript("check_installed_runtime_state.py");
        var wslCheckerScriptPath = ConvertWindowsPathToWsl(checkerScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {checkerScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"python3 {ToBashSingleQuoted(wslCheckerScriptPath)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report("Checking installed runtime against the current Manager pins...");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode > 1)
        {
            throw new InvalidOperationException("Installed runtime state check failed.");
        }

        progress.Report(
            result.ExitCode == 0
                ? "Installed runtime matches the current Manager pins."
                : "Installed runtime differs from the current Manager pins.");
    }

    public async Task<string> GetRuntimeDependencyModeAsync(
        InstallSettings settings,
        CancellationToken cancellationToken)
    {
        var commonPathsScript = RequireScript("common_paths.sh");
        var wslCommonPathsPath = ConvertWindowsPathToWsl(commonPathsScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {commonPathsScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"source {ToBashSingleQuoted(wslCommonPathsPath)}; " +
            "if [[ -f \"${NYMPHS3D_RUNTIME_CODE_MODE_FILE}\" ]]; then " +
            "  tr -d '\\r\\n' < \"${NYMPHS3D_RUNTIME_CODE_MODE_FILE}\"; " +
            "else " +
            "  printf 'pinned'; " +
            "fi";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress: null,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Runtime dependency mode check failed.");
        }

        return (result.CombinedOutput ?? string.Empty).Trim();
    }

    public async Task ApplyRuntimeDependencyModeAsync(
        InstallSettings settings,
        string mode,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var normalizedMode = mode.Trim().ToLowerInvariant() switch
        {
            "latest" => "latest",
            "pinned" => "pinned",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Expected pinned or latest runtime dependency mode."),
        };
        var applyScript = RequireScript("apply_runtime_dependency_mode.sh");
        var wslApplyScriptPath = ConvertWindowsPathToWsl(applyScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {applyScript}");

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export NYMPHS3D_RUNTIME_ROOT=\"$HOME\"; " +
            "export NYMPHS3D_Z_IMAGE_DIR=\"$HOME/Z-Image\"; " +
            "export NYMPHS3D_N2D2_DIR=\"$NYMPHS3D_Z_IMAGE_DIR\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
            $"bash {ToBashSingleQuoted(wslApplyScriptPath)} --mode {ToBashSingleQuoted(normalizedMode)}";

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        progress.Report(
            normalizedMode == "latest"
                ? "Applying latest upstream runtime dependencies to the dev runtime..."
                : "Restoring release-pinned runtime dependencies...");

        var result = await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Runtime dependency mode '{normalizedMode}' failed.");
        }
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

    public string? GetExistingManagedDistroInstallLocation(string? distroName = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var normalizedDistroName = string.IsNullOrWhiteSpace(distroName)
            ? ManagedDistroName
            : distroName.Trim();

        using var lxssKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (lxssKey is null)
        {
            return null;
        }

        foreach (var subKeyName in lxssKey.GetSubKeyNames())
        {
            using var distroKey = lxssKey.OpenSubKey(subKeyName);
            var distributionName = distroKey?.GetValue("DistributionName") as string;
            if (!string.Equals(distributionName, normalizedDistroName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var basePath = distroKey?.GetValue("BasePath") as string;
            return string.IsNullOrWhiteSpace(basePath) ? null : basePath;
        }

        return null;
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

    private static Dictionary<string, string?>? BuildInstallerEnvironment(InstallSettings settings)
    {
        Dictionary<string, string?>? environmentVariables = null;

        if (!string.IsNullOrWhiteSpace(settings.HuggingFaceToken))
        {
            environmentVariables ??= new Dictionary<string, string?>();
            environmentVariables["NYMPHS3D_HF_TOKEN"] = settings.HuggingFaceToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(settings.TrellisGgufQuant))
        {
            environmentVariables ??= new Dictionary<string, string?>();
            environmentVariables["TRELLIS_GGUF_QUANT"] = NormalizeTrellisGgufPrefetchQuant(settings.TrellisGgufQuant);
        }

        var githubToken = ResolveGitHubTokenFromEnvironment();
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            environmentVariables ??= new Dictionary<string, string?>();
            environmentVariables["NYMPHS3D_GITHUB_TOKEN"] = githubToken;
        }

        return environmentVariables;
    }

    private static string NormalizeTrellisGgufPrefetchQuant(string? quant)
    {
        var normalized = (quant ?? "Q5_K_M").Trim().ToUpperInvariant();
        return normalized switch
        {
            "ALL" => "all",
            "Q4_K_M" => "Q4_K_M",
            "Q5_K_M" => "Q5_K_M",
            "Q6_K" => "Q6_K",
            "Q8_0" => "Q8_0",
            _ => "Q5_K_M",
        };
    }

    private static string NormalizeTrellisGgufRuntimeQuant(string? quant)
    {
        var normalized = NormalizeTrellisGgufPrefetchQuant(quant);
        return normalized == "all" ? "Q5_K_M" : normalized;
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

    private async Task<CommandResult> RunNymphsBrainCommandAsync(
        InstallSettings settings,
        string toolName,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var toolPath = $"{settings.BrainInstallRoot}/bin/{toolName}";
        var toolInvocation = BuildNymphsBrainToolInvocation(settings, toolName, toolPath);
        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            "export CI=true; " +
            "export LMS_NO_INTERACTIVE=1; " +
            BuildNymphsBrainToolPreflight(settings, toolName, toolPath) +
            toolInvocation;

        var arguments = new List<string>
        {
            "-d", settings.DistroName,
            "--user", settings.LinuxUser,
            "--",
            "/bin/bash", "-lc", bashCommand,
        };

        return await _processRunner.RunAsync(
            fileName: "wsl.exe",
            arguments,
            workingDirectory: Environment.SystemDirectory,
            progress,
            environmentVariables: null,
            cancellationToken).ConfigureAwait(false);
    }

    private string BuildNymphsBrainToolInvocation(InstallSettings settings, string toolName, string toolPath)
    {
        if (toolName == "lms-start")
        {
            return $"timeout --foreground 120s {ToBashSingleQuoted(toolPath)}";
        }

        if (toolName == "mcp-start")
        {
            return ToBashSingleQuoted(toolPath);
        }

        if (toolName == "brain-apply-openrouter-key")
        {
            if (string.IsNullOrWhiteSpace(settings.OpenRouterApiKey))
            {
                return "echo 'OpenRouter API key is missing.' >&2; exit 2";
            }

            var secretDir = $"{settings.BrainInstallRoot}/secrets";
            var secretFile = $"{secretDir}/llm-wrapper.env";

            return new StringBuilder()
                .Append("mkdir -p ")
                .Append(ToBashSingleQuoted(secretDir))
                .Append("; ")
                .Append("remote_model='deepseek/deepseek-chat'; ")
                .Append("if [[ -f ")
                .Append(ToBashSingleQuoted(secretFile))
                .Append(" ]]; then existing_remote=$(sed -n 's/^REMOTE_LLM_MODEL=//p' ")
                .Append(ToBashSingleQuoted(secretFile))
                .Append(" | tail -1); if [[ -n \"${existing_remote}\" ]]; then remote_model=\"${existing_remote}\"; fi; fi; ")
                .Append("{ printf '%s\\n' ")
                .Append(ToBashSingleQuoted("# Nymphs-Brain llm-wrapper configuration"))
                .Append("; printf 'OPENROUTER_API_KEY=%s\\n' ")
                .Append(ToBashSingleQuoted(settings.OpenRouterApiKey))
                .Append("; printf 'REMOTE_LLM_MODEL=%s\\n' \"${remote_model}\"; } > ")
                .Append(ToBashSingleQuoted(secretFile))
                .Append("; ")
                .Append("chmod 600 ")
                .Append(ToBashSingleQuoted(secretFile))
                .Append("; ")
                .Append("echo 'OpenRouter key updated for Nymphs-Brain llm-wrapper.'")
                .ToString();
        }

        if (toolName == "brain-refresh")
        {
            var brainScript = RequireScript("install_nymphs_brain.sh");
            var wslBrainScriptPath = ConvertWindowsPathToWsl(brainScript)
                ?? throw new InvalidOperationException($"Could not convert script path for WSL: {brainScript}");
            var commandBuilder = new StringBuilder()
                .Append("echo 'Refreshing Nymphs-Brain from the Manager-packaged installer...'; ")
                .Append("bash ")
                .Append(ToBashSingleQuoted(wslBrainScriptPath))
                .Append(" --install-root ")
                .Append(ToBashSingleQuoted(settings.BrainInstallRoot))
                .Append(" --quiet");

            if (!string.IsNullOrWhiteSpace(settings.OpenRouterApiKey))
            {
                commandBuilder
                    .Append(" --openrouter-api-key ")
                    .Append(ToBashSingleQuoted(settings.OpenRouterApiKey));
            }

            return commandBuilder.ToString();
        }

        return ToBashSingleQuoted(toolPath);
    }

    private static string BuildNymphsBrainToolPreflight(InstallSettings settings, string toolName, string toolPath)
    {
        if (toolName is "brain-refresh" or "brain-apply-openrouter-key")
        {
            return string.Empty;
        }

        return $"if [[ ! -x {ToBashSingleQuoted(toolPath)} ]]; then echo 'Nymphs-Brain tool missing: {toolPath}'; exit 1; fi; ";
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
            .Select(name => name.Replace("\0", "").Trim())
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

    private static Dictionary<string, string> ParseKeyValueStatusLine(string line)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length == 2 && !string.IsNullOrWhiteSpace(pieces[0]))
            {
                values[pieces[0]] = pieces[1];
            }
        }

        return values;
    }

    private static string QuoteWindowsCommandArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static bool IsAllowedNymphsBrainTool(string toolName)
    {
        return toolName is
            "lms-start" or
            "brain-apply-openrouter-key" or
            "brain-refresh" or
            "lms-update" or
            "lms-stop" or
            "mcp-start" or
            "mcp-stop" or
            "mcp-status" or
            "open-webui-start" or
            "open-webui-update" or
            "open-webui-stop" or
            "open-webui-status" or
            "brain-status";
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

#if LEGACY_MANAGER_MODULE_TOOLS
    private static string FriendlyBackendLabel(string backend)
    {
        return backend switch
        {
            "all" => "all core backend",
            "zimage" => "Z-Image",
            "trellis" => "TRELLIS.2",
            _ => backend,
        };
    }

    private static string NormalizeModelPrefetchBackend(string backend)
    {
        var normalized = (backend ?? "all").Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => "all",
            "all" => "all",
            "zimage" => "zimage",
            "trellis" => "trellis",
            _ => throw new ArgumentException($"Unsupported model prefetch backend: {backend}", nameof(backend)),
        };
    }

    private static bool TryParseRuntimeBackendStatus(string line, out RuntimeBackendStatus status)
    {
        status = RuntimeBackendStatus.Unknown("unknown", "Unknown", "No runtime status was returned.");
        if (!line.StartsWith("backend=", StringComparison.Ordinal))
        {
            return false;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length == 2)
            {
                map[pieces[0]] = pieces[1];
            }
        }

        if (!map.TryGetValue("backend", out var backendId) || string.IsNullOrWhiteSpace(backendId))
        {
            return false;
        }

        var displayName = map.GetValueOrDefault("label") ?? FriendlyBackendLabel(backendId);
        var envReady = string.Equals(map.GetValueOrDefault("env_ready"), "yes", StringComparison.OrdinalIgnoreCase);
        var modelsReady = string.Equals(map.GetValueOrDefault("models_ready"), "yes", StringComparison.OrdinalIgnoreCase);
        var testReady = string.Equals(map.GetValueOrDefault("test_ready"), "yes", StringComparison.OrdinalIgnoreCase);
        var detail = map.GetValueOrDefault("detail") ?? string.Empty;

        status = new RuntimeBackendStatus(backendId, displayName, envReady, modelsReady, testReady, detail);
        return true;
    }

#endif

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
