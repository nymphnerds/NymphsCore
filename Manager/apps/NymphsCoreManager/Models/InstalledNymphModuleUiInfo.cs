namespace NymphsCoreManager.Models;

public sealed record InstalledNymphModuleUiInfo(
    string ModuleId,
    string Title,
    string Entrypoint,
    string WindowsPath);
