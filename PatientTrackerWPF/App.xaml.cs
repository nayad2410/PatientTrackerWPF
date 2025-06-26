using System.Configuration;
using System.Data;
using System.Windows;

namespace PatientTrackerWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set app title
            this.MainWindow = null; // Don't auto-create MainWindow

            // Start with login window
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
        }
    }
