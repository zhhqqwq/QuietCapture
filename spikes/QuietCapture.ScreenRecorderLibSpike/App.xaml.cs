using System.Windows;
using QuietCapture.ScreenRecorderLibSpike.Gates.G02SystemAudio;
using QuietCapture.ScreenRecorderLibSpike.Gates.G03SystemAudioMicrophone;

namespace QuietCapture.ScreenRecorderLibSpike;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? gate = e.Args
            .FirstOrDefault(arg => arg.StartsWith("--gate=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1];

        Window window = gate?.ToLowerInvariant() switch
        {
            "g0-2" => new G02SystemAudioWindow(),
            "g0-3" => new G03SystemAudioMicrophoneWindow(),
            _ => new MainWindow()
        };

        MainWindow = window;
        window.Show();
    }
}
