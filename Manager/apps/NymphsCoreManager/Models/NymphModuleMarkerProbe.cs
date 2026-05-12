namespace NymphsCoreManager.Models;

public sealed record NymphModuleMarkerProbe(
    string ModuleId,
    bool MarkerPresent,
    bool InstallRootPresent,
    bool RepairCandidatePresent,
    string Version,
    string InstallRoot);
