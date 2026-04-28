namespace NymphsCoreManager.Models;

public sealed record BrainMonitorSnapshot(
    bool IsRunning,
    string Model,
    string Context,
    string GpuVram,
    string GpuTemp,
    string TokensPerSecond)
{
    public static BrainMonitorSnapshot Offline { get; } = new(false, "-", "-", "-", "-", "-");
}
