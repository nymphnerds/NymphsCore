using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ManagerFEUI.Services
{
    public enum ServerState
    {
        Unknown,
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }

    public class ServerManagerService : IDisposable
    {
        private readonly DispatcherTimer _healthCheckTimer;
        private bool _disposed;
        private const string ScriptPathInWSL = "/tmp/llama_health.sh";

        public ServerState State { get; private set; } = ServerState.Unknown;
        public string Pid { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string ContextSize { get; private set; } = "";
        public string GpuVram { get; private set; } = "";
        public string GpuTemp { get; private set; } = "";
        public double TokensPerSec { get; private set; }
        public int QueueDepth { get; private set; }
        public TimeSpan Uptime { get; private set; }

        public event Action<ServerState>? OnStateChanged;
        public event Action? OnStatusUpdated;

        public ServerManagerService()
        {
            _healthCheckTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => HealthCheck(), Dispatcher.CurrentDispatcher);
        }

        public void StartMonitoring()
        {
            DeployScript();
            _healthCheckTimer.Start();
        }
        public void StopMonitoring() => _healthCheckTimer.Stop();

        private void SetState(ServerState newState)
        {
            if (State != newState)
            {
                State = newState;
                OnStateChanged?.Invoke(newState);
            }
        }

        public async Task StartServer()
        {
            SetState(ServerState.Starting);
            try
            {
                await ExecuteWslAsync("bash ~/Nymphs-Brain/scripts/start_server.sh");
                await Task.Delay(2000);
                HealthCheck();
            }
            catch
            {
                SetState(ServerState.Error);
            }
        }

        public async Task StopServer()
        {
            SetState(ServerState.Stopping);
            try
            {
                await ExecuteWslAsync("pkill -f llama-server");
                await Task.Delay(1000);
                HealthCheck();
            }
            catch
            {
                HealthCheck();
            }
        }

        public async Task RestartServer()
        {
            await StopServer();
            await Task.Delay(2000);
            await StartServer();
        }

        public async Task RepairInstall()
        {
            await ExecuteWslAsync("bash ~/NymphsCore/repair.sh 2>/dev/null || true");
        }

        private async void HealthCheck()
        {
            if (_disposed) return;

            try
            {
                var output = await ExecuteScriptAsync(ScriptPathInWSL, 15000);
                var fields = output.Trim().Split('\t');

                if (fields.Length >= 5 && fields[0].Trim() == "RUNNING")
                {
                    Pid = fields[1].Trim();

                    int.TryParse(fields[2].Trim(), out var queue);
                    QueueDepth = queue;

                    double.TryParse(fields[3].Trim(), out var tps);
                    TokensPerSec = tps;

                    var etimeStr = fields[4].Trim();
                    Uptime = ParseEtime(etimeStr);

                    SetState(ServerState.Running);
                }
                else
                {
                    if (State == ServerState.Starting)
                    {
                        SetState(ServerState.Error);
                    }
                    else if (State == ServerState.Stopping)
                    {
                        SetState(ServerState.Stopped);
                    }
                    else if (State == ServerState.Running)
                    {
                        SetState(ServerState.Stopped);
                    }
                    else if (State == ServerState.Unknown)
                    {
                        SetState(ServerState.Stopped);
                    }

                    Pid = "";
                    QueueDepth = 0;
                    TokensPerSec = 0;
                    Uptime = TimeSpan.Zero;
                }
            }
            catch
            {
                // On error, keep current state
            }

            OnStatusUpdated?.Invoke();
        }

        /// <summary>
        /// Deploy the health check script to WSL using base64 encoding.
        /// </summary>
        private void DeployScript()
        {
            try
            {
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "health_query.sh");
                if (!File.Exists(scriptPath))
                {
                    scriptPath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "", "health_query.sh");
                }
                if (!File.Exists(scriptPath))
                {
                    scriptPath = "health_query.sh";
                }

                if (!File.Exists(scriptPath)) return;

                var scriptContent = File.ReadAllText(scriptPath);
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
            }
            catch { }
        }

        private async Task<string> ExecuteScriptAsync(string scriptPath, int timeoutMs)
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

                using var proc = Process.Start(psi);
                if (proc == null) return "";

                proc.WaitForExit(timeoutMs);
                return await proc.StandardOutput.ReadToEndAsync();
            }
            catch { return ""; }
        }

        /// <summary>
        /// Parse ps etime format into TimeSpan.
        /// Formats: MM:SS, HH:MM:SS, D-HH:MM:SS
        /// </summary>
        private static TimeSpan ParseEtime(string etime)
        {
            try
            {
                etime = etime.Trim();
                if (string.IsNullOrEmpty(etime) || etime == "0:00" || etime == "0") return TimeSpan.Zero;

                int days = 0;
                var timePart = etime;

                var dashIdx = timePart.IndexOf('-');
                if (dashIdx > 0)
                {
                    if (int.TryParse(timePart.Substring(0, dashIdx), out days))
                    {
                        timePart = timePart.Substring(dashIdx + 1);
                    }
                }

                var parts = timePart.Split(':');
                int hours = 0, minutes = 0, seconds = 0;

                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out minutes);
                    int.TryParse(parts[1], out seconds);
                }
                else if (parts.Length == 3)
                {
                    int.TryParse(parts[0], out hours);
                    int.TryParse(parts[1], out minutes);
                    int.TryParse(parts[2], out seconds);
                }

                return TimeSpan.FromDays(days)
                    .Add(TimeSpan.FromHours(hours))
                    .Add(TimeSpan.FromMinutes(minutes))
                    .Add(TimeSpan.FromSeconds(seconds));
            }
            catch { return TimeSpan.Zero; }
        }

        private async Task ExecuteWslAsync(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d NymphsCore bash -c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit(30000);
                await proc.StandardOutput.ReadToEndAsync();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _healthCheckTimer.Stop();
            _healthCheckTimer.Tick -= (_, _) => HealthCheck();
        }
    }
}