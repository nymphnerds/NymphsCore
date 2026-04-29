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
    string Detail)
{
    public static ZImageTrainerStatus Unknown(string detail) =>
        new("unknown", false, false, false, false, false, 0, 0, detail);

    public bool Installed => string.Equals(InstallState, "installed", StringComparison.OrdinalIgnoreCase);

    public string ReadinessLabel =>
        Running
            ? "Training"
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
