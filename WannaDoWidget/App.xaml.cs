using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WannaDoWidget;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
    public partial class App : System.Windows.Application
    {
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += App_DispatcherUnhandledException;

            _mainWindow = new MainWindow();
            
            // Ensure the window handle is created so hotkey registers immediately
            var helper = new System.Windows.Interop.WindowInteropHelper(_mainWindow);
            helper.EnsureHandle();
        }

        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WannaDoWidget");

                Directory.CreateDirectory(appDataFolder);
                File.WriteAllText(
                    Path.Combine(appDataFolder, "crash.txt"),
                    $"{DateTime.Now:O}{Environment.NewLine}{e.Exception}");
            }
            catch
            {
                // Preserve the original exception if crash logging itself fails.
            }
        }
    }

