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
}
