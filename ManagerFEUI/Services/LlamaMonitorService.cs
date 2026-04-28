using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using ManagerFEUI.Models;

namespace ManagerFEUI.Services
{
    public enum ServerStatus
    {
        Online,
        Offline,
        Starting,
        Error
    }

    public class LlamaMonitorService : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _logTimer;
        private readonly ObservableCollection<MetricPoint> _cpuHistory = new();
        private readonly ObservableCollection<MetricPoint> _memHistory = new();
        private readonly ObservableCollection<MetricPoint> _vramHistory = new();
        private readonly ObservableCollection<string> _logLines = new();
        private const int MaxHistory = 60;
        private const string DebugLogPath = "/tmp/llama_monitor_debug.log";
        private const string ScriptPathInWSL = "/tmp/llama_monitor.sh";
        
        // Windows CPU counter (non-readOnly so internal state tracking works correctly)
        private PerformanceCounter? _cpuCounter;
        private bool _cpuCounterInitialized = false;
        private bool _disposed = false;

        public ServiceStatus Status { get; private set; } = new(
            "llama-server", "", 0, 0, 0, 0, 0, 0, 0, DateTime.UtcNow);

        // Raw display values for the RAM card
        public double MemUsedGb { get; private set; }
        public double TotalMemGb { get; private set; }

        public IReadOnlyList<MetricPoint> CpuHistory => _cpuHistory;
        public IReadOnlyList<MetricPoint> MemHistory => _memHistory;
        public IReadOnlyList<MetricPoint> VramHistory => _vramHistory;
        public IReadOnlyList<string> LogLines => _logLines;

        public event Action<ServerStatus>? OnStatusChanged;
        public event Action<List<MetricPoint>, List<MetricPoint>, List<MetricPoint>>? OnHistoryUpdated;
        public event Action? OnLogUpdated;

        public void StartMonitoring()
        {
            InitializeCpuCounter();
            DeployScript();
            _timer.Start();
        }
        public void StopMonitoring() => _timer.Stop();

        /// <summary>
        /// Initialize Windows PerformanceCounter for system-wide CPU usage.
        /// Non-readOnly mode so internal state tracking works correctly.
        /// First two reads are discarded (need time delta between samples).
        /// </summary>
        private void InitializeCpuCounter()
        {
            if (_cpuCounterInitialized) return;
            try
            {
                _cpuCounter = new PerformanceCounter(
                    "Processor", "% Processor Time", "_Total",
                    readOnly: false)
                {
                    MachineName = "."
                };
                // First read is always 0, so prime it
                _cpuCounter.NextValue();
                _cpuCounterInitialized = true;
                LogDebug("CPU counter initialized (stateful mode)");
            }
            catch (Exception ex)
            {
                LogDebug($"CPU counter init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current Windows system CPU usage percentage.
        /// </summary>
        private double GetWindowsCpuPercent()
        {
            try
            {
                if (_cpuCounter == null) return 0;
                double val = _cpuCounter.NextValue();
                return Math.Clamp(val, 0.0, 100.0);
            }
            catch (Exception ex)
            {
                LogDebug($"CPU counter read error: {ex.Message}");
            }
            return 0;
        }
        public void Execute() => Poll();

        public LlamaMonitorService()
        {
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Poll(), Dispatcher.CurrentDispatcher);
            _logTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => PollLogs(), Dispatcher.CurrentDispatcher);
        }

        private void Poll()
        {
            var s = ReadStatus();
            Status = s;

            var status = s.IsHealthy ? ServerStatus.Online : ServerStatus.Offline;
            OnStatusChanged?.Invoke(status);

            _cpuHistory.Add(new MetricPoint(s.CpuPercent, DateTime.UtcNow));
            _memHistory.Add(new MetricPoint(s.RamPercent, DateTime.UtcNow));
            _vramHistory.Add(new MetricPoint(s.VramPercent, DateTime.UtcNow));

            while (_cpuHistory.Count > MaxHistory) _cpuHistory.RemoveAt(0);
            while (_memHistory.Count > MaxHistory) _memHistory.RemoveAt(0);
            while (_vramHistory.Count > MaxHistory) _vramHistory.RemoveAt(0);

            OnHistoryUpdated?.Invoke(
                new List<MetricPoint>(_cpuHistory),
                new List<MetricPoint>(_memHistory),
                new List<MetricPoint>(_vramHistory));
        }

        private void PollLogs()
        {
            var lines = ReadLogTail();
            foreach (var l in lines)
            {
                _logLines.Add(l);
                if (_logLines.Count > 500) _logLines.RemoveAt(0);
            }
            OnLogUpdated?.Invoke();
        }

        /// <summary>
        /// Deploy the monitoring script to WSL /tmp. Reads the embedded monitor_query.sh
        /// from the output directory and copies it into WSL.
        /// </summary>
        private void DeployScript()
        {
            try
            {
                // Find the script file next to the executable
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "monitor_query.sh");
                if (!File.Exists(scriptPath))
                {
                    // Try parent directory (during debug)
                    scriptPath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "", "monitor_query.sh");
                }
                if (!File.Exists(scriptPath))
                {
                    // Last resort: try project root
                    scriptPath = "monitor_query.sh";
                }

                if (!File.Exists(scriptPath))
                {
                    LogDebug($"Script not found at any location");
                    return;
                }

                var scriptContent = File.ReadAllText(scriptPath);

                // Deploy to WSL using base64 encoding to avoid ALL shell interpretation issues
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(scriptContent));

                var psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = $"-d NymphsCore bash -c \"echo '{base64}' | base64 -d > {ScriptPathInWSL} && chmod +x {ScriptPathInWSL}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(10000);
                var err = p.StandardError.ReadToEnd();
                LogDebug($"DeployScript: {(string.IsNullOrEmpty(err) ? "OK" : "ERR: " + err)}");
            }
            catch (Exception ex)
            {
                LogDebug($"DeployScript exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute the monitoring script inside WSL and parse the TSV output.
        /// </summary>
        private ServiceStatus ReadStatus()
        {
            try
            {
                // Get Windows CPU separately
                double cpu = GetWindowsCpuPercent();

                var output = ExecuteScript(ScriptPathInWSL, 15000).Trim();

                LogDebug($"RAW: [{output}], WindowsCPU: {cpu:F1}%");

                if (string.IsNullOrEmpty(output))
                {
                    return Status with { CpuPercent = cpu, LastSeen = DateTime.UtcNow };
                }

                var fields = output.Split('\t');
                if (fields.Length < 9)
                {
                    LogDebug($"MALFORMED: fields={fields.Length}");
                    return Status with { CpuPercent = cpu, LastSeen = DateTime.UtcNow };
                }

                bool reachable = fields[0].Trim() == "1";
                var pid = fields[1].Trim();

                // Parse WSL-wide RAM from raw KB values
                long memUsedKb = ParseLong(fields[2]);
                long totalMemKb = ParseLong(fields[3]);
                double memPercent = 0;
                if (totalMemKb > 1000 && memUsedKb > 0)
                {
                    memPercent = (memUsedKb / (double)totalMemKb) * 100.0;
                    memPercent = Math.Clamp(memPercent, 0.0, 100.0);
                }

                // Parse VRAM percentage from raw MB values
                double vramUsed = ParseDouble(fields[4]);
                double vramTotal = ParseDouble(fields[5]);
                double vramPercent = 0;
                if (vramTotal > 100 && vramUsed > 0)
                {
                    vramPercent = (vramUsed / vramTotal) * 100.0;
                    vramPercent = Math.Clamp(vramPercent, 0.0, 100.0);
                }

                long queue = ParseLong(fields[6]);
                double ptps = ParseDouble(fields[7]);
                double dtps = ParseDouble(fields[8]);

                // Store raw GB values for display
                MemUsedGb = memUsedKb / 1048576.0; // KB to GB
                TotalMemGb = totalMemKb / 1048576.0;

                LogDebug($"PARSED: cpu={cpu:F1} (Windows), mem%={memPercent:F1} ({MemUsedGb:F1}GB/{TotalMemGb:F1}GB), vram%={vramPercent:F1} (used={vramUsed}/total={vramTotal}), pid={pid}, queue={queue}, ptps={ptps}, dtps={dtps}");

                return new ServiceStatus(
                    "llama-server",
                    pid,
                    cpu,
                    memPercent,
                    vramPercent,
                    queue,
                    ptps,
                    dtps,
                    (int)queue,
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                LogDebug($"EXCEPTION: {ex.Message}");
                return Status with { LastSeen = DateTime.UtcNow };
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _logTimer?.Stop();
            _cpuCounter?.Dispose();
        }

        private List<string> ReadLogTail()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = "-d NymphsCore bash -c \"tail -20 /tmp/llama-server.log 2>/dev/null\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(5000);
                var output = p.StandardOutput.ReadToEnd();
                return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// Execute a bash script inside WSL by path.
        /// </summary>
        private string ExecuteScript(string scriptPath, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = $"-d NymphsCore bash {scriptPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(timeoutMs);
                return p.StandardOutput.ReadToEnd();
            }
            catch { return ""; }
        }

        private void LogDebug(string msg)
        {
            try
            {
                File.AppendAllText(DebugLogPath, $"[{DateTime.UtcNow:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }

        private static double ParseDouble(string s)
        {
            s = s.Trim();
            return double.TryParse(s, out var v) ? v : 0;
        }

        private static long ParseLong(string s)
        {
            s = s.Trim();
            return long.TryParse(s, out var v) ? v : 0;
        }
    }
}