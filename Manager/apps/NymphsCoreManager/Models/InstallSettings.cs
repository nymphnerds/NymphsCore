namespace NymphsCoreManager.Models;

public sealed class InstallSettings
{
    public string DistroName { get; set; } = "NymphsCore";

    public string LinuxUser { get; set; } = "nymph";

    public required string TarPath { get; set; }

    public required string InstallLocation { get; set; }

    public bool RepairExistingDistro { get; set; }

    /// <summary>
    /// When true, the installer only runs lightweight module/add-on work
    /// (e.g. <c>install_nymphs_brain.sh</c>, optional model prefetch) and
    /// skips the heavy finalize + backend venv resync pass. Used by the
    /// "Add optional modules" action on an already-installed system so
    /// users can add Nymphs-Brain (or similar) without triggering a full
    /// 10-30 minute repair.
    /// </summary>
    public bool ModuleOnlyRun { get; set; }

    public WslConfigMode WslConfigMode { get; set; } = WslConfigMode.Recommended;

    public int WslMemoryGb { get; set; }

    public int WslProcessors { get; set; }

    public int WslSwapGb { get; set; }

    public string BrainInstallRoot { get; set; } = "/home/nymph/Nymphs-Brain";
}
