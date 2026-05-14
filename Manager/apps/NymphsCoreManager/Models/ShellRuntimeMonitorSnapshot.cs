namespace NymphsCoreManager.Models;

public sealed record ShellRuntimeMonitorSnapshot(
    bool IsAvailable,
    string DistributionLabel,
    string KernelLabel,
    string UptimeLabel,
    int CpuPercent,
    string MemoryUsageLabel,
    int MemoryPercent,
    string WslDiskUsageLabel,
    int DiskPercent,
    string WindowsDiskUsageLabel,
    string GpuVramLabel,
    string GpuTempLabel,
    string BrainLlmStateLabel,
    string BrainModelLabel,
    string BrainRemoteModelLabel,
    string BrainContextLabel,
    string BrainTokensPerSecondLabel)
{
    public static ShellRuntimeMonitorSnapshot Offline { get; } = new(
        false,
        "WSL: Offline",
        "Kernel: -",
        "Uptime: -",
        0,
        "- / -",
        0,
        "- / -",
        0,
        "- / -",
        "Unavailable",
        "Unavailable",
        "LLM: Offline",
        "Local: -",
        "Remote: -",
        "Context: -",
        "TPS: -");
}
