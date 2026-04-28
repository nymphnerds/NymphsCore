using System.Diagnostics;
using System.Drawing;
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
    private RefreshIconButton? _refreshBtn;
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

        // Refresh icon button (top-right)
        _refreshBtn = new RefreshIconButton
        {
            Name = "refreshBtn",
            Location = new Point(320, 12),
            Size = new Size(34, 34),
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
        EnsureScriptDeployed();
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
    private static readonly string WslScriptPath = "~/Nymphs-Brain/scripts/monitor_query.sh";

    /// <summary>
    /// Ensure the helper script is deployed to WSL.
    /// Copies from the app's directory (next to .exe) to the WSL home path.
    /// </summary>
    private static async Task EnsureScriptDeployed()
    {
        string localScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "monitor_query.sh");
        if (!File.Exists(localScript))
            return; // Script not bundled; skip

        // Check if already deployed in WSL
        var checkArgs = $"-d {WslDistro} test -f {WslScriptPath}";
        try
        {
            using var checkProc = Process.Start(new ProcessStartInfo("wsl.exe", checkArgs)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
            })!;
            checkProc.WaitForExit(5000);
            if (checkProc.ExitCode == 0)
                return; // Already deployed
        }
        catch
        {
            return; // WSL not available
        }

        // Copy to WSL via /mnt
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        // Guess drive letter from path (e.g., C:\... -> /mnt/c/...)
        if (appDir.Length >= 2 && appDir[1] == ':')
        {
            string drive = char.ToLower(appDir[0]).ToString();
            string unixPath = appDir.Substring(2).Replace('\\', '/').Replace("/", "/");
            string wslSource = $"/mnt/{drive}{unixPath}monitor_query.sh";
            string mkdirArgs = $"-d {WslDistro} mkdir -p ~/Nymphs-Brain/scripts";
            string cpArgs = $"-d {WslDistro} cp \"{wslSource}\" {WslScriptPath}";
            try
            {
                using (Process.Start(new ProcessStartInfo("wsl.exe", mkdirArgs)
                { UseShellExecute = false, CreateNoWindow = true })) { }
                using (Process.Start(new ProcessStartInfo("wsl.exe", cpArgs)
                { UseShellExecute = false, CreateNoWindow = true })) { }
            }
            catch { }
        }
    }

    /// <summary>
    /// Run a query through the WSL helper script.
    /// Format: wsl.exe -d NymphsCore ~/Nymphs-Brain/scripts/monitor_query.sh query
    /// </summary>
    private static async Task<string> WslQuery(string query)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {WslDistro} {WslScriptPath} {query}",
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

    // Custom button that draws a blue refresh icon
    private class RefreshIconButton : Panel
    {
        private static readonly Color IconBlue = Color.FromArgb(33, 150, 243);
        private static readonly Color HoverBg = Color.FromArgb(60, 60, 60);
        private static readonly Color NormalBg = Color.FromArgb(35, 35, 35);

        public event EventHandler Click = delegate { };

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Click(this, e);
            base.OnMouseClick(e);
        }

        public RefreshIconButton()
        {
            BackColor = NormalBg;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            BackColor = HoverBg;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            BackColor = NormalBg;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Skip default background painting
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw rounded rectangle background
            var bounds = new Rectangle(0, 0, Width, Height);
            var radius = Math.Min(Width, Height) / 2;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(new Rectangle(0, 0, radius, radius), 180, 90);
                path.AddArc(new Rectangle(Width - radius, 0, radius, radius), 270, 90);
                path.AddArc(new Rectangle(Width - radius, Height - radius, radius, radius), 0, 90);
                path.AddArc(new Rectangle(0, Height - radius, radius, radius), 90, 90);
                path.CloseFigure();

                g.FillPath(new SolidBrush(BackColor), path);
            }

            // Draw refresh icon
            var cx = Width / 2;
            var cy = Height / 2;
            var iconRadius = Math.Min(Width, Height) / 2 - 5;

            using (var pen = new Pen(IconBlue, 2.5f))
            {
                // Draw circular arc (270 degrees)
                g.DrawArc(pen, cx - iconRadius, cy - iconRadius, iconRadius * 2, iconRadius * 2, 30, 270);

                // Draw arrowhead at the start of the arc (top-right area)
                // Arrow angle: the arc starts at 30 degrees
                var arrowAngle = 30 * Math.PI / 180;
                var arrowX = cx + (int)(iconRadius * Math.Cos(arrowAngle));
                var arrowY = cy - (int)(iconRadius * Math.Sin(arrowAngle));

                var arrowSize = 6;
                // Arrow pointing in the direction of the arc (tangent direction at 30 deg)
                var tangentAngle = arrowAngle + Math.PI / 2;
                var dx1 = Math.Cos(tangentAngle + 2.2) * arrowSize;
                var dy1 = Math.Sin(tangentAngle + 2.2) * arrowSize;
                var dx2 = Math.Cos(tangentAngle - 2.2) * arrowSize;
                var dy2 = Math.Sin(tangentAngle - 2.2) * arrowSize;

                using (var arrowBrush = new SolidBrush(IconBlue))
                {
                    var arrowPath = new System.Drawing.Drawing2D.GraphicsPath();
                    arrowPath.AddLine(arrowX + (float)dx1, arrowY + (float)dy1, arrowX, arrowY);
                    arrowPath.AddLine(arrowX, arrowY, arrowX + (float)dx2, arrowY + (float)dy2);
                    arrowPath.CloseFigure();
                    g.FillPath(arrowBrush, arrowPath);
                }
            }
        }
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