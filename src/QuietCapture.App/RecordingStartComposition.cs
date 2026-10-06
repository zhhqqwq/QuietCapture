using QuietCapture.Core.Models;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Preflight;
using QuietCapture.Core.Recording;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Settings;
using QuietCapture.Core.Storage;
using QuietCapture.Infrastructure.Windows.Audio;

namespace QuietCapture.App;

internal sealed class RecordingStartComposition
{
    private readonly RecordingStartPreparationService
        _service;

    private RecordingStartComposition(
        RecordingStartPreparationService service)
    {
        _service = service;
    }

    public static RecordingStartComposition CreateDefault(
        IFileSystem fileSystem,
        string recoveryIndexPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (string.IsNullOrWhiteSpace(
                recoveryIndexPath))
        {
            throw new ArgumentException(
                "Recovery index path must not be empty.",
                nameof(recoveryIndexPath));
        }

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                recoveryIndexPath);

        var persistence =
            new SessionPersistenceService(
                new SessionStore(fileSystem),
                indexStore);

        var sessionManager =
            new SessionManager(
                fileSystem,
                new OutputPlanner(fileSystem),
                persistence);

        var preflight =
            new RecordingPreflightService(
                fileSystem,
                new WindowsAudioDeviceResolver());

        return new RecordingStartComposition(
            new RecordingStartPreparationService(
                preflight,
                sessionManager));
    }

    public RecordingStartPreparationResult Prepare(
        AppSettings settings,
        CaptureTarget target,
        PixelSize outputSize,
        DateTimeOffset createdAt)
    {
        return _service.Prepare(
            settings,
            target,
            outputSize,
            createdAt);
    }
}
