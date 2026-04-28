using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagerFEUI.Models;
using ManagerFEUI.Services;
using ManagerFEUI.Views;

namespace ManagerFEUI.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private object? _currentPage;
        private string _currentPageName = "Dashboard";
        private string _statusText = "Checking...";
        private string _statusColor = "#7a9488";
        private string _modelInfo = "—";
        private string _contextInfo = "—";
        private string _gpuTempInfo = "—";
        private string _wslStatusText = "—";
        private string _wslStatusColor = "#7a9488";
        private bool _hasErrors = false;
        private bool _serverBusy = false;

        public LlamaMonitorService MonitorService { get; }
        public ServerManagerService ServerManager { get; }
        public WslService Wsl { get; }

        public ObservableCollection<string> AllLogLines { get; } = new();
        public ObservableCollection<string> RecentLogLines { get; } = new();
        public ObservableCollection<MetricCardData> MetricCards { get; } = new();

        // Metric display properties
        private string _cpuUsageText = "--";
        private string _cpuUsageSub = "--";
        private string _ramUsageText = "--";
        private string _ramUsageSub = "--";
        private string _vramUsageText = "--";
        private string _vramUsageSub = "--";
        private string _latencyText = "--";
        private string _latencySub = "--";
        private string _tpsText = "--";
        private string _tpsSub = "--";
        private string _queueText = "--";
        private string _queueSub = "--";
        private string _uptimeText = "--";
        private string _uptimeSub = "--";

        private List<MetricPoint> _cpuHistory = new();
        private List<MetricPoint> _ramHistory = new();
        private List<MetricPoint> _vramHistory = new();

        // WSL Status
        public string WslStatusText { get => _wslStatusText; set { _wslStatusText = value; OnPropertyChanged(); } }
        public string WslStatusColor { get => _wslStatusColor; set { _wslStatusColor = value; OnPropertyChanged(); } }

        // Status bar
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public string ModelInfo
        {
            get => _modelInfo;
            set { _modelInfo = value; OnPropertyChanged(); }
        }

        public string ContextInfo
        {
            get => _contextInfo;
            set { _contextInfo = value; OnPropertyChanged(); }
        }

        public string GpuTempInfo
        {
            get => _gpuTempInfo;
            set { _gpuTempInfo = value; OnPropertyChanged(); }
        }

        public bool ServerBusy
        {
            get => _serverBusy;
            set { _serverBusy = value; OnPropertyChanged(); }
        }

        // Current page
        public object? CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public string CurrentPageName
        {
            get => _currentPageName;
            set { _currentPageName = value; OnPropertyChanged(); }
        }

        // Metrics
        public string CpuUsageText { get => _cpuUsageText; set { _cpuUsageText = value; OnPropertyChanged(); } }
        public string CpuUsageSub { get => _cpuUsageSub; set { _cpuUsageSub = value; OnPropertyChanged(); } }
        public string RamUsageText { get => _ramUsageText; set { _ramUsageText = value; OnPropertyChanged(); } }
        public string RamUsageSub { get => _ramUsageSub; set { _ramUsageSub = value; OnPropertyChanged(); } }
        public string VramUsageText { get => _vramUsageText; set { _vramUsageText = value; OnPropertyChanged(); } }
        public string VramUsageSub { get => _vramUsageSub; set { _vramUsageSub = value; OnPropertyChanged(); } }
        public string LatencyText { get => _latencyText; set { _latencyText = value; OnPropertyChanged(); } }
        public string LatencySub { get => _latencySub; set { _latencySub = value; OnPropertyChanged(); } }
        public string TpsText { get => _tpsText; set { _tpsText = value; OnPropertyChanged(); } }
        public string TpsSub { get => _tpsSub; set { _tpsSub = value; OnPropertyChanged(); } }
        public string QueueText { get => _queueText; set { _queueText = value; OnPropertyChanged(); } }
        public string QueueSub { get => _queueSub; set { _queueSub = value; OnPropertyChanged(); } }
        public string UptimeText { get => _uptimeText; set { _uptimeText = value; OnPropertyChanged(); } }
        public string UptimeSub { get => _uptimeSub; set { _uptimeSub = value; OnPropertyChanged(); } }

        public List<MetricPoint> CpuHistory => _cpuHistory;
        public List<MetricPoint> RamHistory => _ramHistory;
        public List<MetricPoint> VramHistory => _vramHistory;

        // Commands
        public ICommand NavigateDashboardCommand { get; }
        public ICommand NavigateLogsCommand { get; }
        public ICommand NavigateToolsCommand { get; }
        public ICommand NavigateBrainCommand { get; }
        public ICommand NavigateAddonsCommand { get; }
        public ICommand NavigateInstallerCommand { get; }

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand RestartServerCommand { get; }
        public ICommand RepairInstallCommand { get; }
        public ICommand ShowLogsCommand { get; }
        public ICommand RefreshCommand { get; }

        // WSL Commands
        public ICommand StartWslCommand { get; }
        public ICommand StopWslCommand { get; }

        // INotifyDataErrorInfo
        private Dictionary<string, List<string>> _errors = new();
        public System.Collections.IEnumerable GetErrors(string? propertyName)
        {
            propertyName ??= "";
            return _errors.TryGetValue(propertyName, out var errs) ? (System.Collections.IEnumerable)errs : Array.Empty<string>();
        }
        public bool HasErrors => _hasErrors;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public DashboardViewModel()
        {
            MonitorService = new LlamaMonitorService();
            ServerManager = new ServerManagerService();
            Wsl = new WslService();

            // Navigation commands
            NavigateDashboardCommand = new RelayCommand(() => NavigateTo("Dashboard", new DashboardPage()));
            NavigateLogsCommand = new RelayCommand(() => NavigateTo("Logs", new LogsPage()));
            NavigateToolsCommand = new RelayCommand(() => NavigateTo("Runtime Tools", new PlaceholderPage("Runtime Tools")));
            NavigateBrainCommand = new RelayCommand(() => NavigateTo("Brain", new PlaceholderPage("Brain")));
            NavigateAddonsCommand = new RelayCommand(() => NavigateTo("Addons", new PlaceholderPage("Addons")));
            NavigateInstallerCommand = new RelayCommand(() => NavigateTo("Installer", new PlaceholderPage("Installer")));

            // Server commands
            StartServerCommand = new RelayCommand(async () => await ExecuteServerAction("Starting...", ServerManager.StartServer));
            StopServerCommand = new RelayCommand(async () => await ExecuteServerAction("Stopping...", ServerManager.StopServer));
            RestartServerCommand = new RelayCommand(async () => await ExecuteServerAction("Restarting...", ServerManager.RestartServer));
            RepairInstallCommand = new RelayCommand(async () => await ExecuteServerAction("Repairing...", ServerManager.RepairInstall));
            ShowLogsCommand = new RelayCommand(() => NavigateTo("Logs", new LogsPage()));
            RefreshCommand = new RelayCommand(() => MonitorService.Execute());

            // WSL commands
            StartWslCommand = new RelayCommand(() =>
            {
                Wsl.StartWsl();
                UpdateWslState(Wsl.State);
            });
            StopWslCommand = new RelayCommand(() =>
            {
                Wsl.StopWsl();
                UpdateWslState(Wsl.State);
            });

            // Wire up events
            MonitorService.OnStatusChanged += status => UpdateStatus(status);
            MonitorService.OnHistoryUpdated += (cpu, mem, vram) => UpdateHistory(cpu, mem, vram);
            ServerManager.OnStateChanged += state => UpdateServerState(state);
            ServerManager.OnStatusUpdated += () => UpdateFromServerManager();
            Wsl.OnStateChanged += state => UpdateWslState(state);

            // Initialize pages
            CurrentPage = new DashboardPage();

            // Start monitoring
            MonitorService.StartMonitoring();
            ServerManager.StartMonitoring();
            Wsl.StartPolling();

            // Initialize metric cards
            InitializeMetricCards();

            // Initial WSL state update
            UpdateWslState(Wsl.State);
        }

        private void InitializeMetricCards()
        {
            MetricCards.Clear();
            MetricCards.Add(new MetricCardData("CPU Usage", 0, "--", "--", "#2dd4a8"));
            MetricCards.Add(new MetricCardData("WSL Memory", 0, "--", "--", "#3b82f6"));
            MetricCards.Add(new MetricCardData("VRAM Usage", 0, "--", "--", "#f59e0b"));
            MetricCards.Add(new MetricCardData("API Latency", 0, "--", "--", "#a855f7"));
            MetricCards.Add(new MetricCardData("Token Gen", 0, "--", "--", "#ec4899"));
            MetricCards.Add(new MetricCardData("Queue Depth", 0, "--", "--", "#06b6d4"));
            MetricCards.Add(new MetricCardData("Uptime", 0, "--", "--", "#f97316"));
        }

        /// <summary>
        /// Push real metric data into the observable cards so the UI updates.
        /// </summary>
        private void UpdateMetricCardsFromData()
        {
            int idx = 0;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.Percentage = _cpuHistory.Count > 0 ? _cpuHistory.Last().Value : 0;
                card.DisplayValue = CpuUsageText;
                card.Sub = CpuUsageSub;
            }
            idx++;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.Percentage = _ramHistory.Count > 0 ? _ramHistory.Last().Value : 0;
                card.DisplayValue = RamUsageText;
                card.Sub = RamUsageSub;
            }
            idx++;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.Percentage = _vramHistory.Count > 0 ? _vramHistory.Last().Value : 0;
                card.DisplayValue = VramUsageText;
                card.Sub = VramUsageSub;
            }
            idx++;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.DisplayValue = LatencyText;
                card.Sub = LatencySub;
            }
            idx++;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.DisplayValue = TpsText;
                card.Sub = TpsSub;
            }
            idx++;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.DisplayValue = QueueText;
                card.Sub = QueueSub;
            }
            idx++;
            if (MetricCards.Count > idx)
            {
                var card = MetricCards[idx];
                card.DisplayValue = UptimeText;
                card.Sub = UptimeSub;
            }
        }

        private void NavigateTo(string name, object page)
        {
            CurrentPageName = name;
            CurrentPage = page;
        }

        private void UpdateStatus(ServerStatus status)
        {
            switch (status)
            {
                case ServerStatus.Online:
                    StatusText = "Connected";
                    StatusColor = "#2dd4a8";
                    break;
                case ServerStatus.Offline:
                    StatusText = "Disconnected";
                    StatusColor = "#ef4444";
                    break;
                case ServerStatus.Starting:
                    StatusText = "Starting...";
                    StatusColor = "#f59e0b";
                    break;
                case ServerStatus.Error:
                    StatusText = "Error";
                    StatusColor = "#ef4444";
                    break;
            }
        }

        private void UpdateServerState(ServerState state)
        {
            switch (state)
            {
                case ServerState.Running:
                    StatusText = "Connected";
                    StatusColor = "#2dd4a8";
                    break;
                case ServerState.Starting:
                    StatusText = "Starting...";
                    StatusColor = "#f59e0b";
                    ServerBusy = true;
                    break;
                case ServerState.Stopping:
                    StatusText = "Stopping...";
                    StatusColor = "#f59e0b";
                    ServerBusy = true;
                    break;
                case ServerState.Stopped:
                    StatusText = "Disconnected";
                    StatusColor = "#ef4444";
                    ServerBusy = false;
                    break;
                case ServerState.Error:
                    StatusText = "Error";
                    StatusColor = "#ef4444";
                    ServerBusy = false;
                    break;
                default:
                    StatusText = "Checking...";
                    StatusColor = "#7a9488";
                    break;
            }
        }

        private void UpdateWslState(WslState state)
        {
            switch (state)
            {
                case WslState.Running:
                    WslStatusText = "WSL Running";
                    WslStatusColor = "#2dd4a8";
                    break;
                case WslState.Starting:
                    WslStatusText = "WSL Starting...";
                    WslStatusColor = "#f59e0b";
                    break;
                case WslState.Stopping:
                    WslStatusText = "WSL Stopping...";
                    WslStatusColor = "#f59e0b";
                    break;
                case WslState.Stopped:
                    WslStatusText = "WSL Stopped";
                    WslStatusColor = "#7a9488";
                    break;
                case WslState.NotInstalled:
                    WslStatusText = "WSL Not Installed";
                    WslStatusColor = "#ef4444";
                    break;
            }
        }

        private void UpdateFromServerManager()
        {
            ModelInfo = string.IsNullOrEmpty(ServerManager.Model) ? "—" : ServerManager.Model;
            ContextInfo = string.IsNullOrEmpty(ServerManager.ContextSize) ? "—" : ServerManager.ContextSize;
            GpuTempInfo = string.IsNullOrEmpty(ServerManager.GpuTemp) ? "—" : ServerManager.GpuTemp;

            if (ServerManager.State == ServerState.Running)
            {
                TpsText = ServerManager.TokensPerSec.ToString("F1");
                TpsSub = "tok/s";
                QueueText = ServerManager.QueueDepth.ToString();
                QueueSub = "queued";
                UptimeText = FormatUptime(ServerManager.Uptime);
                UptimeSub = "running";
            }
            else
            {
                TpsText = "--";
                TpsSub = "tok/s";
                QueueText = "--";
                QueueSub = "queued";
                UptimeText = "--";
                UptimeSub = "running";
            }

            UpdateMetricCardsFromData();
        }

        private void UpdateHistory(List<MetricPoint> cpu, List<MetricPoint> mem, List<MetricPoint> vram)
        {
            _cpuHistory = cpu;
            _ramHistory = mem;
            _vramHistory = vram;

            if (cpu.Count > 0)
            {
                var latest = cpu.Last();
                CpuUsageText = latest.Value.ToString("F1") + "%";
                CpuUsageSub = "System CPU";
            }
            if (mem.Count > 0)
            {
                var latest = mem.Last();
                // Display as GB: "45.9 GB / 61.6 GB"
                double usedGb = MonitorService.MemUsedGb;
                double totalGb = MonitorService.TotalMemGb;
                if (usedGb > 0 && totalGb > 0)
                {
                    RamUsageText = $"{usedGb:F1} GB / {totalGb:F1} GB";
                }
                else
                {
                    RamUsageText = latest.Value.ToString("F1") + "%";
                }
                RamUsageSub = "WSL Memory";
            }
            if (vram.Count > 0)
            {
                var latest = vram.Last();
                VramUsageText = latest.Value.ToString("F1") + "%";
                VramUsageSub = "GPU VRAM";
            }

            UpdateMetricCardsFromData();

            OnPropertyChanged(nameof(CpuHistory));
            OnPropertyChanged(nameof(RamHistory));
            OnPropertyChanged(nameof(VramHistory));
        }

        private async Task ExecuteServerAction(string status, Func<Task> action)
        {
            ServerBusy = true;
            StatusText = status;
            StatusColor = "#f59e0b";
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _errors["Server"] = new List<string> { ex.Message };
                _hasErrors = true;
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Server"));
            }
            finally
            {
                ServerBusy = false;
            }
        }

        private string FormatUptime(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h";
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
