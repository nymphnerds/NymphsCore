using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using NymphsCoreManager.Models;

namespace NymphsCoreManager.Services;

public sealed class InstallerWorkflowService
{
    public const string ManagedDistroName = "NymphsCore";
    public const string ManagedLinuxUser = "nymph";
    public const string WslAvailabilityCheckKey = "wsl_availability";
    public const string ExistingWslDistrosCheckKey = "existing_wsl_distros";
    public const string GuideUrl = "https://nymphnerds.github.io/NymphsCore/home/guides.html";
    public const string ReadmeUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/Manager/README.md";
    public const string FootprintDocUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/docs/FOOTPRINT.md";
    public const string AddonGuideUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/docs/BLENDER_ADDON_USER_GUIDE.md";

    private readonly ProcessRunner _processRunner = new();

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

    public async Task RunZImageTrainerInstallAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var trainerScript = RequireScript("install_zimage_trainer.sh");
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

        progress.Report("Z-Image Trainer: installing DiffSynth-Studio sidecar in an isolated venv.");
        progress.Report("Z-Image Trainer: default method is Turbo Differential LoRA with the Turbo training adapter.");

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
        CancellationToken cancellationToken)
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

        progress?.Report("Checking Z-Image Trainer sidecar status...");
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Z-Image Trainer status check failed.");
        }

        return ParseZImageTrainerStatus(result.CombinedOutput);
    }

    public async Task CreateZImageTrainerJobAsync(
        InstallSettings settings,
        string datasetName,
        string loraName,
        string trainingType,
        string trainingAmount,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var normalizedDataset = NormalizeTrainerName(datasetName, "dataset");
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var datasetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/datasets/{normalizedDataset}";
        var jobPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/jobs/{normalizedLora}.sh";
        var metadataPath = $"{datasetPath}/metadata.csv";
        await SyncZImageTrainerSupportFilesAsync(settings, progress, cancellationToken).ConfigureAwait(false);
        var adapterPath = await EnsureZImageTrainerTrainingAdapterAsync(settings, progress, cancellationToken).ConfigureAwait(false);
        var metadataStatus = await PrepareZImageTrainerMetadataAsync(
            settings,
            normalizedDataset,
            progress,
            cancellationToken).ConfigureAwait(false);

        var jobPreset = ResolveZImageTrainerPreset(trainingType, trainingAmount);
        var jobScript = BuildZImageTrainerJobScript(settings.LinuxUser, normalizedDataset, normalizedLora, adapterPath, jobPreset);
        const string jobHeredocMarker = "__NYMPHS_ZIMAGE_JOB__";

        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted(datasetPath)} {ToBashSingleQuoted($"/home/{settings.LinuxUser}/ZImage-Trainer/jobs")} {ToBashSingleQuoted($"/home/{settings.LinuxUser}/ZImage-Trainer/loras")}; " +
            $"cat > {ToBashSingleQuoted(jobPath)} <<'{jobHeredocMarker}'\n" +
            $"{jobScript}\n" +
            $"{jobHeredocMarker}\n" +
            $"chmod +x {ToBashSingleQuoted(jobPath)}; " +
            $"echo 'Created training job: {jobPath}'; " +
            $"echo 'Dataset folder: {datasetPath}'; " +
            $"echo 'Metadata file: {metadataPath}'; " +
            $"echo 'Images found: {metadataStatus.ImageCount}'; " +
            $"echo 'Captions still blank: {metadataStatus.MissingCaptionCount}'";

        progress.Report(
            $"Creating Z-Image Trainer job '{normalizedLora}' for dataset '{normalizedDataset}' using {jobPreset.TypeLabel} / {jobPreset.AmountLabel}.");
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Z-Image Trainer job creation failed.");
        }
    }

    public async Task RunZImageTrainerJobAsync(
        InstallSettings settings,
        string loraName,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var normalizedLora = NormalizeTrainerName(loraName, "LoRA");
        var runnerPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/ztrain-run-config";
        var jobPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/jobs/{normalizedLora}.sh";

        var bashCommand =
            "set -euo pipefail; " +
            $"export HOME={ToBashSingleQuoted($"/home/{settings.LinuxUser}")}; " +
            $"export USER={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"export LOGNAME={ToBashSingleQuoted(settings.LinuxUser)}; " +
            $"if [[ ! -x {ToBashSingleQuoted(runnerPath)} ]]; then echo 'Trainer runner missing. Repair Trainer first.'; exit 1; fi; " +
            $"if [[ ! -f {ToBashSingleQuoted(jobPath)} ]]; then echo 'Training job not found. Create Job first.'; exit 1; fi; " +
            $"{ToBashSingleQuoted(runnerPath)} {ToBashSingleQuoted(jobPath)}";

        progress.Report($"Starting Z-Image Trainer job '{normalizedLora}'.");
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Z-Image Trainer job failed.");
        }
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
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var adapterRepoDir = $"/home/{settings.LinuxUser}/ZImage-Trainer/adapters/zimage_turbo_training_adapter";
        var adapterPathFile = $"{adapterRepoDir}/selected_adapter_path.txt";
        var trainerPython = $"/home/{settings.LinuxUser}/ZImage-Trainer/.venv-ztrain/bin/python";
        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted(adapterRepoDir)}; " +
            $"if [[ ! -x {ToBashSingleQuoted(trainerPython)} ]]; then echo 'Trainer Python missing: {trainerPython}. Repair Trainer first.' >&2; exit 1; fi; " +
            $"if [[ -f {ToBashSingleQuoted(adapterPathFile)} ]]; then " +
            $"  IFS= read -r SELECTED_ADAPTER < {ToBashSingleQuoted(adapterPathFile)} || true; " +
            "  SELECTED_ADAPTER=\"${SELECTED_ADAPTER%$'\\r'}\"; " +
            "  if [[ -n \"$SELECTED_ADAPTER\" && -f \"$SELECTED_ADAPTER\" ]]; then " +
            "    echo \"Turbo training adapter already ready: $SELECTED_ADAPTER\"; " +
            "    exit 0; " +
            "  fi; " +
            "fi; " +
            $"ADAPTER_REPO_DIR={ToBashSingleQuoted(adapterRepoDir)} ADAPTER_PATH_FILE={ToBashSingleQuoted(adapterPathFile)} {ToBashSingleQuoted(trainerPython)} - <<'PYEOF'\n" +
            "import os\n" +
            "from pathlib import Path\n" +
            "from huggingface_hub import snapshot_download\n" +
            "adapter_dir = Path(os.environ['ADAPTER_REPO_DIR'])\n" +
            "path_file = Path(os.environ['ADAPTER_PATH_FILE'])\n" +
            "adapter_dir.mkdir(parents=True, exist_ok=True)\n" +
            "snapshot_download(\n" +
            "    repo_id='ostris/zimage_turbo_training_adapter',\n" +
            "    local_dir=str(adapter_dir),\n" +
            "    allow_patterns=['*.safetensors', '*.bin', '*.pt', '*.pth', '*.ckpt'],\n" +
            ")\n" +
            "candidates = sorted(\n" +
            "    [\n" +
            "        path for path in adapter_dir.rglob('*')\n" +
            "        if path.is_file() and path.suffix.lower() in {'.safetensors', '.bin', '.pt', '.pth', '.ckpt'}\n" +
            "    ],\n" +
            "    key=lambda path: (\n" +
            "        0 if path.suffix.lower() == '.safetensors' else 1,\n" +
            "        0 if path.parent == adapter_dir else 1,\n" +
            "        len(path.name),\n" +
            "        str(path).lower(),\n" +
            "    ),\n" +
            ")\n" +
            "if not candidates:\n" +
            "    raise SystemExit('No adapter weight file was downloaded for ostris/zimage_turbo_training_adapter')\n" +
            "selected = candidates[0].resolve()\n" +
            "path_file.write_text(str(selected) + '\\n', encoding='utf-8')\n" +
            "print(f'Turbo training adapter ready: {selected}', flush=True)\n" +
            "PYEOF";

        progress?.Report("Ensuring Turbo training adapter is ready for the trainer.");
        var result = await RunWslBashAsync(settings, bashCommand, progress, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Could not prepare the Turbo training adapter.");
        }

        var selectedLine = result.CombinedOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.StartsWith("Turbo training adapter", StringComparison.OrdinalIgnoreCase));

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

        return $"{adapterRepoDir}/zimage_turbo_training_adapter_v1.safetensors";
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
        var managedTrainScript = RequireScript("ztrain_zimage.py");
        var wslManagedTrainScriptPath = ConvertWindowsPathToWsl(managedTrainScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {managedTrainScript}");
        var managedRunnerScript = RequireScript("ztrain_run_config.sh");
        var wslManagedRunnerScriptPath = ConvertWindowsPathToWsl(managedRunnerScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {managedRunnerScript}");
        var captionBrainScript = RequireScript("zimage_caption_brain.sh");
        var wslCaptionBrainScriptPath = ConvertWindowsPathToWsl(captionBrainScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {captionBrainScript}");
        var captionBrainPythonScript = RequireScript("zimage_caption_brain.py");
        var wslCaptionBrainPythonScriptPath = ConvertWindowsPathToWsl(captionBrainPythonScript)
            ?? throw new InvalidOperationException($"Could not convert script path for WSL: {captionBrainPythonScript}");
        var trainerEntrypointPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/ztrain-zimage.py";
        var trainerRunnerPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/ztrain-run-config";
        var captionShellTargetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/zimage-caption-brain.sh";
        var captionPythonTargetPath = $"/home/{settings.LinuxUser}/ZImage-Trainer/bin/zimage-caption-brain.py";
        var bashCommand =
            "set -euo pipefail; " +
            $"mkdir -p {ToBashSingleQuoted($"/home/{settings.LinuxUser}/ZImage-Trainer/bin")}; " +
            $"install -m 755 {ToBashSingleQuoted(wslManagedTrainScriptPath)} {ToBashSingleQuoted(trainerEntrypointPath)}; " +
            $"install -m 755 {ToBashSingleQuoted(wslManagedRunnerScriptPath)} {ToBashSingleQuoted(trainerRunnerPath)}; " +
            $"install -m 755 {ToBashSingleQuoted(wslCaptionBrainScriptPath)} {ToBashSingleQuoted(captionShellTargetPath)}; " +
            $"install -m 755 {ToBashSingleQuoted(wslCaptionBrainPythonScriptPath)} {ToBashSingleQuoted(captionPythonTargetPath)}; " +
            $"echo 'Managed trainer entrypoint synced: {trainerEntrypointPath}'; " +
            $"echo 'Managed trainer runner synced: {trainerRunnerPath}'; " +
            $"echo 'Caption Brain helper synced: {captionShellTargetPath}'; " +
            $"echo 'Caption Brain client synced: {captionPythonTargetPath}'";

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
        var loraCount = ParseNonNegativeInt(values.GetValueOrDefault("ZIMAGE_TRAINER_LORAS_FOUND"));
        var datasetCount = ParseNonNegativeInt(values.GetValueOrDefault("ZIMAGE_TRAINER_DATASETS_FOUND"));

        var detail = installState switch
        {
            "installed" => $"DiffSynth trainer is installed. {loraCount} LoRA file(s), {datasetCount} dataset folder(s).",
            "partial" => "Trainer files are partially present. Run Install/Repair to complete the sidecar.",
            _ => "Trainer sidecar is not installed yet.",
        };

        if (running)
        {
            detail += " A training process appears to be running.";
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
            detail);
    }

    private static bool IsYes(string? value) =>
        string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);

    private static int ParseNonNegativeInt(string? value) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;

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

    private sealed record ZImageTrainerPreset(
        string Id,
        string Label,
        string TypeLabel,
        string AmountLabel,
        int DatasetRepeat,
        int Epochs,
        string LearningRate,
        int Rank,
        int MaxPixels,
        string MinTimestepBoundary,
        string MaxTimestepBoundary);

    private static ZImageTrainerPreset ResolveZImageTrainerPreset(string trainingType, string trainingAmount)
    {
        var normalizedType = (trainingType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedAmount = (trainingAmount ?? string.Empty).Trim().ToLowerInvariant();

        return (normalizedType, normalizedAmount) switch
        {
            // This range is an inference from DiffSynth's existing timestep-boundary examples.
            // We use it to emphasize the early/high-noise region for broad style/composition shifts.
            ("style", "quick") => new ZImageTrainerPreset("style_quick", "Stylized Look / Quick Test", "Stylized Look", "Quick Test", 18, 2, "1e-4", 16, 786432, "0.0", "0.35"),
            ("style", "strong") => new ZImageTrainerPreset("style_strong", "Stylized Look / Strong", "Stylized Look", "Strong", 60, 6, "8e-5", 32, 1048576, "0.0", "0.35"),
            ("style", _) => new ZImageTrainerPreset("style_normal", "Stylized Look / Normal", "Stylized Look", "Normal", 40, 4, "8e-5", 32, 1048576, "0.0", "0.35"),
            ("character", "quick") or ("concept", "quick") => new ZImageTrainerPreset("character_quick", "Character / Object / Quick Test", "Character / Object", "Quick Test", 10, 1, "1e-4", 16, 786432, "0.0", "1.0"),
            ("character", "strong") or ("concept", "strong") => new ZImageTrainerPreset("character_strong", "Character / Object / Strong", "Character / Object", "Strong", 70, 7, "1e-4", 32, 1048576, "0.0", "1.0"),
            ("character", _) or ("concept", _) or _ => new ZImageTrainerPreset("character_normal", "Character / Object / Normal", "Character / Object", "Normal", 50, 5, "1e-4", 32, 1048576, "0.0", "1.0"),
        };
    }

    private static string BuildZImageTrainerJobScript(string linuxUser, string datasetName, string loraName, string adapterPath, ZImageTrainerPreset preset)
    {
        return $$"""
#!/usr/bin/env bash
set -euo pipefail

# Preset: {{preset.Label}}
# Default method: Z-Image Turbo Differential LoRA with the Turbo training adapter.

cd "/home/{{linuxUser}}/ZImage-Trainer/DiffSynth-Studio"
source "/home/{{linuxUser}}/ZImage-Trainer/.venv-ztrain/bin/activate"

if [[ ! -f "/home/{{linuxUser}}/ZImage-Trainer/bin/ztrain-zimage.py" ]]; then
  echo "PWD: $(pwd)" >&2
  echo "HOME: ${HOME:-<unset>}" >&2
  echo "Managed trainer entrypoint expected at /home/{{linuxUser}}/ZImage-Trainer/bin/ztrain-zimage.py" >&2
  echo "DiffSynth path expected at /home/{{linuxUser}}/ZImage-Trainer/DiffSynth-Studio" >&2
  ls -l "/home/{{linuxUser}}/ZImage-Trainer/bin" >&2 || true
  echo "Managed trainer entrypoint missing: /home/{{linuxUser}}/ZImage-Trainer/bin/ztrain-zimage.py" >&2
  exit 1
fi

if ! python -c "import torchaudio" >/dev/null 2>&1; then
  echo "Installing missing trainer dependency: torchaudio"
  python -m pip install torchaudio
fi

if [[ ! -f "{{adapterPath}}" ]]; then
  echo "Turbo training adapter file missing: {{adapterPath}}" >&2
  ls -l "/home/{{linuxUser}}/ZImage-Trainer/adapters/zimage_turbo_training_adapter" >&2 || true
  exit 1
fi

export PYTHONUNBUFFERED=1
echo "Training bootstrap complete. Launching accelerate..."
echo "The first model load can sit quietly for a while, especially on the first run."

if command -v stdbuf >/dev/null 2>&1; then
  stdbuf -oL -eL accelerate launch "/home/{{linuxUser}}/ZImage-Trainer/bin/ztrain-zimage.py" \
    --dataset_base_path "/home/{{linuxUser}}/ZImage-Trainer/datasets/{{datasetName}}" \
    --dataset_metadata_path "/home/{{linuxUser}}/ZImage-Trainer/datasets/{{datasetName}}/metadata.csv" \
    --max_pixels {{preset.MaxPixels}} \
    --dataset_repeat {{preset.DatasetRepeat}} \
    --model_id_with_origin_paths "Tongyi-MAI/Z-Image-Turbo:transformer/*.safetensors,Tongyi-MAI/Z-Image-Turbo:text_encoder/*.safetensors,Tongyi-MAI/Z-Image-Turbo:vae/diffusion_pytorch_model.safetensors" \
    --learning_rate {{preset.LearningRate}} \
    --num_epochs {{preset.Epochs}} \
    --remove_prefix_in_ckpt "pipe.dit." \
    --output_path "/home/{{linuxUser}}/ZImage-Trainer/loras/{{loraName}}" \
    --lora_base_model "dit" \
    --lora_target_modules "to_q,to_k,to_v,to_out.0,w1,w2,w3" \
    --lora_rank {{preset.Rank}} \
    --preset_lora_path "{{adapterPath}}" \
    --preset_lora_model "dit" \
    --min_timestep_boundary {{preset.MinTimestepBoundary}} \
    --max_timestep_boundary {{preset.MaxTimestepBoundary}} \
    --use_gradient_checkpointing \
    --dataset_num_workers 8
else
accelerate launch "/home/{{linuxUser}}/ZImage-Trainer/bin/ztrain-zimage.py" \
  --dataset_base_path "/home/{{linuxUser}}/ZImage-Trainer/datasets/{{datasetName}}" \
  --dataset_metadata_path "/home/{{linuxUser}}/ZImage-Trainer/datasets/{{datasetName}}/metadata.csv" \
  --data_file_keys "image" \
  --max_pixels {{preset.MaxPixels}} \
  --dataset_repeat {{preset.DatasetRepeat}} \
  --model_id_with_origin_paths "Tongyi-MAI/Z-Image-Turbo:transformer/*.safetensors,Tongyi-MAI/Z-Image-Turbo:text_encoder/*.safetensors,Tongyi-MAI/Z-Image-Turbo:vae/diffusion_pytorch_model.safetensors" \
  --learning_rate {{preset.LearningRate}} \
  --num_epochs {{preset.Epochs}} \
  --remove_prefix_in_ckpt "pipe.dit." \
  --output_path "/home/{{linuxUser}}/ZImage-Trainer/loras/{{loraName}}" \
  --lora_base_model "dit" \
  --lora_target_modules "to_q,to_k,to_v,to_out.0,w1,w2,w3" \
  --lora_rank {{preset.Rank}} \
  --preset_lora_path "{{adapterPath}}" \
  --preset_lora_model "dit" \
  --min_timestep_boundary {{preset.MinTimestepBoundary}} \
  --max_timestep_boundary {{preset.MaxTimestepBoundary}} \
  --use_gradient_checkpointing \
  --dataset_num_workers 8
fi
""";
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
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
                return pathStart >= 0 && pathStart + 1 < normalized.Length
                    ? "/" + normalized[(pathStart + 1)..]
                    : null;
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
