using System.Configuration;
using System.Data;
using System.Windows;

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

            _mainWindow = new MainWindow();
            
            // Ensure the window handle is created so hotkey registers immediately
            var helper = new System.Windows.Interop.WindowInteropHelper(_mainWindow);
            helper.EnsureHandle();
        }
    }

