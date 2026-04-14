using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Nymphs3DInstaller.Services;
using Nymphs3DInstaller.ViewModels;

namespace Nymphs3DInstaller.Views;

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
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.LogLines.CollectionChanged -= OnLogLinesChanged;
        }

        base.OnClosed(e);
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not NotifyCollectionChangedAction.Add || LogListBox.Items.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            var lastItem = LogListBox.Items[^1];
            LogListBox.ScrollIntoView(lastItem);
        }), DispatcherPriority.Background);
    }

    private void OnHuggingFaceTokenChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not PasswordBox passwordBox)
        {
            return;
        }

        _viewModel.HuggingFaceToken = passwordBox.Password;
    }
}
