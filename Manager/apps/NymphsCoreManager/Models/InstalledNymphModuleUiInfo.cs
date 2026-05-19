namespace NymphsCoreManager.Models;

public sealed record InstalledNymphModuleUiInfo(
    string ModuleId,
    string Type,
    string Title,
    string Entrypoint,
    string WindowsPath,
    bool RequiresRunning = false,
    string StartAction = "");
