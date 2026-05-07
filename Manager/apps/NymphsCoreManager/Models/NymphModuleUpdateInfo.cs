namespace NymphsCoreManager.Models;

public sealed record NymphModuleUpdateInfo(
    string ModuleId,
    string? InstalledVersion,
    string? RemoteVersion,
    bool IsUpdateAvailable,
    string Detail);
