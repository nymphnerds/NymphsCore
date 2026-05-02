namespace NymphsCoreManager.Models;

public sealed record ZImageTrainerStatus(
    string InstallState,
    bool RepoExists,
    bool VenvExists,
    bool DatasetRootExists,
    bool OutputRootExists,
    bool Running,
    int LoraCount,
    int DatasetCount,
    bool OfficialUiRunning,
    bool GradioUiRunning,
    string Detail)
{
    public static ZImageTrainerStatus Unknown(string detail) =>
        new("unknown", false, false, false, false, false, 0, 0, false, false, detail);

    public bool Installed => string.Equals(InstallState, "installed", StringComparison.OrdinalIgnoreCase);

    public string ReadinessLabel =>
        Running
            ? "Training Active"
            : Installed
                ? "Installed"
                : "Not Installed";

    public string BadgeBackground =>
        Running
            ? "#B7791F"
            : Installed
                ? "#235756"
                : "#6B6259";
}
