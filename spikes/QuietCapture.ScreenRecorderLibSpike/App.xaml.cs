using System.Windows;

namespace QuietCapture.ScreenRecorderLibSpike;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Shutdown();
    }
}
