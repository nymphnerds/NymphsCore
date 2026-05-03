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
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(new InstallerWorkflowService())
        {
            RequestClose = Close,
        };

        DataContext = _viewModel;
        _viewModel.LogLines.CollectionChanged += OnLogLinesChanged;
        Loaded += OnLoaded;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.LogLines.CollectionChanged -= OnLogLinesChanged;
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
        SyncPasswordBoxesFromViewModel();
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

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not NotifyCollectionChangedAction.Add)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            var activeLogListBox = GetActiveLogListBox();

            if (activeLogListBox.Items.Count == 0)
            {
                return;
            }

            var lastItem = activeLogListBox.Items[^1];
            activeLogListBox.UpdateLayout();
            activeLogListBox.ScrollIntoView(lastItem);

            var scrollViewer = FindDescendant<ScrollViewer>(activeLogListBox);
            scrollViewer?.ScrollToEnd();
        }), DispatcherPriority.Render);
    }

    private ListBox GetActiveLogListBox()
    {
        if (_viewModel?.IsBrainToolsStep == true)
        {
            return BrainLogListBox;
        }

        if (_viewModel?.IsRuntimeToolsStep == true)
        {
            return RuntimeLogListBox;
        }

        if (_viewModel?.IsZImageTrainerStep == true)
        {
            return ZImageTrainerLogListBox;
        }

        return LogListBox;
    }

    private void OnHuggingFaceTokenChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not PasswordBox passwordBox)
        {
            return;
        }

        _viewModel.HuggingFaceToken = passwordBox.Password;
        SyncHuggingFaceTokenBoxes(passwordBox.Password, passwordBox);
    }

    private void OnBrainOpenRouterApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not PasswordBox passwordBox)
        {
            return;
        }

        _viewModel.BrainOpenRouterApiKey = passwordBox.Password;
    }

    private void SyncPasswordBoxesFromViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        SyncHuggingFaceTokenBoxes(_viewModel.HuggingFaceToken, null);
        if (BrainOpenRouterApiKeyBox.Password != _viewModel.BrainOpenRouterApiKey)
        {
            BrainOpenRouterApiKeyBox.Password = _viewModel.BrainOpenRouterApiKey;
        }
    }

    private void SyncHuggingFaceTokenBoxes(string value, PasswordBox? source)
    {
        if (!ReferenceEquals(source, HuggingFaceTokenBox) && HuggingFaceTokenBox.Password != value)
        {
            HuggingFaceTokenBox.Password = value;
        }

        if (!ReferenceEquals(source, RuntimeHuggingFaceTokenBox) && RuntimeHuggingFaceTokenBox.Password != value)
        {
            RuntimeHuggingFaceTokenBox.Password = value;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
            {
                return match;
            }

            var nestedMatch = FindDescendant<T>(child);
            if (nestedMatch is not null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private async void OnStopZImageTrainingClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.StopZImageTrainingFromUiAsync();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
