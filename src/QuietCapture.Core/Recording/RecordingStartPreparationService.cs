using QuietCapture.Core.Models;
using QuietCapture.Core.Preflight;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Settings;

namespace QuietCapture.Core.Recording;

public sealed class RecordingStartPreparationService
{
    private readonly RecordingPreflightService
        _preflightService;
    private readonly SessionManager _sessionManager;

    public RecordingStartPreparationService(
        RecordingPreflightService preflightService,
        SessionManager sessionManager)
    {
        _preflightService =
            preflightService ??
            throw new ArgumentNullException(
                nameof(preflightService));

        _sessionManager =
            sessionManager ??
            throw new ArgumentNullException(
                nameof(sessionManager));
    }

    public RecordingStartPreparationResult Prepare(
        AppSettings settings,
        CaptureTarget target,
        PixelSize outputSize,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(target);

        RecordingIntent intent =
            RecordingIntent.FromSettings(
                target,
                outputSize,
                settings);

        RecordingPreflightResult preflight =
            _preflightService.Resolve(intent);

        if (!preflight.Succeeded)
        {
            return RecordingStartPreparationResult.Failed(
                new RecordingStartFailureDetail(
                    RecordingStartFailure.Preflight,
                    preflight.Failure,
                    preflight.Error ??
                        "Recording preflight failed."));
        }

        try
        {
            SessionMetadata session =
                _sessionManager.CreateSession(
                    preflight.Target ??
                        throw new InvalidOperationException(
                            "Successful preflight did not provide a capture target."),
                    preflight.Options ??
                        throw new InvalidOperationException(
                            "Successful preflight did not provide recording options."),
                    preflight.OutputDirectory ??
                        throw new InvalidOperationException(
                            "Successful preflight did not provide an output directory."),
                    createdAt);

            return RecordingStartPreparationResult
                .Success(session);
        }
        catch (Exception ex)
            when (IsSessionCreationException(ex))
        {
            return RecordingStartPreparationResult.Failed(
                new RecordingStartFailureDetail(
                    RecordingStartFailure
                        .SessionCreation,
                    PreflightFailure: null,
                    ex.Message));
        }
    }

    private static bool IsSessionCreationException(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            ArgumentException;
    }
}
