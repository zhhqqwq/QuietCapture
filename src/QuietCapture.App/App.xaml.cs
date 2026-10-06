using System.Windows;
using QuietCapture.Core.Recovery;
using QuietCapture.Core.Settings;
using QuietCapture.Infrastructure.Windows.Storage;

namespace QuietCapture.App;

public partial class App : Application
{
    internal AppSettings? StartupSettings
        { get; private set; }

    internal RecoveryStartupResult? StartupRecoveryResult
        { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var fileSystem =
            new WindowsFileSystem();

        SettingsStartupComposition settings =
            SettingsStartupComposition.CreateDefault(
                fileSystem);

        AppSettings startupSettings =
            settings.LoadOrDefault();

        StartupSettings =
            startupSettings;

        RecoveryStartupComposition recovery =
            RecoveryStartupComposition.CreateDefault(
                fileSystem);

        StartupRecoveryResult =
            recovery.Run(
                settings.GetKnownOutputDirectories(
                    startupSettings));

        // The product UI/startup shell is still deferred. Settings and
        // recovery startup composition are now active and backend-independent.
        Shutdown();
    }
}
