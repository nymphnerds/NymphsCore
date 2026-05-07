using System.Collections.Generic;

namespace NymphsCoreManager.ViewModels;

public sealed class NymphModuleViewModel : ViewModelBase
{
    private bool _isInstalled;
    private bool _isRunning;
    private string _versionLabel = "Not detected";
    private string _stateLabel = "Available";
    private string _statusBrush = "#6E745A";
    private string _detail = "Module not installed yet.";
    private string _secondaryDetail = "Install wrappers and live lifecycle hooks will land as this shell grows.";

    public NymphModuleViewModel(
        string id,
        string name,
        string monogram,
        string category,
        string kind,
        string description,
        string installPath,
        string accentBrush,
        IReadOnlyList<string> capabilities)
    {
        Id = id;
        Name = name;
        Monogram = monogram;
        Category = category;
        Kind = kind;
        Description = description;
        InstallPath = installPath;
        AccentBrush = accentBrush;
        Capabilities = capabilities;
    }

    public string Id { get; }

    public string Name { get; }

    public string Monogram { get; }

    public string Category { get; }

    public string Kind { get; }

    public string Description { get; }

    public string InstallPath { get; }

    public string AccentBrush { get; }

    public IReadOnlyList<string> Capabilities { get; }

    public bool IsInstalled
    {
        get => _isInstalled;
        private set => SetProperty(ref _isInstalled, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public string VersionLabel
    {
        get => _versionLabel;
        private set => SetProperty(ref _versionLabel, value);
    }

    public string StateLabel
    {
        get => _stateLabel;
        private set => SetProperty(ref _stateLabel, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public string SecondaryDetail
    {
        get => _secondaryDetail;
        private set => SetProperty(ref _secondaryDetail, value);
    }

    public string AvailabilityLabel => IsInstalled ? "Installed Nymph" : "Available Nymph";

    public string NavigationSubtitle => IsInstalled ? StateLabel : "Available";

    public string InstallPathLabel => IsInstalled ? InstallPath : "Not installed in managed distro";

    public bool CanOpenInstallPath => IsInstalled;

    public void ApplyState(
        bool isInstalled,
        bool isRunning,
        string versionLabel,
        string stateLabel,
        string statusBrush,
        string detail,
        string secondaryDetail)
    {
        IsInstalled = isInstalled;
        IsRunning = isRunning;
        VersionLabel = versionLabel;
        StateLabel = stateLabel;
        StatusBrush = statusBrush;
        Detail = detail;
        SecondaryDetail = secondaryDetail;

        OnPropertyChanged(nameof(AvailabilityLabel));
        OnPropertyChanged(nameof(NavigationSubtitle));
        OnPropertyChanged(nameof(InstallPathLabel));
        OnPropertyChanged(nameof(CanOpenInstallPath));
    }
}
