using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using NymphsCoreManager.Models;
using NymphsCoreManager.Services;

namespace NymphsCoreManager.ViewModels;

public sealed class ManagerShellViewModel : ViewModelBase, IDisposable
{
    private readonly InstallerWorkflowService _workflowService;
    private readonly InstallSettings _settings;
    private readonly DispatcherTimer _sidebarArtTimer;
    private readonly DispatcherTimer _runtimeMonitorTimer;
    private readonly List<string> _sidebarArtPaths = [];
    private readonly string _sidebarPortraitOverrideFileName = "NymphMycelium1.png";
    private readonly List<NymphModuleViewModel> _allModules;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _checkForUpdatesCommand;
    private readonly RelayCommand<NymphModuleViewModel> _openModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _installModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _openModuleInstallPathCommand;
    private readonly RelayCommand<NymphModuleViewModel> _uninstallModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _deleteModuleCommand;
    private ShellNavigationItemViewModel? _selectedNavigationItem;
    private NymphModuleViewModel? _selectedModule;
    private ManagerPageKind _currentPageKind = ManagerPageKind.Home;
    private string _currentPageTitle = "Home";
    private string _currentPageSubtitle = "Overview of your system and modules";
    private string _statusMessage = "Preparing Manager shell...";
    private string _updateSummary = "No update check has been run this session.";
    private string _lastRefreshedText = "Not refreshed yet";
    private string _runtimePanelTitle = "WSL: Checking";
    private string _runtimePanelSummary = "Kernel: -";
    private string _runtimePanelDetail = "Uptime: -";
    private string _runtimePanelStatusLabel = "Checking";
    private string _runtimePanelStatusBrush = "#5E6D6B";
    private string _runtimeCpuUsageLabel = "-";
    private string _runtimeMemoryUsageLabel = "- / -";
    private string _runtimeDiskUsageLabel = "- / -";
    private string _runtimeWindowsDiskUsageLabel = "- / -";
    private string _runtimeGpuVramLabel = "Unavailable";
    private string _runtimeGpuTempLabel = "Unavailable";
    private double _runtimeCpuBarWidth;
    private double _runtimeMemoryBarWidth;
    private double _runtimeDiskBarWidth;
    private string _systemChecksSummary = "System checks have not run yet.";
    private string _installedModulesSummary = "Scanning installed Nymphs...";
    private string _availableModulesSummary = "Manifest-aware shell is loading the known module roster.";
    private string _currentSidebarArtPath = string.Empty;
    private bool _isBusy;
    private bool _hasLoadedModuleState;
    private bool _isRefreshingRuntimeMonitorLive;
    private int _sidebarArtIndex = -1;

    public ManagerShellViewModel(InstallerWorkflowService workflowService)
    {
        _workflowService = workflowService;
        _settings = CreateDefaultInstallSettings();

        PrimaryNavigationItems = new ObservableCollection<ShellNavigationItemViewModel>
        {
            new("Home", "Core overview", "HM", "\uE80F", "#29E6DA", ManagerPageKind.Home),
            new("Logs", "Recent activity", "LG", "\uE8A5", "#F0F3F2", ManagerPageKind.Logs),
            new("Guide", "Docs and onboarding", "GD", "\uE82D", "#F0F3F2", ManagerPageKind.Guide),
        };
        ModuleNavigationItems = [];
        InstalledModules = [];
        AvailableModules = [];
        SystemChecks = [];
        ActivityLines = [];
        RecentLogLines = [];
        UnifiedLogLines = [];

        _allModules =
        [
            new NymphModuleViewModel(
                "brain",
                "Brain",
                "BR",
                "service",
                "repo",
                "Local coding and orchestration stack managed as an optional Nymph.",
                BuildManagedDistroPath("home", _settings.LinuxUser, "Nymphs-Brain"),
                "#97DF48",
                ["install", "status", "start", "stop", "open", "logs", "uninstall"]),
            new NymphModuleViewModel(
                "zimage",
                "Z-Image Turbo",
                "ZI",
                "runtime",
                "repo",
                "Z-Image Turbo image generation runtime managed as an optional Nymph.",
                BuildManagedDistroPath("home", _settings.LinuxUser, "Z-Image"),
                "#22DDF0",
                ["install", "status", "configure", "open", "logs", "uninstall"]),
            new NymphModuleViewModel(
                "lora",
                "LoRA",
                "LO",
                "trainer",
                "repo",
                "AI Toolkit powered LoRA training sidecar for Z-Image Turbo.",
                BuildManagedDistroPath("home", _settings.LinuxUser, "ZImage-Trainer"),
                "#39C7FF",
                ["install", "status", "configure", "open", "logs", "uninstall"]),
            new NymphModuleViewModel(
                "trellis",
                "TRELLIS.2",
                "TR",
                "runtime",
                "repo",
                "3D structure generation runtime handled as an installable module rather than a permanent core section.",
                BuildManagedDistroPath("home", _settings.LinuxUser, "TRELLIS.2"),
                "#C8EE47",
                ["install", "status", "configure", "open", "logs", "uninstall"]),
            new NymphModuleViewModel(
                "worbi",
                "WORBI",
                "WB",
                "tool",
                "archive",
                "Local worldbuilding application packaged as an optional self-contained Nymph.",
                BuildManagedDistroPath("home", _settings.LinuxUser, "worbi"),
                "#A9E347",
                ["install", "status", "start", "stop", "open", "logs", "uninstall"]),
        ];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        _checkForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsBusy);
        _openModuleCommand = new RelayCommand<NymphModuleViewModel>(OpenModule, module => module?.IsInstalled == true);
        _installModuleCommand = new RelayCommand<NymphModuleViewModel>(InstallModule, module => module?.IsInstalled == false && !IsBusy);
        _openModuleInstallPathCommand = new RelayCommand<NymphModuleViewModel>(OpenModuleInstallPath, module => module?.CanOpenInstallPath == true);
        _uninstallModuleCommand = new RelayCommand<NymphModuleViewModel>(UninstallModule, module => module?.IsInstalled == true && !IsBusy);
        _deleteModuleCommand = new RelayCommand<NymphModuleViewModel>(DeleteModule, module => module?.IsInstalled == true && !IsBusy);

        LoadSidebarArtwork();
        LoadHistoricalLogs();
        PrimeModulePresence();
        RebuildModuleCollections();
        RebuildModuleNavigation();
        HasLoadedModuleState = true;
        SelectedNavigationItem = PrimaryNavigationItems[0];

        _sidebarArtTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(12),
        };
        _sidebarArtTimer.Tick += OnSidebarArtTimerTick;

        _runtimeMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        _runtimeMonitorTimer.Tick += OnRuntimeMonitorTimerTick;
        _runtimeMonitorTimer.Start();
    }

    public ObservableCollection<ShellNavigationItemViewModel> PrimaryNavigationItems { get; }

    public ObservableCollection<ShellNavigationItemViewModel> ModuleNavigationItems { get; }

    public ObservableCollection<NymphModuleViewModel> InstalledModules { get; }

    public ObservableCollection<NymphModuleViewModel> AvailableModules { get; }

    public ObservableCollection<SystemCheckItem> SystemChecks { get; }

    public ObservableCollection<string> ActivityLines { get; }

    public ObservableCollection<string> RecentLogLines { get; }

    public ObservableCollection<string> UnifiedLogLines { get; }

    public AsyncRelayCommand RefreshCommand => _refreshCommand;

    public AsyncRelayCommand CheckForUpdatesCommand => _checkForUpdatesCommand;

    public RelayCommand<NymphModuleViewModel> OpenModuleCommand => _openModuleCommand;

    public RelayCommand<NymphModuleViewModel> InstallModuleCommand => _installModuleCommand;

    public RelayCommand<NymphModuleViewModel> OpenModuleInstallPathCommand => _openModuleInstallPathCommand;

    public RelayCommand<NymphModuleViewModel> UninstallModuleCommand => _uninstallModuleCommand;

    public RelayCommand<NymphModuleViewModel> DeleteModuleCommand => _deleteModuleCommand;

    public RelayCommand OpenGuideCommand => new(() => SafeRun(_workflowService.OpenGuide, "Guide opened."));

    public RelayCommand OpenReadmeCommand => new(() => SafeRun(_workflowService.OpenReadme, "README opened."));

    public RelayCommand OpenAddonGuideCommand => new(() => SafeRun(_workflowService.OpenAddonGuide, "Addon guide opened."));

    public RelayCommand OpenFootprintCommand => new(() => SafeRun(_workflowService.OpenFootprintDoc, "Footprint document opened."));

    public RelayCommand OpenLogFolderCommand => new(() => SafeRun(_workflowService.OpenLogFolder, "Log folder opened."));

    public RelayCommand OpenTerminalCommand => new(OpenTerminal);

    public RelayCommand ShowSystemChecksCommand => new(() => SelectPrimaryPage(ManagerPageKind.SystemChecks));

    public RelayCommand ShowLogsCommand => new(() => SelectPrimaryPage(ManagerPageKind.Logs));

    public RelayCommand ShowHomeCommand => new(() => SelectPrimaryPage(ManagerPageKind.Home));

    public RelayCommand ShowGuideCommand => new(() => SelectPrimaryPage(ManagerPageKind.Guide));

    public ShellNavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (!SetProperty(ref _selectedNavigationItem, value) || value is null)
            {
                return;
            }

            CurrentPageKind = value.PageKind;
            SelectedModule = value.Module;

            switch (value.PageKind)
            {
                case ManagerPageKind.Home:
                    CurrentPageTitle = "Home";
                    CurrentPageSubtitle = "// Overview of your system and modules";
                    break;
                case ManagerPageKind.SystemChecks:
                    CurrentPageTitle = "System Checks";
                    CurrentPageSubtitle = "// Live platform diagnostics and readiness checks";
                    break;
                case ManagerPageKind.Logs:
                    CurrentPageTitle = "Logs";
                    CurrentPageSubtitle = "// Session activity, runtime checks, and shell feedback";
                    break;
                case ManagerPageKind.Guide:
                    CurrentPageTitle = "Guide";
                    CurrentPageSubtitle = "// Core docs, onboarding, and next-step references";
                    break;
                case ManagerPageKind.Module when value.Module is not null:
                    CurrentPageTitle = value.Module.Name;
                    CurrentPageSubtitle = value.Module.Description;
                    break;
            }
        }
    }

    public NymphModuleViewModel? SelectedModule
    {
        get => _selectedModule;
        private set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                OnPropertyChanged(nameof(HasSelectedModule));
            }
        }
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    public string CurrentPageSubtitle
    {
        get => _currentPageSubtitle;
        private set => SetProperty(ref _currentPageSubtitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string UpdateSummary
    {
        get => _updateSummary;
        private set => SetProperty(ref _updateSummary, value);
    }

    public string LastRefreshedText
    {
        get => _lastRefreshedText;
        private set => SetProperty(ref _lastRefreshedText, value);
    }

    public string RuntimePanelTitle
    {
        get => _runtimePanelTitle;
        private set => SetProperty(ref _runtimePanelTitle, value);
    }

    public string RuntimePanelSummary
    {
        get => _runtimePanelSummary;
        private set => SetProperty(ref _runtimePanelSummary, value);
    }

    public string RuntimePanelDetail
    {
        get => _runtimePanelDetail;
        private set => SetProperty(ref _runtimePanelDetail, value);
    }

    public string RuntimePanelStatusLabel
    {
        get => _runtimePanelStatusLabel;
        private set => SetProperty(ref _runtimePanelStatusLabel, value);
    }

    public string RuntimePanelStatusBrush
    {
        get => _runtimePanelStatusBrush;
        private set => SetProperty(ref _runtimePanelStatusBrush, value);
    }

    public string RuntimeCpuUsageLabel
    {
        get => _runtimeCpuUsageLabel;
        private set => SetProperty(ref _runtimeCpuUsageLabel, value);
    }

    public string RuntimeMemoryUsageLabel
    {
        get => _runtimeMemoryUsageLabel;
        private set => SetProperty(ref _runtimeMemoryUsageLabel, value);
    }

    public string RuntimeDiskUsageLabel
    {
        get => _runtimeDiskUsageLabel;
        private set => SetProperty(ref _runtimeDiskUsageLabel, value);
    }

    public string RuntimeWindowsDiskUsageLabel
    {
        get => _runtimeWindowsDiskUsageLabel;
        private set => SetProperty(ref _runtimeWindowsDiskUsageLabel, value);
    }

    public string RuntimeGpuVramLabel
    {
        get => _runtimeGpuVramLabel;
        private set => SetProperty(ref _runtimeGpuVramLabel, value);
    }

    public string RuntimeGpuTempLabel
    {
        get => _runtimeGpuTempLabel;
        private set => SetProperty(ref _runtimeGpuTempLabel, value);
    }

    public double RuntimeCpuBarWidth
    {
        get => _runtimeCpuBarWidth;
        private set => SetProperty(ref _runtimeCpuBarWidth, value);
    }

    public double RuntimeMemoryBarWidth
    {
        get => _runtimeMemoryBarWidth;
        private set => SetProperty(ref _runtimeMemoryBarWidth, value);
    }

    public double RuntimeDiskBarWidth
    {
        get => _runtimeDiskBarWidth;
        private set => SetProperty(ref _runtimeDiskBarWidth, value);
    }

    public string SystemChecksSummary
    {
        get => _systemChecksSummary;
        private set => SetProperty(ref _systemChecksSummary, value);
    }

    public string InstalledModulesSummary
    {
        get => _installedModulesSummary;
        private set => SetProperty(ref _installedModulesSummary, value);
    }

    public string AvailableModulesSummary
    {
        get => _availableModulesSummary;
        private set => SetProperty(ref _availableModulesSummary, value);
    }

    public string CurrentSidebarArtPath
    {
        get => _currentSidebarArtPath;
        private set => SetProperty(ref _currentSidebarArtPath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            _refreshCommand.RaiseCanExecuteChanged();
            _checkForUpdatesCommand.RaiseCanExecuteChanged();
            _openModuleCommand.RaiseCanExecuteChanged();
            _installModuleCommand.RaiseCanExecuteChanged();
            _openModuleInstallPathCommand.RaiseCanExecuteChanged();
            _uninstallModuleCommand.RaiseCanExecuteChanged();
            _deleteModuleCommand.RaiseCanExecuteChanged();
        }
    }

    public int InstalledModuleCount => InstalledModules.Count;

    public int AvailableModuleCount => AvailableModules.Count;

    public IReadOnlyList<NymphModuleViewModel> HomeInstalledModules
    {
        get
        {
            return InstalledModules.ToList();
        }
    }

    public IReadOnlyList<NymphModuleViewModel> HomeAvailableModules
    {
        get
        {
            return AvailableModules.ToList();
        }
    }

    public bool ShowInstalledModulesSection => HasLoadedModuleState && HomeInstalledModules.Count > 0;

    public bool ShowAvailableModulesSection => HasLoadedModuleState && HomeAvailableModules.Count > 0;

    public bool IsHomePage => CurrentPageKind == ManagerPageKind.Home;

    public bool IsSystemChecksPage => CurrentPageKind == ManagerPageKind.SystemChecks;

    public bool IsLogsPage => CurrentPageKind == ManagerPageKind.Logs;

    public bool IsGuidePage => CurrentPageKind == ManagerPageKind.Guide;

    public bool IsModulePage => CurrentPageKind == ManagerPageKind.Module;

    public bool HasSelectedModule => SelectedModule is not null;

    public bool HasLoadedModuleState
    {
        get => _hasLoadedModuleState;
        private set => SetProperty(ref _hasLoadedModuleState, value);
    }

    private ManagerPageKind CurrentPageKind
    {
        get => _currentPageKind;
        set
        {
            if (!SetProperty(ref _currentPageKind, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsHomePage));
            OnPropertyChanged(nameof(IsSystemChecksPage));
            OnPropertyChanged(nameof(IsLogsPage));
            OnPropertyChanged(nameof(IsGuidePage));
            OnPropertyChanged(nameof(IsModulePage));
        }
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
    }

    public void Dispose()
    {
        _sidebarArtTimer.Stop();
        _sidebarArtTimer.Tick -= OnSidebarArtTimerTick;
        _runtimeMonitorTimer.Stop();
        _runtimeMonitorTimer.Tick -= OnRuntimeMonitorTimerTick;
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Refreshing the modular shell...";
        AppendActivity("Refreshing Manager shell.");

        try
        {
            await RefreshSystemChecksAsync().ConfigureAwait(true);
            await RefreshRuntimeMonitorAsync().ConfigureAwait(true);
            await RefreshModuleStateAsync().ConfigureAwait(true);
            LoadHistoricalLogs();
            LastRefreshedText = $"Last refreshed {DateTime.Now:HH:mm:ss}";
            StatusMessage = "Manager shell refreshed.";
            AppendActivity("Manager shell refreshed.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Refresh finished with warnings.";
            AppendActivity($"Refresh warning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        UpdateSummary = "Checking managed repo state inside the shared distro...";
        AppendActivity("Checking for updates.");

        try
        {
            await _workflowService.RunManagedRepoUpdateCheckAsync(
                _settings,
                new Progress<string>(AppendActivity),
                CancellationToken.None).ConfigureAwait(true);

            UpdateSummary = $"Last update check finished at {DateTime.Now:HH:mm:ss}.";
            AppendActivity("Update check completed.");
            await RefreshModuleStateAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            UpdateSummary = "Update check needs attention.";
            AppendActivity($"Update check warning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshSystemChecksAsync()
    {
        var checks = await _workflowService.RunSystemChecksAsync(CancellationToken.None).ConfigureAwait(true);

        SystemChecks.Clear();
        foreach (var check in checks)
        {
            SystemChecks.Add(check);
        }

        var passCount = checks.Count(check => check.Status == CheckState.Pass);
        var warningCount = checks.Count(check => check.Status == CheckState.Warning);
        var failCount = checks.Count(check => check.Status == CheckState.Fail);

        SystemChecksSummary = $"{passCount}/{checks.Count} checks passed";
        RuntimePanelTitle = "Shared runtime";
        RuntimePanelSummary = failCount > 0
            ? "Core setup needs attention before every module can behave reliably."
            : warningCount > 0
                ? "Base platform is mostly ready, with a few checks worth reviewing."
                : "Base platform looks healthy for a modular shell.";

        var wslCheck = checks.FirstOrDefault(check => string.Equals(check.Key, InstallerWorkflowService.WslAvailabilityCheckKey, StringComparison.OrdinalIgnoreCase));
        RuntimePanelDetail = wslCheck?.Details ?? "The shared base runtime check has not returned details yet.";
    }

    private async Task RefreshRuntimeMonitorAsync()
    {
        var snapshot = await _workflowService.GetShellRuntimeMonitorAsync(_settings, CancellationToken.None).ConfigureAwait(true);

        RuntimePanelTitle = snapshot.DistributionLabel;
        RuntimePanelSummary = snapshot.KernelLabel;
        RuntimePanelDetail = snapshot.UptimeLabel;
        RuntimePanelStatusLabel = snapshot.IsAvailable ? "Running" : "Offline";
        RuntimePanelStatusBrush = snapshot.IsAvailable ? "#6FD96C" : "#B74322";
        RuntimeCpuUsageLabel = $"{snapshot.CpuPercent}%";
        RuntimeMemoryUsageLabel = snapshot.MemoryUsageLabel;
        RuntimeDiskUsageLabel = snapshot.WslDiskUsageLabel;
        RuntimeWindowsDiskUsageLabel = snapshot.WindowsDiskUsageLabel;
        RuntimeGpuVramLabel = snapshot.GpuVramLabel;
        RuntimeGpuTempLabel = snapshot.GpuTempLabel;
        RuntimeCpuBarWidth = ComputeRuntimeBarWidth(snapshot.CpuPercent);
        RuntimeMemoryBarWidth = ComputeRuntimeBarWidth(snapshot.MemoryPercent);
        RuntimeDiskBarWidth = ComputeRuntimeBarWidth(snapshot.DiskPercent);
    }

    private async Task RefreshModuleStateAsync()
    {
        RuntimeBackendStatus? zimageRuntime = null;
        RuntimeBackendStatus? trellisRuntime = null;
        string? brainStatusOutput = null;

        try
        {
            var runtimeStatuses = await _workflowService.GetRuntimeBackendStatusesAsync(
                _settings,
                new Progress<string>(AppendActivity),
                CancellationToken.None).ConfigureAwait(true);

            runtimeStatuses.TryGetValue("zimage", out zimageRuntime);
            runtimeStatuses.TryGetValue("trellis", out trellisRuntime);
        }
        catch (Exception ex)
        {
            AppendActivity($"Runtime status warning: {ex.Message}");
        }

        try
        {
            brainStatusOutput = await _workflowService.GetNymphsBrainStatusAsync(_settings, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendActivity($"Brain status warning: {ex.Message}");
        }

        ApplyBrainState(brainStatusOutput);
        ApplyRuntimeModuleState("zimage", zimageRuntime);
        ApplyRuntimeModuleState("trellis", trellisRuntime);
        ApplyLoraState();
        ApplyWorbiState();

        RebuildModuleCollections();
        RebuildModuleNavigation();
        HasLoadedModuleState = true;
    }

    private void ApplyBrainState(string? statusOutput)
    {
        var module = FindModule("brain");
        var installPath = module.InstallPath;
        var installed = SafeDirectoryExists(installPath);
        var llmState = "unknown";
        var modelState = "Not detected";

        if (!string.IsNullOrWhiteSpace(statusOutput))
        {
            var lines = statusOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            llmState = NormalizeBrainServiceState(
                FindStatusValue(lines, "llama-server:")
                ?? FindStatusValue(lines, "LLM server:")
                ?? "unknown");
            modelState = FindStatusValue(lines, "Model loaded:")
                ?? FindStatusValue(lines, "Model configured:")
                ?? "Not detected";
        }

        var running = string.Equals(llmState, "running", StringComparison.OrdinalIgnoreCase);
        var stateLabel = running
            ? "Running"
            : installed
                ? "Installed"
                : "Available";

        module.ApplyState(
            installed,
            running,
            "Manifest not wired yet",
            stateLabel,
            running ? "#6FD96C" : installed ? "#4CD0C1" : "#6E745A",
            installed
                ? $"Local model: {NormalizeLoadedBrainModel(modelState)}"
                : "Brain is available as an optional Nymph.",
            installed
                ? "Dedicated Brain controls can move into its own page as the manifest-driven shell fills out."
                : "Install wrappers are still being migrated into the new modular contract.");
    }

    private void ApplyRuntimeModuleState(string moduleId, RuntimeBackendStatus? runtimeStatus)
    {
        var module = FindModule(moduleId);
        var installed = SafeDirectoryExists(module.InstallPath) || runtimeStatus is not null && !runtimeStatus.IsNotChecked;

        var stateLabel = installed
            ? runtimeStatus?.ReadinessLabel ?? "Installed"
            : "Available";

        var statusBrush = runtimeStatus?.TestReady == true
            ? "#6FD96C"
            : runtimeStatus?.EnvironmentReady == true
                ? "#4CD0C1"
                : installed
                    ? "#B7791F"
                    : "#6E745A";

        module.ApplyState(
            installed,
            runtimeStatus?.TestReady == true,
            "Manifest not wired yet",
            stateLabel,
            statusBrush,
            runtimeStatus?.CompactDetail
                ?? (installed
                    ? "Module files were detected, but live runtime detail is not available yet."
                    : $"{module.Name} is available as an optional Nymph."),
            installed
                ? "This runtime is no longer treated as a permanent built-in core section."
                : "Install it only when you want this runtime in the shared platform.");
    }

    private void ApplyWorbiState()
    {
        var module = FindModule("worbi");
        var installPath = module.InstallPath;
        var installed = SafeDirectoryExists(installPath);
        var serverPidPath = Path.Combine(installPath, "logs", "worbi-server.pid");
        var clientPidPath = Path.Combine(installPath, "logs", "worbi-client.pid");
        var running = SafeFileExists(serverPidPath) || SafeFileExists(clientPidPath);

        module.ApplyState(
            installed,
            running,
            "Manifest not wired yet",
            running ? "Running" : installed ? "Installed" : "Available",
            running ? "#6FD96C" : installed ? "#4CD0C1" : "#6E745A",
            installed
                ? "Local-first worldbuilding app under the managed distro. Default app endpoint is `:8082`."
                : "WORBI is available as an optional archive-based Nymph.",
            installed
                ? "Manager-owned wrapper entrypoints can land here cleanly once the manifest registry is wired."
                : "The shell already understands WORBI as a module even before the full manifest layer is live.");
    }

    private void ApplyLoraState()
    {
        var module = FindModule("lora");
        var installPath = module.InstallPath;
        var installed = SafeDirectoryExists(installPath);
        var officialUiLauncher = Path.Combine(installPath, "bin", "ztrain-start-official-ui");
        var gradioLauncher = Path.Combine(installPath, "bin", "ztrain-start-gradio-ui");
        var trainerPython = Path.Combine(installPath, "ai-toolkit", "venv", "bin", "python");
        var adapterMarker = Path.Combine(installPath, "adapters", "zimage_turbo_training_adapter", "selected_adapter_path.txt");
        var isReady = SafeFileExists(officialUiLauncher)
            && SafeFileExists(gradioLauncher)
            && SafeFileExists(trainerPython)
            && SafeFileExists(adapterMarker);

        module.ApplyState(
            installed,
            false,
            "Manifest not wired yet",
            installed ? (isReady ? "Installed" : "Needs attention") : "Available",
            installed ? (isReady ? "#4CD0C1" : "#B7791F") : "#6E745A",
            installed
                ? isReady
                    ? "AI Toolkit training sidecar is installed with official UI, Gradio UI, Python environment, and Z-Image Turbo adapter markers present."
                    : "LoRA trainer files were detected, but one or more manager launch/support files are missing."
                : "LoRA training is available as an optional Nymph.",
            installed
                ? "This module owns datasets, jobs, LoRA outputs, and trainer UI launchers under ZImage-Trainer."
                : "Install it only when you want local Z-Image Turbo LoRA training.");
    }

    private void PrimeModulePresence()
    {
        foreach (var module in _allModules)
        {
            var installed = SafeDirectoryExists(module.InstallPath);
            var running = false;
            var stateLabel = installed ? "Installed" : "Available";
            var statusBrush = installed ? "#4CD0C1" : "#6E745A";
            var detail = installed
                ? "Module files detected. Live runtime detail is still loading."
                : $"{module.Name} is available as an optional Nymph.";
            var secondaryDetail = installed
                ? "Fast startup state from local install detection."
                : "Install it when you want this module in the shared platform.";

            if (string.Equals(module.Id, "brain", StringComparison.OrdinalIgnoreCase))
            {
                detail = installed
                    ? "Brain files detected. Live model status is still loading."
                    : "Brain is available as an optional Nymph.";
            }
            else if (string.Equals(module.Id, "worbi", StringComparison.OrdinalIgnoreCase))
            {
                var serverPidPath = Path.Combine(module.InstallPath, "logs", "worbi-server.pid");
                var clientPidPath = Path.Combine(module.InstallPath, "logs", "worbi-client.pid");
                running = SafeFileExists(serverPidPath) || SafeFileExists(clientPidPath);
                stateLabel = running ? "Running" : installed ? "Installed" : "Available";
                statusBrush = running ? "#6FD96C" : installed ? "#4CD0C1" : "#6E745A";
                detail = installed
                    ? "WORBI files detected. Live app status is still loading."
                    : "WORBI is available as an optional archive-based Nymph.";
            }

            module.ApplyState(
                installed,
                running,
                "Manifest not wired yet",
                stateLabel,
                statusBrush,
                detail,
                secondaryDetail);
        }
    }

    private void RebuildModuleCollections()
    {
        InstalledModules.Clear();
        foreach (var module in _allModules.Where(module => module.IsInstalled))
        {
            InstalledModules.Add(module);
        }

        AvailableModules.Clear();
        foreach (var module in _allModules.Where(module => !module.IsInstalled))
        {
            AvailableModules.Add(module);
        }

        InstalledModulesSummary = InstalledModules.Count > 0
            ? $"{InstalledModules.Count} installed Nymphs are ready for dedicated pages."
            : "No optional Nymphs were detected yet.";
        AvailableModulesSummary = AvailableModules.Count > 0
            ? $"{AvailableModules.Count} optional Nymphs are available to add."
            : "Every known Nymph in this shell is already installed.";

        OnPropertyChanged(nameof(InstalledModuleCount));
        OnPropertyChanged(nameof(AvailableModuleCount));
        OnPropertyChanged(nameof(HomeInstalledModules));
        OnPropertyChanged(nameof(HomeAvailableModules));
        OnPropertyChanged(nameof(ShowInstalledModulesSection));
        OnPropertyChanged(nameof(ShowAvailableModulesSection));
    }

    private void RebuildModuleNavigation()
    {
        var selectedModuleId = SelectedModule?.Id;
        ModuleNavigationItems.Clear();

        foreach (var module in InstalledModules)
        {
            ModuleNavigationItems.Add(new ShellNavigationItemViewModel(
                module.Name,
                module.NavigationSubtitle,
                module.Monogram,
                string.Empty,
                module.AccentBrush,
                ManagerPageKind.Module,
                module));
        }

        if (CurrentPageKind == ManagerPageKind.Module)
        {
            var replacement = InstalledModules.FirstOrDefault(module => string.Equals(module.Id, selectedModuleId, StringComparison.OrdinalIgnoreCase));
            if (replacement is not null)
            {
                ShowModulePage(replacement);
            }
            else
            {
                SelectPrimaryPage(ManagerPageKind.Home);
            }
        }
    }

    private void LoadSidebarArtwork()
    {
        var artFolder = ResolveSidebarArtFolder();
        var overridePath = Path.Combine(artFolder, _sidebarPortraitOverrideFileName);

        _sidebarArtPaths.Clear();

        if (File.Exists(overridePath))
        {
            CurrentSidebarArtPath = overridePath;
            return;
        }

        if (Directory.Exists(artFolder))
        {
            _sidebarArtPaths.AddRange(
                Directory.GetFiles(artFolder, "*.png", SearchOption.TopDirectoryOnly)
                    .Where(path => !string.Equals(Path.GetFileName(path), _sidebarPortraitOverrideFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        if (_sidebarArtPaths.Count == 0)
        {
            CurrentSidebarArtPath = "/AppAssets/splash.png";
            return;
        }

        AdvanceSidebarArt();
    }

    private static string ResolveSidebarArtFolder()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "AppAssets", "SidebarPortraits")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AppAssets", "SidebarPortraits")),
            Path.Combine(AppContext.BaseDirectory, "AppAssets", "SidebarPortraits"),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private void LoadHistoricalLogs()
    {
        var unifiedEntries = new List<(DateTime Timestamp, int Sequence, string Line)>();
        var sequence = 0;

        foreach (var line in ActivityLines.TakeLast(200))
        {
            unifiedEntries.Add((ParseUnifiedLogTimestamp(line), sequence++, $"shell  {line}"));
        }

        try
        {
            if (Directory.Exists(_workflowService.LogFolderPath))
            {
                var latestLog = Directory.GetFiles(_workflowService.LogFolderPath, "*.log", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(latestLog))
                {
                    var fileName = Path.GetFileName(latestLog);
                    foreach (var line in File.ReadLines(latestLog).TakeLast(200))
                    {
                        unifiedEntries.Add((ParseUnifiedLogTimestamp(line), sequence++, $"{fileName}  {line}"));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            unifiedEntries.Add((DateTime.MaxValue, sequence++, $"shell  log-read warning  {ex.Message}"));
        }

        var mergedLines = unifiedEntries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.Sequence)
            .Select(entry => entry.Line)
            .TakeLast(240)
            .ToList();

        UnifiedLogLines.Clear();
        foreach (var line in mergedLines)
        {
            UnifiedLogLines.Add(line);
        }

        RecentLogLines.Clear();
        foreach (var line in mergedLines.TakeLast(8))
        {
            RecentLogLines.Add(line);
        }
    }

    private void AppendActivity(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}";
        ActivityLines.Add(line);

        while (ActivityLines.Count > 200)
        {
            ActivityLines.RemoveAt(0);
        }

        LoadHistoricalLogs();
    }

    private void OpenModule(NymphModuleViewModel? module)
    {
        if (module is null || !module.IsInstalled)
        {
            return;
        }

        ShowModulePage(module);
    }

    private void InstallModule(NymphModuleViewModel? module)
    {
        _ = InstallModuleAsync(module);
    }

    private async Task InstallModuleAsync(NymphModuleViewModel? module)
    {
        if (module is null || module.IsInstalled || IsBusy)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"Install {module.Name} from the Nymphs registry?\n\nThe manager will read nymphs-registry, clone the trusted module repo, and run its install script inside the managed WSL distro.",
            "Install Module",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            AppendActivity($"{module.Name} install cancelled.");
            return;
        }

        IsBusy = true;
        StatusMessage = $"Installing {module.Name} from the Nymphs registry...";

        try
        {
            await _workflowService.RunNymphModuleInstallFromRegistryAsync(
                _settings,
                module.Id,
                new Progress<string>(AppendActivity),
                CancellationToken.None).ConfigureAwait(true);

            AppendActivity($"{module.Name} install completed.");
            await RefreshModuleStateAsync().ConfigureAwait(true);
            StatusMessage = $"{module.Name} installed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} install needs attention.";
            AppendActivity($"{module.Name} install warning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowModulePage(NymphModuleViewModel module)
    {
        SelectedNavigationItem = null;
        CurrentPageKind = ManagerPageKind.Module;
        SelectedModule = module;
        CurrentPageTitle = module.Name;
        CurrentPageSubtitle = module.Description;
    }

    private void OpenModuleInstallPath(NymphModuleViewModel? module)
    {
        if (module is null || !module.CanOpenInstallPath)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = module.InstallPath,
                UseShellExecute = true,
            });
            AppendActivity($"{module.Name} install path opened.");
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open {module.Name} install path: {ex.Message}");
        }
    }

    private void UninstallModule(NymphModuleViewModel? module)
    {
        _ = RunModuleUninstallAsync(module, purge: false);
    }

    private void DeleteModule(NymphModuleViewModel? module)
    {
        _ = RunModuleUninstallAsync(module, purge: true);
    }

    private async Task RunModuleUninstallAsync(NymphModuleViewModel? module, bool purge)
    {
        if (module is null || !module.IsInstalled || IsBusy)
        {
            return;
        }

        var prompt = purge
            ? $"Delete {module.Name} completely?\n\nThis removes the module install folder and its module data from the managed WSL distro."
            : $"Uninstall {module.Name}?\n\nThis removes the module install folder but saves known user data to ~/NymphsModuleBackups inside the managed WSL distro.";
        var caption = purge ? "Delete Module + Data" : "Uninstall Module";
        var icon = purge ? MessageBoxImage.Warning : MessageBoxImage.Question;
        var confirmation = MessageBox.Show(prompt, caption, MessageBoxButton.YesNo, icon);
        if (confirmation != MessageBoxResult.Yes)
        {
            AppendActivity($"{module.Name} uninstall cancelled.");
            return;
        }

        IsBusy = true;
        StatusMessage = purge
            ? $"Deleting {module.Name} from the managed distro..."
            : $"Uninstalling {module.Name} from the managed distro...";

        try
        {
            await _workflowService.RunNymphModuleUninstallAsync(
                _settings,
                module.Id,
                purge,
                new Progress<string>(AppendActivity),
                CancellationToken.None).ConfigureAwait(true);

            AppendActivity(purge
                ? $"{module.Name} delete completed."
                : $"{module.Name} uninstall completed.");
            await RefreshModuleStateAsync().ConfigureAwait(true);

            if (!module.IsInstalled && CurrentPageKind == ManagerPageKind.Module)
            {
                SelectPrimaryPage(ManagerPageKind.Home);
            }

            StatusMessage = purge
                ? $"{module.Name} deleted."
                : $"{module.Name} uninstalled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} uninstall needs attention.";
            AppendActivity($"{module.Name} uninstall warning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenTerminal()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {_settings.DistroName} --cd ~",
                UseShellExecute = true,
            });
            AppendActivity("Managed distro terminal opened.");
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open managed distro terminal: {ex.Message}");
        }
    }

    private void SafeRun(Action action, string successMessage)
    {
        try
        {
            action();
            AppendActivity(successMessage);
        }
        catch (Exception ex)
        {
            AppendActivity($"Action warning: {ex.Message}");
        }
    }

    private void SelectPrimaryPage(ManagerPageKind pageKind)
    {
        var item = PrimaryNavigationItems.FirstOrDefault(nav => nav.PageKind == pageKind);
        if (item is not null)
        {
            SelectedNavigationItem = item;
            return;
        }

        CurrentPageKind = pageKind;
        SelectedModule = null;

        switch (pageKind)
        {
            case ManagerPageKind.SystemChecks:
                CurrentPageTitle = "System Checks";
                CurrentPageSubtitle = "// Live platform diagnostics and readiness checks";
                break;
            case ManagerPageKind.Home:
                CurrentPageTitle = "Home";
                CurrentPageSubtitle = "// Overview of your system and modules";
                break;
            case ManagerPageKind.Logs:
                CurrentPageTitle = "Logs";
                CurrentPageSubtitle = "// Session activity, runtime checks, and shell feedback";
                break;
            case ManagerPageKind.Guide:
                CurrentPageTitle = "Guide";
                CurrentPageSubtitle = "// Core docs, onboarding, and next-step references";
                break;
        }
    }

    private void OnSidebarArtTimerTick(object? sender, EventArgs e)
    {
        // Rotation is disabled for the single-artwork sidebar test stage.
    }

    private async void OnRuntimeMonitorTimerTick(object? sender, EventArgs e)
    {
        if (_isRefreshingRuntimeMonitorLive || IsBusy)
        {
            return;
        }

        _isRefreshingRuntimeMonitorLive = true;
        try
        {
            await RefreshRuntimeMonitorAsync().ConfigureAwait(true);
            LastRefreshedText = $"Last refreshed {DateTime.Now:HH:mm:ss}";
        }
        catch
        {
            // Manual refresh already reports warnings. Live polling stays quiet.
        }
        finally
        {
            _isRefreshingRuntimeMonitorLive = false;
        }
    }

    private void AdvanceSidebarArt()
    {
        if (_sidebarArtPaths.Count == 0)
        {
            return;
        }

        _sidebarArtIndex = (_sidebarArtIndex + 1) % _sidebarArtPaths.Count;
        CurrentSidebarArtPath = _sidebarArtPaths[_sidebarArtIndex];
    }

    private static InstallSettings CreateDefaultInstallSettings()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new InstallSettings
        {
            TarPath = string.Empty,
            InstallLocation = Path.Combine(userProfile, "NymphsCore"),
            DistroName = InstallerWorkflowService.ManagedDistroName,
            LinuxUser = InstallerWorkflowService.ManagedLinuxUser,
        };
    }

    private static string NormalizeBrainServiceState(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("running", StringComparison.OrdinalIgnoreCase))
        {
            return "running";
        }

        if (normalized.StartsWith("stopped", StringComparison.OrdinalIgnoreCase))
        {
            return "stopped";
        }

        return normalized;
    }

    private static string NormalizeLoadedBrainModel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not detected";
        }

        return value.Trim();
    }

    private static string? FindStatusValue(IEnumerable<string> lines, string prefix)
    {
        var line = lines.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line is null ? null : line[prefix.Length..].Trim();
    }

    private static bool SafeDirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool SafeFileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static double ComputeRuntimeBarWidth(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        return Math.Max(6, Math.Round(clamped * 0.78, MidpointRounding.AwayFromZero));
    }

    private static DateTime ParseUnifiedLogTimestamp(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return DateTime.MaxValue;
        }

        var start = line.IndexOf('[');
        var end = line.IndexOf(']', start + 1);
        if (start < 0 || end <= start)
        {
            return DateTime.MaxValue;
        }

        var token = line.Substring(start + 1, end - start - 1).Trim();

        if (DateTime.TryParseExact(
                token,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var datedTimestamp))
        {
            return datedTimestamp;
        }

        if (TimeSpan.TryParseExact(token, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var timeOnly))
        {
            return DateTime.Today.Add(timeOnly);
        }

        return DateTime.MaxValue;
    }

    private string BuildManagedDistroPath(params string[] segments)
    {
        var parts = new List<string> { $@"\\wsl.localhost\{_settings.DistroName}" };
        parts.AddRange(segments);
        return Path.Combine(parts.ToArray());
    }

    private NymphModuleViewModel FindModule(string id)
    {
        return _allModules.First(module => string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
