using System.Configuration;
using System.Data;
using System.Windows;

namespace Lab_Feedback_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Handles application startup logic, including initializing appearance settings and invoking base startup
        /// behavior.
        /// </summary>
        /// <remarks>Overrides the default startup behavior to configure the application's main window
        /// appearance using the system theme and Mica backdrop. Call the base implementation to ensure standard startup
        /// processing.</remarks>
        /// <param name="e">An object that contains the arguments for the startup event.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(
                MainWindow,
                Wpf.Ui.Controls.WindowBackdropType.Mica,
                true
            );
        }
    }

}
