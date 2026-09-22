using System.Windows;

namespace Lab_Feedback_WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // MainWindow is created by StartupUri before OnStartup returns
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(
                MainWindow,
                Wpf.Ui.Controls.WindowBackdropType.Mica,
                true
            );
        }
    }
}
