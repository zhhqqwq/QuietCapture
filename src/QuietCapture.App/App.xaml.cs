using System.Windows;
using QuietCapture.Core.Recovery;

namespace QuietCapture.App;

public partial class App : Application
{
    internal RecoveryStartupResult? StartupRecoveryResult
        { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RecoveryStartupComposition recovery =
            RecoveryStartupComposition.CreateDefault();

        StartupRecoveryResult =
            recovery.Run(
                Array.Empty<string>());

        // The product UI/startup shell is still deferred. Recovery composition
        // is now active and backend-independent.
        Shutdown();
    }
}
