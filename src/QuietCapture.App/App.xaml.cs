using System.Windows;

namespace QuietCapture.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Repository bootstrap only. Product startup is added after Phase 0 freezes
        // the backend-facing contracts and composition requirements.
        Shutdown();
    }
}
