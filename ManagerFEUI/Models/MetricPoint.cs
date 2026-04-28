using System;

namespace ManagerFEUI.Models
{
    public record MetricPoint(double Value, DateTime Timestamp);

    public record ServiceStatus(
        string Name,
        string Pid,
        double CpuPercent,
        double RamPercent,
        double VramPercent,
        long RequestsInFlight,
        double AvgPromptTps,
        double AvgDecodeTps,
        int QueueSize,
        DateTime LastSeen)
    {
        public bool IsHealthy => !string.IsNullOrEmpty(Pid) && LastSeen.AddSeconds(30) > DateTime.UtcNow;
        public string CpuIcon => CpuPercent > 80 ? "🔴" : CpuPercent > 50 ? "🟡" : "🟢";
        public string MemoryIcon => RamPercent > 80 ? "🔴" : RamPercent > 50 ? "🟡" : "🟢";
    }
}