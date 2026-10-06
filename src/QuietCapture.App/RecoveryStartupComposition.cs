using QuietCapture.Core.Recovery;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Infrastructure.Windows.Storage;

namespace QuietCapture.App;

internal sealed class RecoveryStartupComposition
{
    private const string ProductDataDirectoryName =
        "ScreenRecorder";
    private const string RecoveryIndexFileName =
        "recovery-index.json";

    private readonly RecoveryStartupCoordinator
        _coordinator;

    private RecoveryStartupComposition(
        RecoveryStartupCoordinator coordinator,
        string recoveryIndexPath)
    {
        _coordinator = coordinator;
        RecoveryIndexPath = recoveryIndexPath;
    }

    public string RecoveryIndexPath { get; }

    public static RecoveryStartupComposition CreateDefault()
    {
        var fileSystem =
            new WindowsFileSystem();

        string appData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .ApplicationData);

        string indexPath =
            Path.Combine(
                appData,
                ProductDataDirectoryName,
                RecoveryIndexFileName);

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var cleanup =
            new SessionCleanupPolicy(
                fileSystem,
                indexStore);

        var resolution =
            new RecoveryResolutionService(
                indexStore,
                cleanup);

        var scanner =
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator());

        return new RecoveryStartupComposition(
            new RecoveryStartupCoordinator(
                scanner,
                resolution),
            indexPath);
    }

    public RecoveryStartupResult Run(
        IEnumerable<string> knownOutputDirectories)
    {
        return _coordinator.Run(
            RecoveryIndexPath,
            knownOutputDirectories);
    }
}
