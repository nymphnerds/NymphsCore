using System;
using System.IO;
using System.Windows;
using ManagerFEUI.Views;

namespace ManagerFEUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Catch unhandled exceptions to diagnose crashes
            DispatcherUnhandledException += (s, args) =>
            {
                args.Handled = true;
                var msg = args.Exception.ToString();
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "ManagerFEUI-crash.log"), msg); } catch { }
                MessageBox.Show(msg, "ManagerFEUI Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "ManagerFEUI-crash.log"), ex?.ToString() ?? "Unknown"); } catch { }
                MessageBox.Show(ex?.ToString() ?? "Unknown error", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            // Show main window
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show MainWindow: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}