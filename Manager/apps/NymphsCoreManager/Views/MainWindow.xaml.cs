using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
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
    private ManagerShellViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new ManagerShellViewModel(new InstallerWorkflowService());

        DataContext = _viewModel;
        Loaded += OnLoaded;

        if (_viewModel is not null)
        {
            _viewModel.UnifiedLogLines.CollectionChanged += OnUnifiedLogLinesChanged;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
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
        _ = Dispatcher.BeginInvoke(ScrollMainContentToTop, DispatcherPriority.Loaded);
        ScrollUnifiedLogToLatest();
    }

    private void OnUnifiedLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(ScrollUnifiedLogToLatest, DispatcherPriority.Background);
    }

    private void ScrollUnifiedLogToLatest()
    {
        if (UnifiedLogList.Items.Count == 0)
        {
            return;
        }

        var lastItem = UnifiedLogList.Items[UnifiedLogList.Items.Count - 1];
        UnifiedLogList.ScrollIntoView(lastItem);

        if (FindDescendant<ScrollViewer>(UnifiedLogList) is { } scrollViewer)
        {
            scrollViewer.ScrollToBottom();
        }
    }

    private void ScrollMainContentToTop()
    {
        MainContentScrollViewer.ScrollToTop();
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
