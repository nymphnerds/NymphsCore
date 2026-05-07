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

    public string DisplayStateLabel => HasUpdate ? "Update available" : StateLabel;

    public string DisplayStatusBrush => HasUpdate ? "#D49A2A" : StatusBrush;

    public string AvailabilityLabel => IsInstalled ? "Installed Nymph" : "Available Nymph";

    public string NavigationSubtitle => IsInstalled ? DisplayStateLabel : "Available";

    public string InstallPathLabel => IsInstalled ? InstallPath : "Not installed in managed distro";

    public bool CanOpenInstallPath => IsInstalled;

    public bool CanInstall => !IsInstalled;

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
        OnPropertyChanged(nameof(DisplayStateLabel));
        OnPropertyChanged(nameof(DisplayStatusBrush));
    }

    public void ApplyManifestInfo(NymphModuleManifestInfo manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Version))
        {
            RemoteVersionLabel = manifest.Version;
        }

        if (!string.IsNullOrWhiteSpace(manifest.Description))
        {
            Detail = manifest.Description;
        }

        var sourceLine = string.IsNullOrWhiteSpace(manifest.SourceSummary)
            ? manifest.ManifestUrl
            : manifest.SourceSummary;
        SecondaryDetail = $"Registry manifest: {manifest.ManifestUrl}\nSource: {sourceLine}";
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
    }

    public void ClearUpdateState(string detail)
    {
        HasUpdate = false;
        UpdateDetail = detail;

        OnPropertyChanged(nameof(DisplayStateLabel));
        OnPropertyChanged(nameof(DisplayStatusBrush));
        OnPropertyChanged(nameof(NavigationSubtitle));
    }
}
