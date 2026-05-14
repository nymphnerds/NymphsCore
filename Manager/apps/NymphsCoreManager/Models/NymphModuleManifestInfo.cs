namespace NymphsCoreManager.Models;

public sealed record NymphModuleManifestInfo(
    string Id,
    string Name,
    string ShortName,
    string Category,
    string Kind,
    string Version,
    string Description,
    string OverviewDetail,
    string ManifestUrl,
    string RepositoryUrl,
    string SourceSummary,
    string InstallRoot,
    IReadOnlyList<NymphModuleActionFieldInfo> InstallFields,
    string InstallOptionsTitle,
    string ManagerUiTitle,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<NymphModuleActionInfo> ManagerActions,
    IReadOnlyList<NymphModuleActionGroupInfo> ManagerActionGroups,
    IReadOnlyList<string> DevCapabilities,
    int SortOrder);
