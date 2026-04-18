namespace Nymphs3DInstaller.Models;

public sealed class InstallSettings
{
    public string DistroName { get; set; } = "NymphsCore";

    public string LinuxUser { get; set; } = "nymph";

    public required string TarPath { get; set; }

    public required string InstallLocation { get; set; }

    public bool PrefetchModelsNow { get; set; }

    public bool RepairExistingDistro { get; set; }

    public string HuggingFaceToken { get; set; } = string.Empty;

    public WslConfigMode WslConfigMode { get; set; } = WslConfigMode.Recommended;

    public int WslMemoryGb { get; set; }

    public int WslProcessors { get; set; }

    public int WslSwapGb { get; set; }

    public bool InstallNymphsBrain { get; set; }

    public bool DownloadBrainModelNow { get; set; }

    public string BrainInstallRoot { get; set; } = "/home/nymph/Nymphs-Brain";

    public string BrainModelId { get; set; } = "auto";

    public string BrainQuantization { get; set; } = "q4_k_m";

    public int BrainContextLength { get; set; } = 16384;
}
