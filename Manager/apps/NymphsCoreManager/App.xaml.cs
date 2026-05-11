using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using NymphsCoreManager.Views;

namespace NymphsCoreManager;

public partial class App : Application
{
    private const string BuildMarker = "fetch-models-live-download-detail-20260511-2105";

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NymphsCore");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "manager-app.log");

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        EnsureLogDirectory();
        LogLine("Application starting.");
        LogLine($"BuildMarker: {BuildMarker}");
        LogLine($"BaseDirectory: {AppContext.BaseDirectory}");
        LogLine($"CurrentDirectory: {Environment.CurrentDirectory}");

        try
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LogException("Startup failure", ex);
            ShowFatalError(ex);
            Shutdown(-1);
            return;
        }

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("Dispatcher unhandled exception", e.Exception);
        ShowFatalError(e.Exception);
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("AppDomain unhandled exception", ex);
            ShowFatalError(ex);
            return;
        }

        LogLine($"Unhandled non-exception object: {e.ExceptionObject}");
    }

    private static void EnsureLogDirectory()
    {
        Directory.CreateDirectory(LogDirectory);
    }

    private static void LogException(string context, Exception ex)
    {
        var builder = new StringBuilder();
        builder.AppendLine(context);
        builder.AppendLine(ex.ToString());
        LogLine(builder.ToString());
    }

    private static void LogLine(string message)
    {
        EnsureLogDirectory();
        File.AppendAllText(
            LogFilePath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void ShowFatalError(Exception ex)
    {
        try
        {
            MessageBox.Show(
                "The manager app failed to start.\n\n" +
                ex.Message +
                "\n\nLog file:\n" +
                LogFilePath,
                "NymphsCore Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Best effort only. If MessageBox also fails, the log file still exists.
        }
    }
}
