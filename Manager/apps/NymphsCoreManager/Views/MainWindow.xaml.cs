using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using NymphsCoreManager.Services;
using NymphsCoreManager.ViewModels;

namespace NymphsCoreManager.Views;

public partial class MainWindow : Window
{
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

        await _viewModel.InitializeAsync();
        SyncPasswordBoxesFromViewModel();
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
}
