using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace ManagerFEUI.Services
{
    public enum WslState
    {
        NotInstalled,
        Stopped,
        Starting,
        Running,
        Stopping
    }

    public class WslService : IDisposable
    {
        private readonly DispatcherTimer _pollTimer;
        private bool _disposed;
        private WslState _state = WslState.Stopped;

        public WslState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChanged?.Invoke(value);
                }
            }
        }

        public bool IsRunning => State == WslState.Running;
        public bool IsAvailable => State != WslState.NotInstalled;

        public event Action<WslState>? OnStateChanged;

        public WslService()
        {
            _pollTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => CheckWslStatus(), Dispatcher.CurrentDispatcher);
            CheckWslStatus();
        }

        public void StartPolling() => _pollTimer.Start();
        public void StopPolling() => _pollTimer.Stop();

        public void CheckWslStatus()
        {
            if (_disposed) return;

            try
            {
                // Check if WSL is installed and the distro exists
                var listOutput = RunNativeCommand("wsl.exe", "-l -q");
                if (string.IsNullOrWhiteSpace(listOutput))
                {
                    State = WslState.NotInstalled;
                    return;
                }

                // Check if NymphsCore distro is listed
                if (!listOutput.Split('\n').Any(l => l.Trim().Equals("NymphsCore", StringComparison.OrdinalIgnoreCase)))
                {
                    State = WslState.NotInstalled;
                    return;
                }

                // Check if the distro is currently running
                var runningOutput = RunNativeCommand("wsl.exe", "-l -r -q");
                bool isRunning = runningOutput.Split('\n').Any(l => l.Trim().Equals("NymphsCore", StringComparison.OrdinalIgnoreCase));

                if (isRunning)
                {
                    if (State != WslState.Running)
                    {
                        State = WslState.Running;
                    }
                }
                else
                {
                    if (State == WslState.Starting)
                    {
                        State = WslState.Stopped;
                    }
                    else if (State == WslState.Stopping)
                    {
                        State = WslState.Stopped;
                    }
                    else if (State == WslState.Running)
                    {
                        State = WslState.Stopped;
                    }
                    else if (State == WslState.NotInstalled)
                    {
                        State = WslState.Stopped;
                    }
                }
            }
            catch
            {
                State = WslState.NotInstalled;
            }
        }

        public void StartWsl()
        {
            State = WslState.Starting;
            try
            {
                // Launch WSL in background - this starts the distro
                var psi = new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = "-d NymphsCore bash -c 'echo WSL started'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(15000);
                }
                CheckWslStatus();
            }
            catch
            {
                State = WslState.Stopped;
            }
        }

        public void StopWsl()
        {
            State = WslState.Stopping;
            try
            {
                RunNativeCommand("wsl.exe", "--terminate NymphsCore");
                System.Threading.Thread.Sleep(1000);
                CheckWslStatus();
            }
            catch
            {
                CheckWslStatus();
            }
        }

        public string ExecuteWslCommand(string bashCommand, int timeoutMs = 5000)
        {
            return RunNativeCommand("wsl.exe", $"-d NymphsCore bash -c \"{bashCommand}\"", timeoutMs);
        }

        private string RunNativeCommand(string fileName, string arguments, int timeoutMs = 5000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return "";
                p.WaitForExit(timeoutMs);
                return p.StandardOutput.ReadToEnd();
            }
            catch { return ""; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer.Stop();
        }
    }
}