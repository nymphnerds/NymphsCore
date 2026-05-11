using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Navigation;
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
                ModuleUiBrowser.Navigate("about:blank");
            }
            catch (Exception)
            {
                // Best effort only: the app is already closing.
            }

            ModuleUiBrowser.Visibility = Visibility.Collapsed;
        }
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
        Dispatcher.BeginInvoke(ScrollUnifiedLogToLatest, DispatcherPriority.Background);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManagerShellViewModel.ModuleUiSource))
        {
            Dispatcher.BeginInvoke(NavigateModuleUiBrowser, DispatcherPriority.Background);
        }
    }

    private void NavigateModuleUiBrowser()
    {
        if (ModuleUiBrowser is null || _viewModel?.ModuleUiSource is null)
        {
            return;
        }

        ModuleUiBrowser.Navigate(_viewModel.ModuleUiSource);
    }

    private void ModuleUiBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        if (_viewModel?.HandleModuleUiNavigation(e.Uri) == true)
        {
            e.Cancel = true;
        }
    }

    private void ModuleUiBrowser_LoadCompleted(object sender, NavigationEventArgs e)
    {
        // The embedded browser is only a generic host for installed module-owned local HTML.
    }

    private void ScrollUnifiedLogToLatest()
    {
        if (UnifiedLogTextBox is null)
        {
            return;
        }

        if (UnifiedLogTextBox.IsKeyboardFocusWithin || UnifiedLogTextBox.SelectionLength > 0)
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
