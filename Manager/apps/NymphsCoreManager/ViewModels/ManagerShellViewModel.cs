using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using NymphsCoreManager.Models;
using NymphsCoreManager.Services;

namespace NymphsCoreManager.ViewModels;

public sealed class ManagerShellViewModel : ViewModelBase, IDisposable
{
    private const string CloseModuleUiActionName = "__close_module_ui";
    private readonly InstallerWorkflowService _workflowService;
    private readonly SharedSecretsService _sharedSecretsService = new();
    private readonly InstallSettings _settings;
    private readonly DispatcherTimer _sidebarArtTimer;
    private readonly DispatcherTimer _runtimeMonitorTimer;
    private readonly List<string> _sidebarArtPaths = [];
    private readonly string _sidebarPortraitOverrideFileName = "NymphMycelium1.png";
    private readonly List<NymphModuleViewModel> _allModules;
    private readonly HashSet<string> _modulesWithActiveLifecycle = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _operationCancellation = new();
    private bool _shutdownStarted;
    private bool _hasRunStartupUpdateCheck;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _setupWindowsWslCommand;
    private readonly AsyncRelayCommand _setupBaseRuntimeCommand;
    private readonly AsyncRelayCommand _uninstallBaseRuntimeCommand;
    private readonly AsyncRelayCommand _checkForUpdatesCommand;
    private readonly RelayCommand<NymphModuleViewModel> _openModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _installModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _repairModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _updateModuleCommand;
    private readonly RelayCommand _openModelCacheCommand;
    private readonly RelayCommand<NymphModuleViewModel> _openModuleInstallPathCommand;
    private readonly RelayCommand<NymphModuleViewModel> _openModuleUiCommand;
    private readonly RelayCommand<NymphModuleViewModel> _openModuleSourceCommand;
    private readonly RelayCommand<NymphModuleActionInfo> _runModuleActionCommand;
    private readonly RelayCommand<NymphModuleActionGroupInfo> _runModuleActionGroupCommand;
    private readonly RelayCommand<NymphModuleActionLinkInfo> _openModuleActionLinkCommand;
    private readonly RelayCommand<NymphModuleActionFieldInfo> _applyModuleActionSecretCommand;
    private readonly RelayCommand<NymphModuleActionFieldInfo> _clearModuleActionSecretCommand;
    private readonly RelayCommand<string> _runModuleDevActionCommand;
    private readonly RelayCommand _toggleDeveloperModeCommand;
    private readonly RelayCommand<NymphModuleViewModel> _uninstallModuleCommand;
    private readonly RelayCommand<NymphModuleViewModel> _deleteModuleCommand;
    private DriveChoice? _selectedBaseRuntimeDrive;
    private ShellNavigationItemViewModel? _selectedNavigationItem;
    private NymphModuleViewModel? _selectedModule;
    private NymphModuleViewModel? _displayedModule;
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
    private string _runtimeBrainLlmStateLabel = "LLM: Offline";
    private string _runtimeBrainModelLabel = "Local: -";
    private string _runtimeBrainRemoteModelLabel = "Remote: -";
    private string _runtimeBrainContextLabel = "Context: -";
    private string _runtimeBrainTokensPerSecondLabel = "TPS: -";
    private double _runtimeCpuBarWidth;
    private double _runtimeMemoryBarWidth;
    private double _runtimeDiskBarWidth;
    private string _baseRuntimeSummary = "Base runtime has not been checked yet.";
    private string _baseRuntimeDetail = "Install the NymphsCore WSL shell first. Modules install later from the registry.";
    private string _baseRuntimeCardSubtitle = "Install base shell";
    private string _baseRuntimeCardStatus = "Not installed";
    private string _baseRuntimeActionText = "Install Base Runtime";
    private string _baseRuntimeProgressText = "Ready.";
    private string _baseRuntimeOperationLabel = "idle";
    private bool _isBaseRuntimeOperationActive;
    private bool _managedDistroDetected;
    private bool _windowsWslReady;
    private string _systemChecksSummary = "System checks have not run yet.";
    private string _installedModulesSummary = "Scanning installed Nymphs...";
    private string _availableModulesSummary = "Manifest-aware shell is loading the known module roster.";
    private string _moduleActionFeedbackTitle = "No module command has run yet.";
    private string _moduleActionFeedbackDetail = "Use the manager contract buttons below to run this module's live commands.";
    private string _stickyModuleActionFeedbackModuleId = string.Empty;
    private DateTime _stickyModuleActionFeedbackUntilUtc = DateTime.MinValue;
    private string _moduleLogsTitle = "No module logs loaded.";
    private string _moduleLogsDetail = string.Empty;
    private string _moduleUiTitle = "Module UI";
    private string _moduleUiStatus = "No installed module UI is loaded.";
    private string _moduleUiSource = string.Empty;
    private string _currentSidebarArtPath = string.Empty;
    private string _unifiedLogText = string.Empty;
    private bool _isBusy;
    private bool _hasLoadedModuleState;
    private bool _isRefreshingRuntimeMonitorLive;
    private bool _isRuntimeMonitorAvailable;
    private bool _showModuleLogs;
    private bool _isDeveloperMode;
    private int _sidebarArtIndex = -1;

    public ManagerShellViewModel(InstallerWorkflowService workflowService)
    {
        _workflowService = workflowService;
        _settings = CreateDefaultInstallSettings();
        var sharedSecrets = _sharedSecretsService.Load();
        _settings.HuggingFaceToken = sharedSecrets.HuggingFaceToken?.Trim() ?? string.Empty;
        _settings.OpenRouterApiKey = sharedSecrets.OpenRouterApiKey?.Trim() ?? string.Empty;

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
        BaseRuntimeDrives = new ObservableCollection<DriveChoice>(_workflowService.GetAvailableDrives());
        _selectedBaseRuntimeDrive = BaseRuntimeDrives
            .OrderByDescending(drive => drive.FreeBytes)
            .FirstOrDefault();
        if (_selectedBaseRuntimeDrive is not null)
        {
            _settings.InstallLocation = _selectedBaseRuntimeDrive.InstallPath;
        }
        ActivityLines = [];
        RecentLogLines = [];
        UnifiedLogLines = [];

        _allModules = [];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        _setupWindowsWslCommand = new AsyncRelayCommand(SetupWindowsWslAsync, CanSetupWindowsWsl);
        _setupBaseRuntimeCommand = new AsyncRelayCommand(SetupBaseRuntimeAsync, CanSetupBaseRuntime);
        _uninstallBaseRuntimeCommand = new AsyncRelayCommand(UninstallBaseRuntimeAsync, CanUninstallBaseRuntime);
        _checkForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsBusy);
        _openModuleCommand = new RelayCommand<NymphModuleViewModel>(OpenModule, module => module is not null);
        _installModuleCommand = new RelayCommand<NymphModuleViewModel>(InstallModule, module => module?.IsInstalled == false && !IsBusy);
        _repairModuleCommand = new RelayCommand<NymphModuleViewModel>(RepairModule, module => module?.CanRepair == true && !IsBusy);
        _updateModuleCommand = new RelayCommand<NymphModuleViewModel>(UpdateModule, module => module?.CanUpdate == true && !IsBusy);
        _openModelCacheCommand = new RelayCommand(OpenModelCache);
        _openModuleInstallPathCommand = new RelayCommand<NymphModuleViewModel>(OpenModuleInstallPath, module => module?.CanOpenInstallPath == true);
        _openModuleUiCommand = new RelayCommand<NymphModuleViewModel>(OpenModuleUi, module => module?.HasInstalledModuleUi == true);
        _openModuleSourceCommand = new RelayCommand<NymphModuleViewModel>(OpenModuleSource, module => module?.HasRepositoryUrl == true);
        _runModuleActionCommand = new RelayCommand<NymphModuleActionInfo>(RunSelectedModuleAction, CanRunSelectedModuleAction);
        _runModuleActionGroupCommand = new RelayCommand<NymphModuleActionGroupInfo>(RunSelectedModuleActionGroup, CanRunSelectedModuleActionGroup);
        _openModuleActionLinkCommand = new RelayCommand<NymphModuleActionLinkInfo>(OpenModuleActionLink, link => link is not null && !string.IsNullOrWhiteSpace(link.Url));
        _applyModuleActionSecretCommand = new RelayCommand<NymphModuleActionFieldInfo>(ApplyModuleActionSecret, field => field is not null && field.IsSecret);
        _clearModuleActionSecretCommand = new RelayCommand<NymphModuleActionFieldInfo>(ClearModuleActionSecret, field => field is not null && field.IsSecret);
        _runModuleDevActionCommand = new RelayCommand<string>(RunSelectedModuleDevAction, CanRunSelectedModuleDevAction);
        _toggleDeveloperModeCommand = new RelayCommand(ToggleDeveloperMode);
        _uninstallModuleCommand = new RelayCommand<NymphModuleViewModel>(UninstallModule, module => module?.CanUninstall == true && !IsBusy);
        _deleteModuleCommand = new RelayCommand<NymphModuleViewModel>(DeleteModule, module => module?.IsInstalled == true && !IsBusy);

        LoadSidebarArtwork();
        LoadHistoricalLogs();
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

    public ObservableCollection<DriveChoice> BaseRuntimeDrives { get; }

    public ObservableCollection<string> ActivityLines { get; }

    public ObservableCollection<string> RecentLogLines { get; }

    public ObservableCollection<string> UnifiedLogLines { get; }

    public string UnifiedLogText
    {
        get => _unifiedLogText;
        private set => SetProperty(ref _unifiedLogText, value);
    }

    public AsyncRelayCommand RefreshCommand => _refreshCommand;

    public AsyncRelayCommand SetupWindowsWslCommand => _setupWindowsWslCommand;

    public AsyncRelayCommand SetupBaseRuntimeCommand => _setupBaseRuntimeCommand;

    public AsyncRelayCommand UninstallBaseRuntimeCommand => _uninstallBaseRuntimeCommand;

    public AsyncRelayCommand CheckForUpdatesCommand => _checkForUpdatesCommand;

    public RelayCommand<NymphModuleViewModel> OpenModuleCommand => _openModuleCommand;

    public RelayCommand<NymphModuleViewModel> InstallModuleCommand => _installModuleCommand;

    public RelayCommand<NymphModuleViewModel> RepairModuleCommand => _repairModuleCommand;

    public RelayCommand<NymphModuleViewModel> UpdateModuleCommand => _updateModuleCommand;

    public RelayCommand OpenModelCacheCommand => _openModelCacheCommand;

    public RelayCommand<NymphModuleViewModel> OpenModuleInstallPathCommand => _openModuleInstallPathCommand;

    public RelayCommand<NymphModuleViewModel> OpenModuleUiCommand => _openModuleUiCommand;

    public RelayCommand<NymphModuleViewModel> OpenModuleSourceCommand => _openModuleSourceCommand;

    public RelayCommand<NymphModuleActionInfo> RunModuleActionCommand => _runModuleActionCommand;

    public RelayCommand<NymphModuleActionGroupInfo> RunModuleActionGroupCommand => _runModuleActionGroupCommand;

    public RelayCommand<NymphModuleActionLinkInfo> OpenModuleActionLinkCommand => _openModuleActionLinkCommand;

    public RelayCommand<NymphModuleActionFieldInfo> ApplyModuleActionSecretCommand => _applyModuleActionSecretCommand;

    public RelayCommand<NymphModuleActionFieldInfo> ClearModuleActionSecretCommand => _clearModuleActionSecretCommand;

    public RelayCommand<string> RunModuleDevActionCommand => _runModuleDevActionCommand;

    public RelayCommand ToggleDeveloperModeCommand => _toggleDeveloperModeCommand;

    public RelayCommand<NymphModuleViewModel> UninstallModuleCommand => _uninstallModuleCommand;

    public RelayCommand<NymphModuleViewModel> DeleteModuleCommand => _deleteModuleCommand;

    public RelayCommand OpenGuideCommand => new(() => SafeRun(_workflowService.OpenGuide, "Guide opened."));

    public RelayCommand OpenReadmeCommand => new(() => SafeRun(_workflowService.OpenReadme, "README opened."));

    public RelayCommand OpenSourceCommand => new(() => SafeRun(_workflowService.OpenSourceRepo, "Source repo opened."));

    public RelayCommand OpenAddonGuideCommand => new(() => SafeRun(_workflowService.OpenAddonGuide, "Addon guide opened."));

    public RelayCommand OpenFootprintCommand => new(() => SafeRun(_workflowService.OpenFootprintDoc, "Footprint document opened."));

    public RelayCommand OpenLogFolderCommand => new(() => SafeRun(_workflowService.OpenLogFolder, "Log folder opened."));

    public RelayCommand OpenTerminalCommand => new(OpenTerminal);

    public RelayCommand ShowBaseRuntimeCommand => new(() => SelectPrimaryPage(ManagerPageKind.BaseRuntime));

    public RelayCommand ShowSystemChecksCommand => new(() => SelectPrimaryPage(ManagerPageKind.SystemChecks));

    public RelayCommand ShowLogsCommand => new(() => SelectPrimaryPage(ManagerPageKind.Logs));

    public RelayCommand ShowHomeCommand => new(() => SelectPrimaryPage(ManagerPageKind.Home));

    public RelayCommand ShowGuideCommand => new(() => SelectPrimaryPage(ManagerPageKind.Guide));

    public string AppVersionLabel { get; } = BuildAppVersionLabel();

    public string ModuleActionFeedbackTitle
    {
        get => _moduleActionFeedbackTitle;
        private set => SetProperty(ref _moduleActionFeedbackTitle, value);
    }

    public string ModuleActionFeedbackDetail
    {
        get => _moduleActionFeedbackDetail;
        private set => SetProperty(ref _moduleActionFeedbackDetail, value);
    }

    public bool ShowModuleLogs
    {
        get => _showModuleLogs;
        private set => SetProperty(ref _showModuleLogs, value);
    }

    public string ModuleLogsTitle
    {
        get => _moduleLogsTitle;
        private set => SetProperty(ref _moduleLogsTitle, value);
    }

    public string ModuleLogsDetail
    {
        get => _moduleLogsDetail;
        private set => SetProperty(ref _moduleLogsDetail, value);
    }

    public string ModuleUiTitle
    {
        get => _moduleUiTitle;
        private set => SetProperty(ref _moduleUiTitle, value);
    }

    public string ModuleUiStatus
    {
        get => _moduleUiStatus;
        private set => SetProperty(ref _moduleUiStatus, value);
    }

    public string ModuleUiSource
    {
        get => _moduleUiSource;
        private set => SetProperty(ref _moduleUiSource, value);
    }

    public bool IsDeveloperMode
    {
        get => _isDeveloperMode;
        set
        {
            if (SetProperty(ref _isDeveloperMode, value))
            {
                OnPropertyChanged(nameof(DeveloperModeLabel));
                OnPropertyChanged(nameof(DeveloperModeBrush));
                OnPropertyChanged(nameof(BottomStatusText));
                OnPropertyChanged(nameof(ShowDevContract));
                _runModuleDevActionCommand.RaiseCanExecuteChanged();
                StatusMessage = _isDeveloperMode ? "Developer mode enabled." : "Developer mode disabled.";
                AppendActivity(StatusMessage);
            }
        }
    }

    public bool ShowDevContract => IsDeveloperMode && DisplayedModule?.DevCapabilities.Count > 0;

    public bool ShowDeleteModuleData => string.Equals(DisplayedModule?.Id, "worbi", StringComparison.OrdinalIgnoreCase);

    public bool ShowInstallModuleAction => DisplayedModule?.CanInstall == true && !IsModuleLifecycleActive(DisplayedModule);

    public bool ShowModuleInstallFields =>
        DisplayedModule?.CanInstall == true &&
        !IsModuleLifecycleActive(DisplayedModule) &&
        DisplayedModule.InstallOptionFields.Count > 0;

    public bool ShowInstalledModuleActions => DisplayedModule?.IsInstalled == true && DisplayedModule.ManagerActions.Count > 0;

    public bool ShowInstalledModuleActionGroups => DisplayedModule?.IsInstalled == true && DisplayedModule.ManagerActionGroups.Count > 0;

    public bool ShowModuleUiAction => DisplayedModule?.HasInstalledModuleUi == true;

    public IReadOnlyList<NymphModuleActionInfo> DisplayedModuleContractActions
    {
        get
        {
            if (DisplayedModule is null)
            {
                return Array.Empty<NymphModuleActionInfo>();
            }

            if (!string.Equals(DisplayedModule.Id, "brain", StringComparison.OrdinalIgnoreCase))
            {
                return DisplayedModule.ManagerActions;
            }

            var actions = new List<NymphModuleActionInfo>();
            foreach (var action in DisplayedModule.ManagerActions)
            {
                var actionId = action.Id.Trim().ToLowerInvariant();
                var actionName = action.ActionName.Trim().ToLowerInvariant();
                if (actionName == "start" && DisplayedModule.IsRunning)
                {
                    continue;
                }

                if (actionName == "stop" && !DisplayedModule.IsRunning)
                {
                    continue;
                }

                if (actionId == "webui" && CurrentPageKind == ManagerPageKind.ModuleUi)
                {
                    actions.Add(action with
                    {
                        Id = "close_webui",
                        Label = "Close WebUI",
                        EntryPoint = CloseModuleUiActionName,
                        ResultMode = "manager_close"
                    });
                    continue;
                }

                actions.Add(action);
            }

            return actions;
        }
    }

    public IReadOnlyList<NymphModuleActionFieldInfo> DisplayedModuleInstallFields =>
        DisplayedModule?.CanInstall == true
            ? DisplayedModule.InstallOptionFields
            : Array.Empty<NymphModuleActionFieldInfo>();

    public string DisplayedModuleInstallOptionsTitle =>
        DisplayedModule?.InstallOptionsTitle ?? "Install Options";

    public IReadOnlyList<NymphModuleActionGroupInfo> DisplayedModuleActionGroups
    {
        get
        {
            if (DisplayedModule is null || !DisplayedModule.IsInstalled)
            {
                return Array.Empty<NymphModuleActionGroupInfo>();
            }

            RefreshActionGroupSecretState(DisplayedModule);
            return DisplayedModule.ManagerActionGroups;
        }
    }

    public string DeveloperModeLabel => IsDeveloperMode ? "Dev Mode On" : "Dev Mode Off";

    public string DeveloperModeBrush => IsDeveloperMode ? "#97DF48" : "#445A5C";

    public string BottomStatusText => $"{StatusMessage}  |  {DeveloperModeLabel}  |  {UpdateSummary}";

    public ShellNavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            var changed = SetProperty(ref _selectedNavigationItem, value);
            if (value is null)
            {
                return;
            }

            if (!changed && CurrentPageKind == value.PageKind)
            {
                return;
            }

            CurrentPageKind = value.PageKind;
            SelectedModule = value.Module;
            DisplayedModule = value.PageKind == ManagerPageKind.Module ? value.Module : null;
            if (value.PageKind != ManagerPageKind.ModuleUi)
            {
                ModuleUiSource = string.Empty;
            }

            switch (value.PageKind)
            {
                case ManagerPageKind.Home:
                    CurrentPageTitle = "Home";
                    CurrentPageSubtitle = "Overview of your system and modules";
                    break;
                case ManagerPageKind.BaseRuntime:
                    CurrentPageTitle = "Base Runtime";
                    CurrentPageSubtitle = "Managed WSL shell, platform status, and repair actions";
                    break;
                case ManagerPageKind.SystemChecks:
                    CurrentPageTitle = "System Checks";
                    CurrentPageSubtitle = "Live platform diagnostics and readiness checks";
                    break;
                case ManagerPageKind.Logs:
                    CurrentPageTitle = "Logs";
                    CurrentPageSubtitle = "Session activity, runtime checks, and shell feedback";
                    break;
                case ManagerPageKind.Guide:
                    CurrentPageTitle = "Guide";
                    CurrentPageSubtitle = "Core docs, onboarding, and next-step references";
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
                OnPropertyChanged(nameof(ShowDevContract));
                _updateModuleCommand.RaiseCanExecuteChanged();
                _runModuleActionCommand.RaiseCanExecuteChanged();
                _runModuleActionGroupCommand.RaiseCanExecuteChanged();
                _runModuleDevActionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public NymphModuleViewModel? DisplayedModule
    {
        get => _displayedModule;
        private set
        {
            if (SetProperty(ref _displayedModule, value))
            {
                OnPropertyChanged(nameof(HasSelectedModule));
                OnPropertyChanged(nameof(ShowDevContract));
                OnPropertyChanged(nameof(ShowDeleteModuleData));
                OnPropertyChanged(nameof(ShowInstallModuleAction));
                OnPropertyChanged(nameof(ShowModuleInstallFields));
                OnPropertyChanged(nameof(DisplayedModuleInstallOptionsTitle));
                OnPropertyChanged(nameof(ShowInstalledModuleActions));
                OnPropertyChanged(nameof(ShowInstalledModuleActionGroups));
                OnPropertyChanged(nameof(ShowModuleUiAction));
                OnPropertyChanged(nameof(DisplayedModuleContractActions));
                OnPropertyChanged(nameof(DisplayedModuleInstallFields));
                OnPropertyChanged(nameof(DisplayedModuleActionGroups));
                _repairModuleCommand.RaiseCanExecuteChanged();
                _updateModuleCommand.RaiseCanExecuteChanged();
                _runModuleActionCommand.RaiseCanExecuteChanged();
                _runModuleActionGroupCommand.RaiseCanExecuteChanged();
                _runModuleDevActionCommand.RaiseCanExecuteChanged();
                _uninstallModuleCommand.RaiseCanExecuteChanged();
                _deleteModuleCommand.RaiseCanExecuteChanged();
                _openModuleUiCommand.RaiseCanExecuteChanged();
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
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(BottomStatusText));
            }
        }
    }

    public string UpdateSummary
    {
        get => _updateSummary;
        private set
        {
            if (SetProperty(ref _updateSummary, value))
            {
                OnPropertyChanged(nameof(BottomStatusText));
            }
        }
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

    public string RuntimeBrainLlmStateLabel
    {
        get => _runtimeBrainLlmStateLabel;
        private set => SetProperty(ref _runtimeBrainLlmStateLabel, value);
    }

    public string RuntimeBrainModelLabel
    {
        get => _runtimeBrainModelLabel;
        private set => SetProperty(ref _runtimeBrainModelLabel, value);
    }

    public string RuntimeBrainRemoteModelLabel
    {
        get => _runtimeBrainRemoteModelLabel;
        private set => SetProperty(ref _runtimeBrainRemoteModelLabel, value);
    }

    public string RuntimeBrainContextLabel
    {
        get => _runtimeBrainContextLabel;
        private set => SetProperty(ref _runtimeBrainContextLabel, value);
    }

    public string RuntimeBrainTokensPerSecondLabel
    {
        get => _runtimeBrainTokensPerSecondLabel;
        private set => SetProperty(ref _runtimeBrainTokensPerSecondLabel, value);
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

    public string BaseRuntimeSummary
    {
        get => _baseRuntimeSummary;
        private set => SetProperty(ref _baseRuntimeSummary, value);
    }

    public string BaseRuntimeDetail
    {
        get => _baseRuntimeDetail;
        private set => SetProperty(ref _baseRuntimeDetail, value);
    }

    public string BaseRuntimeCardSubtitle
    {
        get => _baseRuntimeCardSubtitle;
        private set => SetProperty(ref _baseRuntimeCardSubtitle, value);
    }

    public string BaseRuntimeCardStatus
    {
        get => _baseRuntimeCardStatus;
        private set => SetProperty(ref _baseRuntimeCardStatus, value);
    }

    public string BaseRuntimeActionText
    {
        get => _baseRuntimeActionText;
        private set => SetProperty(ref _baseRuntimeActionText, value);
    }

    public string BaseRuntimeProgressText
    {
        get => _baseRuntimeProgressText;
        private set => SetProperty(ref _baseRuntimeProgressText, value);
    }

    public string BaseRuntimeOperationLabel
    {
        get => _baseRuntimeOperationLabel;
        private set => SetProperty(ref _baseRuntimeOperationLabel, value);
    }

    public bool IsBaseRuntimeOperationActive
    {
        get => _isBaseRuntimeOperationActive;
        private set => SetProperty(ref _isBaseRuntimeOperationActive, value);
    }

    public bool ManagedDistroDetected
    {
        get => _managedDistroDetected;
        private set
        {
            if (SetProperty(ref _managedDistroDetected, value))
            {
                OnPropertyChanged(nameof(BaseRuntimeStatusLabel));
                OnPropertyChanged(nameof(BaseRuntimeStatusBrush));
                OnPropertyChanged(nameof(BaseRuntimeInstallPath));
                OnPropertyChanged(nameof(BaseRuntimeDriveSummary));
                OnPropertyChanged(nameof(CanChooseBaseRuntimeDrive));
                _setupBaseRuntimeCommand.RaiseCanExecuteChanged();
                _uninstallBaseRuntimeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BaseRuntimeStatusLabel => ManagedDistroDetected ? "Ready" : "Not installed";

    public string BaseRuntimeStatusBrush => ManagedDistroDetected ? "#97DF48" : "#D9B36B";

    public DriveChoice? SelectedBaseRuntimeDrive
    {
        get => _selectedBaseRuntimeDrive;
        set
        {
            if (!SetProperty(ref _selectedBaseRuntimeDrive, value))
            {
                return;
            }

            if (value is not null && !ManagedDistroDetected)
            {
                _settings.InstallLocation = value.InstallPath;
            }

            OnPropertyChanged(nameof(BaseRuntimeInstallPath));
            OnPropertyChanged(nameof(BaseRuntimeDriveSummary));
            _setupBaseRuntimeCommand.RaiseCanExecuteChanged();
        }
    }

    public string BaseRuntimeInstallPath =>
        ManagedDistroDetected
            ? _settings.InstallLocation
            : SelectedBaseRuntimeDrive?.InstallPath ?? _settings.InstallLocation;

    public string BaseRuntimeDriveSummary =>
        ManagedDistroDetected
            ? "Current runtime location. Unregister to choose another drive."
            : SelectedBaseRuntimeDrive is null
                ? "No fixed Windows drive is available for the managed distro."
                : "Choose where the fresh runtime will be installed.";

    public bool CanChooseBaseRuntimeDrive => !ManagedDistroDetected && !IsBusy;

    public string WindowsWslStatusLabel => WindowsWslReady ? "Ready" : "Missing / not ready";

    public string WindowsWslStatusBrush => WindowsWslReady ? "#97DF48" : "#D9B36B";

    public string SetupWindowsWslActionText => WindowsWslReady ? "Windows WSL Ready" : "Set Up Windows WSL";

    public bool WindowsWslReady
    {
        get => _windowsWslReady;
        private set
        {
            if (SetProperty(ref _windowsWslReady, value))
            {
                OnPropertyChanged(nameof(WindowsWslStatusLabel));
                OnPropertyChanged(nameof(WindowsWslStatusBrush));
                OnPropertyChanged(nameof(SetupWindowsWslActionText));
                _setupWindowsWslCommand.RaiseCanExecuteChanged();
                _setupBaseRuntimeCommand.RaiseCanExecuteChanged();
            }
        }
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
            _setupWindowsWslCommand.RaiseCanExecuteChanged();
            _setupBaseRuntimeCommand.RaiseCanExecuteChanged();
            _uninstallBaseRuntimeCommand.RaiseCanExecuteChanged();
            _checkForUpdatesCommand.RaiseCanExecuteChanged();
            _openModuleCommand.RaiseCanExecuteChanged();
            _installModuleCommand.RaiseCanExecuteChanged();
            _repairModuleCommand.RaiseCanExecuteChanged();
            _updateModuleCommand.RaiseCanExecuteChanged();
            _openModuleInstallPathCommand.RaiseCanExecuteChanged();
            _openModuleUiCommand.RaiseCanExecuteChanged();
            _runModuleActionCommand.RaiseCanExecuteChanged();
            _runModuleActionGroupCommand.RaiseCanExecuteChanged();
            _uninstallModuleCommand.RaiseCanExecuteChanged();
            _deleteModuleCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanChooseBaseRuntimeDrive));
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

    public bool ShowHomeCheckingState => IsHomePage && !HasLoadedModuleState;

    public bool ShowHomeContent => IsHomePage && HasLoadedModuleState;

    public bool IsBaseRuntimePage => CurrentPageKind == ManagerPageKind.BaseRuntime;

    public bool IsSystemChecksPage => CurrentPageKind == ManagerPageKind.SystemChecks;

    public bool IsLogsPage => CurrentPageKind == ManagerPageKind.Logs;

    public bool IsGuidePage => CurrentPageKind == ManagerPageKind.Guide;

    public bool IsModulePage => CurrentPageKind == ManagerPageKind.Module;

    public bool IsModuleUiPage => CurrentPageKind == ManagerPageKind.ModuleUi;

    public bool IsStandardContentPage => CurrentPageKind != ManagerPageKind.ModuleUi;

    public bool HasSelectedModule => DisplayedModule is not null;

    public bool HasLoadedModuleState
    {
        get => _hasLoadedModuleState;
        private set
        {
            if (SetProperty(ref _hasLoadedModuleState, value))
            {
                OnPropertyChanged(nameof(ShowHomeCheckingState));
                OnPropertyChanged(nameof(ShowHomeContent));
                OnPropertyChanged(nameof(ShowInstalledModulesSection));
                OnPropertyChanged(nameof(ShowAvailableModulesSection));
            }
        }
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
            OnPropertyChanged(nameof(IsBaseRuntimePage));
            OnPropertyChanged(nameof(IsSystemChecksPage));
            OnPropertyChanged(nameof(IsLogsPage));
            OnPropertyChanged(nameof(IsGuidePage));
            OnPropertyChanged(nameof(IsModulePage));
            OnPropertyChanged(nameof(IsModuleUiPage));
            OnPropertyChanged(nameof(IsStandardContentPage));
            OnPropertyChanged(nameof(ShowHomeCheckingState));
            OnPropertyChanged(nameof(ShowHomeContent));
            OnPropertyChanged(nameof(DisplayedModuleContractActions));
            _runModuleActionCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
    }

    public void Dispose()
    {
        _operationCancellation.Cancel();
        _operationCancellation.Dispose();
        _sidebarArtTimer.Stop();
        _sidebarArtTimer.Tick -= OnSidebarArtTimerTick;
        _runtimeMonitorTimer.Stop();
        _runtimeMonitorTimer.Tick -= OnRuntimeMonitorTimerTick;
    }

    public async Task ShutdownAsync()
    {
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        _operationCancellation.Cancel();
        AppendActivity("Manager shutdown requested. Stopping active module lifecycle jobs in the managed WSL distro...");

        try
        {
            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await _workflowService.CancelActiveNymphModuleLifecyclesAsync(
                _settings,
                new Progress<string>(AppendActivity),
                shutdownTimeout.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendActivity($"Shutdown lifecycle cleanup warning: {ex.Message}");
        }
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Refreshing the modular shell...";
        HasLoadedModuleState = false;
        AppendActivity("Refreshing Manager shell.");
        var refreshSucceeded = false;

        try
        {
            await RefreshBaseRuntimeStateAsync().ConfigureAwait(true);
            await RefreshModuleRosterAsync().ConfigureAwait(true);
            await RefreshSystemChecksAsync().ConfigureAwait(true);
            LoadHistoricalLogs();
            StatusMessage = "Manager shell loaded. Refreshing live status...";
            AppendActivity("Manager shell loaded. Refreshing live status.");
            refreshSucceeded = true;
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

        if (refreshSucceeded)
        {
            _ = RefreshRuntimeMonitorInBackgroundAsync();
        }
    }

    private async Task RefreshRuntimeMonitorInBackgroundAsync()
    {
        await Task.Yield();

        StatusMessage = ManagedDistroDetected
            ? "Refreshing module status..."
            : "Refreshing runtime monitor...";

        if (ManagedDistroDetected)
        {
            await RefreshModuleStateInBackgroundAsync().ConfigureAwait(true);
        }
        else if (_allModules.Count > 0)
        {
            HasLoadedModuleState = true;
        }

        StatusMessage = "Refreshing runtime monitor...";
        await RefreshRuntimeMonitorSafelyAsync().ConfigureAwait(true);

        LastRefreshedText = $"Last refreshed {DateTime.Now:HH:mm:ss}";
        StatusMessage = _isRuntimeMonitorAvailable || ManagedDistroDetected
            ? "Manager shell refreshed."
            : "Manager shell refreshed. Runtime is offline.";
        AppendActivity(StatusMessage);

        if (ManagedDistroDetected)
        {
            await CheckForUpdatesOnStartupAsync().ConfigureAwait(true);
        }
    }

    private async Task RefreshRuntimeMonitorSafelyAsync()
    {
        try
        {
            await RefreshRuntimeMonitorAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _isRuntimeMonitorAvailable = false;
            AppendActivity($"Runtime monitor refresh warning: {ex.Message}");
        }
    }

    private async Task RefreshModuleStateInBackgroundAsync()
    {
        await Task.Yield();
        try
        {
            StatusMessage = "Refreshing module status...";
            AppendActivity("Module status refresh started in background.");
            await RefreshModuleStateAsync().ConfigureAwait(true);
            AppendActivity("Module status refresh completed.");
        }
        catch (Exception ex)
        {
            AppendActivity($"Module status refresh warning: {ex.Message}");
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        UpdateSummary = "Checking module registry...";
        AppendActivity("Checking for updates.");

        try
        {
            var updateResults = await _workflowService.CheckNymphModuleRegistryUpdatesAsync(
                _settings,
                _allModules.Where(module => module.IsInstalled).Select(module => module.Id),
                new Progress<string>(AppendActivity),
                CancellationToken.None).ConfigureAwait(true);

            ApplyModuleUpdateResults(updateResults);
            var updateCount = updateResults.Count(result => result.IsUpdateAvailable);
            UpdateSummary = updateCount > 0
                ? $"{updateCount} module update(s) available."
                : $"All installed modules current at {DateTime.Now:HH:mm:ss}.";
            AppendActivity(UpdateSummary);
        }
        catch (Exception ex)
        {
            UpdateSummary = $"Update check needs attention: {ex.Message}";
            AppendActivity($"Update check warning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (_hasRunStartupUpdateCheck || IsBusy || _allModules.All(module => !module.IsInstalled))
        {
            return;
        }

        _hasRunStartupUpdateCheck = true;
        await CheckForUpdatesAsync().ConfigureAwait(true);
    }

    private bool CanSetupBaseRuntime()
    {
        return !IsBusy && WindowsWslReady && (ManagedDistroDetected || SelectedBaseRuntimeDrive is not null);
    }

    private bool CanSetupWindowsWsl()
    {
        return !IsBusy && !WindowsWslReady;
    }

    private bool CanUninstallBaseRuntime()
    {
        return !IsBusy && ManagedDistroDetected;
    }

    private async Task SetupWindowsWslAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsBaseRuntimeOperationActive = true;
        BaseRuntimeOperationLabel = "wsl";
        StatusMessage = "Setting up Windows WSL support...";
        BaseRuntimeProgressText = StatusMessage;
        AppendActivity(StatusMessage);

        try
        {
            await _workflowService.BootstrapWslAsync(
                new Progress<string>(ReportBaseRuntimeProgress),
                CancellationToken.None).ConfigureAwait(true);

            await RefreshSystemChecksAsync().ConfigureAwait(true);
            if (WindowsWslReady)
            {
                StatusMessage = "Windows WSL is ready.";
                BaseRuntimeProgressText = "Windows WSL is ready. Click Install Base Runtime next.";
            }
            else
            {
                StatusMessage = "Windows WSL setup ran.";
                BaseRuntimeProgressText = "Windows WSL setup ran. If Windows asked for a restart, restart Windows, reopen Manager, then install Base Runtime.";
            }

            AppendActivity(BaseRuntimeProgressText);
            LastRefreshedText = $"Last refreshed {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Windows WSL setup needs attention.";
            BaseRuntimeProgressText = ex.Message;
            AppendActivity($"Windows WSL setup warning: {ex.Message}");
        }
        finally
        {
            IsBaseRuntimeOperationActive = false;
            IsBusy = false;
        }
    }

    private async Task SetupBaseRuntimeAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!WindowsWslReady)
        {
            BaseRuntimeOperationLabel = "wsl";
            BaseRuntimeProgressText = "Windows WSL is not ready. Run Set Up Windows WSL first, restart if Windows asks, then install Base Runtime.";
            StatusMessage = "Windows WSL is required before Base Runtime can install.";
            AppendActivity(BaseRuntimeProgressText);
            return;
        }

        IsBusy = true;
        IsBaseRuntimeOperationActive = true;
        BaseRuntimeOperationLabel = ManagedDistroDetected ? "repair" : "install";
        StatusMessage = ManagedDistroDetected
            ? "Repairing base NymphsCore runtime shell..."
            : "Installing base NymphsCore runtime shell...";
        BaseRuntimeProgressText = StatusMessage;
        AppendActivity(StatusMessage);

        try
        {
            ReportBaseRuntimeProgress("Checking for an existing managed WSL runtime...");
            var existingDistroName = await _workflowService.GetExistingManagedDistroNameAsync(CancellationToken.None).ConfigureAwait(true);
            var settings = CreateBaseRuntimeSettings(existingDistroName);

            AppendActivity(settings.RepairExistingDistro
                ? $"Reusing existing {settings.DistroName} distro for base shell repair."
                : $"Creating {settings.DistroName} distro at {settings.InstallLocation}.");
            AppendActivity("Module install choices are intentionally skipped here. Modules are installed later from registry cards.");
            ReportBaseRuntimeProgress(settings.RepairExistingDistro
                ? "Preparing repair of the existing managed WSL runtime..."
                : "Preparing fresh Ubuntu runtime shell install...");

            await _workflowService.ImportBaseDistroAsync(
                settings,
                new Progress<string>(ReportBaseRuntimeProgress),
                CancellationToken.None).ConfigureAwait(true);

            ApplyRuntimeSettings(settings);
            ManagedDistroDetected = true;
            BaseRuntimeActionText = "Repair Base Runtime";
            BaseRuntimeSummary = $"{settings.DistroName} base shell is ready.";
            BaseRuntimeDetail = "No optional modules were installed. Use module cards to install each Nymph individually.";
            BaseRuntimeCardSubtitle = "WSL shell ready";
            BaseRuntimeCardStatus = "Ready";
            StatusMessage = "Base runtime shell ready.";
            BaseRuntimeProgressText = "Ready.";
            AppendActivity(StatusMessage);

            await RefreshSystemChecksAsync().ConfigureAwait(true);
            await RefreshRuntimeMonitorAsync().ConfigureAwait(true);
            await RefreshModuleStateAsync().ConfigureAwait(true);
            LastRefreshedText = $"Last refreshed {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Base runtime setup needs attention.";
            BaseRuntimeSummary = "Base runtime setup failed.";
            BaseRuntimeDetail = ex.Message;
            BaseRuntimeProgressText = ex.Message;
            AppendActivity($"Base runtime setup warning: {ex.Message}");
        }
        finally
        {
            IsBaseRuntimeOperationActive = false;
            IsBusy = false;
        }
    }

    private async Task UninstallBaseRuntimeAsync()
    {
        if (IsBusy || !ManagedDistroDetected)
        {
            return;
        }

        var existingDistroName = await _workflowService.GetExistingManagedDistroNameAsync(CancellationToken.None).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(existingDistroName))
        {
            ManagedDistroDetected = false;
            BaseRuntimeProgressText = "No managed runtime distro was found.";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Unregister the managed WSL runtime?\n\nThis removes the WSL distro '{existingDistroName}' and deletes its runtime folder. Modules installed inside that distro will be removed too.",
            "Unregister WSL Runtime",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            BaseRuntimeProgressText = "Base runtime uninstall cancelled.";
            AppendActivity(BaseRuntimeProgressText);
            return;
        }

        IsBusy = true;
        IsBaseRuntimeOperationActive = true;
        BaseRuntimeOperationLabel = "unregister";
        StatusMessage = $"Unregistering {existingDistroName} WSL runtime...";
        BaseRuntimeProgressText = StatusMessage;
        AppendActivity(StatusMessage);

        try
        {
            var settings = CreateBaseRuntimeSettings(existingDistroName);
            await _workflowService.UninstallBaseRuntimeAsync(
                settings,
                new Progress<string>(ReportBaseRuntimeProgress),
                CancellationToken.None).ConfigureAwait(true);

            ApplyRuntimeSettings(CreateDefaultInstallSettings());
            ManagedDistroDetected = false;
            BaseRuntimeActionText = "Install Base Runtime";
            BaseRuntimeSummary = "No NymphsCore managed WSL shell detected.";
            BaseRuntimeDetail = "Install the base shell first. Modules remain registry-managed and optional.";
            BaseRuntimeCardSubtitle = "Install base shell";
            BaseRuntimeCardStatus = "Not installed";
            BaseRuntimeProgressText = "Base runtime uninstalled.";
            StatusMessage = "Base runtime uninstalled.";
            AppendActivity(StatusMessage);

            await RefreshSystemChecksAsync().ConfigureAwait(true);
            await RefreshRuntimeMonitorAsync().ConfigureAwait(true);
            await RefreshModuleStateAsync().ConfigureAwait(true);
            LastRefreshedText = $"Last refreshed {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Base runtime uninstall needs attention.";
            BaseRuntimeSummary = "Base runtime uninstall failed.";
            BaseRuntimeDetail = ex.Message;
            BaseRuntimeProgressText = ex.Message;
            AppendActivity($"Base runtime uninstall warning: {ex.Message}");
        }
        finally
        {
            IsBaseRuntimeOperationActive = false;
            IsBusy = false;
        }
    }

    private void ReportBaseRuntimeProgress(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalized = message.Trim();
        BaseRuntimeProgressText = normalized;
        AppendActivity(normalized);
    }

    private async Task RefreshBaseRuntimeStateAsync()
    {
        try
        {
            using var runtimeStateTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var existingDistroName = await _workflowService.GetExistingManagedDistroNameAsync(runtimeStateTimeout.Token).ConfigureAwait(true);
            ManagedDistroDetected = !string.IsNullOrWhiteSpace(existingDistroName);
            if (ManagedDistroDetected)
            {
                _settings.DistroName = existingDistroName!;
                _settings.InstallLocation = _workflowService.GetExistingManagedDistroInstallLocation(existingDistroName) ?? _settings.InstallLocation;
                OnPropertyChanged(nameof(BaseRuntimeInstallPath));
                OnPropertyChanged(nameof(BaseRuntimeDriveSummary));
                BaseRuntimeActionText = "Repair Base Runtime";
                BaseRuntimeSummary = $"{existingDistroName} managed WSL shell detected.";
                BaseRuntimeDetail = "Base runtime is present. Modules remain registry-managed and optional.";
                BaseRuntimeCardSubtitle = "WSL shell detected";
                BaseRuntimeCardStatus = "Ready";
                BaseRuntimeProgressText = "Ready.";
            }
            else
            {
                BaseRuntimeActionText = "Install Base Runtime";
                BaseRuntimeSummary = "No NymphsCore managed WSL shell detected.";
                var installTarget = $"Install target: {BaseRuntimeInstallPath}.";
                BaseRuntimeDetail = _workflowService.BaseTarAvailable
                    ? $"Ready to import from {_workflowService.BaseTarPath}. {installTarget}"
                    : $"Ready to bootstrap a fresh Ubuntu base locally. {installTarget}";
                BaseRuntimeCardSubtitle = "Install base shell";
                BaseRuntimeCardStatus = "Not installed";
                BaseRuntimeProgressText = "Ready to install.";
            }
        }
        catch (Exception ex)
        {
            ManagedDistroDetected = false;
            BaseRuntimeActionText = "Install Base Runtime";
            BaseRuntimeSummary = "Base runtime state could not be checked.";
            BaseRuntimeDetail = ex.Message;
            BaseRuntimeCardSubtitle = "Check failed";
            BaseRuntimeCardStatus = "Needs attention";
            BaseRuntimeProgressText = ex.Message;
        }
    }

    private void ApplyModuleUpdateResults(IReadOnlyList<NymphModuleUpdateInfo> updateResults)
    {
        foreach (var result in updateResults)
        {
            var module = _allModules.FirstOrDefault(module => string.Equals(module.Id, result.ModuleId, StringComparison.OrdinalIgnoreCase));
            module?.ApplyUpdateState(result.InstalledVersion, result.RemoteVersion, result.IsUpdateAvailable, result.Detail);
        }

        if (DisplayedModule is not null)
        {
            SetModuleDetailPaneFeedback(DisplayedModule);
        }

        RebuildModuleNavigation();
        OnPropertyChanged(nameof(HomeInstalledModules));
        OnPropertyChanged(nameof(HomeAvailableModules));
        _updateModuleCommand.RaiseCanExecuteChanged();
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
        WindowsWslReady = wslCheck?.Status == CheckState.Pass;
        RuntimePanelDetail = wslCheck?.Details ?? "The shared base runtime check has not returned details yet.";
    }

    private async Task RefreshRuntimeMonitorAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var snapshot = await _workflowService.GetShellRuntimeMonitorAsync(_settings, timeout.Token).ConfigureAwait(true);

        RuntimePanelTitle = snapshot.DistributionLabel;
        RuntimePanelSummary = snapshot.KernelLabel;
        RuntimePanelDetail = snapshot.UptimeLabel;
        RuntimePanelStatusLabel = snapshot.IsAvailable ? "Running" : "Offline";
        RuntimePanelStatusBrush = snapshot.IsAvailable ? "#6FD96C" : "#B74322";
        _isRuntimeMonitorAvailable = snapshot.IsAvailable;
        RuntimeCpuUsageLabel = $"{snapshot.CpuPercent}%";
        RuntimeMemoryUsageLabel = snapshot.MemoryUsageLabel;
        RuntimeDiskUsageLabel = snapshot.WslDiskUsageLabel;
        RuntimeWindowsDiskUsageLabel = snapshot.WindowsDiskUsageLabel;
        RuntimeGpuVramLabel = snapshot.GpuVramLabel;
        RuntimeGpuTempLabel = snapshot.GpuTempLabel;
        RuntimeBrainLlmStateLabel = snapshot.BrainLlmStateLabel;
        RuntimeBrainModelLabel = snapshot.BrainModelLabel;
        RuntimeBrainRemoteModelLabel = snapshot.BrainRemoteModelLabel;
        RuntimeBrainContextLabel = snapshot.BrainContextLabel;
        RuntimeBrainTokensPerSecondLabel = snapshot.BrainTokensPerSecondLabel;
        RuntimeCpuBarWidth = ComputeRuntimeBarWidth(snapshot.CpuPercent);
        RuntimeMemoryBarWidth = ComputeRuntimeBarWidth(snapshot.MemoryPercent);
        RuntimeDiskBarWidth = ComputeRuntimeBarWidth(snapshot.DiskPercent);

        if (ManagedDistroDetected && snapshot.IsAvailable)
        {
            BaseRuntimeCardSubtitle = snapshot.DistributionLabel;
            BaseRuntimeCardStatus = "Ready";
        }
    }

    private async Task RefreshModuleRosterAsync()
    {
        var selectedModuleId = DisplayedModule?.Id ?? SelectedModule?.Id;
        var previousModules = _allModules.ToDictionary(module => module.Id, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<NymphModuleManifestInfo> manifests;

        try
        {
            using var registryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            manifests = await _workflowService.GetNymphModuleRegistryManifestInfosAsync(registryTimeout.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendActivity($"Module registry warning: {ex.Message}");
            if (_allModules.Count == 0)
            {
                InstalledModulesSummary = "Module registry could not be loaded yet.";
                AvailableModulesSummary = "Check network access or the Nymphs registry JSON.";
            }
            else
            {
                RebuildModuleCollections();
                RebuildModuleNavigation();
                HasLoadedModuleState = true;
            }

            return;
        }

        _allModules.Clear();
        var index = 0;
        foreach (var manifest in manifests)
        {
            var module = CreateModuleViewModel(manifest, index++);
            if (previousModules.TryGetValue(module.Id, out var previousModule))
            {
                module.ApplyActionGroupFieldStateFrom(previousModule);
            }

            _allModules.Add(module);
        }

        await PrimeModulePresenceAsync(manifests).ConfigureAwait(true);
        RebuildModuleCollections();
        RebuildModuleNavigation();
        HasLoadedModuleState = true;

        if (CurrentPageKind == ManagerPageKind.Module && !string.IsNullOrWhiteSpace(selectedModuleId))
        {
            var replacement = _allModules.FirstOrDefault(module => string.Equals(module.Id, selectedModuleId, StringComparison.OrdinalIgnoreCase));
            if (replacement is not null)
            {
                ShowModulePage(replacement);
            }
        }
    }

    private NymphModuleViewModel CreateModuleViewModel(NymphModuleManifestInfo manifest, int index)
    {
        var module = new NymphModuleViewModel(
            manifest.Id,
            manifest.Name,
            manifest.ShortName,
            manifest.Category,
            manifest.Kind,
            manifest.Description,
            ResolveManagedInstallPath(manifest.InstallRoot, manifest.Id),
            BuildModuleAccent(manifest.Id, index),
            manifest.Capabilities,
            manifest.ManagerActions,
            manifest.InstallFields,
            manifest.InstallOptionsTitle,
            manifest.ManagerActionGroups,
            manifest.DevCapabilities);

        module.ApplyManifestInfo(manifest);
        return module;
    }

    private async Task RefreshModuleStateAsync()
    {
        foreach (var module in _allModules)
        {
            if (_modulesWithActiveLifecycle.Contains(module.Id))
            {
                continue;
            }

            var snapshot = await RunModuleStatusSnapshotAsync(module).ConfigureAwait(true);
            ApplyModuleSnapshot(module, snapshot);
        }

        RebuildModuleCollections();
        RebuildModuleNavigation();
        HasLoadedModuleState = true;
    }

    private async Task<NymphStatusSnapshot> RunModuleStatusSnapshotAsync(NymphModuleViewModel module)
    {
        try
        {
            using var statusTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var output = await _workflowService.RunNymphModuleActionAsync(
                _settings,
                module.Id,
                "status",
                new Progress<string>(_ => { }),
                statusTimeout.Token).ConfigureAwait(true);

            var snapshot = NymphStatusSnapshot.FromStatusOutput(module.Id, output);
            var markerVersion = await _workflowService.GetInstalledNymphModuleMarkerVersionAsync(
                _settings,
                module.Id,
                statusTimeout.Token).ConfigureAwait(true);
            if (!snapshot.IsInstalled && !string.IsNullOrWhiteSpace(markerVersion))
            {
                return new NymphStatusSnapshot(
                    module.Id,
                    IsInstalled: true,
                    IsRunning: false,
                    Version: markerVersion,
                    State: "installed",
                    Detail: $"{module.Name} is installed, but its status entrypoint reported not installed. The install marker is being trusted while the status script is fixed.",
                    InstallRoot: module.InstallPath,
                    Health: "status-warning",
                    Values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["status_error"] = "status_reported_not_installed",
                    });
            }

            return snapshot;
        }
        catch (OperationCanceledException)
        {
            AppendActivity($"{module.Name} status timed out; keeping Manager responsive.");
            var markerVersion = await _workflowService.GetInstalledNymphModuleMarkerVersionAsync(
                _settings,
                module.Id,
                CancellationToken.None).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(markerVersion))
            {
                return new NymphStatusSnapshot(
                    module.Id,
                    IsInstalled: true,
                    IsRunning: false,
                    Version: markerVersion,
                    State: "installed",
                    Detail: $"{module.Name} is installed, but its status check timed out. Runtime health has not been verified yet.",
                    InstallRoot: module.InstallPath,
                    Health: "status-timeout",
                    Values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["status_error"] = "timeout",
                    });
            }

            return new NymphStatusSnapshot(
                module.Id,
                IsInstalled: false,
                IsRunning: false,
                Version: "not-installed",
                State: "available",
                Detail: $"{module.Name} status check timed out. The module is treated as not installed until it reports cleanly.",
                InstallRoot: module.InstallPath,
                Health: "timeout",
                Values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["status_error"] = "timeout",
                });
        }
        catch (Exception ex)
        {
            AppendActivity($"{module.Name} status warning: {FirstNonEmptyLine(ex.Message)}");
            var markerVersion = await _workflowService.GetInstalledNymphModuleMarkerVersionAsync(
                _settings,
                module.Id,
                CancellationToken.None).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(markerVersion))
            {
                return new NymphStatusSnapshot(
                    module.Id,
                    IsInstalled: true,
                    IsRunning: false,
                    Version: markerVersion,
                    State: "installed",
                    Detail: $"{module.Name} is installed, but its status entrypoint failed. Use // status or // logs for the raw module error.",
                    InstallRoot: module.InstallPath,
                    Health: "status-warning",
                    Values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["status_error"] = ex.Message,
                    });
            }

            if (IsNormalUnavailableModuleStatus(ex.Message))
            {
                return new NymphStatusSnapshot(
                    module.Id,
                    IsInstalled: false,
                    IsRunning: false,
                    Version: "not-installed",
                    State: "available",
                    Detail: $"{module.Name} is available from the registry, but is not installed yet.",
                    InstallRoot: module.InstallPath,
                    Health: "unknown",
                    Values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            return new NymphStatusSnapshot(
                module.Id,
                IsInstalled: false,
                IsRunning: false,
                Version: "not-installed",
                State: "available",
                Detail: $"{module.Name} status entrypoint is not available yet.",
                InstallRoot: module.InstallPath,
                Health: "unknown",
                Values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["status_error"] = ex.Message,
                });
        }
    }

    private void ApplyModuleSnapshot(NymphModuleViewModel module, NymphStatusSnapshot snapshot)
    {
        var modelsReady = snapshot.Get("models_ready");
        var modelDownloadNeeded = snapshot.IsInstalled &&
            string.Equals(modelsReady, "false", StringComparison.OrdinalIgnoreCase);
        var repairNeeded = !snapshot.IsInstalled &&
            (string.Equals(snapshot.State, "repair_needed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(snapshot.Health, "repair-needed", StringComparison.OrdinalIgnoreCase));
        var stateLabel = modelDownloadNeeded
            ? "Model download needed"
            : repairNeeded
                ? "Repair needed"
            : NormalizeModuleStateLabel(snapshot.State, snapshot.IsInstalled, snapshot.IsRunning);
        var statusBrush = snapshot.IsRunning
            ? "#6FD96C"
            : snapshot.IsInstalled
                ? modelDownloadNeeded ? "#D49A2A" : StateNeedsAttention(snapshot.State, snapshot.Health) ? "#B7791F" : "#4CD0C1"
                : repairNeeded ? "#D49A2A" : "#6E745A";

        var secondaryParts = new List<string>();
        var isBrainModule = string.Equals(module.Id, "brain", StringComparison.OrdinalIgnoreCase);
        if (modelDownloadNeeded)
        {
            secondaryParts.Add("Models: download needed");
        }
        else if (repairNeeded)
        {
            secondaryParts.Add("Install: repair needed");
        }

        if (snapshot.IsInstalled &&
            !string.IsNullOrWhiteSpace(snapshot.Health) &&
            !modelDownloadNeeded &&
            !isBrainModule)
        {
            secondaryParts.Add($"Health: {snapshot.Health}");
        }

        var statusError = snapshot.Get("status_error");
        if (snapshot.IsInstalled && !string.IsNullOrWhiteSpace(statusError))
        {
            secondaryParts.Add($"Status warning: {statusError}");
        }

        var runtimePresent = snapshot.Get("runtime_present");
        if (!isBrainModule && string.Equals(runtimePresent, "true", StringComparison.OrdinalIgnoreCase))
        {
            secondaryParts.Add($"Runtime: {runtimePresent}");
        }

        var dataPresent = snapshot.Get("data_present");
        if (!isBrainModule && string.Equals(dataPresent, "true", StringComparison.OrdinalIgnoreCase))
        {
            secondaryParts.Add($"Data: {dataPresent}");
        }

        var url = snapshot.Get("url") ?? snapshot.Get("frontend_url") ?? snapshot.Get("backend_url");
        if (snapshot.IsInstalled && !string.IsNullOrWhiteSpace(url))
        {
            secondaryParts.Add($"URL: {url}");
        }

        if (isBrainModule)
        {
            AddBrainModuleStatusDetails(secondaryParts, snapshot);
        }

        var detail = isBrainModule && snapshot.IsInstalled
            ? "Live Brain status"
            : snapshot.Detail;

        module.ApplyState(
            snapshot.IsInstalled,
            snapshot.IsRunning,
            snapshot.IsInstalled ? ValueOrFallback(snapshot.Version, "unknown") : "Not installed",
            stateLabel,
            statusBrush,
            detail,
            secondaryParts.Count == 0
                ? "Status came from the module-owned status entrypoint."
                : string.Join(Environment.NewLine, secondaryParts));
        if (ShouldLogModuleStatusSnapshot(snapshot))
        {
            AppendActivity(BuildModuleStatusLogLine(module, snapshot));
        }

        RefreshInstalledModuleUiInfo(module);
        _updateModuleCommand.RaiseCanExecuteChanged();
    }

    private static bool IsNormalUnavailableModuleStatus(string? message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("installed=false", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("state=available", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("Module is available from the registry, but is not installed yet.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldLogModuleStatusSnapshot(NymphStatusSnapshot snapshot)
    {
        if (snapshot.IsInstalled || snapshot.IsRunning)
        {
            return true;
        }

        var statusError = snapshot.Get("status_error");
        return !string.IsNullOrWhiteSpace(statusError) &&
               !IsNormalUnavailableModuleStatus(statusError);
    }

    private static string BuildModuleStatusLogLine(NymphModuleViewModel module, NymphStatusSnapshot snapshot)
    {
        var parts = new List<string>
        {
            $"{module.Name} status:",
            $"installed={snapshot.IsInstalled.ToString().ToLowerInvariant()}",
            $"running={snapshot.IsRunning.ToString().ToLowerInvariant()}",
            $"state={ValueOrFallback(snapshot.State, "unknown")}",
            $"health={ValueOrFallback(snapshot.Health, "unknown")}",
            $"version={ValueOrFallback(snapshot.Version, "unknown")}",
        };

        AddStatusValue(parts, "runtime_present", snapshot.Get("runtime_present"));
        AddStatusValue(parts, "env_ready", snapshot.Get("env_ready"));
        AddStatusValue(parts, "models_ready", snapshot.Get("models_ready"));
        AddStatusValue(parts, "recommended_precision", snapshot.Get("recommended_precision"));
        AddStatusValue(parts, "gpu_vram_mb", snapshot.Get("gpu_vram_mb"));
        AddStatusValue(parts, "llm_running", snapshot.Get("llm_running"));
        AddStatusValue(parts, "mcp_running", snapshot.Get("mcp_running"));
        AddStatusValue(parts, "open_webui_running", snapshot.Get("open_webui_running"));
        AddStatusValue(parts, "local_model", snapshot.Get("local_model"));
        AddStatusValue(parts, "remote_model", snapshot.Get("remote_model"));
        AddStatusValue(parts, "openrouter_key", snapshot.Get("openrouter_key"));

        if (!string.IsNullOrWhiteSpace(snapshot.Detail))
        {
            parts.Add($"detail={snapshot.Detail}");
        }

        return string.Join(" ", parts);
    }

    private static void AddBrainModuleStatusDetails(ICollection<string> parts, NymphStatusSnapshot snapshot)
    {
        parts.Add(
            $"LLM: {FormatRunningState(snapshot.Get("llm_running"))}   |   " +
            $"MCP: {FormatRunningState(snapshot.Get("mcp_running"))}   |   " +
            $"WebUI: {FormatRunningState(snapshot.Get("open_webui_running"))}");
        parts.Add($"Local model: {FormatBrainModelValue(snapshot.Get("local_model"))}");
        parts.Add($"Remote model: {FormatBrainModelValue(snapshot.Get("remote_model"))}");
        parts.Add($"OpenRouter key: {FormatOpenRouterKeyState(snapshot.Get("openrouter_key"))}");
    }

    private static string FormatRunningState(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            ? "Running"
            : string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                ? "Stopped"
                : ValueOrFallback(value, "Unknown");
    }

    private static string FormatOpenRouterKeyState(string? value)
    {
        return string.Equals(value, "saved", StringComparison.OrdinalIgnoreCase)
            ? "Saved"
            : string.Equals(value, "not_set", StringComparison.OrdinalIgnoreCase)
                ? "Not set"
                : ValueOrFallback(value, "Unknown");
    }

    private static string FormatBrainModelValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
            ? "None"
            : value.Trim();
    }

    private static string FirstNonEmptyLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unknown status error";
        }

        return message
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? message.Trim();
    }

    private static void AddStatusValue(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{key}={value.Trim()}");
        }
    }

    private void RefreshInstalledModuleUiInfo(NymphModuleViewModel module)
    {
        if (!module.IsInstalled)
        {
            module.ApplyInstalledModuleUi(null);
            if (DisplayedModule is not null && string.Equals(DisplayedModule.Id, module.Id, StringComparison.OrdinalIgnoreCase))
            {
                OnPropertyChanged(nameof(ShowModuleUiAction));
                OnPropertyChanged(nameof(DisplayedModuleContractActions));
                OnPropertyChanged(nameof(ShowInstalledModuleActionGroups));
                OnPropertyChanged(nameof(DisplayedModuleActionGroups));
                _openModuleUiCommand.RaiseCanExecuteChanged();
                _runModuleActionGroupCommand.RaiseCanExecuteChanged();
            }

            return;
        }

        try
        {
            module.ApplyInstalledModuleUi(_workflowService.GetCachedInstalledNymphModuleUiInfo(_settings, module.Id));
            var controls = _workflowService.GetInstalledNymphModuleControls(_settings, module.Id);
            if (controls is not null)
            {
                module.ApplyInstalledModuleControls(controls.Value.ManagerActions, controls.Value.ManagerActionGroups);
            }
        }
        catch (Exception ex)
        {
            module.ApplyInstalledModuleUi(null);
            AppendActivity($"{module.Name} module controls warning: {ex.Message}");
        }

        if (DisplayedModule is not null && string.Equals(DisplayedModule.Id, module.Id, StringComparison.OrdinalIgnoreCase))
        {
            OnPropertyChanged(nameof(ShowModuleUiAction));
            OnPropertyChanged(nameof(DisplayedModuleContractActions));
            OnPropertyChanged(nameof(ShowInstalledModuleActionGroups));
            OnPropertyChanged(nameof(DisplayedModuleActionGroups));
            _openModuleUiCommand.RaiseCanExecuteChanged();
            _runModuleActionGroupCommand.RaiseCanExecuteChanged();
            SetModuleDetailPaneFeedback(module);
        }
    }

    private async Task PrimeModulePresenceAsync(IReadOnlyList<NymphModuleManifestInfo> manifests)
    {
        IReadOnlyDictionary<string, NymphModuleMarkerProbe> markerProbes;
        var retryMarkerScan = false;
        try
        {
            using var markerTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            markerProbes = ManagedDistroDetected
                ? await _workflowService.GetInstalledNymphModuleMarkerProbesAsync(
                    _settings,
                    manifests,
                    markerTimeout.Token).ConfigureAwait(true)
                : new Dictionary<string, NymphModuleMarkerProbe>(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException ex)
        {
            AppendActivity($"Fast module marker scan warning: {FirstNonEmptyLine(ex.Message)}");
            markerProbes = new Dictionary<string, NymphModuleMarkerProbe>(StringComparer.OrdinalIgnoreCase);
            retryMarkerScan = ManagedDistroDetected;
        }
        catch (Exception ex)
        {
            AppendActivity($"Fast module marker scan warning: {FirstNonEmptyLine(ex.Message)}");
            markerProbes = new Dictionary<string, NymphModuleMarkerProbe>(StringComparer.OrdinalIgnoreCase);
        }

        var markerCount = markerProbes.Values.Count(probe => probe.MarkerPresent);
        var repairCount = markerProbes.Values.Count(probe => probe.RepairCandidatePresent);
        AppendActivity($"Fast module marker scan found {markerCount} installed marker(s), {repairCount} repair candidate(s).");

        ApplyModuleMarkerProbes(markerProbes, markMissingAsAvailable: true);

        if (retryMarkerScan)
        {
            _ = RetryModuleMarkerScanInBackgroundAsync(manifests);
        }
    }

    private async Task RetryModuleMarkerScanInBackgroundAsync(IReadOnlyList<NymphModuleManifestInfo> manifests)
    {
        await Task.Yield();

        try
        {
            using var markerTimeout = CancellationTokenSource.CreateLinkedTokenSource(_operationCancellation.Token);
            markerTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var markerProbes = await _workflowService.GetInstalledNymphModuleMarkerProbesAsync(
                _settings,
                manifests,
                markerTimeout.Token).ConfigureAwait(true);

            var markerCount = markerProbes.Values.Count(probe => probe.MarkerPresent);
            var repairCount = markerProbes.Values.Count(probe => probe.RepairCandidatePresent);
            AppendActivity($"Deferred module marker scan found {markerCount} installed marker(s), {repairCount} repair candidate(s).");

            if (ApplyModuleMarkerProbes(markerProbes, markMissingAsAvailable: false))
            {
                RebuildModuleCollections();
                RebuildModuleNavigation();

                if (DisplayedModule is not null)
                {
                    RefreshDisplayedModuleDetails(DisplayedModule);
                }
            }
        }
        catch (OperationCanceledException) when (_operationCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AppendActivity($"Deferred module marker scan warning: {FirstNonEmptyLine(ex.Message)}");
        }
    }

    private bool ApplyModuleMarkerProbes(
        IReadOnlyDictionary<string, NymphModuleMarkerProbe> markerProbes,
        bool markMissingAsAvailable)
    {
        var changed = false;

        foreach (var module in _allModules)
        {
            var wasInstalled = module.IsInstalled;
            var previousState = module.StateLabel;

            if (markerProbes.TryGetValue(module.Id, out var probe) && probe.MarkerPresent)
            {
                module.ApplyState(
                    isInstalled: true,
                    isRunning: false,
                    versionLabel: ValueOrFallback(probe.Version, "unknown"),
                    stateLabel: "Installed",
                    statusBrush: "#4CD0C1",
                    detail: $"{module.Name} is installed. Runtime health will refresh in the background.",
                    secondaryDetail: $"Startup detection used the standard module marker: {probe.InstallRoot}/.nymph-module-version");
                RefreshInstalledModuleUiInfo(module);
                changed = changed || wasInstalled != module.IsInstalled || !string.Equals(previousState, module.StateLabel, StringComparison.Ordinal);
                continue;
            }

            if (markerProbes.TryGetValue(module.Id, out probe) && probe.RepairCandidatePresent)
            {
                module.ApplyState(
                    isInstalled: false,
                    isRunning: false,
                    versionLabel: "Not installed",
                    stateLabel: "Repair needed",
                    statusBrush: "#D49A2A",
                    detail: $"{module.Name} has files in the expected install folder, but the standard module marker is missing.",
                    secondaryDetail: "Use Repair Module to refresh module-owned scripts and write the install marker.");
                changed = changed || wasInstalled != module.IsInstalled || !string.Equals(previousState, module.StateLabel, StringComparison.Ordinal);
                continue;
            }

            if (!markMissingAsAvailable)
            {
                continue;
            }

            module.ApplyState(
                isInstalled: false,
                isRunning: false,
                versionLabel: "Not installed",
                stateLabel: "Available",
                statusBrush: "#6E745A",
                detail: $"{module.Name} is available from the registry, but is not installed yet.",
                secondaryDetail: "Install state came from the fast standard module marker scan.");
            changed = changed || wasInstalled != module.IsInstalled || !string.Equals(previousState, module.StateLabel, StringComparison.Ordinal);
        }

        return changed;
    }

    private string GetInstalledModuleVersionLabel(string moduleId, bool isInstalled)
    {
        if (!isInstalled)
        {
            return "Not installed";
        }

        try
        {
            return _workflowService.GetInstalledNymphModuleVersion(_settings, moduleId)
                ?? "Manifest not detected";
        }
        catch (Exception ex)
        {
            AppendActivity($"{moduleId} manifest version warning: {ex.Message}");
            return "Manifest read failed";
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
        var selectedModuleId = DisplayedModule?.Id ?? SelectedModule?.Id;
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

        if (CurrentPageKind == ManagerPageKind.Module || CurrentPageKind == ManagerPageKind.ModuleUi)
        {
            var replacement = _allModules.FirstOrDefault(module => string.Equals(module.Id, selectedModuleId, StringComparison.OrdinalIgnoreCase));
            if (replacement is not null)
            {
                if (CurrentPageKind == ManagerPageKind.ModuleUi && replacement.HasInstalledModuleUi)
                {
                    RefreshDisplayedModuleUi(replacement);
                }
                else
                {
                    ShowModulePage(replacement);
                }
            }
            else
            {
                SelectPrimaryPage(ManagerPageKind.Home);
            }
        }
    }

    private void RefreshDisplayedModuleUi(NymphModuleViewModel module)
    {
        var uiInfo = module.InstalledModuleUiInfo;
        if (uiInfo is null)
        {
            ShowModulePage(module);
            return;
        }

        SelectedModule = module;
        DisplayedModule = module;
        CurrentPageTitle = module.Name;
        CurrentPageSubtitle = module.Description;
        ModuleUiTitle = uiInfo.Title;
        ModuleUiStatus = $"Loaded from installed module: {uiInfo.Entrypoint}";
        ModuleUiSource = uiInfo.WindowsPath;
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
                    foreach (var line in ReadRecentLogLines(latestLog, 200))
                    {
                        if (IsNoisyUnavailableModuleLogLine(line))
                        {
                            continue;
                        }

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

        UnifiedLogText = string.Join(Environment.NewLine, mergedLines);

        RecentLogLines.Clear();
        foreach (var line in mergedLines.TakeLast(8))
        {
            RecentLogLines.Add(line);
        }
    }

    private static IReadOnlyList<string> ReadRecentLogLines(string path, int maxLines)
    {
        const int maxBytesToRead = 256 * 1024;

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var start = Math.Max(0, stream.Length - maxBytesToRead);
        stream.Seek(start, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (start > 0 && lines.Count > 0)
        {
            lines.RemoveAt(0);
        }

        return lines.TakeLast(maxLines).ToList();
    }

    private void AppendActivity(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}";
        ActivityLines.Add(line);
        PersistActivityLine(line);

        while (ActivityLines.Count > 200)
        {
            ActivityLines.RemoveAt(0);
        }

        LoadHistoricalLogs();
    }

    private void PersistActivityLine(string line)
    {
        try
        {
            Directory.CreateDirectory(_workflowService.LogFolderPath);
            File.AppendAllText(
                Path.Combine(_workflowService.LogFolderPath, "manager-app.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] shell {line}{Environment.NewLine}");
        }
        catch
        {
            // The UI log should keep working even if the file is temporarily locked.
        }
    }

    private void OpenModule(NymphModuleViewModel? module)
    {
        if (module is null)
        {
            return;
        }

        ShowModulePage(module);
    }

    private void OpenModuleUi(NymphModuleViewModel? module)
    {
        if (module is null || !module.HasInstalledModuleUi)
        {
            return;
        }

        var uiInfo = module.InstalledModuleUiInfo;
        if (uiInfo is null)
        {
            module.ApplyInstalledModuleUi(null);
            RefreshDisplayedModuleActionState();
            SetModuleActionFeedback(
                $"{module.Name}: module UI unavailable",
                "The installed module does not expose a valid local Manager UI file.");
            return;
        }

        SelectedNavigationItem = null;
        SelectedModule = module;
        DisplayedModule = module;
        CurrentPageTitle = module.Name;
        CurrentPageSubtitle = module.Description;
        ModuleUiTitle = uiInfo.Title;
        ModuleUiStatus = $"Loaded from installed module: {uiInfo.Entrypoint}";
        CurrentPageKind = ManagerPageKind.ModuleUi;
        ModuleUiSource = uiInfo.WindowsPath;
        AppendActivity($"{module.Name} module UI opened.");
    }

    public bool HandleModuleUiNavigation(Uri? uri)
    {
        if (uri is null ||
            !string.Equals(uri.Scheme, "nymphs-module-action", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _ = RunModuleUiActionAsync(uri);
        return true;
    }

    private async Task RunModuleUiActionAsync(Uri uri)
    {
        var module = DisplayedModule;
        if (module is null || !module.IsInstalled || IsBusy)
        {
            return;
        }

        var action = ResolveModuleUiAction(uri);
        if (string.IsNullOrWhiteSpace(action) ||
            !module.Capabilities.Any(capability => string.Equals(capability, action, StringComparison.OrdinalIgnoreCase)))
        {
            ModuleUiStatus = $"Unsupported module UI action: {action}";
            return;
        }

        var args = ResolveModuleUiActionArguments(uri).ToList();
        if (args.Any(argument => string.Equals(argument, "--hf_token", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(argument, "--hf-token", StringComparison.OrdinalIgnoreCase)))
        {
            ApplyModuleHuggingFaceTokenArgument(args);
        }

        IsBusy = true;
        StatusMessage = $"Running {module.Name} {action}...";
        ModuleUiStatus = args.Count == 0
            ? $"Running {action}..."
            : $"Running {action} {string.Join(" ", args)}...";
        AppendActivity($"{module.Name} {action} started from module UI.");
        var liveLines = new List<string>();
        SetModuleActionFeedback(
            $"{module.Name}: {action} in progress",
            "Waiting for module output...");
        SelectPrimaryPage(ManagerPageKind.Logs);

        try
        {
            var liveProgress = new Progress<string>(line =>
            {
                AppendActivity(line);
                AppendModuleLiveLine(module, action, line, liveLines);
                ModuleUiStatus = BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, liveLines));
            });
            var output = await _workflowService.RunNymphModuleActionAsync(
                _settings,
                module.Id,
                action,
                args,
                liveProgress,
                CancellationToken.None).ConfigureAwait(true);

            AppendModuleActionOutput(module, action, output);
            ModuleUiStatus = BuildModuleActionFeedbackDetail(output);
            SetModuleActionFeedback(
                $"{module.Name}: {action} finished",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, liveLines.Append(output))));
            if (action is not "logs" and not "open")
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
            }

            StatusMessage = $"{module.Name} {action} finished.";
        }
        catch (Exception ex)
        {
            ModuleUiStatus = ex.Message;
            StatusMessage = $"{module.Name} {action} needs attention.";
            AppendActivity($"{module.Name} module UI action warning: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string ResolveModuleUiAction(Uri uri)
    {
        var action = string.IsNullOrWhiteSpace(uri.Host)
            ? uri.AbsolutePath.Trim('/')
            : uri.Host;

        action = action.Trim().ToLowerInvariant();
        return Regex.IsMatch(action, "^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)
            ? action
            : string.Empty;
    }

    private static IReadOnlyList<string> ResolveModuleUiActionArguments(Uri uri)
    {
        var args = new List<string>();
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return args;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).Trim().ToLowerInvariant();
            if (!Regex.IsMatch(key, "^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant))
            {
                continue;
            }

            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]).Trim() : string.Empty;
            var isHuggingFaceToken = string.Equals(key, "hf_token", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "hf-token", StringComparison.OrdinalIgnoreCase);
            if ((!isHuggingFaceToken && string.IsNullOrWhiteSpace(value)) ||
                value.Any(char.IsControl) ||
                value.Length > 256)
            {
                continue;
            }

            args.Add($"--{key}");
            args.Add(value);
        }

        return args;
    }

    private void ApplyModuleHuggingFaceTokenArgument(IReadOnlyList<string> args)
    {
        var token = GetModuleActionArgumentValue(args, "--hf_token") ??
            GetModuleActionArgumentValue(args, "--hf-token");
        if (token is null)
        {
            return;
        }

        _settings.HuggingFaceToken = token.Trim();
        var existing = _sharedSecretsService.Load();
        existing.HuggingFaceToken = _settings.HuggingFaceToken;
        _sharedSecretsService.Save(existing);

        AppendActivity(string.IsNullOrWhiteSpace(_settings.HuggingFaceToken)
            ? "Hugging Face token cleared for model downloads."
            : "Hugging Face token saved for model downloads.");
    }

    private static string? GetModuleActionArgumentValue(IReadOnlyList<string> args, string key)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private void InstallModule(NymphModuleViewModel? module)
    {
        _ = InstallModuleAsync(module);
    }

    private void RepairModule(NymphModuleViewModel? module)
    {
        _ = RepairModuleAsync(module);
    }

    private void UpdateModule(NymphModuleViewModel? module)
    {
        _ = UpdateModuleAsync(module);
    }

    private async Task InstallModuleAsync(NymphModuleViewModel? module)
    {
        if (module is null || module.IsInstalled || IsBusy)
        {
            return;
        }

        var installEnvironment = BuildInstallFieldEnvironment(module);
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
        _modulesWithActiveLifecycle.Add(module.Id);
        StatusMessage = $"Installing {module.Name} from the Nymphs registry...";
        ShowModuleLogs = false;
        var installLines = new List<string>();
        ApplyModuleLifecycleState(
            module,
            "Installing",
            "#D9B36B",
            "Installing",
            $"{module.Name} install is running inside the managed WSL distro.",
            "Status refresh is paused for this module until the lifecycle action finishes.");
        SetModuleActionFeedback(
            $"{module.Name}: installing",
            BuildInstallFieldFeedback(module, installEnvironment));

        try
        {
            await _workflowService.RunNymphModuleInstallFromRegistryAsync(
                _settings,
                module.Id,
                installEnvironment,
                CreateModuleLiveProgress(module, "install", installLines),
                _operationCancellation.Token).ConfigureAwait(true);
            StatusMessage = $"{module.Name} installed.";
            SetModuleActionFeedback(
                $"{module.Name}: install finished",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, installLines)));

            AppendActivity($"{module.Name} install completed.");
            var installedVersion = ExtractInstalledModuleVersion(installLines);
            ApplyImmediateModuleInstallResult(module, isInstalled: true, "Install completed. Live status verification will refresh next.", installedVersion);
            ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
            _modulesWithActiveLifecycle.Remove(module.Id);
            try
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
                ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
            }
            catch (Exception refreshException)
            {
                AppendActivity($"{module.Name} installed, but state refresh needs attention: {refreshException.Message}");
                SetModuleActionFeedback(
                    $"{module.Name}: install finished",
                    $"{BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, installLines))}\n\nState refresh warning: {refreshException.Message}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} install needs attention.";
            AppendActivity($"{module.Name} install warning: {ex.Message}");
            SetModuleActionFeedback(
                $"{module.Name}: install needs attention",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, installLines.Append(ex.Message))));
        }
        finally
        {
            _modulesWithActiveLifecycle.Remove(module.Id);
            IsBusy = false;
        }
    }

    private async Task RepairModuleAsync(NymphModuleViewModel? module)
    {
        if (module is null || !module.CanRepair || IsBusy)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"Repair {module.Name} from the Nymphs registry?\n\nThe manager will fetch the trusted module repo again and rerun its install script inside the managed WSL distro. Module-owned installers should preserve declared outputs, logs, and model caches by default.",
            "Repair Module",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            AppendActivity($"{module.Name} repair cancelled.");
            return;
        }

        IsBusy = true;
        _modulesWithActiveLifecycle.Add(module.Id);
        StatusMessage = $"Repairing {module.Name} from the Nymphs registry...";
        ShowModuleLogs = false;
        var repairLines = new List<string>();
        ApplyModuleLifecycleState(
            module,
            "Repairing",
            "#D9B36B",
            module.IsInstalled ? module.VersionLabel : "Repairing",
            $"{module.Name} repair is running inside the managed WSL distro.",
            "Status refresh is paused for this module until the lifecycle action finishes.");
        SetModuleActionFeedback(
            $"{module.Name}: repairing",
            "Fetching the module registry entry and rerunning the module install flow...");

        try
        {
            await _workflowService.RunNymphModuleInstallFromRegistryAsync(
                _settings,
                module.Id,
                CreateModuleLiveProgress(module, "repair", repairLines),
                _operationCancellation.Token).ConfigureAwait(true);
            StatusMessage = $"{module.Name} repair finished.";
            SetModuleActionFeedback(
                $"{module.Name}: repair finished",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, repairLines)));

            AppendActivity($"{module.Name} repair completed.");
            var installedVersion = ExtractInstalledModuleVersion(repairLines) ?? module.RemoteVersionLabel;
            ApplyImmediateModuleInstallResult(module, isInstalled: true, "Repair completed. Live status verification will refresh next.", installedVersion);
            ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
            _modulesWithActiveLifecycle.Remove(module.Id);

            try
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
                ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
            }
            catch (Exception refreshException)
            {
                AppendActivity($"{module.Name} repaired, but state refresh needs attention: {refreshException.Message}");
                SetModuleActionFeedback(
                    $"{module.Name}: repair finished",
                    $"{BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, repairLines))}\n\nState refresh warning: {refreshException.Message}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} repair needs attention.";
            AppendActivity($"{module.Name} repair warning: {ex.Message}");
            SetModuleActionFeedback(
                $"{module.Name}: repair needs attention",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, repairLines.Append(ex.Message))));
        }
        finally
        {
            _modulesWithActiveLifecycle.Remove(module.Id);
            IsBusy = false;
        }
    }

    private async Task UpdateModuleAsync(NymphModuleViewModel? module)
    {
        if (module is null || !module.CanUpdate || IsBusy)
        {
            return;
        }

        var remoteVersion = string.IsNullOrWhiteSpace(module.RemoteVersionLabel)
            ? "the latest registry version"
            : module.RemoteVersionLabel;
        var confirmation = MessageBox.Show(
            $"Update {module.Name} to {remoteVersion} from the Nymphs registry?\n\nThe manager will fetch the module repo again and run the module update entrypoint when available.",
            "Update Module",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            AppendActivity($"{module.Name} update cancelled.");
            return;
        }

        IsBusy = true;
        _modulesWithActiveLifecycle.Add(module.Id);
        StatusMessage = $"Updating {module.Name} from the Nymphs registry...";
        ShowModuleLogs = false;
        var updateLines = new List<string>();
        ApplyModuleLifecycleState(
            module,
            "Updating",
            "#D9B36B",
            module.VersionLabel,
            $"{module.Name} update is running inside the managed WSL distro.",
            "Status refresh is paused for this module until the lifecycle action finishes.");
        SetModuleActionFeedback(
            $"{module.Name}: updating",
            "Fetching the module registry entry and running the module update flow...");

        try
        {
            await _workflowService.RunNymphModuleUpdateFromRegistryAsync(
                _settings,
                module.Id,
                CreateModuleLiveProgress(module, "update", updateLines),
                _operationCancellation.Token).ConfigureAwait(true);
            StatusMessage = $"{module.Name} updated.";
            UpdateSummary = $"{module.Name} updated from the registry.";
            SetModuleActionFeedback(
                $"{module.Name}: update finished",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, updateLines)));

            AppendActivity($"{module.Name} update completed.");
            var installedVersion = ExtractInstalledModuleVersion(updateLines) ?? module.RemoteVersionLabel;
            ApplyImmediateModuleInstallResult(module, isInstalled: true, "Update completed. Live status verification will refresh next.", installedVersion);
            ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
            _modulesWithActiveLifecycle.Remove(module.Id);

            try
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
                ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);

                var updateResults = await _workflowService.CheckNymphModuleRegistryUpdatesAsync(
                    _settings,
                    _allModules.Where(candidate => candidate.IsInstalled).Select(candidate => candidate.Id),
                    new Progress<string>(AppendActivity),
                    CancellationToken.None).ConfigureAwait(true);

                ApplyModuleUpdateResults(updateResults);
                ClearModuleUpdateAfterSuccessfulInstall(module, installedVersion);
                SetModuleActionFeedback(
                    $"{module.Name}: update finished",
                    $"{BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, updateLines))}\n\nModule files and manager wrappers were refreshed from the registry. Try // start again.");
            }
            catch (Exception refreshException)
            {
                AppendActivity($"{module.Name} updated, but follow-up state refresh needs attention: {refreshException.Message}");
                SetModuleActionFeedback(
                    $"{module.Name}: update finished",
                    $"{BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, updateLines))}\n\nState refresh warning: {refreshException.Message}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} update needs attention.";
            AppendActivity($"{module.Name} update warning: {ex.Message}");
            SetModuleActionFeedback(
                $"{module.Name}: update needs attention",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, updateLines.Append(ex.Message))));
        }
        finally
        {
            _modulesWithActiveLifecycle.Remove(module.Id);
            IsBusy = false;
        }
    }

    private void ShowModulePage(NymphModuleViewModel module)
    {
        SelectedNavigationItem = null;
        CurrentPageKind = ManagerPageKind.Module;
        SelectedModule = module;
        DisplayedModule = module;
        CurrentPageTitle = module.Name;
        CurrentPageSubtitle = module.Description;
        ModuleUiSource = string.Empty;
        ShowModuleLogs = false;
        ModuleLogsTitle = $"{module.Name} module logs";
        ModuleLogsDetail = "Click // logs to load this module's recent logs.";
        SetModuleDetailPaneFeedback(module);
        _ = LoadModuleManifestInfoAsync(module);
    }

    private async Task LoadModuleManifestInfoAsync(NymphModuleViewModel module)
    {
        try
        {
            var manifest = await _workflowService.GetNymphModuleManifestInfoAsync(
                module.Id,
                CancellationToken.None).ConfigureAwait(true);
            if (manifest is null || DisplayedModule?.Id != module.Id)
            {
                return;
            }

            module.ApplyManifestInfo(manifest);
            _updateModuleCommand.RaiseCanExecuteChanged();
            CurrentPageSubtitle = module.Detail;
            SetModuleDetailPaneFeedback(module);
        }
        catch (Exception ex)
        {
            AppendActivity($"{module.Name} manifest info warning: {ex.Message}");
        }
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

    private void OpenModelCache()
    {
        try
        {
            var cachePath = BuildManagedDistroPath("home", _settings.LinuxUser, "NymphsData", "cache", "huggingface");
            Directory.CreateDirectory(cachePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = cachePath,
                UseShellExecute = true,
            });
            AppendActivity("Model cache opened.");
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open model cache: {ex.Message}");
        }
    }

    private void OpenModuleSource(NymphModuleViewModel? module)
    {
        if (module is null || !module.HasRepositoryUrl)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = module.RepositoryUrl,
                UseShellExecute = true,
            });
            AppendActivity($"{module.Name} GitHub page opened.");
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open {module.Name} GitHub page: {ex.Message}");
        }
    }

    private void OpenModuleActionLink(NymphModuleActionLinkInfo? link)
    {
        if (link is null || string.IsNullOrWhiteSpace(link.Url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link.Url,
                UseShellExecute = true,
            });
            AppendActivity($"Module source link opened: {link.Label}.");
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open module source link: {ex.Message}");
        }
    }

    private void ClearModuleActionSecret(NymphModuleActionFieldInfo? field)
    {
        if (field is null || !field.IsSecret)
        {
            return;
        }

        var secretKind = ResolveSharedSecretKind(field);
        if (string.IsNullOrWhiteSpace(secretKind))
        {
            return;
        }

        SetSharedSecretValue(secretKind, string.Empty);
        field.SecretValue = string.Empty;
        RefreshDisplayedActionGroupSecretState();
        AppendActivity($"{field.Label} cleared.");
    }

    private void ApplyModuleActionSecret(NymphModuleActionFieldInfo? field)
    {
        if (field is null || !field.IsSecret)
        {
            return;
        }

        var secretKind = ResolveSharedSecretKind(field);
        if (string.IsNullOrWhiteSpace(secretKind))
        {
            return;
        }

        var enteredSecret = field.SecretValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(enteredSecret))
        {
            AppendActivity(field.HasSavedSecret
                ? $"{field.Label} is already saved."
                : $"Enter a {field.Label} before applying.");
            return;
        }

        SetSharedSecretValue(secretKind, enteredSecret);
        field.SecretValue = string.Empty;
        RefreshDisplayedActionGroupSecretState();
        AppendActivity($"{field.Label} saved.");
    }

    private void OpenTextInNotepad(string name, string text)
    {
        try
        {
            var safeName = Regex.Replace(name, @"[^A-Za-z0-9_.-]+", "-");
            var tempPath = Path.Combine(Path.GetTempPath(), $"nymphs-{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(tempPath, text, Encoding.UTF8);
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{tempPath}\"",
                UseShellExecute = true,
            });
            AppendActivity($"Action output opened in Notepad: {tempPath}");
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open action output in Notepad: {ex.Message}");
        }
    }

    private bool CanRunSelectedModuleAction(NymphModuleActionInfo? actionInfo)
    {
        if (DisplayedModule is null || actionInfo is null || string.IsNullOrWhiteSpace(actionInfo.ActionName))
        {
            return false;
        }

        var normalizedAction = actionInfo.ActionName.Trim().ToLowerInvariant();
        if (normalizedAction == CloseModuleUiActionName)
        {
            return CurrentPageKind == ManagerPageKind.ModuleUi &&
                   DisplayedModule.IsInstalled;
        }

        if (IsBusy && normalizedAction is not "stop" and not "logs")
        {
            return false;
        }

        if (!DisplayedModule.Capabilities.Any(capability => string.Equals(capability, normalizedAction, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return normalizedAction switch
        {
            "install" => false,
            "uninstall" => false,
            _ => DisplayedModule.IsInstalled,
        };
    }

    private bool CanRunSelectedModuleActionGroup(NymphModuleActionGroupInfo? actionGroup)
    {
        if (DisplayedModule is null || actionGroup is null || string.IsNullOrWhiteSpace(actionGroup.EntryPoint))
        {
            return false;
        }

        var normalizedAction = actionGroup.EntryPoint.Trim().ToLowerInvariant();
        if (IsBusy && normalizedAction is not "stop" and not "logs")
        {
            return false;
        }

        return DisplayedModule.IsInstalled &&
               DisplayedModule.Capabilities.Any(capability => string.Equals(capability, normalizedAction, StringComparison.OrdinalIgnoreCase));
    }

    private void RunSelectedModuleAction(NymphModuleActionInfo? action)
    {
        _ = RunSelectedModuleActionAsync(action);
    }

    private void RunSelectedModuleActionGroup(NymphModuleActionGroupInfo? actionGroup)
    {
        _ = RunSelectedModuleActionGroupAsync(actionGroup);
    }

    private async Task RunSelectedModuleActionGroupAsync(NymphModuleActionGroupInfo? actionGroup)
    {
        var module = DisplayedModule;
        if (module is null || actionGroup is null || string.IsNullOrWhiteSpace(actionGroup.EntryPoint))
        {
            return;
        }

        var normalizedAction = actionGroup.EntryPoint.Trim().ToLowerInvariant();
        if (!module.IsInstalled || (IsBusy && normalizedAction is not "stop" and not "logs"))
        {
            return;
        }

        var actionLabel = string.IsNullOrWhiteSpace(actionGroup.SubmitLabel)
            ? actionGroup.Title
            : actionGroup.SubmitLabel;
        var resultMode = string.IsNullOrWhiteSpace(actionGroup.ResultMode)
            ? "show_logs"
            : actionGroup.ResultMode.Trim().ToLowerInvariant();
        var (args, environment) = BuildActionGroupInvocation(actionGroup);
        var ownsBusyState = !IsBusy;
        if (ownsBusyState)
        {
            IsBusy = true;
        }

        StatusMessage = $"Running {module.Name} {actionLabel}...";
        ShowModuleLogs = false;
        ClearStickyModuleActionFeedback();
        SetModuleActionFeedback(
            $"{module.Name}: {actionLabel} started",
            "Command sent to the managed WSL distro. Waiting for module output...");
        if ((resultMode is "show_logs" or "logs") && normalizedAction is "logs")
        {
            SelectPrimaryPage(ManagerPageKind.Logs);
        }

        var liveLines = new List<string>();
        try
        {
            var liveProgress = new Progress<string>(line =>
            {
                AppendActivity(line);
                AppendModuleLiveLine(module, normalizedAction, line, liveLines);
                SetModuleActionFeedback(
                    $"{module.Name}: {actionLabel} running",
                    BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, liveLines)));
            });
            var output = await _workflowService.RunNymphModuleActionAsync(
                _settings,
                module.Id,
                normalizedAction,
                args,
                environment,
                liveProgress,
                CancellationToken.None).ConfigureAwait(true);

            AppendModuleActionOutput(module, normalizedAction, output);
            var successDetail = BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, liveLines.Append(output)));

            if (normalizedAction is not "logs" and not "open")
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
            }

            SetStickyModuleActionFeedback(
                module,
                BuildModuleActionSuccessTitle(module, actionLabel, normalizedAction),
                BuildModuleActionSuccessDetail(normalizedAction, successDetail));
            StatusMessage = BuildModuleActionSuccessStatus(module, actionLabel, normalizedAction);
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} {actionLabel} needs attention.";
            SetModuleActionFeedback(
                $"{module.Name}: {actionLabel} failed",
                BuildModuleActionFeedbackDetail(ex.Message));
            AppendActivity($"{module.Name} action group warning: {ex.Message}");
        }
        finally
        {
            if (ownsBusyState)
            {
                IsBusy = false;
            }
            else if (string.Equals(normalizedAction, "stop", StringComparison.OrdinalIgnoreCase))
            {
                IsBusy = false;
            }
        }
    }

    private (IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> Environment) BuildActionGroupInvocation(NymphModuleActionGroupInfo actionGroup)
    {
        var args = new List<string>();
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in actionGroup.Fields)
        {
            if (field.IsSecret)
            {
                ApplySecretFieldToInvocation(field, environment);
                continue;
            }

            if (string.IsNullOrWhiteSpace(field.ArgumentName))
            {
                continue;
            }

            var value = field.SelectedValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            args.Add(field.ArgumentName.Trim());
            args.Add(value);
        }

        return (args, environment);
    }

    private IReadOnlyDictionary<string, string?> BuildInstallFieldEnvironment(NymphModuleViewModel module)
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in module.InstallFields)
        {
            if (field.IsSecret || string.IsNullOrWhiteSpace(field.EnvironmentName))
            {
                continue;
            }

            var environmentName = field.EnvironmentName.Trim();
            if (!IsSafeEnvironmentName(environmentName))
            {
                continue;
            }

            var value = field.SelectedValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
            {
                continue;
            }

            environment[environmentName] = value;
        }

        return environment;
    }

    private bool IsModuleLifecycleActive(NymphModuleViewModel? module)
    {
        return module is not null && _modulesWithActiveLifecycle.Contains(module.Id);
    }

    private static string BuildInstallFieldFeedback(
        NymphModuleViewModel module,
        IReadOnlyDictionary<string, string?> environment)
    {
        var lines = new List<string>
        {
            "Starting module registry install...",
        };

        var optionLines = new List<string>();
        foreach (var field in module.InstallFields)
        {
            if (field.IsSecret || string.IsNullOrWhiteSpace(field.EnvironmentName))
            {
                continue;
            }

            var environmentName = field.EnvironmentName.Trim();
            if (!environment.TryGetValue(environmentName, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var optionLabel = field.Options.FirstOrDefault(option =>
                string.Equals(option.Value, value, StringComparison.Ordinal))?.Label ?? value.Trim();
            optionLines.Add($"{field.Label}: {optionLabel}");
        }

        if (optionLines.Count > 0)
        {
            lines.Add("");
            lines.Add("Install options:");
            lines.AddRange(optionLines);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsSafeEnvironmentName(string value)
    {
        return Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$");
    }

    private void ApplySecretFieldToInvocation(NymphModuleActionFieldInfo field, IDictionary<string, string> environment)
    {
        var secretKind = ResolveSharedSecretKind(field);
        if (string.IsNullOrWhiteSpace(secretKind))
        {
            return;
        }

        var enteredSecret = field.SecretValue?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(enteredSecret))
        {
            SetSharedSecretValue(secretKind, enteredSecret);
            field.SecretValue = string.Empty;
            AppendActivity($"{field.Label} saved.");
        }

        var secret = GetSharedSecretValue(secretKind).Trim();
        if (!string.IsNullOrWhiteSpace(secret) &&
            !string.IsNullOrWhiteSpace(field.EnvironmentName) &&
            IsSafeEnvironmentName(field.EnvironmentName.Trim()))
        {
            environment[field.EnvironmentName.Trim()] = secret;
        }

        RefreshDisplayedActionGroupSecretState();
    }

    private static string ResolveSharedSecretKind(NymphModuleActionFieldInfo field)
    {
        if (string.Equals(field.SecretId, "huggingface.token", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.EnvironmentName, "NYMPHS3D_HF_TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            return "huggingface";
        }

        if (string.Equals(field.SecretId, "openrouter.api_key", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.EnvironmentName, "OPENROUTER_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            return "openrouter";
        }

        return string.Empty;
    }

    private string GetSharedSecretValue(string secretKind)
    {
        return secretKind switch
        {
            "huggingface" => _settings.HuggingFaceToken ?? string.Empty,
            "openrouter" => _settings.OpenRouterApiKey ?? string.Empty,
            _ => string.Empty,
        };
    }

    private void SetSharedSecretValue(string secretKind, string value)
    {
        var existing = _sharedSecretsService.Load();
        switch (secretKind)
        {
            case "huggingface":
                _settings.HuggingFaceToken = value;
                existing.HuggingFaceToken = value;
                break;
            case "openrouter":
                _settings.OpenRouterApiKey = value;
                existing.OpenRouterApiKey = value;
                break;
            default:
                return;
        }

        _sharedSecretsService.Save(existing);
    }

    private bool CanRunSelectedModuleDevAction(string? action)
    {
        if (!IsDeveloperMode || IsBusy || DisplayedModule is null || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return DisplayedModule.DevCapabilities.Any(capability => string.Equals(capability, action.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private void RunSelectedModuleDevAction(string? action)
    {
        var module = DisplayedModule;
        if (module is null || string.IsNullOrWhiteSpace(action) || !IsDeveloperMode)
        {
            return;
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        AppendActivity($"{module.Name} dev action requested: {normalizedAction}.");
        StatusMessage = $"{module.Name} dev action selected.";
        ShowModuleLogs = false;
        SetModuleActionFeedback(
            $"{module.Name}: dev action selected",
            $"Dev contract '{normalizedAction}' is reserved for this module repo to implement. When the module manifest exposes a dev entrypoint, this link can run it without changing the Manager page.");
    }

    private void ToggleDeveloperMode()
    {
        IsDeveloperMode = !IsDeveloperMode;
    }

    private async Task RunSelectedModuleActionAsync(NymphModuleActionInfo? actionInfo)
    {
        var module = DisplayedModule;
        if (module is null || actionInfo is null || string.IsNullOrWhiteSpace(actionInfo.ActionName))
        {
            return;
        }

        var normalizedAction = actionInfo.ActionName.Trim().ToLowerInvariant();
        var actionLabel = string.IsNullOrWhiteSpace(actionInfo.DisplayLabel) ? normalizedAction : actionInfo.DisplayLabel;
        if (normalizedAction == CloseModuleUiActionName)
        {
            ShowModulePage(module);
            StatusMessage = $"{module.Name} WebUI closed.";
            AppendActivity($"{module.Name} WebUI closed.");
            return;
        }

        var resultMode = string.IsNullOrWhiteSpace(actionInfo.ResultMode)
            ? "show_output"
            : actionInfo.ResultMode.Trim().ToLowerInvariant();
        var canRunWhileBusy = normalizedAction is "stop" or "logs";
        if (IsBusy && !canRunWhileBusy)
        {
            return;
        }

        if (!module.IsInstalled)
        {
            return;
        }

        if (resultMode is "open_terminal" or "terminal")
        {
            try
            {
                _workflowService.OpenNymphModuleActionTerminal(
                    _settings,
                    module.Id,
                    normalizedAction,
                    $"{module.Name} - {actionLabel}");
                StatusMessage = $"{module.Name} {actionLabel} opened in a terminal.";
                SetModuleActionFeedback(
                    $"{module.Name}: {actionLabel} opened",
                    "Use the terminal window to continue the interactive module action.");
                AppendActivity($"{module.Name} {actionLabel} terminal opened.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"{module.Name} {actionLabel} terminal failed to open.";
                SetModuleActionFeedback(
                    $"{module.Name}: {actionLabel} terminal failed",
                    BuildModuleActionFeedbackDetail(ex.Message));
                AppendActivity($"{module.Name} terminal action warning: {ex.Message}");
            }

            return;
        }

        var ownsBusyState = !IsBusy;
        if (ownsBusyState)
        {
            IsBusy = true;
        }
        StatusMessage = $"Running {module.Name} {actionLabel}...";
        if (!string.Equals(normalizedAction, "logs", StringComparison.OrdinalIgnoreCase))
        {
            ShowModuleLogs = false;
        }

        ClearStickyModuleActionFeedback();
        SetModuleActionFeedback(
            $"{module.Name}: running {actionLabel}",
            $"Command sent to the managed WSL distro. Waiting for {actionLabel} output...");

        try
        {
            var output = await _workflowService.RunNymphModuleActionAsync(
                _settings,
                module.Id,
                normalizedAction,
                new Progress<string>(AppendActivity),
                CancellationToken.None).ConfigureAwait(true);

            AppendModuleActionOutput(module, normalizedAction, output);
            var successDetail = BuildModuleActionFeedbackDetail(output);
            SetStickyModuleActionFeedback(
                module,
                BuildModuleActionSuccessTitle(module, actionLabel, normalizedAction),
                BuildModuleActionSuccessDetail(normalizedAction, successDetail));

            if (resultMode is "open_notepad" or "notepad")
            {
                OpenTextInNotepad($"{module.Id}-{actionInfo.Id}", BuildModuleActionFeedbackDetail(output));
                SetModuleActionFeedback(
                    $"{module.Name}: {actionLabel} opened",
                    "Action output opened in Notepad.");
            }
            else if (string.Equals(normalizedAction, "logs", StringComparison.OrdinalIgnoreCase))
            {
                ShowModuleLogs = true;
                ModuleLogsTitle = $"{module.Name} module logs";
                ModuleLogsDetail = BuildModuleActionFeedbackDetail(output);
                SetModuleActionFeedback(
                    $"{module.Name}: logs loaded",
                    "Module logs are shown below.");
            }
            else if (resultMode is "open_directory" or "open_folder" or "directory" or "folder")
            {
                OpenFirstDirectoryFromOutput(module, actionLabel, output);
            }

            var shouldOpenInManager = resultMode is "open_in_manager" or "manager_url";
            if (shouldOpenInManager)
            {
                if (!OpenFirstUrlFromOutput(module, output, quietWhenMissing: true) &&
                    normalizedAction is not "open" &&
                    module.Capabilities.Any(capability => string.Equals(capability, "open", StringComparison.OrdinalIgnoreCase)))
                {
                    var openOutput = await _workflowService.RunNymphModuleActionAsync(
                        _settings,
                        module.Id,
                        "open",
                        new Progress<string>(AppendActivity),
                        CancellationToken.None).ConfigureAwait(true);

                    AppendModuleActionOutput(module, "open", openOutput);
                    OpenFirstUrlFromOutput(module, openOutput);
                }
            }

            if (resultMode is "open_external_browser" or "external_browser")
            {
                if (!OpenFirstUrlFromOutput(module, output, forceExternalBrowser: true, quietWhenMissing: true) &&
                    normalizedAction is not "open" &&
                    module.Capabilities.Any(capability => string.Equals(capability, "open", StringComparison.OrdinalIgnoreCase)))
                {
                    var openOutput = await _workflowService.RunNymphModuleActionAsync(
                        _settings,
                        module.Id,
                        "open",
                        new Progress<string>(AppendActivity),
                        CancellationToken.None).ConfigureAwait(true);

                    AppendModuleActionOutput(module, "open", openOutput);
                    OpenFirstUrlFromOutput(module, openOutput, forceExternalBrowser: true);
                }
            }

            if (string.Equals(normalizedAction, "stop", StringComparison.OrdinalIgnoreCase) &&
                CurrentPageKind == ManagerPageKind.ModuleUi &&
                DisplayedModule is not null &&
                string.Equals(DisplayedModule.Id, module.Id, StringComparison.OrdinalIgnoreCase))
            {
                ShowModulePage(module);
            }

            if (normalizedAction is not "logs" and not "open")
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
            }

            SetStickyModuleActionFeedback(
                module,
                BuildModuleActionSuccessTitle(module, actionLabel, normalizedAction),
                BuildModuleActionSuccessDetail(normalizedAction, successDetail));
            StatusMessage = BuildModuleActionSuccessStatus(module, actionLabel, normalizedAction);
        }
        catch (Exception ex)
        {
            StatusMessage = $"{module.Name} {actionLabel} needs attention.";
            AppendActivity($"{module.Name} {actionLabel} warning: {ex.Message}");
            SetModuleActionFeedback(
                $"{module.Name}: {actionLabel} needs attention",
                BuildModuleActionFeedbackDetail(ex.Message));
            if (string.Equals(normalizedAction, "logs", StringComparison.OrdinalIgnoreCase))
            {
                ShowModuleLogs = true;
                ModuleLogsTitle = $"{module.Name} module logs";
                ModuleLogsDetail = BuildModuleActionFeedbackDetail(ex.Message);
            }
        }
        finally
        {
            if (ownsBusyState)
            {
                IsBusy = false;
            }
        }
    }

    private void AppendModuleActionOutput(NymphModuleViewModel module, string action, string output)
    {
        AppendActivity($"{module.Name} {action} completed.");

        foreach (var line in output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(12))
        {
            AppendActivity($"{module.Id}/{action}: {line}");
        }
    }

    private IProgress<string> CreateModuleLiveProgress(NymphModuleViewModel module, string action, List<string> liveLines)
    {
        return new Progress<string>(message =>
        {
            AppendActivity(message);
            AppendModuleLiveLine(module, action, message, liveLines);
        });
    }

    private void AppendModuleLiveLine(NymphModuleViewModel module, string action, string? message, List<string> liveLines)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        liveLines.Add(message.Trim());
        while (liveLines.Count > 40)
        {
            liveLines.RemoveAt(0);
        }

        SetModuleActionFeedback(
            $"{module.Name}: {action} in progress",
            BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, liveLines)));
    }

    private void SetModuleActionFeedback(string title, string detail)
    {
        ModuleActionFeedbackTitle = title;
        ModuleActionFeedbackDetail = string.IsNullOrWhiteSpace(detail) ? "The command finished without output." : detail.Trim();
    }

    private void SetStickyModuleActionFeedback(NymphModuleViewModel module, string title, string detail)
    {
        _stickyModuleActionFeedbackModuleId = module.Id;
        _stickyModuleActionFeedbackUntilUtc = DateTime.UtcNow.AddSeconds(45);
        SetModuleActionFeedback(title, detail);
    }

    private void ClearStickyModuleActionFeedback()
    {
        _stickyModuleActionFeedbackModuleId = string.Empty;
        _stickyModuleActionFeedbackUntilUtc = DateTime.MinValue;
    }

    private bool ShouldPreserveModuleActionFeedback(NymphModuleViewModel module)
    {
        return CurrentPageKind == ManagerPageKind.Module &&
               DateTime.UtcNow < _stickyModuleActionFeedbackUntilUtc &&
               string.Equals(_stickyModuleActionFeedbackModuleId, module.Id, StringComparison.OrdinalIgnoreCase);
    }

    private void SetModuleDetailPaneFeedback(NymphModuleViewModel module)
    {
        if (ShouldPreserveModuleActionFeedback(module))
        {
            return;
        }

        SetModuleActionFeedback(
            BuildModuleDetailPaneTitle(module),
            BuildModuleDetailPaneText(module));
    }

    private static string BuildModuleActionSuccessTitle(NymphModuleViewModel module, string actionLabel, string normalizedAction)
    {
        return IsSmokeTestAction(normalizedAction)
            ? $"{module.Name}: SMOKE TEST PASSED"
            : $"{module.Name}: {actionLabel} finished";
    }

    private static string BuildModuleActionSuccessStatus(NymphModuleViewModel module, string actionLabel, string normalizedAction)
    {
        return IsSmokeTestAction(normalizedAction)
            ? $"{module.Name} {actionLabel} passed."
            : $"{module.Name} {actionLabel} finished.";
    }

    private static string BuildModuleActionSuccessDetail(string normalizedAction, string detail)
    {
        if (!IsSmokeTestAction(normalizedAction))
        {
            return detail;
        }

        var normalizedDetail = string.IsNullOrWhiteSpace(detail)
            ? "The module smoke test completed without output."
            : detail.Trim();
        return $"SUCCESS: backend started, answered /server_info, and stopped cleanly.{Environment.NewLine}{Environment.NewLine}{normalizedDetail}";
    }

    private static bool IsSmokeTestAction(string normalizedAction)
    {
        return normalizedAction is "smoke_test" or "smoke-test" or "smoke";
    }

    private static string BuildModuleDetailPaneText(NymphModuleViewModel module)
    {
        var guideLines = module.ManagerActionGroups
            .SelectMany(BuildModuleActionGroupGuideLines)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var detail = module.HasUpdate
            ? module.UpdateDetail
            : $"{module.Detail}\n\n{module.SecondaryDetail}";

        if (!module.IsInstalled || guideLines.Length == 0)
        {
            return detail;
        }

        return $"{detail}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, guideLines)}";
    }

    private static string BuildModuleDetailPaneTitle(NymphModuleViewModel module)
    {
        return $"{module.Name}: {module.DisplayStateLabel}";
    }

    private static IEnumerable<string> BuildModuleActionGroupGuideLines(NymphModuleActionGroupInfo group)
    {
        if (!string.IsNullOrWhiteSpace(group.Description))
        {
            foreach (var line in group.Description.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return line;
            }
        }

    }

    private static string BuildModuleActionFeedbackDetail(string output)
    {
        var downloadDetail = BuildModelDownloadFeedbackDetail(output);
        if (!string.IsNullOrWhiteSpace(downloadDetail))
        {
            return downloadDetail;
        }

        var lines = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(5)
            .ToArray();

        return lines.Length == 0
            ? "The command finished without output."
            : string.Join(Environment.NewLine, lines);
    }

    private static string? BuildModelDownloadFeedbackDetail(string output)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var downloadLines = lines
            .Where(line => line.Contains("MODEL DOWNLOAD", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (downloadLines.Count == 0)
        {
            return null;
        }

        var latest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var state = "downloading";
        foreach (var line in downloadLines)
        {
            if (line.Contains("MODEL DOWNLOAD COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                state = "complete";
            }
            else if (line.Contains("MODEL DOWNLOAD FAILED", StringComparison.OrdinalIgnoreCase))
            {
                state = "failed";
            }
            else if (line.Contains("MODEL DOWNLOAD STARTED", StringComparison.OrdinalIgnoreCase))
            {
                state = "started";
            }
            else if (line.Contains("status=downloading", StringComparison.OrdinalIgnoreCase))
            {
                state = "downloading";
            }

            foreach (var key in new[]
                     {
                         "phase", "repo", "status", "cache_dir", "progress_interval", "waiting_on",
                         "shared_cache", "downloaded_this_step", "repo_cache_blobs", "active_partial_files",
                         "exit_status",
                     })
            {
                var value = ExtractLogValue(line, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    latest[key] = value;
                }
            }
        }

        var detail = new List<string>
        {
            $"Model download: {state}",
        };
        AddFeedbackLine(detail, "Repo", latest.GetValueOrDefault("repo"));
        AddFeedbackLine(detail, "Phase", latest.GetValueOrDefault("phase"));
        AddFeedbackLine(detail, "Waiting on", latest.GetValueOrDefault("waiting_on"));
        AddFeedbackLine(detail, "Cache", latest.GetValueOrDefault("shared_cache"));
        AddFeedbackLine(detail, "Downloaded this step", latest.GetValueOrDefault("downloaded_this_step"));
        AddFeedbackLine(detail, "Repo cache blobs", latest.GetValueOrDefault("repo_cache_blobs"));
        AddFeedbackLine(detail, "Active partial files", latest.GetValueOrDefault("active_partial_files"));
        AddFeedbackLine(detail, "Cache dir", latest.GetValueOrDefault("cache_dir"));
        AddFeedbackLine(detail, "Exit status", latest.GetValueOrDefault("exit_status"));

        detail.Add("");
        detail.Add("Latest raw download lines:");
        detail.AddRange(downloadLines.TakeLast(4));
        return string.Join(Environment.NewLine, detail);
    }

    private static string? ExtractLogValue(string line, string key)
    {
        var marker = key + "=";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = line.Length;
        foreach (var nextKey in new[]
                 {
                     " phase=", " repo=", " status=", " cache_dir=", " progress_interval=", " waiting_on=",
                     " shared_cache=", " downloaded_this_step=", " repo_cache_blobs=", " active_partial_files=",
                     " exit_status=",
                 })
        {
            var candidate = line.IndexOf(nextKey, start, StringComparison.OrdinalIgnoreCase);
            if (candidate >= 0 && candidate < end)
            {
                end = candidate;
            }
        }

        return line[start..end].Trim();
    }

    private static void AddFeedbackLine(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value.Trim()}");
        }
    }

    private static bool IsNoisyUnavailableModuleLogLine(string line)
    {
        return line.Contains("Module is available from the registry, but is not installed yet.", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("status warning: Module action 'status' failed for 'brain'", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("status warning: Module action 'status' failed for 'lora'", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("status warning: Module action 'status' failed for 'trellis'", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("id=brain", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("id=lora", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("id=trellis", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("install_root=/home/nymph/Nymphs-Brain", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("install_root=/home/nymph/ZImage-Trainer", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("install_root=/home/nymph/TRELLIS.2", StringComparison.OrdinalIgnoreCase);
    }

    private bool OpenFirstUrlFromOutput(
        NymphModuleViewModel module,
        string output,
        bool forceExternalBrowser = false,
        bool quietWhenMissing = false)
    {
        var match = Regex.Match(output, @"https?://[^\s]+", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            if (!quietWhenMissing)
            {
                AppendActivity($"{module.Name} open did not return a URL.");
                SetModuleActionFeedback(
                    $"{module.Name}: no browser URL returned",
                    "The module command finished, but it did not print a URL for the manager to open.");
            }

            return false;
        }

        var url = match.Value.TrimEnd('.', ',', ';');
        try
        {
            if (module.HasInstalledModuleUi && !forceExternalBrowser)
            {
                SelectedNavigationItem = null;
                SelectedModule = module;
                DisplayedModule = module;
                CurrentPageTitle = module.Name;
                CurrentPageSubtitle = module.Description;
                ModuleUiTitle = module.ModuleUiTitle;
                ModuleUiStatus = url;
                CurrentPageKind = ManagerPageKind.ModuleUi;
                ModuleUiSource = url;
                AppendActivity($"{module.Name} opened in Manager at {url}.");
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            AppendActivity($"{module.Name} opened at {url}.");
            SetModuleActionFeedback(
                $"{module.Name}: opened in browser",
                url);
            return true;
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open {module.Name} URL: {ex.Message}");
            SetModuleActionFeedback(
                $"{module.Name}: browser open failed",
                ex.Message);
            return false;
        }
    }

    private bool OpenFirstDirectoryFromOutput(NymphModuleViewModel module, string actionLabel, string output)
    {
        var directory = ExtractDirectoryPath(output);
        if (string.IsNullOrWhiteSpace(directory))
        {
            AppendActivity($"{module.Name} {actionLabel} did not return a directory.");
            SetModuleActionFeedback(
                $"{module.Name}: no directory returned",
                "The module command finished, but it did not print a directory for the manager to open.");
            return false;
        }

        var explorerPath = NormalizeModuleDirectoryForExplorer(directory);
        try
        {
            Directory.CreateDirectory(explorerPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = explorerPath,
                UseShellExecute = true,
            });
            AppendActivity($"{module.Name} {actionLabel} opened: {explorerPath}");
            SetModuleActionFeedback(
                $"{module.Name}: {actionLabel} opened",
                explorerPath);
            return true;
        }
        catch (Exception ex)
        {
            AppendActivity($"Could not open {module.Name} directory: {ex.Message}");
            SetModuleActionFeedback(
                $"{module.Name}: directory open failed",
                ex.Message);
            return false;
        }
    }

    private string NormalizeModuleDirectoryForExplorer(string directory)
    {
        var normalized = directory.Trim().Trim('"', '\'');
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            var segments = normalized
                .TrimStart('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return BuildManagedDistroPath(segments);
        }

        return normalized;
    }

    private static string ExtractDirectoryPath(string output)
    {
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = line.Trim();
            var parts = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                (parts[0].Equals("directory", StringComparison.OrdinalIgnoreCase) ||
                 parts[0].Equals("dir", StringComparison.OrdinalIgnoreCase) ||
                 parts[0].Equals("path", StringComparison.OrdinalIgnoreCase)))
            {
                return parts[1].Trim();
            }

            if (trimmed.StartsWith("/", StringComparison.Ordinal) ||
                trimmed.StartsWith(@"\\", StringComparison.Ordinal) ||
                Regex.IsMatch(trimmed, @"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant))
            {
                return trimmed;
            }
        }

        return string.Empty;
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
        if (module is null || !module.CanUninstall || IsBusy)
        {
            return;
        }

        var targetId = module.Id;
        var targetName = module.Name;

        if (purge && !string.Equals(targetId, "worbi", StringComparison.OrdinalIgnoreCase))
        {
            const string detail = "Delete Module + Data is temporarily disabled for repo/runtime modules while module routing is being audited. Use Uninstall Module only.";
            StatusMessage = $"{targetName} delete blocked.";
            AppendActivity($"Blocked destructive delete for {targetName} ({targetId}).");
            SetModuleActionFeedback($"{targetName}: delete blocked", detail);
            MessageBox.Show(detail, "Delete Disabled", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var prompt = purge
            ? $"Delete {targetName} completely?\n\nThis removes the module install folder and its module data from the managed WSL distro."
            : $"Uninstall {targetName}?\n\nThis removes the module install folder but saves known user data to ~/NymphsModuleBackups inside the managed WSL distro.";
        var caption = purge ? "Delete Module + Data" : "Uninstall Module";
        var icon = purge ? MessageBoxImage.Warning : MessageBoxImage.Question;
        var confirmation = MessageBox.Show(prompt, caption, MessageBoxButton.YesNo, icon);
        if (confirmation != MessageBoxResult.Yes)
        {
            AppendActivity($"{targetName} uninstall cancelled.");
            return;
        }

        IsBusy = true;
        _modulesWithActiveLifecycle.Add(targetId);
        StatusMessage = purge
            ? $"Deleting {targetName} from the managed distro..."
            : $"Uninstalling {targetName} from the managed distro...";
        ShowModuleLogs = false;
        var uninstallLines = new List<string>();
        var actionLabel = purge ? "delete" : "uninstall";
        ApplyModuleLifecycleState(
            module,
            purge ? "Deleting" : "Uninstalling",
            "#D9B36B",
            module.VersionLabel,
            purge
                ? $"{targetName} delete is running inside the managed WSL distro."
                : $"{targetName} uninstall is running inside the managed WSL distro.",
            "Status refresh is paused for this module until the lifecycle action finishes.");
        SetModuleActionFeedback(
            $"{targetName}: {actionLabel} in progress",
            purge
                ? "Deleting the module install folder and data from the managed WSL distro..."
                : "Uninstalling the module while preserving known data when available...");

        try
        {
            AppendActivity($"AUDIT module {actionLabel} requested: id={targetId}, name={targetName}, displayed={DisplayedModule?.Id ?? "-"}, selected={SelectedModule?.Id ?? "-"}.");
            var uninstallOutput = await _workflowService.RunNymphModuleUninstallAsync(
                _settings,
                targetId,
                purge,
                CreateModuleLiveProgress(module, actionLabel, uninstallLines),
                _operationCancellation.Token).ConfigureAwait(true);

            AppendActivity(purge
                ? $"{targetName} delete completed."
                : $"{targetName} uninstall completed.");
            ApplyImmediateModuleInstallResult(
                module,
                isInstalled: false,
                purge
                    ? "Module and data were deleted. Registry install remains available."
                    : "Module was uninstalled. Preserved data was moved to the module backup area when available.");
            SetModuleActionFeedback(
                $"{targetName}: {actionLabel} finished",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, uninstallLines.Append(uninstallOutput))));

            _modulesWithActiveLifecycle.Remove(targetId);
            try
            {
                await RefreshModuleStateAsync().ConfigureAwait(true);
            }
            catch (Exception refreshException)
            {
                AppendActivity($"{targetName} uninstall completed, but state refresh needs attention: {refreshException.Message}");
            }

            if (!module.IsInstalled && CurrentPageKind == ManagerPageKind.Module)
            {
                SelectPrimaryPage(ManagerPageKind.Home);
            }

            StatusMessage = purge
                ? $"{targetName} deleted."
                : $"{targetName} uninstalled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{targetName} uninstall needs attention.";
            AppendActivity($"{targetName} uninstall warning: {ex.Message}");
            SetModuleActionFeedback(
                $"{targetName}: {actionLabel} needs attention",
                BuildModuleActionFeedbackDetail(string.Join(Environment.NewLine, uninstallLines.Append(ex.Message))));
        }
        finally
        {
            _modulesWithActiveLifecycle.Remove(targetId);
            IsBusy = false;
        }
    }

    private void ApplyModuleLifecycleState(
        NymphModuleViewModel module,
        string stateLabel,
        string statusBrush,
        string versionLabel,
        string detail,
        string secondaryDetail)
    {
        module.ApplyState(
            module.IsInstalled,
            module.IsRunning,
            versionLabel,
            stateLabel,
            statusBrush,
            detail,
            secondaryDetail);

        RebuildModuleCollections();
        RebuildModuleNavigation();
        RefreshDisplayedModuleActionState();

        _openModuleCommand.RaiseCanExecuteChanged();
        _installModuleCommand.RaiseCanExecuteChanged();
        _repairModuleCommand.RaiseCanExecuteChanged();
        _updateModuleCommand.RaiseCanExecuteChanged();
        _uninstallModuleCommand.RaiseCanExecuteChanged();
        _deleteModuleCommand.RaiseCanExecuteChanged();
        _openModuleUiCommand.RaiseCanExecuteChanged();
    }

    private void ApplyImmediateModuleInstallResult(NymphModuleViewModel module, bool isInstalled, string detail, string? installedVersionOverride = null)
    {
        module.ApplyState(
            isInstalled,
            isRunning: false,
            !string.IsNullOrWhiteSpace(installedVersionOverride)
                ? installedVersionOverride
                : GetInstalledModuleVersionLabel(module.Id, isInstalled),
            isInstalled ? "Installed" : "Available",
            isInstalled ? "#4CD0C1" : "#6E745A",
            isInstalled
                ? $"{module.Name} files were refreshed in the managed distro."
                : $"{module.Name} is available as an optional Nymph.",
            detail);
        RefreshInstalledModuleUiInfo(module);
        module.ClearUpdateState(isInstalled
            ? $"{module.Name} was refreshed from the registry."
            : $"{module.Name} is not installed.");

        RebuildModuleCollections();
        RebuildModuleNavigation();

        if (DisplayedModule is not null && string.Equals(DisplayedModule.Id, module.Id, StringComparison.OrdinalIgnoreCase))
        {
            RefreshDisplayedModuleDetails(module);
            SetModuleDetailPaneFeedback(module);
        }

        _openModuleCommand.RaiseCanExecuteChanged();
        _installModuleCommand.RaiseCanExecuteChanged();
        _repairModuleCommand.RaiseCanExecuteChanged();
        _updateModuleCommand.RaiseCanExecuteChanged();
        _uninstallModuleCommand.RaiseCanExecuteChanged();
        _deleteModuleCommand.RaiseCanExecuteChanged();
        _openModuleUiCommand.RaiseCanExecuteChanged();
    }

    private void RefreshDisplayedModuleActionState()
    {
        OnPropertyChanged(nameof(ShowInstallModuleAction));
        OnPropertyChanged(nameof(ShowModuleInstallFields));
        OnPropertyChanged(nameof(DisplayedModuleInstallOptionsTitle));
        OnPropertyChanged(nameof(ShowInstalledModuleActions));
        OnPropertyChanged(nameof(ShowInstalledModuleActionGroups));
        OnPropertyChanged(nameof(DisplayedModuleContractActions));
        OnPropertyChanged(nameof(DisplayedModuleInstallFields));
        OnPropertyChanged(nameof(DisplayedModuleActionGroups));
        OnPropertyChanged(nameof(ShowDeleteModuleData));
        OnPropertyChanged(nameof(ShowModuleUiAction));
        _repairModuleCommand.RaiseCanExecuteChanged();
        _runModuleActionCommand.RaiseCanExecuteChanged();
        _runModuleActionGroupCommand.RaiseCanExecuteChanged();
    }

    private void RefreshDisplayedActionGroupSecretState()
    {
        if (DisplayedModule is not null)
        {
            RefreshActionGroupSecretState(DisplayedModule);
        }

        OnPropertyChanged(nameof(DisplayedModuleActionGroups));
    }

    private void RefreshActionGroupSecretState(NymphModuleViewModel module)
    {
        foreach (var field in module.ManagerActionGroups.SelectMany(group => group.Fields))
        {
            if (!field.IsSecret)
            {
                continue;
            }

            var secretKind = ResolveSharedSecretKind(field);
            field.ApplySavedSecretState(!string.IsNullOrWhiteSpace(GetSharedSecretValue(secretKind)));
        }
    }

    private void RefreshDisplayedModuleDetails(NymphModuleViewModel module)
    {
        if (DisplayedModule is null || !string.Equals(DisplayedModule.Id, module.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedModule = module;
        DisplayedModule = module;
        CurrentPageTitle = module.Name;
        CurrentPageSubtitle = module.Detail;
        RefreshDisplayedModuleActionState();
        _openModuleCommand.RaiseCanExecuteChanged();
        _installModuleCommand.RaiseCanExecuteChanged();
        _repairModuleCommand.RaiseCanExecuteChanged();
        _updateModuleCommand.RaiseCanExecuteChanged();
        _uninstallModuleCommand.RaiseCanExecuteChanged();
        _deleteModuleCommand.RaiseCanExecuteChanged();
        _openModuleUiCommand.RaiseCanExecuteChanged();
    }

    private static string? ExtractInstalledModuleVersion(IEnumerable<string> outputLines)
    {
        foreach (var line in outputLines)
        {
            var trimmed = line.Trim();
            const string installedModuleVersionPrefix = "installed_module_version=";
            if (trimmed.StartsWith(installedModuleVersionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var version = trimmed[installedModuleVersionPrefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(version) ? null : version;
            }
        }

        return null;
    }

    private void ClearModuleUpdateAfterSuccessfulInstall(NymphModuleViewModel module, string? installedVersion)
    {
        var version = string.IsNullOrWhiteSpace(installedVersion)
            ? module.VersionLabel
            : installedVersion;

        module.ApplyUpdateState(
            version,
            string.IsNullOrWhiteSpace(module.RemoteVersionLabel) ? version : module.RemoteVersionLabel,
            hasUpdate: false,
            $"{module.Name} is current after the registry install/update finished.");

        RebuildModuleNavigation();
        OnPropertyChanged(nameof(HomeInstalledModules));
        OnPropertyChanged(nameof(HomeAvailableModules));
        _updateModuleCommand.RaiseCanExecuteChanged();
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

    private static string BuildAppVersionLabel()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        }

        version = string.IsNullOrWhiteSpace(version)
            ? "unknown"
            : version.Split('+', 2)[0].Trim();

        return $"NymphsCore v{version}";
    }

    private void SelectPrimaryPage(ManagerPageKind pageKind)
    {
        var item = PrimaryNavigationItems.FirstOrDefault(nav => nav.PageKind == pageKind);
        if (item is not null)
        {
            SelectedNavigationItem = item;
            return;
        }

        SelectedNavigationItem = null;
        CurrentPageKind = pageKind;
        SelectedModule = null;
        DisplayedModule = null;

        switch (pageKind)
        {
            case ManagerPageKind.BaseRuntime:
                CurrentPageTitle = "Base Runtime";
                CurrentPageSubtitle = "Managed WSL shell, platform status, and repair actions";
                break;
            case ManagerPageKind.SystemChecks:
                CurrentPageTitle = "System Checks";
                CurrentPageSubtitle = "Live platform diagnostics and readiness checks";
                break;
            case ManagerPageKind.Home:
                CurrentPageTitle = "Home";
                CurrentPageSubtitle = "Overview of your system and modules";
                break;
            case ManagerPageKind.Logs:
                CurrentPageTitle = "Logs";
                CurrentPageSubtitle = "Session activity, runtime checks, and shell feedback";
                break;
            case ManagerPageKind.Guide:
                CurrentPageTitle = "Guide";
                CurrentPageSubtitle = "Core docs, onboarding, and next-step references";
                break;
        }
    }

    private void OnSidebarArtTimerTick(object? sender, EventArgs e)
    {
        // Rotation is disabled for the single-artwork sidebar test stage.
    }

    private async void OnRuntimeMonitorTimerTick(object? sender, EventArgs e)
    {
        if (_isRefreshingRuntimeMonitorLive)
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

    private InstallSettings CreateBaseRuntimeSettings(string? existingDistroName)
    {
        var settings = CreateDefaultInstallSettings();
        settings.DistroName = existingDistroName ?? InstallerWorkflowService.ManagedDistroName;
        settings.TarPath = _workflowService.BaseTarPath;
        settings.InstallLocation = string.IsNullOrWhiteSpace(existingDistroName)
            ? SelectedBaseRuntimeDrive?.InstallPath ?? _settings.InstallLocation
            : _workflowService.GetExistingManagedDistroInstallLocation(existingDistroName) ?? _settings.InstallLocation;
        settings.LinuxUser = InstallerWorkflowService.ManagedLinuxUser;
        settings.RepairExistingDistro = !string.IsNullOrWhiteSpace(existingDistroName);
        settings.PrefetchModelsNow = false;
        settings.InstallNymphsBrain = false;
        settings.InstallZImageTrainer = false;
        settings.DownloadBrainModelNow = false;
        settings.HuggingFaceToken = string.Empty;
        return settings;
    }

    private void ApplyRuntimeSettings(InstallSettings settings)
    {
        _settings.DistroName = settings.DistroName;
        _settings.TarPath = settings.TarPath;
        _settings.InstallLocation = settings.InstallLocation;
        _settings.LinuxUser = settings.LinuxUser;
        _settings.RepairExistingDistro = settings.RepairExistingDistro;
        _settings.PrefetchModelsNow = false;
        _settings.InstallNymphsBrain = false;
        _settings.InstallZImageTrainer = false;
        _settings.DownloadBrainModelNow = false;
    }

    private static string NormalizeModuleStateLabel(string? state, bool isInstalled, bool isRunning)
    {
        if (isRunning)
        {
            return "Running";
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return isInstalled ? "Installed" : "Available";
        }

        return state.Trim().ToLowerInvariant() switch
        {
            "available" => "Available",
            "installed" => "Installed",
            "running" => "Running",
            "repair_needed" => "Repair needed",
            "needs_attention" => "Needs attention",
            "installing" => "Installing",
            "uninstalling" => "Uninstalling",
            "updating" => "Updating",
            "deleting" => "Deleting",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(state.Trim().Replace('_', ' ')),
        };
    }

    private static bool StateNeedsAttention(string? state, string? health)
    {
        return string.Equals(state, "needs_attention", StringComparison.OrdinalIgnoreCase)
            || string.Equals(health, "degraded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(health, "unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static string ValueOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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

    private string ResolveManagedInstallPath(string installRoot, string moduleId)
    {
        var normalized = string.IsNullOrWhiteSpace(installRoot)
            ? $"$HOME/{moduleId}"
            : installRoot.Trim();

        if (normalized.StartsWith("$HOME/", StringComparison.Ordinal))
        {
            normalized = $"/home/{_settings.LinuxUser}/{normalized["$HOME/".Length..]}";
        }
        else if (normalized.Equals("$HOME", StringComparison.Ordinal))
        {
            normalized = $"/home/{_settings.LinuxUser}";
        }
        else if (normalized.StartsWith("~/", StringComparison.Ordinal))
        {
            normalized = $"/home/{_settings.LinuxUser}/{normalized[2..]}";
        }
        else if (normalized.Equals("~", StringComparison.Ordinal))
        {
            normalized = $"/home/{_settings.LinuxUser}";
        }

        var segments = normalized
            .TrimStart('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return BuildManagedDistroPath(segments);
    }

    private static string BuildModuleAccent(string moduleId, int index)
    {
        var palette = new[]
        {
            "#97DF48",
            "#22DDF0",
            "#39C7FF",
            "#C8EE47",
            "#A9E347",
            "#F0B84A",
            "#66D3A7",
            "#D98CC8",
        };

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return palette[Math.Abs(index) % palette.Length];
        }

        var hash = moduleId.Aggregate(0, (current, character) => unchecked((current * 31) + character));
        return palette[(int)(Math.Abs((long)hash) % palette.Length)];
    }

    private NymphModuleViewModel FindModule(string id)
    {
        return _allModules.First(module => string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
