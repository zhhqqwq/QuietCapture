using System.Windows;
using QuietCapture.ScreenRecorderLibSpike.Gates.G02SystemAudio;
using QuietCapture.ScreenRecorderLibSpike.Gates.G03SystemAudioMicrophone;
using QuietCapture.ScreenRecorderLibSpike.Gates.G04MediaCrashRecovery;
using QuietCapture.ScreenRecorderLibSpike.Gates.G05CaptureExclusion;
using QuietCapture.ScreenRecorderLibSpike.Gates.G06StabilityStopLatency;

namespace QuietCapture.ScreenRecorderLibSpike;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IReadOnlyDictionary<string, string> arguments = ParseArguments(e.Args);
        arguments.TryGetValue("gate", out string? gate);

        if (string.Equals(gate, "g0-4-worker", StringComparison.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exitCode = await G04Worker.RunAsync(arguments);
            Shutdown(exitCode);
            return;
        }

        if (string.Equals(gate, "g0-6-worker", StringComparison.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int exitCode = await G06Worker.RunAsync(arguments);
            Shutdown(exitCode);
            return;
        }

        Window window = gate?.ToLowerInvariant() switch
        {
            "g0-2" => new G02SystemAudioWindow(),
            "g0-3" => new G03SystemAudioMicrophoneWindow(),
            "g0-4" => new G04ControllerWindow(),
            "g0-5" => new G05ControllerWindow(),
            "g0-6" => new G06ControllerWindow(),
            _ => new MainWindow()
        };

        MainWindow = window;
        window.Show();
    }

    private static IReadOnlyDictionary<string, string> ParseArguments(
        IEnumerable<string> args)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string arg in args)
        {
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string value = arg[2..];
            int separator = value.IndexOf('=');

            if (separator <= 0)
            {
                result[value] = "true";
                continue;
            }

            result[value[..separator]] = value[(separator + 1)..];
        }

        return result;
    }
}
