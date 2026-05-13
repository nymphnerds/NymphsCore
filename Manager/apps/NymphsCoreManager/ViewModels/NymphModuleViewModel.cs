using System;
using System.Collections.Generic;
using NymphsCoreManager.Models;

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
    private bool _hasUpdate;
    private string _remoteVersionLabel = "Not checked";
    private string _updateDetail = "No module update check has run yet.";
    private string _repositoryUrl = "";
    private bool _hasInstalledModuleUi;
    private string _moduleUiTitle = "Module UI";
    private InstalledNymphModuleUiInfo? _installedModuleUiInfo;

    public NymphModuleViewModel(
        string id,
        string name,
        string monogram,
        string category,
        string kind,
        string description,
        string installPath,
        string accentBrush,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<NymphModuleActionInfo> managerActions,
        IReadOnlyList<string>? devCapabilities = null)
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
        ManagerActions = managerActions;
        DevCapabilities = devCapabilities ?? Array.Empty<string>();
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

    public IReadOnlyList<NymphModuleActionInfo> ManagerActions { get; private set; }

    public IReadOnlyList<string> DevCapabilities { get; }

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

    public bool HasUpdate
    {
        get => _hasUpdate;
        private set => SetProperty(ref _hasUpdate, value);
    }

    public string RemoteVersionLabel
    {
        get => _remoteVersionLabel;
        private set => SetProperty(ref _remoteVersionLabel, value);
    }

    public string UpdateDetail
    {
        get => _updateDetail;
        private set => SetProperty(ref _updateDetail, value);
    }

    public string RepositoryUrl
    {
        get => _repositoryUrl;
        private set
        {
            if (SetProperty(ref _repositoryUrl, value))
            {
                OnPropertyChanged(nameof(HasRepositoryUrl));
            }
        }
    }

    public bool HasRepositoryUrl => !string.IsNullOrWhiteSpace(RepositoryUrl);

    public bool HasInstalledModuleUi
    {
        get => _hasInstalledModuleUi;
        private set => SetProperty(ref _hasInstalledModuleUi, value);
    }

    public string ModuleUiTitle
    {
        get => _moduleUiTitle;
        private set => SetProperty(ref _moduleUiTitle, value);
    }

    public InstalledNymphModuleUiInfo? InstalledModuleUiInfo
    {
        get => _installedModuleUiInfo;
        private set => SetProperty(ref _installedModuleUiInfo, value);
    }

    public string DisplayStateLabel => HasUpdate ? "Update available" : StateLabel;

    public string DisplayStatusBrush => HasUpdate ? "#D49A2A" : StatusBrush;

    public string AvailabilityLabel => IsInstalled ? "Installed Nymph" : "Available Nymph";

    public string NavigationSubtitle => IsInstalled ? DisplayStateLabel : "Available";

    public string InstallPathLabel => IsInstalled ? InstallPath : "Not installed in managed distro";

    public bool CanOpenInstallPath => IsInstalled;

    public bool CanInstall => !IsInstalled;

    public bool CanUpdate => IsInstalled && (HasUpdate || IsRemoteVersionNewer(VersionLabel, RemoteVersionLabel));

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
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(DisplayStateLabel));
        OnPropertyChanged(nameof(DisplayStatusBrush));

        if (!isInstalled)
        {
            ApplyInstalledModuleUi(null);
        }
    }

    public void ApplyInstalledModuleUi(InstalledNymphModuleUiInfo? uiInfo)
    {
        var manifestTitle = !string.IsNullOrWhiteSpace(ModuleUiTitle) &&
                            !string.Equals(ModuleUiTitle, "Module UI", StringComparison.OrdinalIgnoreCase)
            ? ModuleUiTitle
            : null;
        InstalledModuleUiInfo = IsInstalled ? uiInfo : null;
        HasInstalledModuleUi = IsInstalled && uiInfo is not null;
        ModuleUiTitle = manifestTitle ?? uiInfo?.Title ?? "Module UI";
    }

    public void ApplyManifestInfo(NymphModuleManifestInfo manifest)
    {
        ManagerActions = manifest.ManagerActions;
        OnPropertyChanged(nameof(ManagerActions));

        if (!string.IsNullOrWhiteSpace(manifest.Version))
        {
            RemoteVersionLabel = manifest.Version;
        }

        if (!string.IsNullOrWhiteSpace(manifest.ManagerUiTitle))
        {
            ModuleUiTitle = manifest.ManagerUiTitle;
        }

        if (!IsInstalled && !string.IsNullOrWhiteSpace(manifest.Description))
        {
            Detail = manifest.Description;
        }

        var sourceLine = string.IsNullOrWhiteSpace(manifest.SourceSummary)
            ? manifest.ManifestUrl
            : manifest.SourceSummary;
        RepositoryUrl = manifest.RepositoryUrl;
        var overviewDetail = string.IsNullOrWhiteSpace(manifest.OverviewDetail)
            ? ""
            : $"{manifest.OverviewDetail}\n\n";
        var manifestDetail = $"{overviewDetail}Registry manifest: {manifest.ManifestUrl}\nSource: {sourceLine}";
        SecondaryDetail = IsInstalled &&
                          !string.IsNullOrWhiteSpace(SecondaryDetail) &&
                          !SecondaryDetail.Contains("Registry manifest:", StringComparison.OrdinalIgnoreCase)
            ? $"{SecondaryDetail}\n\n{manifestDetail}"
            : manifestDetail;

        OnPropertyChanged(nameof(CanUpdate));
    }

    public void ApplyUpdateState(string? installedVersion, string? remoteVersion, bool hasUpdate, string detail)
    {
        VersionLabel = string.IsNullOrWhiteSpace(installedVersion) ? VersionLabel : installedVersion;
        RemoteVersionLabel = string.IsNullOrWhiteSpace(remoteVersion) ? "Unknown" : remoteVersion;
        HasUpdate = hasUpdate;
        UpdateDetail = detail;

        OnPropertyChanged(nameof(DisplayStateLabel));
        OnPropertyChanged(nameof(DisplayStatusBrush));
        OnPropertyChanged(nameof(NavigationSubtitle));
        OnPropertyChanged(nameof(CanUpdate));
    }

    public void ClearUpdateState(string detail)
    {
        HasUpdate = false;
        UpdateDetail = detail;

        OnPropertyChanged(nameof(DisplayStateLabel));
        OnPropertyChanged(nameof(DisplayStatusBrush));
        OnPropertyChanged(nameof(NavigationSubtitle));
        OnPropertyChanged(nameof(CanUpdate));
    }

    private static bool IsRemoteVersionNewer(string? installedVersion, string? remoteVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion) || string.IsNullOrWhiteSpace(remoteVersion))
        {
            return false;
        }

        if (IsUnknownVersion(installedVersion) || IsUnknownVersion(remoteVersion))
        {
            return false;
        }

        var installedParts = ParseVersionParts(installedVersion);
        var remoteParts = ParseVersionParts(remoteVersion);
        var length = Math.Max(installedParts.Count, remoteParts.Count);
        for (var index = 0; index < length; index++)
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

    private static bool IsUnknownVersion(string version)
    {
        return version.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
               version.Contains("not", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<int> ParseVersionParts(string version)
    {
        var parts = new List<int>();
        var current = "";
        foreach (var character in version)
        {
            if (char.IsDigit(character))
            {
                current += character;
                continue;
            }

            if (current.Length > 0)
            {
                parts.Add(int.Parse(current));
                current = "";
            }
        }

        if (current.Length > 0)
        {
            parts.Add(int.Parse(current));
        }

        return parts;
    }
}
