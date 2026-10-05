using System.Windows;
using QuietCapture.ScreenRecorderLibSpike.Gates.G02SystemAudio;

namespace QuietCapture.ScreenRecorderLibSpike;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Window window = e.Args.Any(arg =>
            string.Equals(arg, "--gate=g0-2", StringComparison.OrdinalIgnoreCase))
            ? new G02SystemAudioWindow()
            : new MainWindow();

        MainWindow = window;
        window.Show();
    }
}
