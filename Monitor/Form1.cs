using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace LlamaServerMonitor;

public partial class MainForm : Form
{
    private System.Windows.Forms.Timer? _statusTimer;
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private Label? _statusLabel;
    private Label? _modelLabel;
    private Label? _contextLabel;
    private Label? _vramLabel;
    private Label? _tempLabel;
    private Label? _tpsLabel;
    private Button? _refreshBtn;
    private ContextMenuStrip? _mainMenu;

    private DateTime _startTime = DateTime.Now;
    private bool _isRunning = false;

    // Dark theme colors
    private static readonly Color BGColor = Color.Black;
    private static readonly Color Cyan = Color.FromArgb(0, 188, 212);
    private static readonly Color LightGreen = Color.FromArgb(139, 195, 74);
    private static readonly Color Orange = Color.FromArgb(255, 152, 0);
    private static readonly Color RedAccent = Color.FromArgb(255, 82, 82);
    private static readonly Color Yellow = Color.FromArgb(255, 235, 59);

    public MainForm()
    {
        InitializeComponent();
        BuildUI();
        StartMonitoring();
    }

    private void InitializeComponent()
    {
        Text = "Llama Server Monitor";
        Size = new Size(420, 260);
        MinimumSize = new Size(420, 260);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        BackColor = BGColor;
    }

    private void BuildUI()
    {
        // Main menu
        _mainMenu = new ContextMenuStrip();
        _mainMenu.Items.Add("Refresh", null, (s, e) => CheckServer());
        _mainMenu.Items.Add("-");
        _mainMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

        // Status label (top-left)
        _statusLabel = new Label
        {
            Name = "statusLabel",
            Text = "Checking...",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18),
            ForeColor = Color.Gray
        };
        Controls.Add(_statusLabel);

        // Refresh button (top-right)
        _refreshBtn = new Button
        {
            Name = "refreshBtn",
            Text = "Refresh",
            Location = new Point(320, 14),
            Size = new Size(75, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f)
        };
        _refreshBtn.Click += (s, e) => CheckServer();
        Controls.Add(_refreshBtn);

        int y = 58;
        int valueX = 135;

        // Model row
        Controls.Add(CreateLabel("Model:", 20, y));
        _modelLabel = CreateLabel("—", valueX, y, Cyan);
        Controls.Add(_modelLabel);
        y += 30;

        // Context row
        Controls.Add(CreateLabel("Context:", 20, y));
        _contextLabel = CreateLabel("—", valueX, y, LightGreen);
        Controls.Add(_contextLabel);
        y += 30;

        // GPU VRAM row
        Controls.Add(CreateLabel("GPU VRAM:", 20, y));
        _vramLabel = CreateLabel("—", valueX, y, Orange);
        Controls.Add(_vramLabel);
        y += 30;

        // GPU Temp row
        Controls.Add(CreateLabel("GPU Temp:", 20, y));
        _tempLabel = CreateLabel("—", valueX, y, RedAccent);
        Controls.Add(_tempLabel);
        y += 30;

        // Tokens/sec row
        Controls.Add(CreateLabel("Tokens/sec:", 20, y));
        _tpsLabel = CreateLabel("—", valueX, y, Yellow);
        Controls.Add(_tpsLabel);

        // Tray icon
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Show", null, (s, e) => ShowForm());
        _trayMenu.Items.Add("Refresh", null, (s, e) => CheckServer());
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

        _trayIcon = new NotifyIcon
        {
            Text = "Llama Server Monitor",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (s, e) => ShowForm();
    }

    private static Label CreateLabel(string text, int x, int y, Color? valueColor = null)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = false,
            Width = valueColor.HasValue ? 260 : 120,
            Location = new Point(x, y),
            BackColor = Color.Transparent,
            ForeColor = valueColor ?? Color.White
        };
        if (valueColor.HasValue)
        {
            label.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        return label;
    }

    private void ShowForm()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        Focus();
    }

    private void StartMonitoring()
    {
        _statusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _statusTimer.Tick += (s, e) => CheckServer();
        _statusTimer.Start();
        CheckServer();
    }

    private async void CheckServer()
    {
        await Task.Run(async () =>
        {
            var processId = await GetLlamaServerPid();
            var isRunning = processId.HasValue;

            Invoke(() =>
            {
                if (isRunning && !_isRunning)
                {
                    _startTime = DateTime.Now;
                }
                _isRunning = isRunning;
            });

            if (!isRunning)
            {
                Invoke(() =>
                {
                    UpdateStatus("⏹ OFFLINE", Color.Red);
                    _modelLabel!.Text = "—";
                    _contextLabel!.Text = "—";
                    _vramLabel!.Text = "—";
                    _tempLabel!.Text = "—";
                    _tpsLabel!.Text = "—";
                    _trayIcon!.Text = "⏹ Llama Server OFFLINE";
                });
                return;
            }

            var model = await WslQuery("model");
            var context = await WslQuery("context");
            var vram = await WslQuery("gpu-vram");
            var temp = await WslQuery("gpu-temp");
            var tps = await WslQuery("tps");

            Invoke(() =>
            {
                UpdateStatus("▶ RUNNING", Color.Green);
                _modelLabel!.Text = string.IsNullOrEmpty(model) ? "—" : model;
                _contextLabel!.Text = string.IsNullOrEmpty(context) ? "—" : context;
                _vramLabel!.Text = string.IsNullOrEmpty(vram) ? "—" : vram;
                _tempLabel!.Text = string.IsNullOrEmpty(temp) ? "—" : temp;
                _tpsLabel!.Text = string.IsNullOrEmpty(tps) ? "—" : tps;
                _trayIcon!.Text = $"▶ Llama Server running";
            });
        });
    }

    private void UpdateStatus(string text, Color color)
    {
        _statusLabel!.Text = text;
        _statusLabel!.ForeColor = color;
    }

    private static readonly string WslDistro = "NymphsCore";
    private static readonly string MonitorScript = "~/Nymphs-Brain/scripts/monitor_query.sh";

    /// <summary>
    /// Run a query through the WSL helper script.
    /// Format: wsl.exe -d NymphsCore ~/Nymphs-Brain/scripts/monitor_query.sh query
    /// </summary>
    private static async Task<string> WslQuery(string query)
    {
        try
        {
            var scriptArgs = $"{MonitorScript} {query}";

            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {WslDistro} {scriptArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null) return "";
            proc.WaitForExit(8000);
            var output = (await proc.StandardOutput.ReadToEndAsync()).Trim();
            return output;
        }
        catch
        {
            return "";
        }
    }

    // WSL process detection
    private async Task<int?> GetLlamaServerPid()
    {
        var output = await WslQuery("pid");
        if (!string.IsNullOrEmpty(output) && int.TryParse(output, out var pid))
            return pid;
        return null;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer?.Stop();
        _trayIcon?.Dispose();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusTimer?.Dispose();
            _trayIcon?.Dispose();
            _mainMenu?.Dispose();
            _trayMenu?.Dispose();
        }
        base.Dispose(disposing);
    }
}