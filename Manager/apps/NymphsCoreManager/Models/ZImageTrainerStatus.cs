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
    bool QueueWorkerRunning,
    bool QueueRunning,
    bool GradioUiRunning,
    string ActiveState,
    string ActiveInfo,
    string Detail,
    bool SelectedJobExists = false,
    string SelectedJobName = "",
    string SelectedJobState = "",
    string SelectedJobInfo = "",
    string SelectedDatasetName = "",
    bool SelectedDatasetVisible = false,
    int SelectedDatasetImageCount = 0)
{
    public static ZImageTrainerStatus Unknown(string detail) =>
        new("unknown", false, false, false, false, false, 0, 0, false, false, false, false, "idle", string.Empty, detail);

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
