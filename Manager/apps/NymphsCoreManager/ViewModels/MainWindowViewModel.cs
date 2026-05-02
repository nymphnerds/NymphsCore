using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using NymphsCoreManager.Models;
using NymphsCoreManager.Services;

namespace NymphsCoreManager.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const int TotalSteps = 9;
    private const string ModelDownloadDetails =
        "- u2net helper model: about 168 MB\n" +
        "- Tongyi-MAI/Z-Image-Turbo: about 31 GB\n" +
        "- nunchaku-ai/nunchaku-z-image-turbo weights for Z-Image Turbo\n" +
        "- TRELLIS.2 model bundle: a large multi-GB shared-cache download";
    private const string RuntimeDownloadDetails =
        "- Z-Image Turbo via Nunchaku Python .venv: about 5.4 GB\n" +
        "- TRELLIS.2 runtime repo and Python .venv\n" +
        "- CUDA 13.0 in WSL: about 4.9 GB\n" +
        "- Flash Attention may compile during TRELLIS setup and can take a very long time on some machines";
    private const string BrainInstallRoot = "/home/nymph/Nymphs-Brain";
    private static readonly Regex TrainerStepProgressRegex = new(@"(?<current>\d+)\s*/\s*(?<total>\d+)", RegexOptions.Compiled);
    private static readonly Regex TrainerStructuredProgressRegex = new(@"TRAIN_PROGRESS\s+current=(?<current>\d+)\s+total=(?<total>\d+)", RegexOptions.Compiled);
    private static readonly Regex TrainerPercentRegex = new(@"(?<percent>\d{1,3})%", RegexOptions.Compiled);

    private readonly InstallerWorkflowService _workflowService;
    private readonly SharedSecretsService _sharedSecretsService;
    private readonly AsyncRelayCommand _primaryCommand;
    private readonly AsyncRelayCommand _checkForUpdatesCommand;
    private readonly RelayCommand _addOptionalModulesCommand;
    private readonly RelayCommand _repairRuntimeCommand;
    private readonly AsyncRelayCommand _openRuntimeToolsCommand;
    private readonly AsyncRelayCommand _refreshRuntimeStatusCommand;
    private readonly AsyncRelayCommand _openZImageTrainerCommand;
    private readonly AsyncRelayCommand _installZImageTrainerCommand;
    private readonly AsyncRelayCommand _refreshZImageTrainerStatusCommand;
    private readonly AsyncRelayCommand _createZImageTrainerJobCommand;
    private readonly AsyncRelayCommand _openZImageTrainerPicturesCommand;
    private readonly AsyncRelayCommand _openZImageTrainerCaptionsCommand;
    private readonly AsyncRelayCommand _draftZImageTrainerCaptionsCommand;
    private readonly AsyncRelayCommand _startZImageTrainingCommand;
    private readonly AsyncRelayCommand _stopZImageTrainingCommand;
    private readonly AsyncRelayCommand _launchZImageTrainerOfficialUiCommand;
    private readonly AsyncRelayCommand _killZImageTrainerOfficialUiCommand;
    private readonly AsyncRelayCommand _launchZImageTrainerGradioUiCommand;
    private readonly RelayCommand _openZImageTrainerDatasetsCommand;
    private readonly RelayCommand _openZImageTrainerJobsCommand;
    private readonly RelayCommand _openZImageTrainerLorasCommand;
    private readonly AsyncRelayCommand _openBrainToolsCommand;
    private readonly AsyncRelayCommand _refreshBrainStatusCommand;
    private readonly AsyncRelayCommand _fetchModelsNowCommand;
    private readonly AsyncRelayCommand _fetchZImageModelsCommand;
    private readonly AsyncRelayCommand _fetchTrellisModelsCommand;
    private readonly AsyncRelayCommand _checkRuntimeDependencyUpdatesCommand;
    private readonly AsyncRelayCommand _testLatestRuntimeDependenciesCommand;
    private readonly AsyncRelayCommand _restorePinnedRuntimeDependenciesCommand;
    private readonly AsyncRelayCommand _repairTrellisAdapterCommand;
    private readonly AsyncRelayCommand _testZImageCommand;
    private readonly AsyncRelayCommand _testTrellisCommand;
    private readonly AsyncRelayCommand _startBrainLlmCommand;
    private readonly AsyncRelayCommand _openBrainWebUiCommand;
    private readonly AsyncRelayCommand _updateBrainWebUiCommand;
    private readonly AsyncRelayCommand _applyBrainOpenRouterKeyCommand;
    private readonly RelayCommand _changeBrainModelCommand;
    private readonly AsyncRelayCommand _stopBrainLlmCommand;
    private readonly RelayCommand _backCommand;
    private readonly RelayCommand _openLogFolderCommand;
    private readonly RelayCommand _openGuideCommand;
    private readonly DispatcherTimer _brainMonitorRefreshTimer;
    private readonly string _installSessionLogPath;

    private int _currentStepIndex;
    private bool _isBusy;
    private bool _systemChecksCompleted;
    private bool _installCompleted;
    private bool _installSucceeded;
    private bool _managedDistroDetected;
    private bool _updateCheckCompleted;
    private bool _updatesAvailableFromCheck;
    private bool _lastRunUsedExistingInstall;
    private bool _lastRunAppliedUpdates;
    private bool _moduleOnlyRun;
    private int _runtimeToolsReturnStep = 5;
    private string _currentStepTitle = string.Empty;
    private string _currentStepSubtitle = string.Empty;
    private string _primaryButtonText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _finishSummary = string.Empty;
    private string _postInstallActionSummary = string.Empty;
    private string _runtimeToolsSummary = string.Empty;
    private string _updateCheckSummary = string.Empty;
    private string _recommendedWslConfigSummary = string.Empty;
    private string _currentWslConfigSummary = string.Empty;
    private double _progressValue;
    private bool _prefetchModelsNow = true;
    private bool _hasExistingWslConfig;
    private string _huggingFaceToken = string.Empty;
    private TrellisGgufQuantOption? _selectedTrellisGgufQuantOption;
    private int _detectedGpuVramMb;
    private string _managedDistroName = InstallerWorkflowService.ManagedDistroName;
    private DriveChoice? _selectedDrive;
    private WslConfigModeOption? _selectedWslConfigOption;
    private int _wslCustomMemoryGb;
    private int _wslCustomProcessors;
    private int _wslCustomSwapGb;
    private bool _installNymphsBrain;
    private bool _installZImageTrainer;
    private bool _downloadBrainModelNow;
    private BrainModelOption? _selectedBrainModelOption;
    private string _customBrainModelId = string.Empty;
    private int _brainContextLength = 16384;
    private string _brainInstallState = "unknown";
    private string _brainLlmState = "unknown";
    private string _brainMcpState = "unknown";
    private string _brainWebUiState = "unknown";
    private string _brainLoadedModel = "Not checked";
    private string _brainRemoteModel = "unknown";
    private RuntimeBackendStatus _zImageRuntimeStatus = RuntimeBackendStatus.Unknown("zimage", "Z-Image", "Open Runtime Tools to check status.");
    private RuntimeBackendStatus _trellisRuntimeStatus = RuntimeBackendStatus.Unknown("trellis", "TRELLIS.2", "Open Runtime Tools to check status.");
    private ZImageTrainerStatus _zImageTrainerStatus = ZImageTrainerStatus.Unknown("Open Z-Image Trainer to check status.");
    private string _zImageTrainerDatasetName = "my_first_dataset";
    private string _zImageTrainerLoraName = "my_first_lora";
    private string _zImageTrainerPreset = "basic_turbo";
    private string _zImageTrainerTrainingType = "character";
    private string _zImageTrainerTrainingAmount = "normal";
    private int _zImageTrainerSteps = 3000;
    private string _zImageTrainerLearningRate = "1e-4";
    private int _zImageTrainerRank = 16;
    private string _zImageTrainerAdapterVersion = "v1";
    private bool _zImageTrainerLowVram;
    private bool _useZImageTrainerCaptionBrain;
    private string _zImageTrainerCaptionMode = "fill_blanks";
    private bool _zImageTrainerStopRequested;
    private bool _zImageTrainerHasRealStepProgress;
    private double _zImageTrainerProgressPercent;
    private string _zImageTrainerProgressText = "No training run in progress yet.";
    private bool _zImageTrainerProgressVisible;
    private string _brainRuntimeStatusText = "Status: Not checked";
    private string _brainRuntimeModelText = "Model: Not checked";
    private string _brainOpenRouterApiKey = string.Empty;
    private BrainMonitorSnapshot _brainMonitorSnapshot = BrainMonitorSnapshot.Offline;
    private bool _isRefreshingBrainMonitor;

    public MainWindowViewModel(InstallerWorkflowService workflowService)
    {
        _workflowService = workflowService;
        _sharedSecretsService = new SharedSecretsService();
        _installSessionLogPath = _workflowService.CreateInstallSessionLogPath();

        SystemChecks = new ObservableCollection<SystemCheckItem>();
        LogLines = new ObservableCollection<string>();
        WslConfigOptions = new ObservableCollection<WslConfigModeOption>();
        BrainModelOptions = new ObservableCollection<BrainModelOption>();
        TrellisGgufQuantOptions = new ObservableCollection<TrellisGgufQuantOption>();
        ExistingZImageTrainerDatasets = new ObservableCollection<string>();

        _primaryCommand = new AsyncRelayCommand(ExecutePrimaryActionAsync, CanExecutePrimaryAction);
        _checkForUpdatesCommand = new AsyncRelayCommand(RunManagedRepoUpdateCheckAsync, CanRunManagedRepoUpdateCheck);
        _addOptionalModulesCommand = new RelayCommand(StartAddOptionalModules, CanStartAddOptionalModules);
        _repairRuntimeCommand = new RelayCommand(StartRepairRuntime, CanStartRepairRuntime);
        _openRuntimeToolsCommand = new AsyncRelayCommand(OpenRuntimeToolsAsync, CanRunManagedRuntimeAction);
        _refreshRuntimeStatusCommand = new AsyncRelayCommand(RefreshRuntimeStatusAsync, CanRunManagedRuntimeAction);
        _openZImageTrainerCommand = new AsyncRelayCommand(OpenZImageTrainerAsync, CanRunManagedRuntimeAction);
        _installZImageTrainerCommand = new AsyncRelayCommand(InstallZImageTrainerAsync, CanRunManagedRuntimeAction);
        _refreshZImageTrainerStatusCommand = new AsyncRelayCommand(RefreshZImageTrainerStatusAsync, CanRunManagedRuntimeAction);
        _createZImageTrainerJobCommand = new AsyncRelayCommand(CreateZImageTrainerJobAsync, CanCreateZImageTrainerJob);
        _openZImageTrainerPicturesCommand = new AsyncRelayCommand(OpenZImageTrainerPicturesFolderAsync, CanCreateZImageTrainerJob);
        _openZImageTrainerCaptionsCommand = new AsyncRelayCommand(OpenZImageTrainerCaptionsFileAsync, CanCreateZImageTrainerJob);
        _draftZImageTrainerCaptionsCommand = new AsyncRelayCommand(DraftZImageTrainerCaptionsAsync, CanDraftZImageTrainerCaptions);
        _startZImageTrainingCommand = new AsyncRelayCommand(StartZImageTrainingAsync, CanCreateZImageTrainerJob);
        _stopZImageTrainingCommand = new AsyncRelayCommand(StopZImageTrainingAsync, CanStopZImageTraining);
        _launchZImageTrainerOfficialUiCommand = new AsyncRelayCommand(LaunchZImageTrainerOfficialUiAsync, CanLaunchZImageTrainerUi);
        _killZImageTrainerOfficialUiCommand = new AsyncRelayCommand(KillZImageTrainerOfficialUiAsync, CanLaunchZImageTrainerUi);
        _launchZImageTrainerGradioUiCommand = new AsyncRelayCommand(LaunchZImageTrainerGradioUiAsync, CanLaunchZImageTrainerUi);
        _openZImageTrainerDatasetsCommand = new RelayCommand(OpenZImageTrainerDatasetsFolder, CanOpenZImageTrainerFolders);
        _openZImageTrainerJobsCommand = new RelayCommand(OpenZImageTrainerJobsFolder, CanOpenZImageTrainerFolders);
        _openZImageTrainerLorasCommand = new RelayCommand(OpenZImageTrainerLorasFolder, CanOpenZImageTrainerFolders);
        _openBrainToolsCommand = new AsyncRelayCommand(OpenBrainToolsAsync, CanRunManagedRuntimeAction);
        _refreshBrainStatusCommand = new AsyncRelayCommand(RefreshBrainStatusAsync, CanRunManagedRuntimeAction);
        _fetchModelsNowCommand = new AsyncRelayCommand(RunFetchModelsNowAsync, CanRunManagedRuntimeAction);
        _fetchZImageModelsCommand = new AsyncRelayCommand(() => RunFetchModelsNowAsync("zimage"), CanRunManagedRuntimeAction);
        _fetchTrellisModelsCommand = new AsyncRelayCommand(() => RunFetchModelsNowAsync("trellis"), CanRunManagedRuntimeAction);
        _checkRuntimeDependencyUpdatesCommand = new AsyncRelayCommand(CheckRuntimeDependencyUpdatesAsync, CanRunManagedRuntimeAction);
        _testLatestRuntimeDependenciesCommand = new AsyncRelayCommand(() => ApplyRuntimeDependencyModeAsync("latest"), CanRunManagedRuntimeAction);
        _restorePinnedRuntimeDependenciesCommand = new AsyncRelayCommand(() => ApplyRuntimeDependencyModeAsync("pinned"), CanRunManagedRuntimeAction);
        _repairTrellisAdapterCommand = new AsyncRelayCommand(RunTrellisAdapterRepairAsync, CanRunManagedRuntimeAction);
        _testZImageCommand = new AsyncRelayCommand(() => RunSmokeTestAsync("zimage"), CanRunZImageSmokeTest);
        _testTrellisCommand = new AsyncRelayCommand(() => RunSmokeTestAsync("trellis"), CanRunTrellisSmokeTest);
        _startBrainLlmCommand = new AsyncRelayCommand(StartBrainLlmAsync, CanStartBrainLlm);
        _openBrainWebUiCommand = new AsyncRelayCommand(OpenBrainWebUiAsync, CanOpenBrainWebUi);
        _updateBrainWebUiCommand = new AsyncRelayCommand(UpdateBrainWebUiAsync, CanUpdateBrainWebUi);
        _applyBrainOpenRouterKeyCommand = new AsyncRelayCommand(ApplyBrainOpenRouterKeyAsync, CanApplyBrainOpenRouterKey);
        _changeBrainModelCommand = new RelayCommand(OpenBrainModelManager, CanManageBrainModels);
        _stopBrainLlmCommand = new AsyncRelayCommand(StopBrainLlmAsync, CanStopBrainLlm);
        _backCommand = new RelayCommand(GoBack, CanGoBack);
        _openLogFolderCommand = new RelayCommand(_workflowService.OpenLogFolder);
        _openGuideCommand = new RelayCommand(_workflowService.OpenGuide);
        _brainMonitorRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4),
        };
        _brainMonitorRefreshTimer.Tick += async (_, _) => await RefreshBrainMonitorPanelAsync().ConfigureAwait(true);
        _brainMonitorRefreshTimer.Start();

        AvailableDrives = new ObservableCollection<DriveChoice>(_workflowService.GetAvailableDrives());
        SelectedDrive = AvailableDrives
            .OrderByDescending(drive => drive.FreeBytes)
            .FirstOrDefault();

        InitializeWslConfigChoices();
        InitializeBrainChoices();
        InitializeTrellisGgufQuantChoices();
        LoadSharedSecrets();
        ApplyZImageTrainerPresetDefaults(_zImageTrainerPreset);

        RecomputeStepState();
    }

    public Action? RequestClose { get; set; }

    private void LoadSharedSecrets()
    {
        var secrets = _sharedSecretsService.Load();
        _huggingFaceToken = secrets.HuggingFaceToken?.Trim() ?? string.Empty;
        _brainOpenRouterApiKey = secrets.OpenRouterApiKey?.Trim() ?? string.Empty;
        OnPropertyChanged(nameof(HuggingFaceToken));
        OnPropertyChanged(nameof(HuggingFaceTokenStatus));
        OnPropertyChanged(nameof(BrainOpenRouterApiKey));
        OnPropertyChanged(nameof(HasBrainOpenRouterApiKey));
        OnPropertyChanged(nameof(BrainOpenRouterKeyStatus));
    }

    private void SaveSharedSecrets()
    {
        _sharedSecretsService.Save(
            new SharedSecrets
            {
                HuggingFaceToken = HuggingFaceToken,
                OpenRouterApiKey = BrainOpenRouterApiKey,
            });
    }

    public ObservableCollection<SystemCheckItem> SystemChecks { get; }

    public ObservableCollection<DriveChoice> AvailableDrives { get; }

    public ObservableCollection<string> LogLines { get; }

    public ObservableCollection<WslConfigModeOption> WslConfigOptions { get; }

    public ObservableCollection<BrainModelOption> BrainModelOptions { get; }

    public ObservableCollection<TrellisGgufQuantOption> TrellisGgufQuantOptions { get; }

    public ObservableCollection<string> ExistingZImageTrainerDatasets { get; }

    public AsyncRelayCommand PrimaryCommand => _primaryCommand;

    public RelayCommand BackCommand => _backCommand;

    public AsyncRelayCommand CheckForUpdatesCommand => _checkForUpdatesCommand;

    public RelayCommand AddOptionalModulesCommand => _addOptionalModulesCommand;

    public RelayCommand RepairRuntimeCommand => _repairRuntimeCommand;

    public AsyncRelayCommand OpenRuntimeToolsCommand => _openRuntimeToolsCommand;

    public AsyncRelayCommand RefreshRuntimeStatusCommand => _refreshRuntimeStatusCommand;

    public AsyncRelayCommand OpenZImageTrainerCommand => _openZImageTrainerCommand;

    public AsyncRelayCommand InstallZImageTrainerCommand => _installZImageTrainerCommand;

    public AsyncRelayCommand RefreshZImageTrainerStatusCommand => _refreshZImageTrainerStatusCommand;

    public AsyncRelayCommand CreateZImageTrainerJobCommand => _createZImageTrainerJobCommand;

    public AsyncRelayCommand OpenZImageTrainerPicturesCommand => _openZImageTrainerPicturesCommand;

    public AsyncRelayCommand OpenZImageTrainerCaptionsCommand => _openZImageTrainerCaptionsCommand;

    public AsyncRelayCommand DraftZImageTrainerCaptionsCommand => _draftZImageTrainerCaptionsCommand;

    public AsyncRelayCommand StartZImageTrainingCommand => _startZImageTrainingCommand;

    public AsyncRelayCommand StopZImageTrainingCommand => _stopZImageTrainingCommand;

    public AsyncRelayCommand LaunchZImageTrainerOfficialUiCommand => _launchZImageTrainerOfficialUiCommand;

    public AsyncRelayCommand KillZImageTrainerOfficialUiCommand => _killZImageTrainerOfficialUiCommand;

    public AsyncRelayCommand LaunchZImageTrainerGradioUiCommand => _launchZImageTrainerGradioUiCommand;

    public RelayCommand OpenZImageTrainerDatasetsCommand => _openZImageTrainerDatasetsCommand;

    public RelayCommand OpenZImageTrainerJobsCommand => _openZImageTrainerJobsCommand;

    public RelayCommand OpenZImageTrainerLorasCommand => _openZImageTrainerLorasCommand;

    public AsyncRelayCommand OpenBrainToolsCommand => _openBrainToolsCommand;

    public AsyncRelayCommand RefreshBrainStatusCommand => _refreshBrainStatusCommand;

    public AsyncRelayCommand FetchModelsNowCommand => _fetchModelsNowCommand;

    public AsyncRelayCommand FetchZImageModelsCommand => _fetchZImageModelsCommand;

    public AsyncRelayCommand FetchTrellisModelsCommand => _fetchTrellisModelsCommand;

    public AsyncRelayCommand CheckRuntimeDependencyUpdatesCommand => _checkRuntimeDependencyUpdatesCommand;

    public AsyncRelayCommand TestLatestRuntimeDependenciesCommand => _testLatestRuntimeDependenciesCommand;

    public AsyncRelayCommand RestorePinnedRuntimeDependenciesCommand => _restorePinnedRuntimeDependenciesCommand;

    public AsyncRelayCommand RepairTrellisAdapterCommand => _repairTrellisAdapterCommand;

    public AsyncRelayCommand TestZImageCommand => _testZImageCommand;

    public AsyncRelayCommand TestTrellisCommand => _testTrellisCommand;

    public AsyncRelayCommand StartBrainLlmCommand => _startBrainLlmCommand;

    public AsyncRelayCommand OpenBrainWebUiCommand => _openBrainWebUiCommand;

    public AsyncRelayCommand UpdateBrainWebUiCommand => _updateBrainWebUiCommand;

    public AsyncRelayCommand ApplyBrainOpenRouterKeyCommand => _applyBrainOpenRouterKeyCommand;

    public RelayCommand ChangeBrainModelCommand => _changeBrainModelCommand;

    public AsyncRelayCommand StopBrainLlmCommand => _stopBrainLlmCommand;

    public System.Windows.Input.ICommand ZImageRuntimeActionCommand => _fetchZImageModelsCommand;

    public System.Windows.Input.ICommand TrellisRuntimeActionCommand => TrellisNeedsRepair
            ? _repairTrellisAdapterCommand
            : _fetchTrellisModelsCommand;

    public RelayCommand OpenLogFolderCommand => _openLogFolderCommand;

    public RelayCommand OpenGuideCommand => _openGuideCommand;

    public string AppTitle => "NymphsCore Manager";

    public string SidebarTitle => "NymphsCore";

    public bool ShowDefaultSidebarArt => !IsBrainToolsStep;

    public bool ShowBrainSidebarArt => IsBrainToolsStep;

    public string StepCounterText => IsToolStep ? "Tools" : $"Step {CurrentStepNumber} of {VisibleTotalSteps}";

    private int VisibleTotalSteps => ManagedDistroDetected ? 6 : 7;

    private int CurrentStepNumber => ManagedDistroDetected && _currentStepIndex >= 3
        ? _currentStepIndex
        : _currentStepIndex + 1;

    public string CurrentStepTitle
    {
        get => _currentStepTitle;
        private set => SetProperty(ref _currentStepTitle, value);
    }

    public string CurrentStepSubtitle
    {
        get => _currentStepSubtitle;
        private set => SetProperty(ref _currentStepSubtitle, value);
    }

    public string PrimaryButtonText
    {
        get => _primaryButtonText;
        private set => SetProperty(ref _primaryButtonText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string FinishSummary
    {
        get => _finishSummary;
        private set => SetProperty(ref _finishSummary, value);
    }

    public string PostInstallActionSummary
    {
        get => _postInstallActionSummary;
        private set
        {
            if (SetProperty(ref _postInstallActionSummary, value))
            {
                OnPropertyChanged(nameof(HasPostInstallActionSummary));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string BaseTarPath => _workflowService.BaseTarPath;

    public string GuideUrl => InstallerWorkflowService.GuideUrl;

    public string LogFolderPath => _workflowService.LogFolderPath;

    public string InstallSessionLogPath => _installSessionLogPath;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ZImageTrainerActivitySummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public bool IsWelcomeStep => _currentStepIndex == 0;

    public bool IsSystemCheckStep => _currentStepIndex == 1;

    public bool IsInstallLocationStep => _currentStepIndex == 2;

    public bool IsRuntimeSetupStep => _currentStepIndex == 3;

    public bool IsProgressStep => _currentStepIndex == 4;

    public bool IsFinishStep => _currentStepIndex == 5;

    public bool IsRuntimeToolsStep => _currentStepIndex == 6;

    public bool IsZImageTrainerStep => _currentStepIndex == 7;

    public bool IsBrainToolsStep => _currentStepIndex == 8;

    private bool IsToolStep => IsRuntimeToolsStep || IsZImageTrainerStep || IsBrainToolsStep;

    public bool ShowFooterPrimaryButton => !IsToolStep;

    public bool HasSystemCheckFailures => SystemChecks.Any(item => item.Status == CheckState.Fail);

    public bool RequiresWslSetup => SystemChecks.Any(item =>
        item.Key == InstallerWorkflowService.WslAvailabilityCheckKey &&
        item.Status == CheckState.Fail);

    public bool InstallSucceeded => _installSucceeded;

    public bool ShowExistingInstallActions => ManagedDistroDetected && IsSystemCheckStep;

    public bool ShowPostInstallActions => InstallSucceeded || ManagedDistroDetected;

    public bool ManagedDistroDetected
    {
        get => _managedDistroDetected;
        private set
        {
            if (SetProperty(ref _managedDistroDetected, value))
            {
                OnPropertyChanged(nameof(ManagedDistroStatusText));
                OnPropertyChanged(nameof(StepCounterText));
                OnPropertyChanged(nameof(ShowExistingInstallActions));
                OnPropertyChanged(nameof(ShowPostInstallActions));
                OnPropertyChanged(nameof(WelcomeHeadline));
                OnPropertyChanged(nameof(WelcomeLead));
                OnPropertyChanged(nameof(ShowFooterPrimaryButton));
                OnPropertyChanged(nameof(ZImageRuntimeActionCommand));
                OnPropertyChanged(nameof(TrellisRuntimeActionCommand));
                RecomputeStepState();
                RaiseCommandStateChanged();
            }
        }
    }

    public DriveChoice? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                OnPropertyChanged(nameof(SelectedInstallPath));
                RaiseCommandStateChanged();
            }
        }
    }

    public string SelectedInstallPath => SelectedDrive?.InstallPath ?? "Choose a drive to continue.";

    // Context-aware Welcome card text. On first runs we show the marketing
    // headline; on reruns against an existing managed install we show a
    // friendly "welcome back" so returning users know the app sees their
    // install and the Continue button is still an explicit one-click action.
    public string WelcomeHeadline =>
        ManagedDistroDetected
            ? "Welcome back"
            : "Install and configure NymphsCore";

    public string WelcomeLead =>
        ManagedDistroDetected
            ? "Your NymphsCore install is already set up on this PC. Click Manage existing install to check for updates, add optional modules (like Nymphs-Brain), or repair the runtime."
            : "The central hub for the Nymph Nerds game development backend. NymphsCore is the Brains behind the system";


    public bool PrefetchModelsNow
    {
        get => _prefetchModelsNow;
        set
        {
            if (SetProperty(ref _prefetchModelsNow, value))
            {
                OnPropertyChanged(nameof(ModelDownloadDecisionTitle));
                OnPropertyChanged(nameof(ModelDownloadDecisionSummary));
                OnPropertyChanged(nameof(ModelDownloadDecisionDetails));
                OnPropertyChanged(nameof(RuntimeDownloadSummary));
                OnPropertyChanged(nameof(RuntimeDownloadDetailsText));
                RaiseCommandStateChanged();
            }
        }
    }

    public string HuggingFaceToken
    {
        get => _huggingFaceToken;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _huggingFaceToken, normalized))
            {
                SaveSharedSecrets();
                OnPropertyChanged(nameof(HuggingFaceTokenStatus));
            }
        }
    }

    public string HuggingFaceTokenStatus =>
        string.IsNullOrWhiteSpace(HuggingFaceToken)
            ? "No Hugging Face token entered. Installer downloads will use anonymous access."
            : "Hugging Face token saved for Manager downloads and addon-launched local runtimes.";

    public TrellisGgufQuantOption? SelectedTrellisGgufQuantOption
    {
        get => _selectedTrellisGgufQuantOption;
        set
        {
            if (SetProperty(ref _selectedTrellisGgufQuantOption, value))
            {
                OnPropertyChanged(nameof(TrellisGgufQuantSummary));
                OnPropertyChanged(nameof(ModelDownloadDecisionSummary));
                OnPropertyChanged(nameof(ModelDownloadDecisionDetails));
                OnPropertyChanged(nameof(TrellisGgufQuantLabel));
                RaiseCommandStateChanged();
            }
        }
    }

    public string TrellisGgufQuant => SelectedTrellisGgufQuantOption?.Value ?? "all";

    public string TrellisGgufQuantSummary
    {
        get
        {
            var detected = _detectedGpuVramMb > 0
                ? $"Detected GPU VRAM: {_detectedGpuVramMb / 1024.0:F1} GB. "
                : "GPU VRAM was not detected, so the balanced default was selected. ";
            var option = SelectedTrellisGgufQuantOption;
            return option is null
                ? $"{detected}TRELLIS GGUF will fetch all quants unless changed."
                : $"{detected}Selected TRELLIS GGUF download: {option.Title}. {option.Description}";
        }
    }

    public string ModelDownloadDecisionTitle =>
        PrefetchModelsNow ? "Extra model downloads during install" : "Model downloads deferred until later";

    public string ModelDownloadDecisionSummary =>
        PrefetchModelsNow
            ? $"With model prefetch turned on, the installer downloads the required model and helper files now. TRELLIS.2 GGUF will prefetch {TrellisGgufQuantLabel}. This is the smoothest option for non-technical users, but it can still add a long multi-GB download stage on a typical home connection."
            : "With model prefetch turned off, the installer skips the large model and helper downloads for now. The manager or Blender addon will need to download these later on first real use, which can make first launch feel very slow or look stuck.";

    public string TrellisGgufQuantLabel => TrellisGgufQuant == "all" ? "all GGUF quants" : TrellisGgufQuant;

    public string ModelDownloadDecisionDetails => $"{ModelDownloadDetails}\n- TRELLIS.2 GGUF download selected: {TrellisGgufQuantLabel}";

    public string RuntimeDownloadSummary =>
        PrefetchModelsNow
            ? "The installer also prepares the runtime stack now. That work happens either way and is separate from the large model downloads. TRELLIS setup may include a long Flash Attention build."
            : "The installer still has to prepare the runtime stack now, even with model prefetch turned off. Turning prefetch off only skips the large Hugging Face model downloads. TRELLIS setup may still include a long Flash Attention build.";

    public string RuntimeDownloadDetailsText => RuntimeDownloadDetails;

    public bool InstallNymphsBrain
    {
        get => _installNymphsBrain;
        set
        {
            if (SetProperty(ref _installNymphsBrain, value))
            {
                OnPropertyChanged(nameof(BrainInstallSummary));
                OnPropertyChanged(nameof(BrainOptionsEnabled));
                RaiseCommandStateChanged();
            }
        }
    }

    public bool BrainOptionsEnabled => InstallNymphsBrain;

    public bool InstallZImageTrainer
    {
        get => _installZImageTrainer;
        set
        {
            if (SetProperty(ref _installZImageTrainer, value))
            {
                OnPropertyChanged(nameof(ZImageTrainerInstallSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public string ZImageTrainerInstallSummary =>
        InstallZImageTrainer
            ? "Z-Image Trainer will install as a separate AI Toolkit sidecar at /home/nymph/ZImage-Trainer and keep datasets, jobs, and LoRAs together under that folder."
            : "Z-Image Trainer is optional and can be installed later from the Z-Image Trainer page.";

    public bool DownloadBrainModelNow
    {
        get => _downloadBrainModelNow;
        set
        {
            if (SetProperty(ref _downloadBrainModelNow, value))
            {
                OnPropertyChanged(nameof(BrainInstallSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public BrainModelOption? SelectedBrainModelOption
    {
        get => _selectedBrainModelOption;
        set
        {
            if (SetProperty(ref _selectedBrainModelOption, value))
            {
                if (value is not null && !value.IsCustom)
                {
                    BrainContextLength = value.ContextLength;
                }

                OnPropertyChanged(nameof(IsCustomBrainModel));
                OnPropertyChanged(nameof(BrainSelectedModelDescription));
                OnPropertyChanged(nameof(BrainInstallSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public bool IsCustomBrainModel => SelectedBrainModelOption?.IsCustom == true;

    public string CustomBrainModelId
    {
        get => _customBrainModelId;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _customBrainModelId, normalized))
            {
                OnPropertyChanged(nameof(BrainInstallSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public int BrainContextLength
    {
        get => _brainContextLength;
        set
        {
            if (SetProperty(ref _brainContextLength, value))
            {
                OnPropertyChanged(nameof(BrainInstallSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public string BrainInstallPath => BrainInstallRoot;

    public string BrainSelectedModelDescription =>
        SelectedBrainModelOption?.Description ?? "Choose a model preset for the experimental local LLM stack.";

    public string BrainModelSizeGuide =>
        "- Small / safe: about 1 to 2 GB download, low VRAM footprint\n" +
        "- Balanced coder: about 8 to 10 GB download, better on 16 GB+ VRAM\n" +
        "- High-end coder: about 18 to 22 GB download, aimed at 24 GB+ VRAM\n" +
        "- Large experimental: about 18 to 22 GB download, roomy high-end GPU setups\n" +
        "- Installed Nymphs-Brain stack after setup: often about 20 to 35 GB total depending on model";

    public string BrainInstallSummary
    {
        get
        {
            if (!InstallNymphsBrain)
            {
                return "Nymphs-Brain is optional and will not be installed. Core Blender/backend features are unaffected.";
            }

            return $"Experimental Nymphs-Brain will install to {BrainInstallRoot}. Local model and remote llm-wrapper selection happen afterward from the Brain page with Manage Models.";
        }
    }

    public string RecommendedWslConfigSummary
    {
        get => _recommendedWslConfigSummary;
        private set => SetProperty(ref _recommendedWslConfigSummary, value);
    }

    public string CurrentWslConfigSummary
    {
        get => _currentWslConfigSummary;
        private set => SetProperty(ref _currentWslConfigSummary, value);
    }

    public bool HasExistingWslConfig
    {
        get => _hasExistingWslConfig;
        private set
        {
            if (SetProperty(ref _hasExistingWslConfig, value))
            {
                OnPropertyChanged(nameof(ShowCurrentWslConfigSummary));
            }
        }
    }

    public bool ShowCurrentWslConfigSummary => HasExistingWslConfig;

    public WslConfigModeOption? SelectedWslConfigOption
    {
        get => _selectedWslConfigOption;
        set
        {
            if (SetProperty(ref _selectedWslConfigOption, value))
            {
                OnPropertyChanged(nameof(IsCustomWslConfigMode));
                OnPropertyChanged(nameof(WslConfigChoiceSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public bool IsCustomWslConfigMode => SelectedWslConfigOption?.Mode == WslConfigMode.Custom;

    public int WslCustomMemoryGb
    {
        get => _wslCustomMemoryGb;
        set
        {
            if (SetProperty(ref _wslCustomMemoryGb, value))
            {
                OnPropertyChanged(nameof(WslConfigChoiceSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public int WslCustomProcessors
    {
        get => _wslCustomProcessors;
        set
        {
            if (SetProperty(ref _wslCustomProcessors, value))
            {
                OnPropertyChanged(nameof(WslConfigChoiceSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public int WslCustomSwapGb
    {
        get => _wslCustomSwapGb;
        set
        {
            if (SetProperty(ref _wslCustomSwapGb, value))
            {
                OnPropertyChanged(nameof(WslConfigChoiceSummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public string WslConfigChoiceSummary =>
        SelectedWslConfigOption?.Mode switch
        {
            WslConfigMode.KeepExisting => "The installer will leave your current .wslconfig in place.",
            WslConfigMode.Custom => $"Custom values to write: memory={WslCustomMemoryGb}GB, processors={WslCustomProcessors}, swap={WslCustomSwapGb}GB.",
            _ => RecommendedWslConfigSummary,
        };

    public string ManagedDistroStatusText =>
        ManagedDistroDetected
            ? $"Existing managed {_managedDistroName} distro detected. The manager will reuse it in place, keep your downloads where possible, and repair the runtime instead of creating a second install."
            : string.Empty;

    public string UpdateCheckSummary
    {
        get => _updateCheckSummary;
        private set
        {
            if (SetProperty(ref _updateCheckSummary, value))
            {
                OnPropertyChanged(nameof(HasUpdateCheckSummary));
            }
        }
    }

    public bool HasUpdateCheckSummary => !string.IsNullOrWhiteSpace(UpdateCheckSummary);

    public bool HasPostInstallActionSummary => !string.IsNullOrWhiteSpace(PostInstallActionSummary);

    public string RuntimeToolsSummary
    {
        get => _runtimeToolsSummary;
        private set => SetProperty(ref _runtimeToolsSummary, value);
    }

    public string BrainRuntimeStatusText
    {
        get => _brainRuntimeStatusText;
        private set => SetProperty(ref _brainRuntimeStatusText, value);
    }

    public string BrainRuntimeModelText
    {
        get => _brainRuntimeModelText;
        private set => SetProperty(ref _brainRuntimeModelText, value);
    }

    public string BrainOpenRouterApiKey
    {
        get => _brainOpenRouterApiKey;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _brainOpenRouterApiKey, normalized))
            {
                SaveSharedSecrets();
                OnPropertyChanged(nameof(HasBrainOpenRouterApiKey));
                OnPropertyChanged(nameof(BrainOpenRouterKeyStatus));
                OnPropertyChanged(nameof(BrainOpenRouterStatusLabel));
                OnPropertyChanged(nameof(BrainOpenRouterStatusBackground));
                OnPropertyChanged(nameof(BrainOpenRouterDetailText));
                OnPropertyChanged(nameof(BrainOpenRouterKeySimpleStatus));
                OnPropertyChanged(nameof(BrainRemoteModelText));
                OnPropertyChanged(nameof(BrainRemoteModelLine));
                RaiseCommandStateChanged();
            }
        }
    }

    public bool HasBrainOpenRouterApiKey => !string.IsNullOrWhiteSpace(BrainOpenRouterApiKey);

    public string BrainOpenRouterKeyStatus =>
        HasBrainOpenRouterApiKey
            ? "OpenRouter key saved for the Manager, Brain tools, and the Blender addon. Apply Key refreshes the Brain secret file now."
            : "OpenRouter key is optional. Without one, Brain skips llm-wrapper and keeps the rest of the stack running normally.";

    public string BrainOpenRouterKeySimpleStatus => HasBrainOpenRouterApiKey ? "Saved" : "Not set";

    public string BrainOpenRouterStatusLabel => HasBrainOpenRouterApiKey ? "Configured" : "Optional";

    public string BrainOpenRouterStatusBackground => HasBrainOpenRouterApiKey ? "#235756" : "#6B6259";

    public string BrainOpenRouterDetailText
    {
        get
        {
            if (!HasBrainOpenRouterApiKey)
            {
                return "No key saved.";
            }

            return HasUsableBrainRoleModel(_brainRemoteModel)
                ? $"Remote: {_brainRemoteModel}"
                : "Key saved.";
        }
    }

    public string BrainRemoteModelText => HasUsableBrainRoleModel(_brainRemoteModel)
        ? _brainRemoteModel
        : "Not set";

    public string BrainLocalModelText => HasReportedBrainModel()
        ? _brainLoadedModel
        : "Not set";

    public string BrainRemoteModelLine => $"Remote model: {BrainRemoteModelText}";

    public string BrainHeaderBadgeText => CapitalizeStatus(_brainInstallState);

    public string BrainHeaderBadgeBackground => GetBrainStatusBadgeBackground(_brainInstallState);

    public string BrainDashboardSummary => IsBrainInstalled
        ? "Local coding model, MCP gateway, WebUI, and optional OpenRouter tool bridge for Cline and Brain workflows."
        : "Install Nymphs-Brain to unlock the local coding model, MCP gateway, browser UI, and optional OpenRouter tools.";

    public string BrainPrimaryActionText => IsAnyBrainServiceRunning
        ? "Stop Brain"
        : "Start Brain";

    public string BrainWebUiActionText => IsBrainWebUiRunning ? "Stop WebUI" : "Start WebUI";

    public string BrainEndpointsText => IsBrainInstalled
        ? "LLM: http://localhost:8000/v1   MCP: http://localhost:8100   WebUI: http://localhost:8081"
        : "Endpoints become available after the optional Brain module is installed.";

    public string BrainLlmStatusLabel => CapitalizeStatus(_brainLlmState);

    public string BrainLlmStatusBackground => GetBrainStatusBadgeBackground(_brainLlmState);

    public string BrainLlmDetailText => IsBrainLlmRunning
        ? IsBrainChatModelLoaded
            ? "Local endpoint ready."
            : "Server running; no chat model."
        : "Local endpoint stopped.";

    public string BrainMcpStatusLabel => CapitalizeStatus(_brainMcpState);

    public string BrainMcpStatusBackground => GetBrainStatusBadgeBackground(_brainMcpState);

    public string BrainMcpDetailText => IsBrainMcpRunning
        ? "Gateway ready."
        : "Gateway stopped.";

    public string BrainWebUiStatusLabel => CapitalizeStatus(_brainWebUiState);

    public string BrainWebUiStatusBackground => GetBrainStatusBadgeBackground(_brainWebUiState);

    public string BrainWebUiDetailText => IsBrainWebUiRunning
        ? "Browser UI ready."
        : "Browser UI stopped.";

    public string BrainModelStatusLabel => !IsBrainInstalled
        ? "Missing"
        : IsBrainLlmRunning && IsBrainChatModelLoaded
            ? "Loaded"
        : HasReportedBrainModel()
            ? "Configured"
        : HasLoadedEmbeddingOnly()
            ? "No Chat Model"
            : "Not Set";

    public string BrainModelStatusBackground => !IsBrainInstalled
        ? "#B74322"
        : IsBrainChatModelLoaded || HasReportedBrainModel()
            ? "#235756"
            : HasLoadedEmbeddingOnly()
                ? "#B7791F"
                : "#6B6259";

    public string BrainModelDetailText => !IsBrainInstalled
        ? "Brain module missing."
        : IsBrainLlmRunning && IsBrainChatModelLoaded
            ? BuildBrainModelDetailText()
            : HasReportedBrainModel()
                ? $"Local: {_brainLoadedModel}"
            : HasLoadedEmbeddingOnly()
                ? "Embedding model only."
                : "No local model set.";

    public string BrainMonitorStatusText => _brainMonitorSnapshot.IsRunning ? "RUNNING" : "OFFLINE";

    public string BrainMonitorStatusForeground => _brainMonitorSnapshot.IsRunning ? "#00A000" : "#D13B2F";

    public string BrainMonitorModelText => _brainMonitorSnapshot.Model;

    public string BrainMonitorContextText => _brainMonitorSnapshot.Context;

    public string BrainMonitorGpuVramText => _brainMonitorSnapshot.GpuVram;

    public string BrainMonitorGpuTempText => _brainMonitorSnapshot.GpuTemp;

    public string BrainMonitorTokensPerSecondText => _brainMonitorSnapshot.TokensPerSecond;

    public RuntimeBackendStatus ZImageRuntimeStatus
    {
        get => _zImageRuntimeStatus;
        private set
        {
            if (SetProperty(ref _zImageRuntimeStatus, value))
            {
                OnPropertyChanged(nameof(ZImageTestButtonText));
                OnPropertyChanged(nameof(ZImageFetchButtonText));
                OnPropertyChanged(nameof(ZImageRuntimeActionCommand));
                RaiseCommandStateChanged();
            }
        }
    }

    public RuntimeBackendStatus TrellisRuntimeStatus
    {
        get => _trellisRuntimeStatus;
        private set
        {
            if (SetProperty(ref _trellisRuntimeStatus, value))
            {
                OnPropertyChanged(nameof(TrellisTestButtonText));
                OnPropertyChanged(nameof(TrellisFetchButtonText));
                OnPropertyChanged(nameof(TrellisNeedsRepair));
                OnPropertyChanged(nameof(TrellisRuntimeActionCommand));
                RaiseCommandStateChanged();
            }
        }
    }

    public ZImageTrainerStatus ZImageTrainerStatus
    {
        get => _zImageTrainerStatus;
        private set
        {
            if (SetProperty(ref _zImageTrainerStatus, value))
            {
                OnPropertyChanged(nameof(ZImageTrainerPrimaryButtonText));
                OnPropertyChanged(nameof(ZImageTrainerSummary));
                OnPropertyChanged(nameof(ZImageTrainerActivitySummary));
                RaiseCommandStateChanged();
            }
        }
    }

    public string ZImageTrainerPrimaryButtonText =>
        ZImageTrainerStatus.Installed ? "Repair Trainer" : "Install Trainer";

    public string ZImageTrainerSummary =>
        "Default method: Z-Image Turbo LoRA training with AI Toolkit and the Turbo training adapter. Captions stay under your control in metadata.csv. Trainer datasets, jobs, and LoRAs live under /home/nymph/ZImage-Trainer/.";

    public bool CanStartZImageTrainingUi => CanCreateZImageTrainerJob();

    public bool CanStopZImageTrainingUi => CanStopZImageTraining();

    public string ZImageTrainerActivitySummary =>
        ZImageTrainerStatus.Running || IsBusy
            ? "Training is active or a trainer action is still running. You can keep this page open, use Stop Training, or open the Gradio UI / AI Toolkit without losing the current log."
            : "Trainer is idle. Create or review captions, then start a job when your dataset looks right.";

    public double ZImageTrainerProgressPercent
    {
        get => _zImageTrainerProgressPercent;
        private set => SetProperty(ref _zImageTrainerProgressPercent, Math.Clamp(value, 0, 100));
    }

    public string ZImageTrainerProgressText
    {
        get => _zImageTrainerProgressText;
        private set => SetProperty(ref _zImageTrainerProgressText, value);
    }

    public bool ZImageTrainerProgressVisible
    {
        get => _zImageTrainerProgressVisible;
        private set => SetProperty(ref _zImageTrainerProgressVisible, value);
    }

    public string ZImageTrainerDatasetName
    {
        get => _zImageTrainerDatasetName;
        set
        {
            if (SetProperty(ref _zImageTrainerDatasetName, value))
            {
                _createZImageTrainerJobCommand.RaiseCanExecuteChanged();
                _openZImageTrainerPicturesCommand.RaiseCanExecuteChanged();
                _openZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
                _draftZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
                _startZImageTrainingCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ZImageTrainerLoraName
    {
        get => _zImageTrainerLoraName;
        set
        {
            if (SetProperty(ref _zImageTrainerLoraName, value))
            {
                _createZImageTrainerJobCommand.RaiseCanExecuteChanged();
                _openZImageTrainerPicturesCommand.RaiseCanExecuteChanged();
                _openZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
                _draftZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
                _startZImageTrainingCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ZImageTrainerTrainingType
    {
        get => _zImageTrainerTrainingType;
        set => SetProperty(ref _zImageTrainerTrainingType, string.IsNullOrWhiteSpace(value) ? "character" : value);
    }

    public string ZImageTrainerTrainingAmount
    {
        get => _zImageTrainerTrainingAmount;
        set => SetProperty(ref _zImageTrainerTrainingAmount, string.IsNullOrWhiteSpace(value) ? "normal" : value);
    }

    public string ZImageTrainerPreset
    {
        get => _zImageTrainerPreset;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "basic_turbo" : value;
            if (SetProperty(ref _zImageTrainerPreset, normalized))
            {
                ApplyZImageTrainerPresetDefaults(normalized);
            }
        }
    }

    public int ZImageTrainerSteps
    {
        get => _zImageTrainerSteps;
        set => SetProperty(ref _zImageTrainerSteps, value < 1 ? 1 : value);
    }

    public string ZImageTrainerLearningRate
    {
        get => _zImageTrainerLearningRate;
        set => SetProperty(ref _zImageTrainerLearningRate, string.IsNullOrWhiteSpace(value) ? "1e-4" : value.Trim());
    }

    public int ZImageTrainerRank
    {
        get => _zImageTrainerRank;
        set => SetProperty(ref _zImageTrainerRank, value < 1 ? 1 : value);
    }

    public string ZImageTrainerAdapterVersion
    {
        get => _zImageTrainerAdapterVersion;
        set
        {
            var normalized = string.Equals(value, "v2", StringComparison.OrdinalIgnoreCase)
                ? "v2"
                : "v1";
            SetProperty(ref _zImageTrainerAdapterVersion, normalized);
        }
    }

    public bool ZImageTrainerLowVram
    {
        get => _zImageTrainerLowVram;
        set => SetProperty(ref _zImageTrainerLowVram, value);
    }

    public bool UseZImageTrainerCaptionBrain
    {
        get => _useZImageTrainerCaptionBrain;
        set
        {
            if (SetProperty(ref _useZImageTrainerCaptionBrain, value))
            {
                OnPropertyChanged(nameof(ZImageTrainerCaptionBrainSummary));
                _draftZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ZImageTrainerCaptionMode
    {
        get => _zImageTrainerCaptionMode;
        set
        {
            var normalized = string.Equals(value, "overwrite_all", StringComparison.OrdinalIgnoreCase)
                ? "overwrite_all"
                : "fill_blanks";
            if (SetProperty(ref _zImageTrainerCaptionMode, normalized))
            {
                OnPropertyChanged(nameof(ZImageTrainerCaptionBrainSummary));
            }
        }
    }

    public string ZImageTrainerCaptionBrainSummary =>
        !UseZImageTrainerCaptionBrain
            ? "Turn this on if you want Brain to draft one editable caption per image into metadata.csv."
            : "Caption Brain uses a downloaded Brain vision model to draft metadata.csv rows, then you can review and edit them before training.";

    // Button label is intentionally short ("Test") — the backend name is already
    // the card title above the button, so repeating it just made the label clip
    // inside the narrow 3-column card. When models aren't ready we tell the user
    // to fetch first instead.
    public string ZImageTestButtonText => ZImageRuntimeStatus.ModelsReady ? "Test" : "Fetch First";

    public string TrellisTestButtonText => TrellisNeedsRepair
        ? "Repair First"
        : TrellisRuntimeStatus.ModelsReady
            ? "Test"
            : "Fetch First";

    public string ZImageFetchButtonText => "Fetch";

    public bool TrellisNeedsRepair =>
        !TrellisRuntimeStatus.EnvironmentReady ||
        TrellisRuntimeStatus.Detail.Contains("adapter is missing", StringComparison.OrdinalIgnoreCase) ||
        TrellisRuntimeStatus.Detail.Contains("runtime packages are missing", StringComparison.OrdinalIgnoreCase);

    public string TrellisFetchButtonText => TrellisNeedsRepair ? "Repair" : "Fetch";

    private bool CanGoBack()
    {
        return !IsBusy && _currentStepIndex > 0 && !IsProgressStep;
    }

    private bool CanExecutePrimaryAction()
    {
        if (IsBusy)
        {
            return false;
        }

        return _currentStepIndex switch
        {
            0 => true,
            1 => true,
            2 => SelectedDrive is not null,
            3 => SelectedDrive is not null && HasValidWslConfigSelection() && HasValidBrainSelection(),
            4 => _installCompleted,
            5 => true,
            6 => true,
            7 => true,
            _ => false,
        };
    }

    private bool CanRunManagedRepoUpdateCheck()
    {
        return !IsBusy && ManagedDistroDetected && (IsSystemCheckStep || IsInstallLocationStep || IsRuntimeSetupStep);
    }

    private bool CanRunManagedRuntimeAction()
    {
        return !IsBusy;
    }

    private bool CanOpenZImageTrainerFolders()
    {
        return !IsBusy && ManagedDistroDetected && ZImageTrainerStatus.Installed;
    }

    private bool CanCreateZImageTrainerJob()
    {
        return CanOpenZImageTrainerFolders()
            && !string.IsNullOrWhiteSpace(ZImageTrainerLoraName);
    }

    private bool CanDraftZImageTrainerCaptions()
    {
        return CanCreateZImageTrainerJob();
    }

    private bool CanLaunchZImageTrainerUi()
    {
        return ManagedDistroDetected && ZImageTrainerStatus.Installed;
    }

    private bool CanStopZImageTraining()
    {
        return ManagedDistroDetected && IsZImageTrainerStep && !_zImageTrainerStopRequested && (IsBusy || ZImageTrainerStatus.Running);
    }

    private bool CanStartBrainLlm()
    {
        return CanRunManagedRuntimeAction() && IsBrainInstalled;
    }

    private bool CanOpenBrainWebUi()
    {
        return CanRunManagedRuntimeAction() && IsBrainInstalled;
    }

    private bool CanManageBrainModels()
    {
        return CanRunManagedRuntimeAction() && IsBrainInstalled;
    }

    private bool CanUpdateBrainWebUi()
    {
        return CanRunManagedRuntimeAction() && IsBrainInstalled;
    }

    private bool CanApplyBrainOpenRouterKey()
    {
        return CanRunManagedRuntimeAction() && IsBrainInstalled && HasBrainOpenRouterApiKey;
    }

    private bool CanStopBrainLlm()
    {
        return CanRunManagedRuntimeAction() && IsBrainInstalled && IsBrainLlmRunning;
    }

    private bool CanRunZImageSmokeTest()
    {
        return CanRunManagedRuntimeAction() && ZImageRuntimeStatus.TestReady;
    }

    private bool CanRunTrellisSmokeTest()
    {
        return CanRunManagedRuntimeAction() && TrellisRuntimeStatus.TestReady;
    }

    private async Task ExecutePrimaryActionAsync()
    {
        switch (_currentStepIndex)
        {
            case 0:
                MoveToStep(1);
                await RunSystemChecksAsync().ConfigureAwait(true);
                break;
            case 1:
                if (RequiresWslSetup)
                {
                    await RunWslSetupAsync().ConfigureAwait(true);
                }
                else if (!_systemChecksCompleted || HasSystemCheckFailures)
                {
                    await RunSystemChecksAsync().ConfigureAwait(true);
                }
                else
                {
                    MoveToStep(ManagedDistroDetected ? 3 : 2);
                }
                break;
            case 2:
                MoveToStep(3);
                break;
            case 3:
                await RunInstallAsync().ConfigureAwait(true);
                break;
            case 4:
                MoveToStep(5);
                break;
            case 5:
                if (_installSucceeded)
                {
                    await OpenRuntimeToolsAsync().ConfigureAwait(true);
                }
                else
                {
                    RequestClose?.Invoke();
                }
                break;
            case 6:
            case 7:
                RequestClose?.Invoke();
                break;
        }
    }

    private async Task RunSystemChecksAsync()
    {
        IsBusy = true;
        StatusMessage = "Running system checks...";
        SystemChecks.Clear();
        AppendInstallLog("Running system checks.");

        try
        {
            var checks = await _workflowService.RunSystemChecksAsync(CancellationToken.None).ConfigureAwait(true);
            foreach (var check in checks)
            {
                SystemChecks.Add(check);
                AppendInstallLog($"System check: {check.Title} [{check.StatusLabel}] {check.Details}");
            }

            await DetectExistingManagedDistroAsync(logDetection: true).ConfigureAwait(true);

            _systemChecksCompleted = true;
            StatusMessage = HasSystemCheckFailures
                ? "One or more system checks need attention."
                : "System checks passed.";
            AppendInstallLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
            RecomputeStepState();
        }
    }

    private async Task RunManagedRepoUpdateCheckAsync()
    {
        if (!ManagedDistroDetected || SelectedDrive is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Checking managed repo updates...";
        UpdateCheckSummary = string.Empty;
        _updateCheckCompleted = false;
        _updatesAvailableFromCheck = false;
        AppendInstallLog("Starting managed repo update check.");

        var settings = new InstallSettings
        {
            DistroName = _managedDistroName,
            LinuxUser = InstallerWorkflowService.ManagedLinuxUser,
            TarPath = _workflowService.BaseTarPath,
            InstallLocation = SelectedDrive.InstallPath,
            PrefetchModelsNow = PrefetchModelsNow,
            RepairExistingDistro = true,
            HuggingFaceToken = string.Empty,
            TrellisGgufQuant = TrellisGgufQuant,
        };

        var updateCheckLines = new List<string>();
        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);

                if (ShouldShowUpdateCheckLine(sanitizedLine))
                {
                    updateCheckLines.Add(sanitizedLine);
                }
            }
        });

        try
        {
            await _workflowService.RunManagedRepoUpdateCheckAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = "Managed repo update check completed.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "Managed repo update check failed.";
            updateCheckLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            var presentation = BuildFriendlyUpdateCheckSummary(updateCheckLines);
            UpdateCheckSummary = presentation.Summary;
            _updateCheckCompleted = presentation.CheckCompleted;
            _updatesAvailableFromCheck = presentation.UpdatesAvailable;
            IsBusy = false;
            RecomputeStepState();
        }
    }

    private async Task RunWslSetupAsync()
    {
        IsBusy = true;
        StatusMessage = "Setting up Windows WSL support...";
        AppendInstallLog("Starting guided WSL setup.");

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.BootstrapWslAsync(progress, CancellationToken.None).ConfigureAwait(true);
            AppendInstallLog("Guided WSL setup finished. Running checks again.");
            await RunSystemChecksAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "WSL setup stopped with an error.";
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RecomputeStepState();
        }
    }

    private InstallSettings BuildManagedActionSettings()
    {
        return new InstallSettings
        {
            DistroName = _managedDistroName,
            LinuxUser = InstallerWorkflowService.ManagedLinuxUser,
            TarPath = _workflowService.BaseTarPath,
            InstallLocation = SelectedDrive?.InstallPath ?? string.Empty,
            PrefetchModelsNow = true,
            RepairExistingDistro = true,
            HuggingFaceToken = HuggingFaceToken,
            TrellisGgufQuant = TrellisGgufQuant,
            WslConfigMode = WslConfigMode.KeepExisting,
            BrainInstallRoot = BrainInstallRoot,
            OpenRouterApiKey = BrainOpenRouterApiKey,
        };
    }

    private async Task OpenRuntimeToolsAsync()
    {
        MoveToRuntimeTools();
        await RefreshRuntimeStatusAsync().ConfigureAwait(true);
    }

    private async Task OpenZImageTrainerAsync()
    {
        MoveToZImageTrainer();
        await RefreshZImageTrainerStatusAsync().ConfigureAwait(true);
    }

    private async Task OpenBrainToolsAsync()
    {
        MoveToBrainTools();
        await RefreshBrainStatusAsync().ConfigureAwait(true);
    }

    private async Task RefreshZImageTrainerStatusAsync()
    {
        if (!ManagedDistroDetected && !InstallSucceeded)
        {
            LogLines.Clear();
            PostInstallActionSummary = string.Empty;
            StatusMessage = "No managed runtime install detected yet.";
            ZImageTrainerStatus = ZImageTrainerStatus.Unknown("Run the Manager install first, then install the trainer sidecar.");
            AppendInstallLog(StatusMessage);
            RaiseCommandStateChanged();
            return;
        }

        var settings = BuildManagedActionSettings();
        IsBusy = true;
        LogLines.Clear();
        ResetZImageTrainerProgress();
        PostInstallActionSummary = string.Empty;
        StatusMessage = "Checking Z-Image Trainer sidecar...";
        AppendInstallLog("Starting Z-Image Trainer status check.");

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            await RefreshExistingZImageTrainerDatasetsAsync(settings).ConfigureAwait(true);
            await TryImportZImageTrainerJobSettingsAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = ZImageTrainerStatus.Detail;
            StatusMessage = "Z-Image Trainer status check completed.";
            LogLines.Add(StatusMessage);
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            ZImageTrainerStatus = ZImageTrainerStatus.Unknown("Trainer status check failed.");
            PostInstallActionSummary = "Z-Image Trainer status check failed. Use the log panel and log folder to see what failed.";
            StatusMessage = "Z-Image Trainer status check failed.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task InstallZImageTrainerAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            "Installing Z-Image Trainer sidecar...",
            "Starting Z-Image Trainer install/repair.");
        IsBusy = true;
        ResetZImageTrainerProgress();

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.RunZImageTrainerInstallAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            await RefreshExistingZImageTrainerDatasetsAsync(settings).ConfigureAwait(true);
            await TryImportZImageTrainerJobSettingsAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = "Z-Image Trainer sidecar is installed. Use the generated AI Toolkit template under /home/nymph/ZImage-Trainer/config/ for Z-Image Turbo LoRA jobs.";
            RuntimeToolsSummary = "Z-Image Trainer sidecar installed.";
            StatusMessage = "Z-Image Trainer install completed.";
            LogLines.Add("Z-Image Trainer repair completed successfully.");
            if (ZImageTrainerStatus.OfficialUiRunning)
            {
                LogLines.Add("AI Toolkit detected as running.");
            }

            if (ZImageTrainerStatus.GradioUiRunning)
            {
                LogLines.Add("Gradio UI detected as running.");
            }

            if (!ZImageTrainerStatus.OfficialUiRunning && !ZImageTrainerStatus.GradioUiRunning)
            {
                LogLines.Add("Trainer repair completed successfully. Launch a trainer UI when you're ready.");
            }

            AppendInstallLog(StatusMessage);
            AppendInstallLog("Z-Image Trainer repair completed successfully.");
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "Z-Image Trainer install failed. Check the live log and log folder.";
            StatusMessage = "Z-Image Trainer install failed.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private void OpenZImageTrainerDatasetsFolder()
    {
        OpenZImageTrainerFolder("datasets", settings => _workflowService.OpenZImageTrainerDatasetsFolder(settings));
    }

    private async Task OpenZImageTrainerPicturesFolderAsync()
    {
        var settings = BuildManagedActionSettings();
        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.EnsureZImageTrainerPicturesFolderAsync(
                settings,
                ZImageTrainerLoraName,
                progress,
                CancellationToken.None).ConfigureAwait(true);
            var metadataStatus = await _workflowService.PrepareZImageTrainerMetadataAsync(
                settings,
                ZImageTrainerLoraName,
                progress,
                CancellationToken.None).ConfigureAwait(true);
            _workflowService.OpenZImageTrainerPicturesFolder(settings, ZImageTrainerLoraName);
            StatusMessage = metadataStatus.ImageCount == 0
                ? "Opened pictures folder. Add images, then open the captions file."
                : "Opened pictures folder and refreshed metadata.csv.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open pictures folder.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
    }

    private async Task OpenZImageTrainerCaptionsFileAsync()
    {
        var settings = BuildManagedActionSettings();
        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.PrepareZImageTrainerMetadataAsync(
                settings,
                ZImageTrainerLoraName,
                progress,
                CancellationToken.None).ConfigureAwait(true);
            _workflowService.OpenZImageTrainerMetadataFile(settings, ZImageTrainerLoraName);
            StatusMessage = "Opened metadata.csv for captions.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open captions file.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
    }

    private async Task DraftZImageTrainerCaptionsAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            "Drafting captions with Brain...",
            "Preparing metadata.csv and asking Brain to draft one caption per image.");
        IsBusy = true;
        ResetZImageTrainerProgress();

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            var metadataStatus = await _workflowService.DraftZImageTrainerCaptionsAsync(
                settings,
                ZImageTrainerLoraName,
                "style",
                ZImageTrainerCaptionMode,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            StatusMessage = metadataStatus.MissingCaptionCount == 0
                ? "Caption Brain drafted captions for the dataset. Review metadata.csv, then train when it looks right."
                : $"Caption Brain finished, but {metadataStatus.MissingCaptionCount} caption(s) are still blank. Review metadata.csv.";
            PostInstallActionSummary = "Caption drafts are editable. Open metadata.csv to tweak any line before training.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "Caption Brain could not finish the draft pass. Check the live log for the Brain model or endpoint issue.";
            StatusMessage = "Caption Brain failed.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task CreateZImageTrainerJobAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            "Creating Z-Image Trainer job...",
            "Creating a Z-Image Trainer job and refreshing metadata.csv.");
        IsBusy = true;
        ResetZImageTrainerProgress();

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.CreateZImageTrainerJobAsync(
                settings,
                ZImageTrainerLoraName,
                ZImageTrainerLoraName,
                ZImageTrainerPreset,
                ZImageTrainerAdapterVersion,
                ZImageTrainerSteps,
                ZImageTrainerLearningRate,
                ZImageTrainerRank,
                ZImageTrainerLowVram,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            await TryImportZImageTrainerJobSettingsAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = "Starter job created. Add your own captions in metadata.csv before running training.";
            StatusMessage = "Z-Image Trainer job created.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "Could not create the starter training job.";
            StatusMessage = "Z-Image Trainer job creation failed.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task StartZImageTrainingAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            "Starting Z-Image training...",
            "Refreshing captions metadata and starting Z-Image Trainer.");
        IsBusy = true;
        _zImageTrainerStopRequested = false;
        ResetZImageTrainerProgress(show: true, text: "Preparing AI Toolkit job...");
        RaiseCommandStateChanged();

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                UpdateZImageTrainerProgressFromLine(sanitizedLine);
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            var metadataStatus = await _workflowService.PrepareZImageTrainerMetadataAsync(
                settings,
                ZImageTrainerLoraName,
                progress,
                CancellationToken.None).ConfigureAwait(true);
            if (metadataStatus.ImageCount == 0)
            {
                throw new InvalidOperationException("Add training pictures first.");
            }

            if (metadataStatus.MissingCaptionCount > 0)
            {
                throw new InvalidOperationException(
                    $"Add captions in metadata.csv for all {metadataStatus.ImageCount} image(s) before starting training.");
            }

            await _workflowService.CreateZImageTrainerJobAsync(
                settings,
                ZImageTrainerLoraName,
                ZImageTrainerLoraName,
                ZImageTrainerPreset,
                ZImageTrainerAdapterVersion,
                ZImageTrainerSteps,
                ZImageTrainerLearningRate,
                ZImageTrainerRank,
                ZImageTrainerLowVram,
                progress,
                CancellationToken.None).ConfigureAwait(true);
            await _workflowService.RunZImageTrainerJobAsync(
                settings,
                ZImageTrainerLoraName,
                progress,
                CancellationToken.None).ConfigureAwait(true);
            ZImageTrainerProgressVisible = true;
            ZImageTrainerProgressPercent = 0;
            ZImageTrainerProgressText = "Training job submitted to AI Toolkit.";
            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            PostInstallActionSummary = "Training job submitted to AI Toolkit. Open AI Toolkit for detailed logs or use Stop Training here.";
            StatusMessage = ZImageTrainerStatus.Running
                ? "Z-Image training running in AI Toolkit."
                : "Z-Image training submitted.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "Training could not be queued. Check the latest log.";
            StatusMessage = "Z-Image training failed to start.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            _zImageTrainerStopRequested = false;
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task StopZImageTrainingAsync()
    {
        if (_zImageTrainerStopRequested)
        {
            return;
        }

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            _zImageTrainerStopRequested = true;
            StatusMessage = "Requesting training stop...";
            ZImageTrainerProgressVisible = true;
            ZImageTrainerProgressText = "Stopping training...";
            LogLines.Add(StatusMessage);
            AppendInstallLog(StatusMessage);
            RaiseCommandStateChanged();

            var settings = BuildManagedActionSettings();
            var hadActiveJob = await _workflowService.StopZImageTrainerJobAsync(
                settings,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            if (hadActiveJob)
            {
                ZImageTrainerProgressVisible = true;
                ZImageTrainerProgressText = "Stop requested for the active AI Toolkit job.";
                PostInstallActionSummary = "Stop requested for the active AI Toolkit trainer job.";
                StatusMessage = "Stop requested for Z-Image training.";
                ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                _zImageTrainerStopRequested = false;
                ResetZImageTrainerProgress(false, "No training run in progress yet.");
                PostInstallActionSummary = "No active trainer run was found.";
                StatusMessage = "No active Z-Image training run was found.";
                ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            }
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            _zImageTrainerStopRequested = false;
            StatusMessage = "Could not stop Z-Image training.";
            ZImageTrainerProgressVisible = true;
            ZImageTrainerProgressText = "Could not stop the current training run.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            RaiseCommandStateChanged();
        }
    }

    private async Task LaunchZImageTrainerOfficialUiAsync()
    {
        var settings = BuildManagedActionSettings();
        StatusMessage = "Launching AI Toolkit...";
        AppendInstallLog("Starting AI Toolkit.");

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.StartZImageTrainerOfficialUiAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            // IMPORTANT: recurring regression warning.
            // AI Toolkit launch is timing-sensitive around localhost:8675.
            // The launcher script may succeed before a follow-up TcpClient probe does.
            // Do not convert this back into a hard failure after the launcher already reported success.
            // This wait is only here to reduce browser-open races, not to decide whether launch "worked."
            var officialUiReady = await TryWaitForLocalPortAsync(8675, TimeSpan.FromSeconds(20)).ConfigureAwait(true);
            if (!officialUiReady)
            {
                AppendInstallLog("WARNING: AI Toolkit port wait timed out; opening the browser anyway because this path is timing-sensitive.");
            }
            _workflowService.OpenZImageTrainerOfficialUi();
            StatusMessage = "AI Toolkit launched.";
            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "AI Toolkit failed to launch.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            RaiseCommandStateChanged();
        }
    }

    private async Task KillZImageTrainerOfficialUiAsync()
    {
        var settings = BuildManagedActionSettings();
        StatusMessage = "Killing AI Toolkit...";
        AppendInstallLog("Killing AI Toolkit.");

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.KillZImageTrainerOfficialUiAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = "AI Toolkit killed.";
            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "AI Toolkit kill failed.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            RaiseCommandStateChanged();
        }
    }

    private static async Task<bool> TryWaitForLocalPortAsync(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            using var client = new TcpClient();
            try
            {
                var connectTask = client.ConnectAsync("127.0.0.1", port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(500)).ConfigureAwait(true);
                if (completed == connectTask && client.Connected)
                {
                    return true;
                }
            }
            catch (SocketException)
            {
            }

            await Task.Delay(500).ConfigureAwait(true);
        }
        return false;
    }

    private async Task LaunchZImageTrainerGradioUiAsync()
    {
        var settings = BuildManagedActionSettings();
        StatusMessage = "Launching Gradio UI...";
        AppendInstallLog("Starting Gradio UI.");

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.StartZImageTrainerGradioUiAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            _workflowService.OpenZImageTrainerGradioUi();
            StatusMessage = "Gradio UI launched.";
            ZImageTrainerStatus = await _workflowService.GetZImageTrainerStatusAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "Gradio UI failed to launch.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            RaiseCommandStateChanged();
        }
    }


    private void ResetZImageTrainerProgress(bool show = false, string? text = null)
    {
        _zImageTrainerHasRealStepProgress = false;
        ZImageTrainerProgressPercent = 0;
        ZImageTrainerProgressVisible = show;
        ZImageTrainerProgressText = text ?? "No training run in progress yet.";
    }

    private void UpdateZImageTrainerWarmupPhase(string text, double percent)
    {
        if (_zImageTrainerHasRealStepProgress)
        {
            return;
        }

        ZImageTrainerProgressVisible = true;
        ZImageTrainerProgressPercent = Math.Max(ZImageTrainerProgressPercent, percent);
        ZImageTrainerProgressText = text;
    }

    private async Task TryImportZImageTrainerJobSettingsAsync(InstallSettings settings)
    {
        if (string.IsNullOrWhiteSpace(ZImageTrainerLoraName))
        {
            return;
        }

        var imported = await _workflowService.GetZImageTrainerJobSettingsAsync(
            settings,
            ZImageTrainerLoraName,
            CancellationToken.None).ConfigureAwait(true);

        if (imported is null)
        {
            return;
        }

        ZImageTrainerPreset = imported.PresetId;
        ZImageTrainerSteps = imported.Steps;
        ZImageTrainerLearningRate = imported.LearningRate;
        ZImageTrainerRank = imported.Rank;
        ZImageTrainerLowVram = imported.LowVram;
        // Keep the safer UI default / user choice for adapter version instead of
        // silently re-importing an older experimental v2 job setting here.
        // Existing jobs can still be recreated with the current UI selection.
        AppendInstallLog($"Imported trainer settings from jobs/{imported.LoraName}.yaml.");
    }

    private void UpdateZImageTrainerProgressFromLine(string line)
    {
        var lowerLine = line.ToLowerInvariant();

        var structuredMatch = TrainerStructuredProgressRegex.Match(line);
        if (structuredMatch.Success &&
            int.TryParse(structuredMatch.Groups["current"].Value, out var structuredCurrent) &&
            int.TryParse(structuredMatch.Groups["total"].Value, out var structuredTotal) &&
            structuredTotal > 0 &&
            structuredCurrent <= structuredTotal)
        {
            _zImageTrainerHasRealStepProgress = structuredCurrent > 0;

            if (!_zImageTrainerHasRealStepProgress)
            {
                UpdateZImageTrainerWarmupPhase("Entering training loop...", 14);
                return;
            }

            ZImageTrainerProgressVisible = true;
            ZImageTrainerProgressPercent = structuredCurrent * 100d / structuredTotal;
            ZImageTrainerProgressText = $"Training progress: {structuredCurrent}/{structuredTotal} steps";
            return;
        }

        var expectedTotal = Math.Max(1, ZImageTrainerSteps);
        var stepMatch = TrainerStepProgressRegex.Match(line);
        if (stepMatch.Success &&
            int.TryParse(stepMatch.Groups["current"].Value, out var current) &&
            int.TryParse(stepMatch.Groups["total"].Value, out var total) &&
            total > 0 &&
            current <= total)
        {
            var looksLikeOverallTrainingProgress =
                total == expectedTotal ||
                (expectedTotal >= 100 && Math.Abs(total - expectedTotal) <= Math.Max(5, expectedTotal / 20));

            if (looksLikeOverallTrainingProgress)
            {
                _zImageTrainerHasRealStepProgress = current > 0;

                if (!_zImageTrainerHasRealStepProgress)
                {
                    UpdateZImageTrainerWarmupPhase("Entering training loop...", 14);
                    return;
                }

                ZImageTrainerProgressVisible = true;
                ZImageTrainerProgressPercent = current * 100d / total;
                ZImageTrainerProgressText = $"Training progress: {current}/{total} steps";
                return;
            }
        }

        if (lowerLine.Contains("loading zimage model"))
        {
            UpdateZImageTrainerWarmupPhase("Loading Z-Image model...", 2);
            return;
        }

        if (lowerLine.Contains("loading transformer"))
        {
            UpdateZImageTrainerWarmupPhase("Loading transformer...", 4);
            return;
        }

        if (lowerLine.Contains("loading assistant lora") || lowerLine.Contains("merging in assistant lora"))
        {
            UpdateZImageTrainerWarmupPhase("Loading assistant adapter...", 6);
            return;
        }

        if (lowerLine.Contains("loading vae") || lowerLine.Contains("making pipe") || lowerLine.Contains("preparing model") || lowerLine.Contains("model loaded"))
        {
            UpdateZImageTrainerWarmupPhase("Preparing training pipeline...", 8);
            return;
        }

        if (lowerLine.Contains("preprocessing image dimensions") || lowerLine.Contains("found ") && lowerLine.Contains(" images"))
        {
            UpdateZImageTrainerWarmupPhase("Preparing dataset...", 10);
            return;
        }

        if (lowerLine.Contains("buckets made"))
        {
            UpdateZImageTrainerWarmupPhase("Bucketing dataset...", 12);
            return;
        }

        if (lowerLine.Contains("generating baseline samples before training"))
        {
            UpdateZImageTrainerWarmupPhase("Generating baseline preview before training...", 13);
            return;
        }

        if (lowerLine.Contains("generating samples"))
        {
            UpdateZImageTrainerWarmupPhase("Rendering baseline preview...", 14);
            return;
        }

        if (lowerLine.Contains("skipping first sample due to config setting"))
        {
            UpdateZImageTrainerWarmupPhase("Skipping baseline preview and entering training...", 13);
            return;
        }

        var percentMatch = TrainerPercentRegex.Match(line);
        if (percentMatch.Success &&
            int.TryParse(percentMatch.Groups["percent"].Value, out var percent) &&
            percent is >= 0 and <= 100)
        {
            if (lowerLine.Contains("step") || lowerLine.Contains("train"))
            {
                ZImageTrainerProgressVisible = true;
                ZImageTrainerProgressPercent = percent;
                ZImageTrainerProgressText = $"Training progress: {percent}%";
            }
        }
    }

    private async Task RefreshExistingZImageTrainerDatasetsAsync(InstallSettings settings)
    {
        var datasetNames = await _workflowService.GetZImageTrainerDatasetNamesAsync(settings, CancellationToken.None).ConfigureAwait(true);

        ExistingZImageTrainerDatasets.Clear();
        foreach (var datasetName in datasetNames)
        {
            ExistingZImageTrainerDatasets.Add(datasetName);
        }
    }

    private void OpenZImageTrainerJobsFolder()
    {
        OpenZImageTrainerFolder("jobs", settings => _workflowService.OpenZImageTrainerJobsFolder(settings));
    }

    private void OpenZImageTrainerLorasFolder()
    {
        OpenZImageTrainerFolder("LoRA outputs", settings => _workflowService.OpenZImageTrainerLorasFolder(settings));
    }

    private void OpenZImageTrainerFolder(string label, Action<InstallSettings> openAction)
    {
        try
        {
            var settings = BuildManagedActionSettings();
            openAction(settings);
            StatusMessage = $"Opened Z-Image Trainer {label} folder.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open Z-Image Trainer {label} folder.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
    }

    private void ApplyZImageTrainerPresetDefaults(string presetId)
    {
        switch ((presetId ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "style_light":
                ZImageTrainerTrainingType = "style";
                ZImageTrainerTrainingAmount = "light";
                ZImageTrainerSteps = 2500;
                ZImageTrainerLearningRate = "8e-5";
                ZImageTrainerRank = 8;
                ZImageTrainerLowVram = false;
                break;
            case "style_high_noise":
                ZImageTrainerTrainingType = "style";
                ZImageTrainerTrainingAmount = "strong";
                ZImageTrainerSteps = 3000;
                ZImageTrainerLearningRate = "1e-4";
                ZImageTrainerRank = 16;
                ZImageTrainerLowVram = false;
                break;
            case "style":
            case "style_balanced":
                ZImageTrainerTrainingType = "style";
                ZImageTrainerTrainingAmount = "normal";
                ZImageTrainerSteps = 3000;
                ZImageTrainerLearningRate = "1e-4";
                ZImageTrainerRank = 16;
                ZImageTrainerLowVram = false;
                break;
            default:
                ZImageTrainerTrainingType = "character";
                ZImageTrainerTrainingAmount = "quick";
                ZImageTrainerSteps = 3000;
                ZImageTrainerLearningRate = "1e-4";
                ZImageTrainerRank = 16;
                ZImageTrainerLowVram = false;
                break;
        }
    }

    private async Task RefreshRuntimeStatusAsync()
    {
        if (!ManagedDistroDetected && !InstallSucceeded)
        {
            LogLines.Clear();
            RuntimeToolsSummary = "No managed runtime install is detected yet. Run the installer first, then come back here to check backend readiness, fetch models, and run smoke tests.";
            PostInstallActionSummary = string.Empty;
            StatusMessage = "No managed runtime install detected yet.";
            AppendInstallLog(StatusMessage);
            ApplyRuntimeBackendStatuses(new Dictionary<string, RuntimeBackendStatus>());
            SetBrainRuntimeSnapshot("missing", "stopped", "Not available", null, "stopped", "stopped");
            RaiseCommandStateChanged();
            return;
        }

        var settings = BuildManagedActionSettings();
        IsBusy = true;
        LogLines.Clear();
        RuntimeToolsSummary = "Checking backend readiness...";
        PostInstallActionSummary = string.Empty;
        StatusMessage = "Checking backend readiness...";
        AppendInstallLog("Starting runtime tools status check.");

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (string.IsNullOrWhiteSpace(sanitizedLine))
            {
                return;
            }

            if (!sanitizedLine.StartsWith("backend=", StringComparison.Ordinal))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
            }

            AppendInstallLog(sanitizedLine);
        });

        try
        {
            var statuses = await RefreshCoreRuntimeStatusSnapshotAsync(settings, progress).ConfigureAwait(true);
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            RuntimeToolsSummary = BuildRuntimeToolsSummary(statuses);
            StatusMessage = "Runtime tool status check completed.";
            LogLines.Add(StatusMessage);
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            RuntimeToolsSummary = "Runtime tool status check failed. Use the log panel and log folder to see what failed.";
            SetBrainRuntimeSnapshot("unknown", "unknown", "Not checked", null, "unknown", "unknown");
            StatusMessage = "Runtime tool status check failed.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task RefreshBrainStatusAsync()
    {
        if (!ManagedDistroDetected && !InstallSucceeded)
        {
            LogLines.Clear();
            PostInstallActionSummary = string.Empty;
            StatusMessage = "No managed runtime install detected yet.";
            AppendInstallLog(StatusMessage);
            SetBrainRuntimeSnapshot("missing", "stopped", "Not available", null, "stopped", "stopped");
            RaiseCommandStateChanged();
            return;
        }

        var settings = BuildManagedActionSettings();
        IsBusy = true;
        LogLines.Clear();
        PostInstallActionSummary = string.Empty;
        StatusMessage = "Checking Nymphs-Brain status...";
        AppendInstallLog("Starting Nymphs-Brain status check.");

        try
        {
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            StatusMessage = "Nymphs-Brain status check completed.";
            LogLines.Add(StatusMessage);
            AppendInstallLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private void ApplyRuntimeBackendStatuses(IReadOnlyDictionary<string, RuntimeBackendStatus> statuses)
    {
        ZImageRuntimeStatus = statuses.TryGetValue("zimage", out var zimage)
            ? zimage
            : RuntimeBackendStatus.Unknown("zimage", "Z-Image", "No runtime status was returned.");
        TrellisRuntimeStatus = statuses.TryGetValue("trellis", out var trellis)
            ? trellis
            : RuntimeBackendStatus.Unknown("trellis", "TRELLIS.2", "No runtime status was returned.");
        OnPropertyChanged(nameof(ZImageRuntimeActionCommand));
        OnPropertyChanged(nameof(TrellisRuntimeActionCommand));
    }

    private async Task<IReadOnlyDictionary<string, RuntimeBackendStatus>> RefreshCoreRuntimeStatusSnapshotAsync(
        InstallSettings settings,
        IProgress<string> progress)
    {
        var statuses = await _workflowService.GetRuntimeBackendStatusesAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
        ApplyRuntimeBackendStatuses(statuses);
        AppendInstallLog("Core backend runtime status checked.");
        return statuses;
    }

    private static bool CoreBackendsTestReady(IReadOnlyDictionary<string, RuntimeBackendStatus> statuses)
    {
        return statuses.TryGetValue("zimage", out var zimage)
            && statuses.TryGetValue("trellis", out var trellis)
            && zimage.TestReady
            && trellis.TestReady;
    }

    private async Task RefreshBrainRuntimeStatusSnapshotAsync(InstallSettings settings)
    {
        try
        {
            var output = await _workflowService.GetNymphsBrainStatusAsync(settings, CancellationToken.None).ConfigureAwait(true);
            ApplyBrainRuntimeStatus(output);
            AppendInstallLog("Nymphs-Brain status checked.");
        }
        catch (Exception ex)
        {
            SetBrainRuntimeSnapshot("unknown", "unknown", "Not available", null, "unknown", "unknown");
            AppendInstallLog($"Nymphs-Brain status check warning: {ex.Message}");
        }

        try
        {
            var monitorSnapshot = await _workflowService.GetNymphsBrainMonitorAsync(settings, CancellationToken.None).ConfigureAwait(true);
            ApplyBrainMonitorSnapshot(monitorSnapshot);
        }
        catch (Exception ex)
        {
            ApplyBrainMonitorSnapshot(BrainMonitorSnapshot.Offline);
            AppendInstallLog($"Nymphs-Brain monitor warning: {ex.Message}");
        }
    }

    private void ApplyBrainRuntimeStatus(string statusOutput)
    {
        var lines = statusOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var install = FindStatusValue(lines, "Brain install:") ?? "unknown";
        var llm = FindStatusValue(lines, "llama-server:")
            ?? FindStatusValue(lines, "LLM server:")
            ?? "unknown";
        var model = FindStatusValue(lines, "Model loaded:")
            ?? FindStatusValue(lines, "Model configured:")
            ?? "unknown";
        var remoteModel = FindStatusValue(lines, "Remote llm-wrapper model:");
        var mcp = FindStatusValue(lines, "MCP proxy:") ?? "unknown";
        var webUi = FindStatusValue(lines, "Open WebUI:") ?? "unknown";

        SetBrainRuntimeSnapshot(install, NormalizeBrainServiceState(llm), model, remoteModel, mcp, webUi);
    }

    private static string? FindStatusValue(IEnumerable<string> lines, string prefix)
    {
        var line = lines.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line is null ? null : line[prefix.Length..].Trim();
    }

    private static string CapitalizeStatus(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string NormalizeBrainServiceState(string value)
    {
        var normalized = NormalizeBrainStatus(value);
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

    private static string NormalizeBrainRoleModel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var normalized = value.Trim();
        var contextMarker = " (context ";
        var markerIndex = normalized.IndexOf(contextMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            normalized = normalized[..markerIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static string NormalizeLoadedBrainModel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var normalized = value.Trim();
        var lowered = normalized.ToLowerInvariant();

        if (lowered is "unknown" or "none" or "none reported" or "not checked" or "not available")
        {
            return "unknown";
        }

        return normalized;
    }

    private string BuildBrainModelDetailText()
    {
        var hasRemote = HasUsableBrainRoleModel(_brainRemoteModel);
        var loadedText = HasReportedBrainModel() ? _brainLoadedModel : "unknown";

        return hasRemote
            ? $"Local: {loadedText}\nRemote: {_brainRemoteModel}"
            : $"Local: {loadedText}";
    }

    private static bool HasUsableBrainRoleModel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "unknown" or "none" or "none reported" or "not checked" or "not available" => false,
            _ => true,
        };
    }

    private bool IsBrainInstalled => string.Equals(_brainInstallState, "installed", StringComparison.OrdinalIgnoreCase);

    private bool IsBrainLlmRunning => string.Equals(_brainLlmState, "running", StringComparison.OrdinalIgnoreCase);

    private bool IsBrainChatModelLoaded => IsUsableLoadedBrainChatModel(_brainLoadedModel);

    private bool IsBrainMcpRunning => string.Equals(_brainMcpState, "running", StringComparison.OrdinalIgnoreCase);

    private bool IsBrainWebUiRunning => string.Equals(_brainWebUiState, "running", StringComparison.OrdinalIgnoreCase);

    private bool IsAnyBrainServiceRunning => IsBrainLlmRunning || IsBrainMcpRunning || IsBrainWebUiRunning;

    private bool HasConfiguredBrainModel()
    {
        return HasReportedBrainModel();
    }

    private bool HasLoadedEmbeddingOnly()
    {
        return HasReportedBrainModel() && IsEmbeddingModelName(_brainLoadedModel);
    }

    private static bool IsUsableLoadedBrainChatModel(string? value)
    {
        return HasUsableBrainRoleModel(value) && !IsEmbeddingModelName(value);
    }

    private static bool IsEmbeddingModelName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("embedding", StringComparison.Ordinal) ||
               normalized.Contains("embed", StringComparison.Ordinal);
    }

    private void SetBrainRuntimeSnapshot(string install, string llm, string model, string? remoteModel, string mcp, string webUi)
    {
        _brainInstallState = NormalizeBrainStatus(install);
        _brainLlmState = NormalizeBrainStatus(llm);
        _brainMcpState = NormalizeBrainStatus(mcp);
        _brainWebUiState = NormalizeBrainStatus(webUi);
        _brainLoadedModel = NormalizeLoadedBrainModel(model);
        _brainRemoteModel = NormalizeBrainRoleModel(remoteModel);

        BrainRuntimeStatusText = $"Status: {CapitalizeStatus(_brainInstallState)} / LLM {_brainLlmState} / WebUI {_brainWebUiState} / MCP {_brainMcpState}";
        BrainRuntimeModelText = $"Model: {_brainLoadedModel}";

        RaiseBrainRuntimePropertyChanges();
        RaiseCommandStateChanged();
    }

    private void ApplyBrainMonitorSnapshot(BrainMonitorSnapshot snapshot)
    {
        _brainMonitorSnapshot = snapshot;
        OnPropertyChanged(nameof(BrainMonitorStatusText));
        OnPropertyChanged(nameof(BrainMonitorStatusForeground));
        OnPropertyChanged(nameof(BrainMonitorModelText));
        OnPropertyChanged(nameof(BrainMonitorContextText));
        OnPropertyChanged(nameof(BrainMonitorGpuVramText));
        OnPropertyChanged(nameof(BrainMonitorGpuTempText));
        OnPropertyChanged(nameof(BrainMonitorTokensPerSecondText));
    }

    private async Task RefreshBrainMonitorPanelAsync()
    {
        if (_isRefreshingBrainMonitor || _isBusy || !IsBrainToolsStep || !ManagedDistroDetected)
        {
            return;
        }

        _isRefreshingBrainMonitor = true;
        try
        {
            var settings = BuildManagedActionSettings();
            var snapshot = await _workflowService.GetNymphsBrainMonitorAsync(settings, CancellationToken.None).ConfigureAwait(true);
            ApplyBrainMonitorSnapshot(snapshot);
        }
        catch
        {
            // The full Refresh action reports monitor warnings in the activity log.
        }
        finally
        {
            _isRefreshingBrainMonitor = false;
        }
    }

    private void RaiseBrainRuntimePropertyChanges()
    {
        OnPropertyChanged(nameof(BrainHeaderBadgeText));
        OnPropertyChanged(nameof(BrainHeaderBadgeBackground));
        OnPropertyChanged(nameof(BrainDashboardSummary));
        OnPropertyChanged(nameof(BrainPrimaryActionText));
        OnPropertyChanged(nameof(BrainWebUiActionText));
        OnPropertyChanged(nameof(BrainEndpointsText));
        OnPropertyChanged(nameof(BrainLlmStatusLabel));
        OnPropertyChanged(nameof(BrainLlmStatusBackground));
        OnPropertyChanged(nameof(BrainLlmDetailText));
        OnPropertyChanged(nameof(BrainMcpStatusLabel));
        OnPropertyChanged(nameof(BrainMcpStatusBackground));
        OnPropertyChanged(nameof(BrainMcpDetailText));
        OnPropertyChanged(nameof(BrainWebUiStatusLabel));
        OnPropertyChanged(nameof(BrainWebUiStatusBackground));
        OnPropertyChanged(nameof(BrainWebUiDetailText));
        OnPropertyChanged(nameof(BrainOpenRouterStatusLabel));
        OnPropertyChanged(nameof(BrainOpenRouterStatusBackground));
        OnPropertyChanged(nameof(BrainOpenRouterDetailText));
        OnPropertyChanged(nameof(BrainOpenRouterKeySimpleStatus));
        OnPropertyChanged(nameof(BrainLocalModelText));
        OnPropertyChanged(nameof(BrainRemoteModelText));
        OnPropertyChanged(nameof(BrainRemoteModelLine));
        OnPropertyChanged(nameof(BrainModelStatusLabel));
        OnPropertyChanged(nameof(BrainModelStatusBackground));
        OnPropertyChanged(nameof(BrainModelDetailText));
    }

    private static string NormalizeBrainStatus(string status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "unknown"
            : status.Trim().ToLowerInvariant();
    }

    private static string GetBrainStatusBadgeBackground(string status)
    {
        return NormalizeBrainStatus(status) switch
        {
            "running" or "installed" or "loaded" => "#235756",
            "starting" or "pending" or "warning" => "#B7791F",
            "missing" or "failed" or "error" => "#B74322",
            _ => "#6B6259",
        };
    }

    private bool HasReportedBrainModel()
    {
        if (string.IsNullOrWhiteSpace(_brainLoadedModel))
        {
            return false;
        }

        return _brainLoadedModel.ToLowerInvariant() is not ("unknown" or "none" or "none reported" or "not checked" or "not available");
    }

    public async Task InitializeAsync()
    {
        await DetectExistingManagedDistroAsync(logDetection: false).ConfigureAwait(true);

        // We deliberately do NOT auto-skip Welcome here. When an existing install
        // is detected, the Welcome card re-themes itself (see WelcomeHeadline /
        // WelcomeLead / primary button "Manage existing install") so returning
        // users get an explicit one-click entry into System Check rather than
        // a silent jump that could feel like the app lost their state.
        RecomputeStepState();
        RaiseCommandStateChanged();
    }

    public Task StopZImageTrainingFromUiAsync()
    {
        return StopZImageTrainingAsync();
    }

    private async Task DetectExistingManagedDistroAsync(bool logDetection)
    {
        try
        {
            var existingManagedDistroName = await _workflowService.GetExistingManagedDistroNameAsync(CancellationToken.None).ConfigureAwait(true);
            _managedDistroName = existingManagedDistroName ?? InstallerWorkflowService.ManagedDistroName;
            ManagedDistroDetected = !string.IsNullOrWhiteSpace(existingManagedDistroName);
            OnPropertyChanged(nameof(ManagedDistroStatusText));

            if (ManagedDistroDetected && logDetection)
            {
                AppendInstallLog($"System check note: existing managed {_managedDistroName} distro detected. Check for Updates is available before repair/continue.");
            }
        }
        catch (Exception ex)
        {
            _managedDistroName = InstallerWorkflowService.ManagedDistroName;
            ManagedDistroDetected = false;

            if (logDetection)
            {
                AppendInstallLog($"WARNING: existing managed distro detection failed: {ex.Message}");
            }
        }
    }

    private static string BuildRuntimeToolsSummary(IReadOnlyDictionary<string, RuntimeBackendStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return "No backend status was returned from the managed runtime.";
        }

        var readyCount = statuses.Values.Count(status => status.TestReady);
        var missingModelCount = statuses.Values.Count(status => status.EnvironmentReady && !status.ModelsReady);
        var missingRuntimeCount = statuses.Values.Count(status => !status.EnvironmentReady);
        var needsAttentionCount = statuses.Values.Count(status => status.EnvironmentReady && status.ModelsReady && !status.TestReady);

        if (missingRuntimeCount > 0)
        {
            return "One or more backend runtimes are missing or incomplete. Run repair/update before testing.";
        }

        if (missingModelCount > 0)
        {
            return "Runtime environments are present, but some required models are missing. Fetch models before testing those backends.";
        }

        if (needsAttentionCount > 0)
        {
            return "One or more backend runtimes need attention before smoke testing.";
        }

        if (readyCount > 0)
        {
            return "Core backends look ready for smoke testing.";
        }

        return "Runtime tools are available, but backend readiness could not be confirmed.";
    }

    private async Task RunFetchModelsNowAsync()
    {
        await RunFetchModelsNowAsync("all").ConfigureAwait(true);
    }

    private async Task RunFetchModelsNowAsync(string backend)
    {
        var settings = BuildManagedActionSettings();
        var backendLabel = backend switch
        {
            "zimage" => "Z-Image",
            "trellis" => "TRELLIS.2",
            _ => "required",
        };
        PrepareManagedActionRun(
            $"Fetching {backendLabel} model weights...",
            $"Starting {backendLabel} model-only prefetch against the existing runtime.");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.RunModelPrefetchOnlyAsync(settings, progress, CancellationToken.None, backend).ConfigureAwait(true);
            var statuses = await RefreshCoreRuntimeStatusSnapshotAsync(settings, progress).ConfigureAwait(true);
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);

            if (CoreBackendsTestReady(statuses))
            {
                PostInstallActionSummary = "Required models were downloaded and verified successfully.";
                RuntimeToolsSummary = "Model download completed. Core backends are ready for smoke tests.";
                StatusMessage = "Model prefetch completed and verified successfully.";
            }
            else
            {
                PostInstallActionSummary = "Model prefetch finished, but backend readiness is still incomplete.";
                RuntimeToolsSummary = BuildRuntimeToolsSummary(statuses);
                StatusMessage = "Model prefetch completed, but required models did not verify.";
            }

            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "Model prefetch failed. Check the live log and log folder.";
            RuntimeToolsSummary = "Model download failed. Use the live log and log folder to inspect the failure.";
            StatusMessage = "Model prefetch stopped with an error.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task RunTrellisAdapterRepairAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            "Repairing TRELLIS adapter...",
            "Starting TRELLIS GGUF adapter repair against the existing runtime.");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.RunTrellisAdapterRepairAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            var statuses = await RefreshCoreRuntimeStatusSnapshotAsync(settings, progress).ConfigureAwait(true);
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);

            RuntimeToolsSummary = BuildRuntimeToolsSummary(statuses);
            StatusMessage = TrellisRuntimeStatus.TestReady
                ? "TRELLIS adapter repaired and verified successfully."
                : "TRELLIS adapter repair completed, but backend readiness is still incomplete.";
            PostInstallActionSummary = StatusMessage;
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "TRELLIS adapter repair failed. Check the live log and log folder.";
            RuntimeToolsSummary = "TRELLIS adapter repair failed. Use the live log and log folder to inspect the failure.";
            StatusMessage = "TRELLIS adapter repair stopped with an error.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task CheckRuntimeDependencyUpdatesAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            "Checking runtime code updates...",
            "Comparing release-tested runtime source and packages against upstream.");
        IsBusy = true;

        var updatesAvailable = false;
        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                if (sanitizedLine.Contains("update available", StringComparison.OrdinalIgnoreCase))
                {
                    updatesAvailable = true;
                }

                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.CheckRuntimeDependencyUpdatesAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            PostInstallActionSummary = updatesAvailable
                ? "Runtime code updates are available. Use this as your dev test list before moving release pins."
                : "Runtime code pins are up to date.";
            RuntimeToolsSummary = PostInstallActionSummary;
            StatusMessage = "Runtime code update check completed.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = "Runtime code update check failed. Check the live log and log folder.";
            RuntimeToolsSummary = "Runtime code update check failed.";
            StatusMessage = "Runtime code update check stopped with an error.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task ApplyRuntimeDependencyModeAsync(string mode)
    {
        var latestMode = string.Equals(mode, "latest", StringComparison.OrdinalIgnoreCase);
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(
            latestMode ? "Applying latest runtime code..." : "Restoring release-tested runtime code...",
            latestMode
                ? "Applying latest upstream runtime source and packages to this dev runtime."
                : "Restoring the release-tested runtime source and packages.");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.ApplyRuntimeDependencyModeAsync(settings, mode, progress, CancellationToken.None).ConfigureAwait(true);
            var statuses = await RefreshCoreRuntimeStatusSnapshotAsync(settings, progress).ConfigureAwait(true);
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);

            PostInstallActionSummary = latestMode
                ? "Latest runtime code was applied to this dev runtime. Run smoke tests before moving release pins."
                : "Release-tested runtime code was restored.";
            RuntimeToolsSummary = CoreBackendsTestReady(statuses)
                ? PostInstallActionSummary
                : BuildRuntimeToolsSummary(statuses);
            StatusMessage = PostInstallActionSummary;
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = latestMode
                ? "Applying latest runtime code failed. Restore release-tested code if the runtime is now unstable."
                : "Restoring release-tested runtime code failed. Check the live log and log folder.";
            RuntimeToolsSummary = PostInstallActionSummary;
            StatusMessage = "Runtime code action stopped with an error.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task RunSmokeTestAsync(string backend)
    {
        var settings = BuildManagedActionSettings();
        var backendLabel = backend switch
        {
            "zimage" => "Z-Image",
            "trellis" => "TRELLIS.2",
            _ => backend,
        };

        PrepareManagedActionRun(
            $"Running {backendLabel} smoke test...",
            $"Starting {backendLabel} smoke test.");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.RunSmokeTestAsync(settings, backend, progress, CancellationToken.None).ConfigureAwait(true);
            PostInstallActionSummary = $"{backendLabel} smoke test passed.";
            RuntimeToolsSummary = $"{backendLabel} smoke test passed.";
            StatusMessage = $"{backendLabel} smoke test completed successfully.";
            AppendInstallLog(StatusMessage);
        }
        catch (Exception ex)
        {
            PostInstallActionSummary = $"{backendLabel} smoke test failed. Check the live log and log folder.";
            RuntimeToolsSummary = $"{backendLabel} smoke test failed. Use the live log and log folder to inspect the failure.";
            StatusMessage = $"{backendLabel} smoke test stopped with an error.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task StartBrainLlmAsync()
    {
        EnsureBrainToolsActive();

        if (IsAnyBrainServiceRunning)
        {
            await StopBrainServicesAsync().ConfigureAwait(true);
            return;
        }

        await RunNymphsBrainToolSequenceAsync(
            ["lms-start", "mcp-start"],
            "Starting Nymphs-Brain services...",
            "Nymphs-Brain services started.",
            "Nymphs-Brain services failed to start.").ConfigureAwait(true);
    }

    private async Task OpenBrainWebUiAsync()
    {
        EnsureBrainToolsActive();

        if (IsBrainWebUiRunning)
        {
            await RunNymphsBrainToolActionAsync(
                "open-webui-stop",
                "Stopping Nymphs-Brain Open WebUI...",
                "Open WebUI stopped.",
                "Open WebUI failed to stop cleanly.").ConfigureAwait(true);
            return;
        }

        var started = await RunNymphsBrainToolActionAsync(
                "open-webui-start",
                "Starting Nymphs-Brain Open WebUI...",
                "Open WebUI is ready.",
                "Open WebUI failed to start.").ConfigureAwait(true);

        if (started)
        {
            _workflowService.OpenNymphsBrainWebUi();
        }
    }

    private void OpenBrainModelManager()
    {
        EnsureBrainToolsActive();
        var settings = BuildManagedActionSettings();
        try
        {
            _workflowService.OpenNymphsBrainModelManager(settings);
            StatusMessage = "Opened Nymphs-Brain model manager in a terminal.";
            PostInstallActionSummary = "Use the terminal window to download, switch, or remove Brain models. When you finish there, click Brain in the left sidebar again to reload the selected local model and remote llm-wrapper settings.";
            AppendInstallLog("Opened Nymphs-Brain model manager terminal.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open Nymphs-Brain model manager.";
            PostInstallActionSummary = "Could not open the model manager terminal.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
        }
    }

    private async Task UpdateBrainWebUiAsync()
    {
        EnsureBrainToolsActive();
        await RunNymphsBrainToolSequenceAsync(
            ["brain-refresh", "open-webui-update"],
            "Updating Nymphs-Brain stack...",
            "Nymphs-Brain stack update completed.",
            "Nymphs-Brain stack update failed.").ConfigureAwait(true);
    }

    private async Task ApplyBrainOpenRouterKeyAsync()
    {
        EnsureBrainToolsActive();
        await RunNymphsBrainToolActionAsync(
            "brain-apply-openrouter-key",
            "Applying OpenRouter key to Nymphs-Brain...",
            "OpenRouter key applied to the Nymphs-Brain secret file.",
            "OpenRouter key update failed.").ConfigureAwait(true);
    }

    private async Task StopBrainLlmAsync()
    {
        EnsureBrainToolsActive();
        await StopBrainServicesAsync().ConfigureAwait(true);
    }

    private async Task<bool> StopBrainServicesAsync()
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun("Stopping Nymphs-Brain services...", "Stopping Nymphs-Brain services...");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.StopNymphsBrainServicesAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = "Nymphs-Brain services stopped.";
            RuntimeToolsSummary = "Nymphs-Brain services stopped.";
            StatusMessage = "Nymphs-Brain services stopped.";
            AppendInstallLog("Nymphs-Brain services stopped.");
            return true;
        }
        catch (Exception ex)
        {
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = "Nymphs-Brain stop hit a timeout or error. Forced cleanup was attempted; check status before starting again.";
            RuntimeToolsSummary = "Nymphs-Brain stop did not complete cleanly.";
            StatusMessage = "Nymphs-Brain stop did not complete cleanly.";
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
            return false;
        }
        finally
        {
            EnsureBrainToolsActive();
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task<bool> RunNymphsBrainToolSequenceAsync(
        IReadOnlyList<string> toolNames,
        string runningSummary,
        string successSummary,
        string failureSummary)
    {
        var tools = toolNames
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToList();

        if (tools.Count == 0)
        {
            PostInstallActionSummary = successSummary;
            StatusMessage = successSummary;
            AppendInstallLog(successSummary);
            return true;
        }

        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(runningSummary, $"Starting Nymphs-Brain tool sequence: {string.Join(", ", tools)}");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            foreach (var toolName in tools)
            {
                await _workflowService.RunNymphsBrainToolAsync(settings, toolName, progress, CancellationToken.None).ConfigureAwait(true);
            }

            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = successSummary;
            RuntimeToolsSummary = successSummary;
            StatusMessage = successSummary;
            LogLines.Add(successSummary);
            AppendInstallLog(successSummary);
            return true;
        }
        catch (Exception ex)
        {
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = $"{failureSummary} Check the live log and log folder.";
            RuntimeToolsSummary = failureSummary;
            StatusMessage = failureSummary;
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
            return false;
        }
        finally
        {
            EnsureBrainToolsActive();
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private async Task<bool> RunNymphsBrainToolActionAsync(
        string toolName,
        string runningSummary,
        string successSummary,
        string failureSummary)
    {
        var settings = BuildManagedActionSettings();
        PrepareManagedActionRun(runningSummary, $"Starting Nymphs-Brain tool: {toolName}");
        IsBusy = true;

        var progress = new Progress<string>(line =>
        {
            var sanitizedLine = line.Replace("\0", string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitizedLine))
            {
                LogLines.Add(sanitizedLine);
                StatusMessage = sanitizedLine;
                AppendInstallLog(sanitizedLine);
            }
        });

        try
        {
            await _workflowService.RunNymphsBrainToolAsync(settings, toolName, progress, CancellationToken.None).ConfigureAwait(true);
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = successSummary;
            RuntimeToolsSummary = successSummary;
            StatusMessage = successSummary;
            AppendInstallLog(successSummary);
            return true;
        }
        catch (Exception ex)
        {
            await RefreshBrainRuntimeStatusSnapshotAsync(settings).ConfigureAwait(true);
            PostInstallActionSummary = $"{failureSummary} Check the live log and log folder.";
            RuntimeToolsSummary = failureSummary;
            StatusMessage = failureSummary;
            LogLines.Add($"ERROR: {ex.Message}");
            AppendInstallLog($"ERROR: {ex}");
            return false;
        }
        finally
        {
            EnsureBrainToolsActive();
            IsBusy = false;
            RaiseCommandStateChanged();
        }
    }

    private void EnsureBrainToolsActive()
    {
        if (!IsBrainToolsStep)
        {
            MoveToBrainTools();
        }
    }

    private void PrepareManagedActionRun(string summary, string initialLogLine)
    {
        LogLines.Clear();
        StatusMessage = summary;
        PostInstallActionSummary = summary;
        AppendInstallLog(initialLogLine);
        if (!IsToolStep)
        {
            MoveToStep(6);
        }
    }

    private async Task RunInstallAsync()
    {
        if (SelectedDrive is null)
        {
            return;
        }

        MoveToStep(4);
        LogLines.Clear();
        FinishSummary = string.Empty;
        PostInstallActionSummary = string.Empty;
        _installCompleted = false;
        _installSucceeded = false;
        _lastRunUsedExistingInstall = false;
        _lastRunAppliedUpdates = false;
        ProgressValue = 5;
        StatusMessage = "Starting install...";
        IsBusy = true;
        AppendInstallLog("Starting install.");

        try
        {
            var existingManagedDistroName = await _workflowService.GetExistingManagedDistroNameAsync(CancellationToken.None).ConfigureAwait(true);
            var repairExistingDistro = !string.IsNullOrWhiteSpace(existingManagedDistroName);
            var settings = new InstallSettings
            {
                DistroName = existingManagedDistroName ?? InstallerWorkflowService.ManagedDistroName,
                LinuxUser = InstallerWorkflowService.ManagedLinuxUser,
                TarPath = _workflowService.BaseTarPath,
                InstallLocation = SelectedDrive.InstallPath,
                PrefetchModelsNow = PrefetchModelsNow,
                RepairExistingDistro = repairExistingDistro,
                HuggingFaceToken = HuggingFaceToken,
                TrellisGgufQuant = TrellisGgufQuant,
                WslConfigMode = SelectedWslConfigOption?.Mode ?? WslConfigMode.Recommended,
                WslMemoryGb = WslCustomMemoryGb,
                WslProcessors = WslCustomProcessors,
                WslSwapGb = WslCustomSwapGb,
                InstallNymphsBrain = InstallNymphsBrain,
                InstallZImageTrainer = InstallZImageTrainer,
                DownloadBrainModelNow = false,
                BrainInstallRoot = BrainInstallRoot,
                BrainModelId = "auto",
                BrainQuantization = "q4_k_m",
                BrainContextLength = 16384,
                OpenRouterApiKey = BrainOpenRouterApiKey,
                ModuleOnlyRun = _moduleOnlyRun,
            };

            if (settings.ModuleOnlyRun)
            {
                _lastRunUsedExistingInstall = true;
                AppendInstallLog($"Add optional modules: running against existing {settings.DistroName} distro without a full repair pass.");
            }
            else if (settings.RepairExistingDistro)
            {
                _lastRunUsedExistingInstall = true;
                _lastRunAppliedUpdates = _updateCheckCompleted && _updatesAvailableFromCheck;
                AppendInstallLog($"Existing managed {settings.DistroName} distro detected. The installer will repair/continue that distro in place.");
            }
            else
            {
                AppendInstallLog($"Install location: {settings.InstallLocation}");
            }

            AppendInstallLog($"Linux user: {settings.LinuxUser}");
            AppendInstallLog(
                _workflowService.BaseTarAvailable
                    ? $"Legacy base image detected at {settings.TarPath}"
                    : "Local Ubuntu bootstrap mode selected.");
            AppendInstallLog(
                string.IsNullOrWhiteSpace(settings.HuggingFaceToken)
                    ? "Hugging Face token: not provided"
                    : "Hugging Face token: provided");
            AppendInstallLog(
                settings.PrefetchModelsNow
                    ? "Model prefetch: enabled"
                    : "Model prefetch: disabled");
            AppendInstallLog(BuildWslConfigLogLine(settings));
            AppendInstallLog(BuildBrainInstallLogLine(settings));
            AppendInstallLog(BuildZImageTrainerInstallLogLine(settings));
            AppendInstallLog(
                string.IsNullOrWhiteSpace(settings.OpenRouterApiKey)
                    ? "OpenRouter key: not provided"
                    : "OpenRouter key: provided for Brain llm-wrapper");

            var progress = new Progress<string>(line =>
            {
                var sanitizedLine = line.Replace("\0", string.Empty);
                if (!string.IsNullOrWhiteSpace(sanitizedLine))
                {
                    LogLines.Add(sanitizedLine);
                    StatusMessage = sanitizedLine;
                    AppendInstallLog(sanitizedLine);
                }
            });

            var baseStepMessage = settings.ModuleOnlyRun
                ? "Reusing the existing NymphsCore environment to add optional modules..."
                : settings.RepairExistingDistro
                    ? "Continuing with the existing NymphsCore base environment..."
                    : _workflowService.BaseTarAvailable
                        ? "Importing the NymphsCore base environment..."
                        : "Bootstrapping a fresh NymphsCore base environment locally...";
            LogLines.Add(baseStepMessage);
            AppendInstallLog(baseStepMessage);
            await _workflowService.ApplyWslConfigAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            ProgressValue = 20;
            await _workflowService.ImportBaseDistroAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            ProgressValue = 45;

            if (settings.ModuleOnlyRun)
            {
                var skipMsg = "Skipping full runtime setup (finalize + backend venv resync) because this is an 'Add optional modules' run.";
                LogLines.Add(skipMsg);
                AppendInstallLog(skipMsg);

                if (settings.PrefetchModelsNow)
                {
                    LogLines.Add("Running model prefetch against the existing runtime...");
                    AppendInstallLog("Running model prefetch against the existing runtime...");
                    await _workflowService.RunModelPrefetchOnlyAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
                }
                else
                {
                    LogLines.Add("Skipping model prefetch (not selected).");
                    AppendInstallLog("Skipping model prefetch (not selected).");
                }
            }
            else
            {
                LogLines.Add("Running the selected runtime setup...");
                AppendInstallLog("Running the selected runtime setup...");
                await _workflowService.RunRuntimeSetupAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            }
            ProgressValue = 85;

            if (settings.InstallNymphsBrain || settings.InstallZImageTrainer)
            {
                if (settings.ModuleOnlyRun)
                {
                    LogLines.Add("Preparing system dependencies for optional modules...");
                    AppendInstallLog("Preparing system dependencies for optional modules...");
                    await _workflowService.RunSystemDependenciesOnlyAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
                }

                LogLines.Add("Installing experimental Nymphs-Brain module...");
                AppendInstallLog("Installing experimental Nymphs-Brain module...");
                await _workflowService.RunNymphsBrainInstallAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                LogLines.Add("Skipping experimental Nymphs-Brain module.");
                AppendInstallLog("Skipping experimental Nymphs-Brain module.");
            }

            if (settings.InstallZImageTrainer)
            {
                LogLines.Add("Installing Z-Image Trainer module...");
                AppendInstallLog("Installing Z-Image Trainer module...");
                await _workflowService.RunZImageTrainerInstallAsync(settings, progress, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                LogLines.Add("Skipping Z-Image Trainer module.");
                AppendInstallLog("Skipping Z-Image Trainer module.");
            }

            ProgressValue = 95;

            _installSucceeded = true;
            _installCompleted = true;
            _managedDistroName = settings.DistroName;
            ManagedDistroDetected = true;
            ProgressValue = 100;
            StatusMessage = "Install completed successfully.";
            FinishSummary = BuildFinishSummary(settings);
            AppendInstallLog(StatusMessage);
            AppendInstallLog(FinishSummary);
            OnPropertyChanged(nameof(ShowPostInstallActions));
        }
        catch (Exception ex)
        {
            _installCompleted = true;
            _installSucceeded = false;
            ProgressValue = 100;
            LogLines.Add($"ERROR: {ex.Message}");
            StatusMessage = "Install stopped with an error.";
            FinishSummary =
                "The installer did not complete. Use the log panel and log folder to see what failed.";
            AppendInstallLog($"ERROR: {ex}");
            OnPropertyChanged(nameof(ShowPostInstallActions));
        }
        finally
        {
            IsBusy = false;
            RecomputeStepState();
            if (_installCompleted)
            {
                MoveToStep(5);
            }
        }
    }

    private void GoBack()
    {
        if (_currentStepIndex <= 0)
        {
            return;
        }

        if (IsToolStep)
        {
            MoveToStep(_runtimeToolsReturnStep);
            return;
        }

        if (ManagedDistroDetected && IsRuntimeSetupStep)
        {
            // Leaving the module-only screen cancels the "Add optional modules" intent.
            _moduleOnlyRun = false;
            MoveToStep(1);
            return;
        }

        MoveToStep(_currentStepIndex - 1);
    }

    private bool CanStartAddOptionalModules()
    {
        return !IsBusy && ManagedDistroDetected && IsSystemCheckStep && !HasSystemCheckFailures;
    }

    private bool CanStartRepairRuntime()
    {
        return !IsBusy && ManagedDistroDetected && IsSystemCheckStep && !HasSystemCheckFailures;
    }

    private void StartRepairRuntime()
    {
        // Explicit "Repair Runtime..." action from the existing-install card.
        // This is the full repair pass (WSL config + runtime finalize + backend venv resync).
        // It's the same code path as the primary "Repair Runtime" button, just invoked
        // from a clearly labelled side button so it sits as a peer of the other actions.
        _moduleOnlyRun = false;
        AppendInstallLog("User chose 'Repair Runtime...' on existing install. Running full repair pass.");
        MoveToStep(3);
    }

    private void StartAddOptionalModules()
    {
        // Enter a lightweight "add optional modules" run. RunInstallAsync will honor
        // InstallSettings.ModuleOnlyRun and skip the heavy RunRuntimeSetupAsync pass.
        _moduleOnlyRun = true;

        // Sensible defaults so the user sees Nymphs-Brain pre-selected (their usual reason
        // for choosing this action) but is still free to change or untick it on step 3.
        if (!InstallNymphsBrain)
        {
            InstallNymphsBrain = true;
        }

        if (!InstallZImageTrainer)
        {
            InstallZImageTrainer = true;
        }

        // The user is explicitly doing an add-on run against an existing install; do not
        // churn .wslconfig unless they ask for it.
        var keepExistingOption = WslConfigOptions.FirstOrDefault(o => o.Mode == WslConfigMode.KeepExisting);
        if (keepExistingOption is not null)
        {
            SelectedWslConfigOption = keepExistingOption;
        }

        // Don't force a massive model prefetch by default on an add-on run.
        PrefetchModelsNow = false;

        AppendInstallLog("User chose 'Add optional modules' on existing install. Step 3 will run module-only install (no full repair).");
        MoveToStep(3);
    }

    private void MoveToRuntimeTools()
    {
        if (!IsToolStep)
        {
            _runtimeToolsReturnStep = _currentStepIndex;
        }

        MoveToStep(6);
    }

    private void MoveToZImageTrainer()
    {
        if (!IsToolStep)
        {
            _runtimeToolsReturnStep = _currentStepIndex;
        }

        MoveToStep(7);
    }

    private void MoveToBrainTools()
    {
        if (!IsToolStep)
        {
            _runtimeToolsReturnStep = _currentStepIndex;
        }

        MoveToStep(8);
    }

    private void MoveToStep(int stepIndex)
    {
        _currentStepIndex = Math.Clamp(stepIndex, 0, TotalSteps - 1);
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsSystemCheckStep));
        OnPropertyChanged(nameof(IsInstallLocationStep));
        OnPropertyChanged(nameof(IsRuntimeSetupStep));
        OnPropertyChanged(nameof(IsProgressStep));
        OnPropertyChanged(nameof(IsFinishStep));
        OnPropertyChanged(nameof(IsRuntimeToolsStep));
        OnPropertyChanged(nameof(IsZImageTrainerStep));
        OnPropertyChanged(nameof(IsBrainToolsStep));
        OnPropertyChanged(nameof(ShowDefaultSidebarArt));
        OnPropertyChanged(nameof(ShowBrainSidebarArt));
        OnPropertyChanged(nameof(ShowFooterPrimaryButton));
        OnPropertyChanged(nameof(ShowExistingInstallActions));
        RecomputeStepState();
    }

    private void RecomputeStepState()
    {
        switch (_currentStepIndex)
        {
            case 0:
                CurrentStepTitle = ManagedDistroDetected ? "Welcome back" : "Welcome";
                CurrentStepSubtitle = ManagedDistroDetected
                    ? "Your NymphsCore is already set up. Click to manage it."
                    : "Set up the local NymphsCore pipeline for Blender.";
                // Keep short enough to fit the 180px primary button; "Manage existing install" clipped.
                PrimaryButtonText = ManagedDistroDetected ? "Manage Install" : "Continue";
                break;
            case 1:
                CurrentStepTitle = "System Check";
                CurrentStepSubtitle =
                    RequiresWslSetup
                        ? "WSL is not ready yet. Set that up first, then continue."
                        : "Check WSL, distro state, GPU visibility, and drive space.";
                PrimaryButtonText =
                    !_systemChecksCompleted
                        ? "Checking..."
                        : RequiresWslSetup
                            ? "Set Up WSL"
                            : HasSystemCheckFailures
                            ? "Run Checks Again"
                            : ManagedDistroDetected
                                ? "Repair Runtime"
                                : "Continue";
                break;
            case 2:
                CurrentStepTitle = "Install Location";
                CurrentStepSubtitle =
                    "Choose which Windows drive should hold the dedicated NymphsCore distro for a fresh install. If NymphsCore already exists, reruns reuse that managed distro in place.";
                PrimaryButtonText = "Continue";
                break;
            case 3:
                if (_moduleOnlyRun)
                {
                    CurrentStepTitle = "Add Optional Modules";
                    CurrentStepSubtitle =
                        "Add optional modules to the existing NymphsCore install without running a full repair. Tick Nymphs-Brain or Z-Image Trainer below to install local tools. Backend runtimes and models will be left untouched unless you also tick model prefetch.";
                    PrimaryButtonText = "Install Modules";
                }
                else
                {
                    CurrentStepTitle = ManagedDistroDetected ? "Repair Runtime Settings" : "WSL Resources And Models";
                    CurrentStepSubtitle =
                        ManagedDistroDetected
                            ? "Choose how the manager should handle .wslconfig and model downloads while repairing the existing runtime in place."
                            : "Choose how the installer should handle .wslconfig, then decide whether to prefetch models now.";
                    PrimaryButtonText = ManagedDistroDetected ? "Run Repair" : "Start Install";
                }
                break;
            case 4:
                CurrentStepTitle = ManagedDistroDetected || _lastRunUsedExistingInstall ? "Repair Progress" : "Installation Progress";
                CurrentStepSubtitle =
                    ManagedDistroDetected || _lastRunUsedExistingInstall
                        ? "The manager is repairing the existing managed distro and refreshing the selected runtime pieces."
                        : "The app is importing or repairing the managed distro and running the selected setup steps.";
                PrimaryButtonText = "Finish";
                break;
            case 5:
                CurrentStepTitle = _installSucceeded
                    ? GetFinishSuccessTitle()
                    : _lastRunUsedExistingInstall
                        ? "Repair Summary"
                        : "Install Summary";
                CurrentStepSubtitle =
                    _installSucceeded
                        ? GetFinishSuccessSubtitle()
                        : "The installer stopped before everything completed.";
                PrimaryButtonText = _installSucceeded ? "Open Runtime Tools" : "Close";
                break;
            case 6:
                CurrentStepTitle = "Runtime Tools";
                CurrentStepSubtitle =
                    "Fetch models, check readiness, and run smoke tests.";
                PrimaryButtonText = "Close";
                break;
            case 7:
                CurrentStepTitle = "Z-Image Trainer";
                CurrentStepSubtitle =
                    "Install and manage the isolated AI Toolkit sidecar for Z-Image Turbo LoRA training.";
                PrimaryButtonText = "Close";
                break;
            case 8:
                CurrentStepTitle = "Brain";
                CurrentStepSubtitle =
                    "Manage the local coding model, MCP gateway, browser UI, and optional OpenRouter tools for Nymphs-Brain.";
                PrimaryButtonText = "Close";
                break;
        }

        OnPropertyChanged(nameof(StepCounterText));
        RaiseCommandStateChanged();
    }

    private void RaiseCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanStartZImageTrainingUi));
        OnPropertyChanged(nameof(CanStopZImageTrainingUi));
        _primaryCommand.RaiseCanExecuteChanged();
        _checkForUpdatesCommand.RaiseCanExecuteChanged();
        _addOptionalModulesCommand.RaiseCanExecuteChanged();
        _repairRuntimeCommand.RaiseCanExecuteChanged();
        _openRuntimeToolsCommand.RaiseCanExecuteChanged();
        _refreshRuntimeStatusCommand.RaiseCanExecuteChanged();
        _openZImageTrainerCommand.RaiseCanExecuteChanged();
        _installZImageTrainerCommand.RaiseCanExecuteChanged();
        _refreshZImageTrainerStatusCommand.RaiseCanExecuteChanged();
        _createZImageTrainerJobCommand.RaiseCanExecuteChanged();
        _openZImageTrainerPicturesCommand.RaiseCanExecuteChanged();
        _openZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
        _draftZImageTrainerCaptionsCommand.RaiseCanExecuteChanged();
        _startZImageTrainingCommand.RaiseCanExecuteChanged();
        _stopZImageTrainingCommand.RaiseCanExecuteChanged();
        _launchZImageTrainerOfficialUiCommand.RaiseCanExecuteChanged();
        _killZImageTrainerOfficialUiCommand.RaiseCanExecuteChanged();
        _launchZImageTrainerGradioUiCommand.RaiseCanExecuteChanged();
        _openZImageTrainerDatasetsCommand.RaiseCanExecuteChanged();
        _openZImageTrainerJobsCommand.RaiseCanExecuteChanged();
        _openZImageTrainerLorasCommand.RaiseCanExecuteChanged();
        _openBrainToolsCommand.RaiseCanExecuteChanged();
        _refreshBrainStatusCommand.RaiseCanExecuteChanged();
        _fetchModelsNowCommand.RaiseCanExecuteChanged();
        _fetchZImageModelsCommand.RaiseCanExecuteChanged();
        _fetchTrellisModelsCommand.RaiseCanExecuteChanged();
        _checkRuntimeDependencyUpdatesCommand.RaiseCanExecuteChanged();
        _testLatestRuntimeDependenciesCommand.RaiseCanExecuteChanged();
        _restorePinnedRuntimeDependenciesCommand.RaiseCanExecuteChanged();
        _repairTrellisAdapterCommand.RaiseCanExecuteChanged();
        _testZImageCommand.RaiseCanExecuteChanged();
        _testTrellisCommand.RaiseCanExecuteChanged();
        _startBrainLlmCommand.RaiseCanExecuteChanged();
        _openBrainWebUiCommand.RaiseCanExecuteChanged();
        _updateBrainWebUiCommand.RaiseCanExecuteChanged();
        _applyBrainOpenRouterKeyCommand.RaiseCanExecuteChanged();
        _changeBrainModelCommand.RaiseCanExecuteChanged();
        _stopBrainLlmCommand.RaiseCanExecuteChanged();
        _backCommand.RaiseCanExecuteChanged();
    }

    private string GetFinishSuccessTitle()
    {
        if (_lastRunUsedExistingInstall)
        {
            return "Repair Complete";
        }

        return "Install Complete";
    }

    private string GetFinishSuccessSubtitle()
    {
        if (_lastRunUsedExistingInstall)
        {
            return "Your installed runtime was checked and refreshed successfully.";
        }

        return "Your NymphsCore runtime was installed successfully.";
    }

    private string BuildFinishSummary(InstallSettings settings)
    {
        var runtimeTail = settings.PrefetchModelsNow
            ? "Required models were prefetched during setup."
            : "Runtime environments were prepared. The manager or Blender addon will download required models later on first real use.";
        var brainTail = settings.InstallNymphsBrain
            ? $" Experimental Nymphs-Brain was installed to {settings.BrainInstallRoot}."
            : " Experimental Nymphs-Brain was skipped.";
        var trainerTail = settings.InstallZImageTrainer
            ? " Z-Image Trainer was installed to /home/nymph/ZImage-Trainer."
            : " Z-Image Trainer was skipped.";

        if (settings.RepairExistingDistro)
        {
            return $"Your existing NymphsCore runtime was repaired and refreshed in place. Default Linux user: {settings.LinuxUser}. Managed repos were checked during this run. {runtimeTail}{brainTail}{trainerTail}";
        }

        return $"NymphsCore was installed to {settings.InstallLocation}. Default Linux user: {settings.LinuxUser}. {runtimeTail}{brainTail}{trainerTail}";
    }

    private static bool ShouldShowUpdateCheckLine(string line)
    {
        return !(line.StartsWith("Running finalize step inside distro", StringComparison.Ordinal) ||
                 line.StartsWith("Mode:", StringComparison.Ordinal) ||
                 line.StartsWith("Using packaged helper scripts", StringComparison.Ordinal) ||
                 line.StartsWith("Using in-distro helper scripts", StringComparison.Ordinal) ||
                 line.StartsWith("Effective finalize script:", StringComparison.Ordinal) ||
                 line.StartsWith("Managed repo update check completed.", StringComparison.Ordinal) ||
                 line.StartsWith("Managed repo update policy:", StringComparison.Ordinal) ||
                 line.StartsWith("- NymphsCore helper repo is checked here", StringComparison.Ordinal) ||
                 line.StartsWith("- NymphsCore helper repo is checked here", StringComparison.Ordinal) ||
                 line.StartsWith("- The current installer run still uses", StringComparison.Ordinal) ||
                 line.StartsWith("- Backend repos are safe to fast-forward", StringComparison.Ordinal));
    }

    private void InitializeWslConfigChoices()
    {
        var recommended = _workflowService.GetRecommendedWslConfig();
        var current = _workflowService.GetCurrentWslConfig();

        HasExistingWslConfig = current.Exists;
        RecommendedWslConfigSummary =
            $"Recommended for this PC: memory={recommended.MemoryGb}GB, processors={recommended.Processors}, swap={recommended.SwapGb}GB. This is a good fit for the backend on higher-end 4080/5090-class systems when the PC has enough host RAM.";
        CurrentWslConfigSummary = current.Exists
            ? $"Current {current.Path}: memory={FormatNullableValue(current.MemoryGb, "GB")}, processors={FormatNullableValue(current.Processors)}, swap={FormatNullableValue(current.SwapGb, "GB")}."
            : $"No existing {_workflowService.WslConfigPath} was detected.";

        WslCustomMemoryGb = current.MemoryGb ?? recommended.MemoryGb;
        WslCustomProcessors = current.Processors ?? recommended.Processors;
        WslCustomSwapGb = current.SwapGb ?? recommended.SwapGb;

        WslConfigOptions.Add(new WslConfigModeOption(
            WslConfigMode.Recommended,
            "Use recommended values",
            "Best default for most users. Keeps strong WSL headroom for the backend while leaving room for Windows, Blender, and the GPU driver stack."));

        if (current.Exists)
        {
            WslConfigOptions.Add(new WslConfigModeOption(
                WslConfigMode.KeepExisting,
                "Keep current .wslconfig",
                "Leave the current WSL memory, processor, and swap settings unchanged."));
        }

        WslConfigOptions.Add(new WslConfigModeOption(
            WslConfigMode.Custom,
            "Use custom values",
            "Write your own memory, processor, and swap values into .wslconfig."));

        SelectedWslConfigOption = WslConfigOptions.FirstOrDefault(option =>
            option.Mode == (current.Exists ? WslConfigMode.KeepExisting : WslConfigMode.Recommended));
    }

    private void InitializeBrainChoices()
    {
        BrainModelOptions.Add(new BrainModelOption(
            "auto",
            "Auto recommended",
            "Detect GPU VRAM in WSL and choose a sensible experimental default.",
            "auto",
            "q4_k_m",
            16384));
        BrainModelOptions.Add(new BrainModelOption(
            "small",
            "Small / safe",
            "Smallest default for low VRAM or quick smoke testing. Good for proving the stack works.",
            "qwen/qwen3-1.7b",
            "q4_k_m",
            8192));
        BrainModelOptions.Add(new BrainModelOption(
            "balanced",
            "Balanced coder",
            "Good first serious coding-agent preset for 16GB+ VRAM systems.",
            "qwen/qwen2.5-coder-14b",
            "q4_k_m",
            16384));
        BrainModelOptions.Add(new BrainModelOption(
            "high-end",
            "High-end coder",
            "Larger coding model preset for 24GB to 32GB+ VRAM machines.",
            "qwen/qwen2.5-coder-32b",
            "q4_k_m",
            32768));
        BrainModelOptions.Add(new BrainModelOption(
            "large-experimental",
            "Large experimental",
            "Bigger experimental MoE-style preset for roomy 4080/4090/5090-class setups.",
            "qwen/qwen3-30b-a3b",
            "q4_k_m",
            32768));
        BrainModelOptions.Add(new BrainModelOption(
            "custom",
            "Custom model id",
            "Enter a model id manually. The Manager passes it to the Brain model manager without validation.",
            string.Empty,
            "q4_k_m",
            16384));

        SelectedBrainModelOption = BrainModelOptions.FirstOrDefault(option => option.Id == "auto");
    }

    private void InitializeTrellisGgufQuantChoices()
    {
        _detectedGpuVramMb = _workflowService.GetDetectedGpuVramMb();

        TrellisGgufQuantOptions.Add(new TrellisGgufQuantOption(
            "all",
            "All quants - full local cache",
            "Downloads Q4_K_M, Q5_K_M, Q6_K, and Q8_0 so the Blender addon can switch quantization later without another model fetch. Runtime tests use Q5_K_M by default."));
        TrellisGgufQuantOptions.Add(new TrellisGgufQuantOption(
            "Q4_K_M",
            "Q4_K_M - Low VRAM",
            "Smallest TRELLIS GGUF download and lowest VRAM pressure. Best for GPUs below 16 GB or quick smoke tests."));
        TrellisGgufQuantOptions.Add(new TrellisGgufQuantOption(
            "Q5_K_M",
            "Q5_K_M - 16 GB balanced",
            "Recommended for 16 GB GPUs such as RTX 4080 Super. Better quality than Q4 while staying practical for local use."));
        TrellisGgufQuantOptions.Add(new TrellisGgufQuantOption(
            "Q6_K",
            "Q6_K - High quality",
            "Higher quality and larger download. Better for 24 GB+ GPUs or users willing to trade speed and storage for quality."));
        TrellisGgufQuantOptions.Add(new TrellisGgufQuantOption(
            "Q8_0",
            "Q8_0 - Maximum",
            "Largest quantized GGUF option. Intended for very roomy GPUs and comparison testing."));

        var recommended = "all";
        SelectedTrellisGgufQuantOption = TrellisGgufQuantOptions.FirstOrDefault(option => option.Value == recommended)
            ?? TrellisGgufQuantOptions.FirstOrDefault(option => option.Value == "Q5_K_M");
    }

    private bool HasValidWslConfigSelection()
    {
        if (SelectedWslConfigOption is null)
        {
            return false;
        }

        if (SelectedWslConfigOption.Mode != WslConfigMode.Custom)
        {
            return true;
        }

        return WslCustomMemoryGb >= 8 &&
               WslCustomProcessors >= 4 &&
               WslCustomSwapGb >= 0;
    }

    private bool HasValidBrainSelection()
    {
        return true;
    }

    private string ResolveBrainModelId()
    {
        if (SelectedBrainModelOption?.IsCustom == true)
        {
            return CustomBrainModelId;
        }

        return SelectedBrainModelOption?.ModelId ?? "auto";
    }

    private string ResolveBrainQuantization()
    {
        return SelectedBrainModelOption?.Quantization ?? "q4_k_m";
    }

    private static string FormatNullableValue(int? value, string suffix = "")
    {
        return value.HasValue ? $"{value.Value}{suffix}" : "not set";
    }

    private static string BuildWslConfigLogLine(InstallSettings settings)
    {
        return settings.WslConfigMode switch
        {
            WslConfigMode.KeepExisting => "WSL resource settings: keeping current .wslconfig values.",
            WslConfigMode.Custom => $"WSL resource settings: custom memory={settings.WslMemoryGb}GB, processors={settings.WslProcessors}, swap={settings.WslSwapGb}GB.",
            _ => "WSL resource settings: using recommended values for this PC.",
        };
    }

    private static string BuildBrainInstallLogLine(InstallSettings settings)
    {
        if (!settings.InstallNymphsBrain)
        {
            return "Nymphs-Brain: skipped.";
        }

        return settings.DownloadBrainModelNow
            ? $"Nymphs-Brain: enabled, install root={settings.BrainInstallRoot}, model download requested."
            : $"Nymphs-Brain: enabled, install root={settings.BrainInstallRoot}, local and remote model selection handled later with Manage Models.";
    }

    private static string BuildZImageTrainerInstallLogLine(InstallSettings settings)
    {
        return settings.InstallZImageTrainer
            ? "Z-Image Trainer: enabled, install root=/home/nymph/ZImage-Trainer, datasets=/home/nymph/ZImage-Trainer/datasets, outputs=/home/nymph/ZImage-Trainer/loras."
            : "Z-Image Trainer: skipped.";
    }

    private static UpdateCheckPresentation BuildFriendlyUpdateCheckSummary(IEnumerable<string> lines)
    {
        var repoStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var details = new List<string>();

        foreach (var line in lines)
        {
            if (TryParseRepoLine(line, out var repoName, out var state, out var message))
            {
                repoStates[repoName] = state;
            }
        }

        var summary = new StringBuilder();
        var managedRepos = new[]
        {
            "NymphsCore helper repo",
            "Z-Image backend",
            "TRELLIS.2",
        };
        var updatesAvailable = 0;
        var attentionNeeded = 0;
        var upToDate = 0;
        var leftAlone = 0;

        foreach (var repo in managedRepos)
        {
            if (!repoStates.TryGetValue(repo, out var state))
            {
                continue;
            }

            var repoLabel = FriendlyManagedRepoLabel(repo);
            if (repo.Equals("NymphsCore helper repo", StringComparison.OrdinalIgnoreCase) &&
                state is not "up_to_date" and not "ahead_local" and not "behind_clean" and not "missing")
            {
                leftAlone++;
                details.Add($"{repoLabel}: The installer is using the packaged manager scripts for this run, so this checkout does not block install or repair.");
                continue;
            }

            switch (state)
            {
                case "up_to_date":
                case "ahead_local":
                    upToDate++;
                    details.Add($"{repoLabel}: Repo checkout looks current. This does not verify the runtime env, models, or smoke-test health.");
                    break;
                case "behind_clean":
                case "missing":
                    updatesAvailable++;
                    details.Add($"{repoLabel}: Repo update available.");
                    break;
                case "dirty":
                    leftAlone++;
                    details.Add($"{repoLabel}: Local/generated files were found and left alone. This is common for generated images, models, caches, or experiment output.");
                    break;
                case "fetch_timed_out":
                    leftAlone++;
                    details.Add($"{repoLabel}: GitHub did not respond quickly enough, so this checkout was left unchanged.");
                    break;
                case "fetch_failed":
                    leftAlone++;
                    details.Add($"{repoLabel}: The update check could not be completed right now, so this checkout was left unchanged.");
                    break;
                case "branch_mismatch":
                    attentionNeeded++;
                    details.Add($"{repoLabel}: This backend is on a different git branch than the installer currently expects, so it did not change anything automatically.");
                    break;
                case "remote_mismatch":
                    attentionNeeded++;
                    details.Add($"{repoLabel}: This backend points at a different git remote than the installer currently expects.");
                    break;
                case "diverged":
                    attentionNeeded++;
                    details.Add($"{repoLabel}: This backend has local and remote history that no longer fast-forward cleanly.");
                    break;
                default:
                    attentionNeeded++;
                    details.Add($"{repoLabel}: Needs attention before automatic updating can continue.");
                    break;
            }
        }

        // Keep the end-user-visible summary as short, plain English. The long
        // per-repo technical detail goes into the session log for power users.
        if (updatesAvailable > 0)
        {
            var word = updatesAvailable == 1 ? "update" : "updates";
            summary.Append($"✓ {updatesAvailable} {word} available. Click \"Repair Runtime...\" to apply.");
        }
        else if (attentionNeeded > 0)
        {
            summary.Append("⚠ One or two things need a manual look. See the log for details; nothing was changed.");
        }
        else if (upToDate > 0 || leftAlone > 0)
        {
            summary.Append("✓ You're up to date. No updates are available right now.");
        }
        else
        {
            summary.Append("Could not check for updates (no network, or the managed repos were not found).");
        }

        return new UpdateCheckPresentation(
            Summary: summary.ToString().TrimEnd(),
            CheckCompleted: repoStates.Count > 0,
            UpdatesAvailable: updatesAvailable > 0);
    }

    private static string FriendlyManagedRepoLabel(string repoName)
    {
        return repoName switch
        {
            "NymphsCore helper repo" => "Manager helper repo",
            "Z-Image backend" => "Z-Image Turbo via Nunchaku",
            _ => repoName,
        };
    }

    private static bool TryParseRepoLine(string line, out string repoName, out string state, out string message)
    {
        repoName = string.Empty;
        state = string.Empty;
        message = string.Empty;

        if (!line.StartsWith("repo=", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in line.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex];
            var value = segment[(separatorIndex + 1)..];
            switch (key)
            {
                case "repo":
                    repoName = value;
                    break;
                case "state":
                    state = value;
                    break;
                case "message":
                    message = value;
                    break;
            }
        }

        return !string.IsNullOrWhiteSpace(repoName) && !string.IsNullOrWhiteSpace(state);
    }

    private void AppendInstallLog(string message)
    {
        var sanitizedMessage = message.Replace("\0", string.Empty);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {sanitizedMessage}{Environment.NewLine}";
        File.AppendAllText(_installSessionLogPath, line);
    }

    private sealed record UpdateCheckPresentation(string Summary, bool CheckCompleted, bool UpdatesAvailable);
}
