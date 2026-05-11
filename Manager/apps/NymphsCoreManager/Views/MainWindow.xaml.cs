using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using NymphsCoreManager.Services;
using NymphsCoreManager.ViewModels;

namespace NymphsCoreManager.Views;

public partial class MainWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const double MinimumCleanNavSpace = 90;
    private const double SidebarOnlyMonitorWidth = 360;
    private const double MonitorModeWidth = 240;
    private const double MonitorModeHeight = 390;
    private const double MinimumFullModeWidth = 820;
    private const double MinimumFullModeHeight = 560;
    private const double DefaultFullModeWidth = 1060;
    private const double DefaultFullModeHeight = 620;
    private ManagerShellViewModel? _viewModel;
    private Rect? _preMonitorModeBounds;
    private bool _isMonitorMode;
    private bool _shutdownComplete;
    private bool _shutdownInProgress;
    private bool _moduleUiWebMessageAttached;
    private Task<CoreWebView2Environment>? _moduleUiEnvironmentTask;
    private string _lastModuleUiNavigationKey = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new ManagerShellViewModel(new InstallerWorkflowService());

        DataContext = _viewModel;
        Loaded += OnLoaded;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.UnifiedLogLines.CollectionChanged += OnUnifiedLogLinesChanged;
        }
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_shutdownComplete || _viewModel is null)
        {
            base.OnClosing(e);
            return;
        }

        if (_shutdownInProgress)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _shutdownInProgress = true;
        PrepareVisualsForShutdown();
        Hide();

        try
        {
            await _viewModel.ShutdownAsync().ConfigureAwait(true);
        }
        finally
        {
            _shutdownComplete = true;
            Close();
        }
    }

    private void PrepareVisualsForShutdown()
    {
        if (SettingsPopup is not null)
        {
            SettingsPopup.IsOpen = false;
        }

        if (ModuleUiBrowser is not null)
        {
            try
            {
                ModuleUiBrowser.CoreWebView2?.Navigate("about:blank");
            }
            catch (Exception)
            {
                // Best effort only: the app is already closing.
            }

            ModuleUiBrowser.Visibility = Visibility.Collapsed;
        }

        _lastModuleUiNavigationKey = string.Empty;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.UnifiedLogLines.CollectionChanged -= OnUnifiedLogLinesChanged;
            _viewModel.Dispose();
        }

        Loaded -= OnLoaded;

        base.OnClosed(e);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        ApplyDarkTitleBar();
        StartModuleUiPrewarm();
        await _viewModel.InitializeAsync();
        UpdateCompactMonitorMode();
        _ = Dispatcher.BeginInvoke(ScrollMainContentToTop, DispatcherPriority.Loaded);
        ScrollUnifiedLogToLatest();
    }

    private void SidebarRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCompactMonitorMode();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCompactMonitorMode();
    }

    private void UpdateCompactMonitorMode()
    {
        if (SidebarRoot is null ||
            SidebarHeader is null ||
            SidebarNavHost is null ||
            SidebarRuntimePanel is null ||
            SidebarFooterHost is null ||
            MonitorModeToggleText is null ||
            SidebarColumn is null ||
            ShellGutterColumn is null ||
            MainContentColumn is null ||
            MainContentShell is null)
        {
            return;
        }

        var sidebarOnlyMode = _isMonitorMode || ActualWidth < SidebarOnlyMonitorWidth;
        var usedSidebarHeight = SidebarHeader.ActualHeight + SidebarRuntimePanel.ActualHeight + 42;
        var availableNavSpace = SidebarRoot.ActualHeight - usedSidebarHeight;
        var verticallyCompact = _isMonitorMode || availableNavSpace < MinimumCleanNavSpace;

        SidebarNavHost.Visibility = verticallyCompact
            ? Visibility.Collapsed
            : Visibility.Visible;
        SidebarFooterHost.Visibility = verticallyCompact
            ? Visibility.Collapsed
            : Visibility.Visible;
        MonitorModeToggleText.Text = _isMonitorMode ? "full" : "mon";

        if (sidebarOnlyMode)
        {
            SidebarColumn.Width = new GridLength(232);
            ShellGutterColumn.Width = new GridLength(0);
            MainContentColumn.Width = new GridLength(0);
            MainContentShell.Visibility = Visibility.Collapsed;
            return;
        }

        SidebarColumn.Width = new GridLength(232);
        ShellGutterColumn.Width = new GridLength(8);
        MainContentColumn.Width = new GridLength(1, GridUnitType.Star);
        MainContentShell.Visibility = Visibility.Visible;
    }

    private void OnUnifiedLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(ScrollUnifiedLogToLatest, DispatcherPriority.ContextIdle);
    }

    private void UnifiedLogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(ScrollUnifiedLogToLatest, DispatcherPriority.ContextIdle);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManagerShellViewModel.ModuleUiSource) ||
            e.PropertyName == nameof(ManagerShellViewModel.IsModuleUiPage))
        {
            Dispatcher.BeginInvoke(NavigateModuleUiBrowser, DispatcherPriority.Send);
        }
    }

    private void ModuleUiBrowser_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(NavigateModuleUiBrowser, DispatcherPriority.Send);
    }

    private void StartModuleUiPrewarm()
    {
        _ = PrewarmModuleUiBrowserAsync();
    }

    private async Task PrewarmModuleUiBrowserAsync()
    {
        try
        {
            await EnsureModuleUiBrowserInitializedAsync().ConfigureAwait(true);
            ModuleUiBrowser.CoreWebView2.NavigateToString("<!doctype html><html><body></body></html>");
        }
        catch
        {
            // Best effort only. The visible module UI host will still initialize on demand.
        }
    }

    private void ModuleUiBrowser_CoreWebView2InitializationCompleted(
        object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ModuleUiBrowser.NavigateToString(BuildModuleUiErrorHtml(
                e.InitializationException?.Message ?? "WebView2 initialization failed."));
            return;
        }

        AttachModuleUiWebMessageHandler();
        Dispatcher.BeginInvoke(NavigateModuleUiBrowser, DispatcherPriority.Send);
    }

    private Task<CoreWebView2Environment> GetModuleUiEnvironmentAsync()
    {
        return _moduleUiEnvironmentTask ??= CreateModuleUiEnvironmentAsync();
    }

    private static async Task<CoreWebView2Environment> CreateModuleUiEnvironmentAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NymphsCore",
            "WebView2");
        Directory.CreateDirectory(userDataFolder);
        return await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder).ConfigureAwait(true);
    }

    private async Task EnsureModuleUiBrowserInitializedAsync()
    {
        if (ModuleUiBrowser.CoreWebView2 is not null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        AppendModuleUiHostLog("ensure_start");
        var environment = await GetModuleUiEnvironmentAsync().ConfigureAwait(true);
        await ModuleUiBrowser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
        AttachModuleUiWebMessageHandler();
        AppendModuleUiHostLog($"ensure_complete_ms={stopwatch.ElapsedMilliseconds}");
    }

    private async void NavigateModuleUiBrowser()
    {
        if (ModuleUiBrowser is null || _viewModel is null)
        {
            return;
        }

        var source = _viewModel.ModuleUiSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            if (_viewModel.IsModuleUiPage)
            {
                ModuleUiBrowser.NavigateToString(BuildModuleUiErrorHtml("Module UI source is empty."));
            }
            else
            {
                ModuleUiBrowser.CoreWebView2?.Navigate("about:blank");
            }

            _lastModuleUiNavigationKey = string.Empty;
            return;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            AppendModuleUiHostLog($"navigate_request source={source}");
            await EnsureModuleUiBrowserInitializedAsync().ConfigureAwait(true);

            if (File.Exists(source))
            {
                var fullPath = Path.GetFullPath(source);
                var sourceInfo = new FileInfo(fullPath);
                var navigationKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{fullPath}|{sourceInfo.LastWriteTimeUtc.Ticks}|{sourceInfo.Length}");
                if (string.Equals(_lastModuleUiNavigationKey, navigationKey, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _lastModuleUiNavigationKey = navigationKey;
                var uiType = _viewModel.DisplayedModule?.InstalledModuleUiInfo?.Type ?? string.Empty;
                if (string.Equals(uiType, "local_html", StringComparison.OrdinalIgnoreCase))
                {
                    var html = File.ReadAllText(fullPath);
                    html = InjectModuleUiSecrets(html);
                    ModuleUiBrowser.NavigateToString(html);
                    AppendModuleUiHostLog($"navigate_to_string_ms={stopwatch.ElapsedMilliseconds} bytes={html.Length}");
                    return;
                }

                ModuleUiBrowser.Source = new Uri(fullPath);
                AppendModuleUiHostLog($"navigate_file_uri_ms={stopwatch.ElapsedMilliseconds}");
                return;
            }

            AppendModuleUiHostLog($"navigate_missing source={source}");
            ModuleUiBrowser.NavigateToString(BuildModuleUiErrorHtml($"Module UI file was not found: {source}"));
        }
        catch (Exception ex)
        {
            AppendModuleUiHostLog($"navigate_error {ex.Message}");
            ModuleUiBrowser.NavigateToString(BuildModuleUiErrorHtml(ex.Message));
        }
    }

    private void AttachModuleUiWebMessageHandler()
    {
        if (_moduleUiWebMessageAttached || ModuleUiBrowser.CoreWebView2 is null)
        {
            return;
        }

        ModuleUiBrowser.CoreWebView2.WebMessageReceived += ModuleUiBrowser_WebMessageReceived;
        _moduleUiWebMessageAttached = true;
    }

    private void ModuleUiBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!string.Equals(e.TryGetWebMessageAsString(), "back", StringComparison.OrdinalIgnoreCase) ||
            _viewModel?.DisplayedModule is null)
        {
            return;
        }

        _viewModel.OpenModuleCommand.Execute(_viewModel.DisplayedModule);
    }

    private static string BuildModuleUiErrorHtml(string message)
    {
        return "<!doctype html><html><body style=\"margin:16px;background:#071112;color:#9db8b4;font:13px Segoe UI,sans-serif;\">" +
               "<strong style=\"color:#edf8f6;\">Module UI could not be loaded.</strong><br>" +
               WebUtility.HtmlEncode(message) +
               "</body></html>";
    }

    private void ModuleUiBrowser_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            AppendModuleUiHostLog("navigation_start invalid_uri");
            return;
        }

        AppendModuleUiHostLog($"navigation_start scheme={uri.Scheme}");
        if (_viewModel?.HandleModuleUiNavigation(uri) == true)
        {
            e.Cancel = true;
            return;
        }

        if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Scheme))
        {
            return;
        }

        e.Cancel = true;
    }

    private void ModuleUiBrowser_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        AppendModuleUiHostLog($"navigation_complete success={e.IsSuccess} status={e.HttpStatusCode} error={e.WebErrorStatus}");
    }

    private static void AppendModuleUiHostLog(string message)
    {
        try
        {
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NymphsCore");
            Directory.CreateDirectory(logFolder);
            File.AppendAllText(
                Path.Combine(logFolder, "manager-app.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] module-ui-host {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never break the shell.
        }
    }

    private void ScrollUnifiedLogToLatest()
    {
        if (UnifiedLogTextBox is null)
        {
            return;
        }

        UnifiedLogTextBox.CaretIndex = UnifiedLogTextBox.Text.Length;
        UnifiedLogTextBox.ScrollToEnd();
    }

    private void ScrollMainContentToTop()
    {
        MainContentScrollViewer.ScrollToTop();
    }

    private static string InjectModuleUiSecrets(string html)
    {
        if (!html.Contains("fetch_models", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("hf-token", StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        var token = new SharedSecretsService().Load().HuggingFaceToken?.Trim() ?? string.Empty;
        var script = "<style>input#hf-token{min-height:30px;background:#101d1f;color:#f4fffb;border:1px solid #24494d;border-radius:4px;padding:4px 9px;min-width:280px;}</style><script>window.NYMPHSCORE_HF_TOKEN=" +
            JsonSerializer.Serialize(token) +
            @";
window.addEventListener('DOMContentLoaded', function() {
  var tokenInput = document.getElementById('hf-token');
  var controls = document.querySelector('.controls');
  if (!tokenInput && controls) {
    var label = document.createElement('label');
    label.textContent = 'HF Token';
    tokenInput = document.createElement('input');
    tokenInput.id = 'hf-token';
    tokenInput.type = 'password';
    tokenInput.autocomplete = 'off';
    tokenInput.placeholder = 'Saved token optional';
    label.appendChild(tokenInput);
    var firstButton = controls.querySelector('button');
    controls.insertBefore(label, firstButton || null);
  }
  if (tokenInput) {
    try {
      tokenInput.value = window.NYMPHSCORE_HF_TOKEN || window.localStorage.getItem('nymphscore_hf_token') || '';
    } catch (error) {
      tokenInput.value = window.NYMPHSCORE_HF_TOKEN || '';
    }
  }
  var originalFetchModels = window.fetchModels;
  window.fetchModels = function() {
    var token = tokenInput ? tokenInput.value.trim() : '';
    try {
      if (token) {
        window.localStorage.setItem('nymphscore_hf_token', token);
      } else {
        window.localStorage.removeItem('nymphscore_hf_token');
      }
    } catch (error) {}
    if (typeof window.runAction === 'function') {
      window.runAction('fetch_models', {
        precision: document.getElementById('precision').value,
        rank: document.getElementById('rank').value,
        hf_token: token
      });
      return;
    }
    if (typeof originalFetchModels === 'function') {
      originalFetchModels();
    }
  };
});
</script>";

        return html.Contains("</head>", StringComparison.OrdinalIgnoreCase)
            ? Regex.Replace(html, "</head>", script + "</head>", RegexOptions.IgnoreCase)
            : script + html;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
    }

    private void MonitorModeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isMonitorMode)
        {
            ExitMonitorMode();
            return;
        }

        EnterMonitorMode();
    }

    private void EnterMonitorMode()
    {
        SettingsPopup.IsOpen = false;

        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        var currentBounds = new Rect(Left, Top, Width, Height);
        _preMonitorModeBounds = IsUsableFullModeBounds(currentBounds)
            ? currentBounds
            : BuildFullModeBounds(Left + Width);

        _isMonitorMode = true;
        Width = MonitorModeWidth;
        Height = MonitorModeHeight;
        Topmost = true;
        UpdateCompactMonitorMode();
    }

    private void ExitMonitorMode()
    {
        _isMonitorMode = false;
        Topmost = false;

        var preferredRight = Left + Width;
        var bounds = _preMonitorModeBounds is { } savedBounds && IsUsableFullModeBounds(savedBounds)
            ? savedBounds
            : BuildFullModeBounds(preferredRight);

        _preMonitorModeBounds = null;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        UpdateCompactMonitorMode();
        _ = Dispatcher.BeginInvoke(UpdateCompactMonitorMode, DispatcherPriority.Loaded);
    }

    private static bool IsUsableFullModeBounds(Rect bounds)
    {
        return bounds.Width >= MinimumFullModeWidth && bounds.Height >= MinimumFullModeHeight;
    }

    private static Rect BuildFullModeBounds(double preferredRight)
    {
        var workArea = SystemParameters.WorkArea;
        var width = Math.Min(DefaultFullModeWidth, Math.Max(MinimumFullModeWidth, workArea.Width - 40));
        var height = Math.Min(DefaultFullModeHeight, Math.Max(MinimumFullModeHeight, workArea.Height - 40));
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);

        var minLeft = workArea.Left;
        var maxLeft = workArea.Right - width;
        var left = Math.Max(minLeft, Math.Min(maxLeft, preferredRight - width));

        var minTop = workArea.Top;
        var maxTop = workArea.Bottom - height;
        var top = Math.Max(minTop, Math.Min(maxTop, SystemParameters.WorkArea.Top + 40));

        return new Rect(left, top, width, height);
    }

    private void ApplyDarkTitleBar()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(windowHandle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(windowHandle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));

        // Match the native title bar to the current flat shell color so it doesn't read as a separate strip.
        var captionColor = 0x00202514; // RGB(20,37,32) = #142520
        var textColor = 0x00DCD6C8; // warm off-white
        var borderColor = captionColor;
        _ = DwmSetWindowAttribute(windowHandle, DwmwaCaptionColor, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(windowHandle, DwmwaTextColor, ref textColor, sizeof(int));
        _ = DwmSetWindowAttribute(windowHandle, DwmwaBorderColor, ref borderColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            if (FindDescendant<T>(child) is { } nestedChild)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
