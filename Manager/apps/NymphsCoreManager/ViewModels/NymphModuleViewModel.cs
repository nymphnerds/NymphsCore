using System;
using System.Collections.Generic;
using System.Linq;
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
    private IReadOnlyList<NymphModuleActionLinkInfo> _overviewLinks = Array.Empty<NymphModuleActionLinkInfo>();
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
        IReadOnlyList<NymphModuleActionFieldInfo>? installFields = null,
        string installOptionsTitle = "Install Options",
        IReadOnlyList<NymphModuleActionGroupInfo>? managerActionGroups = null,
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
        InstallFields = installFields ?? Array.Empty<NymphModuleActionFieldInfo>();
        InstallOptionsTitle = NormalizeInstallOptionsTitle(installOptionsTitle);
        ManagerActionGroups = managerActionGroups ?? Array.Empty<NymphModuleActionGroupInfo>();
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

    public IReadOnlyList<NymphModuleActionFieldInfo> InstallFields { get; private set; }

    public string InstallOptionsTitle { get; private set; }

    public IReadOnlyList<NymphModuleActionFieldInfo> InstallOptionFields =>
        InstallFields.Where(field => field.IsOptionField).ToArray();

    public bool HasInstallFields => InstallFields.Count > 0;

    public IReadOnlyList<NymphModuleActionGroupInfo> ManagerActionGroups { get; private set; }

    public IReadOnlyList<NymphModuleActionLinkInfo> ManagerActionGroupLinks =>
        ManagerActionGroups
            .SelectMany(group => group.Links)
            .GroupBy(link => link.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

    public bool HasManagerActionGroupLinks => ManagerActionGroupLinks.Count > 0;

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

    public IReadOnlyList<NymphModuleActionLinkInfo> OverviewLinks
    {
        get => _overviewLinks;
        private set
        {
            if (SetProperty(ref _overviewLinks, value))
            {
                OnPropertyChanged(nameof(HasOverviewLinks));
            }
        }
    }

    public bool HasOverviewLinks => OverviewLinks.Count > 0;

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

    public bool CanRepair => IsInstalled ||
                             StateLabel.Contains("repair", StringComparison.OrdinalIgnoreCase);

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
        OnPropertyChanged(nameof(CanRepair));
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

    public void ApplyInstalledModuleControls(
        IReadOnlyList<NymphModuleActionInfo> managerActions,
        IReadOnlyList<NymphModuleActionGroupInfo> managerActionGroups)
    {
        if (managerActions.Count > 0)
        {
            ManagerActions = managerActions;
            OnPropertyChanged(nameof(ManagerActions));
        }

        if (managerActionGroups.Count > 0)
        {
            ManagerActionGroups = PreserveActionGroupFieldState(ManagerActionGroups, managerActionGroups);
            OnPropertyChanged(nameof(ManagerActionGroups));
            OnPropertyChanged(nameof(ManagerActionGroupLinks));
            OnPropertyChanged(nameof(HasManagerActionGroupLinks));
        }
    }

    public void ApplyManifestInfo(NymphModuleManifestInfo manifest)
    {
        ManagerActions = manifest.ManagerActions;
        OnPropertyChanged(nameof(ManagerActions));
        OverviewLinks = BuildOverviewLinks(manifest);
        ManagerActionGroups = PreserveActionGroupFieldState(ManagerActionGroups, manifest.ManagerActionGroups);
        OnPropertyChanged(nameof(ManagerActionGroups));
        OnPropertyChanged(nameof(ManagerActionGroupLinks));
        OnPropertyChanged(nameof(HasManagerActionGroupLinks));
        InstallFields = PreserveFieldState(InstallFields, manifest.InstallFields);
        InstallOptionsTitle = NormalizeInstallOptionsTitle(manifest.InstallOptionsTitle);
        OnPropertyChanged(nameof(InstallFields));
        OnPropertyChanged(nameof(InstallOptionFields));
        OnPropertyChanged(nameof(HasInstallFields));
        OnPropertyChanged(nameof(InstallOptionsTitle));

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

        RepositoryUrl = manifest.RepositoryUrl;
        var overviewDetail = string.IsNullOrWhiteSpace(manifest.OverviewDetail)
            ? ""
            : manifest.OverviewDetail;
        var manifestDetail = overviewDetail;
        if (!string.IsNullOrWhiteSpace(manifestDetail))
        {
            SecondaryDetail = IsInstalled &&
                              !string.IsNullOrWhiteSpace(SecondaryDetail) &&
                              !SecondaryDetail.Contains(manifestDetail, StringComparison.OrdinalIgnoreCase)
                ? $"{SecondaryDetail}\n\n{manifestDetail}"
                : manifestDetail;
        }

        OnPropertyChanged(nameof(CanUpdate));
    }

    public void ApplyActionGroupFieldStateFrom(NymphModuleViewModel previous)
    {
        ManagerActionGroups = PreserveActionGroupFieldState(previous.ManagerActionGroups, ManagerActionGroups);
        InstallFields = PreserveFieldState(previous.InstallFields, InstallFields);
        OnPropertyChanged(nameof(ManagerActionGroups));
        OnPropertyChanged(nameof(ManagerActionGroupLinks));
        OnPropertyChanged(nameof(HasManagerActionGroupLinks));
        OnPropertyChanged(nameof(InstallFields));
        OnPropertyChanged(nameof(InstallOptionFields));
        OnPropertyChanged(nameof(HasInstallFields));
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

    private static IReadOnlyList<NymphModuleActionGroupInfo> PreserveActionGroupFieldState(
        IReadOnlyList<NymphModuleActionGroupInfo> previousGroups,
        IReadOnlyList<NymphModuleActionGroupInfo> nextGroups)
    {
        foreach (var group in nextGroups)
        {
            var previous = previousGroups.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, group.Id, StringComparison.OrdinalIgnoreCase));
            if (previous is not null)
            {
                group.ApplyFieldStateFrom(previous);
            }
        }

        return nextGroups;
    }

    private static IReadOnlyList<NymphModuleActionFieldInfo> PreserveFieldState(
        IReadOnlyList<NymphModuleActionFieldInfo> previousFields,
        IReadOnlyList<NymphModuleActionFieldInfo> nextFields)
    {
        foreach (var field in nextFields)
        {
            var previous = previousFields.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, field.Name, StringComparison.OrdinalIgnoreCase));
            if (previous is not null)
            {
                field.ApplyTransientStateFrom(previous);
            }
        }

        return nextFields;
    }

    private static IReadOnlyList<NymphModuleActionLinkInfo> BuildOverviewLinks(NymphModuleManifestInfo manifest)
    {
        var links = new List<NymphModuleActionLinkInfo>();
        AddOverviewLink(links, "Registry manifest", manifest.ManifestUrl);
        AddOverviewLink(links, "Source repo", manifest.RepositoryUrl);
        if (!string.Equals(manifest.SourceSummary, manifest.RepositoryUrl, StringComparison.OrdinalIgnoreCase))
        {
            AddOverviewLink(links, "Source", manifest.SourceSummary);
        }

        foreach (var link in manifest.OverviewLinks)
        {
            AddOverviewLink(links, link.Label, link.Url);
        }

        return links;
    }

    private static void AddOverviewLink(List<NymphModuleActionLinkInfo> links, string label, string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            links.Any(link => string.Equals(link.Url, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        links.Add(new NymphModuleActionLinkInfo(label, uri.AbsoluteUri));
    }

    private static string NormalizeInstallOptionsTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? "Install Options" : title.Trim();
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
