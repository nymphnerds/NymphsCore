using System.IO;
using System.Security.Principal;
using Nymphs3DInstaller.Models;

namespace Nymphs3DInstaller.Services;

public sealed class InstallerWorkflowService
{
    public const string ManagedDistroName = "NymphsCore";
    public const string ManagedLinuxUser = "nymph";
    public const string WslAvailabilityCheckKey = "wsl_availability";
    public const string ExistingWslDistrosCheckKey = "existing_wsl_distros";
    public const string ReadmeUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/Manager/README.md";
    public const string FootprintDocUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/Manager/docs/install_disk_and_model_footprint.md";
    public const string AddonGuideUrl = "https://github.com/nymphnerds/NymphsCore/blob/main/Blender/Addon/docs/USER_GUIDE.md";

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

    public string LogFolderPath { get; }

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
            await CheckBaseTarPresenceAsync(cancellationToken).ConfigureAwait(false),
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
        progress.Report("Runtime setup note: this stage can sit on one line for several minutes during apt work, CUDA setup, or Python environment creation. That does not mean the installer is frozen.");

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

        var environmentVariables = string.IsNullOrWhiteSpace(settings.HuggingFaceToken)
            ? null
            : new Dictionary<string, string?>
            {
                ["NYMPHS3D_HF_TOKEN"] = settings.HuggingFaceToken,
            };

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

    public async Task RunModelPrefetchOnlyAsync(
        InstallSettings settings,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var runtimeScript = RequireScript("run_finalize_in_distro.ps1");
        var arguments = new List<string>
        {
            "-DistroName", settings.DistroName,
            "-LinuxUser", settings.LinuxUser,
            "-SkipCuda",
            "-SkipBackendEnvs",
            "-SkipVerify",
        };

        progress.Report("Model prefetch: downloading required models into the existing NymphsCore runtime.");

        var environmentVariables = string.IsNullOrWhiteSpace(settings.HuggingFaceToken)
            ? null
            : new Dictionary<string, string?>
            {
                ["NYMPHS3D_HF_TOKEN"] = settings.HuggingFaceToken,
            };

        var result = await RunPowerShellScriptAsync(
            runtimeScript,
            arguments,
            progress,
            environmentVariables,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Model prefetch failed.");
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
            "export NYMPHS3D_H2_DIR=\"$HOME/Hunyuan3D-2\"; " +
            "export NYMPHS3D_Z_IMAGE_DIR=\"$HOME/Z-Image\"; " +
            "export NYMPHS3D_N2D2_DIR=\"$NYMPHS3D_Z_IMAGE_DIR\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
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
            "export NYMPHS3D_H2_DIR=\"$HOME/Hunyuan3D-2\"; " +
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
            "export NYMPHS3D_H2_DIR=\"$HOME/Hunyuan3D-2\"; " +
            "export NYMPHS3D_Z_IMAGE_DIR=\"$HOME/Z-Image\"; " +
            "export NYMPHS3D_N2D2_DIR=\"$NYMPHS3D_Z_IMAGE_DIR\"; " +
            "export NYMPHS3D_TRELLIS_DIR=\"$HOME/TRELLIS.2\"; " +
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
                "Checks that the small NymphsCore base distro tar is available.",
                CheckState.Pass,
                $"Found {fileInfo.Name} at {fileInfo.FullName} ({sizeGiB:F1} GB).");
        }

        try
        {
            if (await ManagedDistroExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return new SystemCheckItem(
                    "Base distro package",
                    "Checks that the small NymphsCore base distro tar is available.",
                    CheckState.Warning,
                    $"Base distro tar was not found, but an existing managed {ManagedDistroName} distro was detected. Repair/continue can reuse the existing distro without replacing it.");
            }
        }
        catch
        {
            // Let the standard missing-tar message surface if WSL inspection is unavailable.
        }

        return new SystemCheckItem(
            "Base distro package",
            "Checks that the small NymphsCore base distro tar is available.",
            CheckState.Fail,
            "Base distro tar was not found. Download NymphsCore.tar from " +
            "https://drive.google.com/file/d/1PIE9LJCcb06MCQ9G4T5ywrBJ8DWeqR5a/view?usp=drive_link " +
            $"then place it next to NymphsCoreManager.exe at {BaseTarPath} and run the checks again.");
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

    private static string? ConvertWindowsPathToWsl(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return null;
        }

        var normalized = windowsPath.Replace('\\', '/');
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
            Path.Combine(AppContext.BaseDirectory, "Nymphs3D2.tar"),
            Path.Combine(PayloadDirectory, "Nymphs3D2.tar"),
            Path.Combine(RepositoryRoot, "payload", "Nymphs3D2.tar"),
            @"D:\WSL\NymphsCore.tar",
            @"D:\WSL\Nymphs3D2.tar",
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static bool IsManagedDistroAlias(string distroName)
    {
        if (string.IsNullOrWhiteSpace(distroName))
        {
            return false;
        }

        if (string.Equals(distroName, ManagedDistroName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!distroName.StartsWith(ManagedDistroName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = distroName[ManagedDistroName.Length..];
        return suffix.Length > 0 && suffix.All(char.IsDigit);
    }

    private static string? FindManagedDistroName(IEnumerable<string> distros)
    {
        return distros
            .Where(IsManagedDistroAlias)
            .OrderBy(name => string.Equals(name, ManagedDistroName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name.Length)
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
            progress.Report($"Using base tar: {settings.TarPath}");
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
            "2mv" => "Hunyuan 2mv",
            "zimage" => "Z-Image",
            "trellis" => "TRELLIS.2",
            _ => backend,
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
}
