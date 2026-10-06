using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Sessions;

public sealed class SessionFinalizationService
{
    private readonly MediaPublisher _mediaPublisher;
    private readonly SessionPersistenceService
        _persistenceService;
    private readonly SessionCleanupPolicy _cleanupPolicy;

    public SessionFinalizationService(
        MediaPublisher mediaPublisher,
        SessionPersistenceService persistenceService,
        SessionCleanupPolicy cleanupPolicy)
    {
        _mediaPublisher =
            mediaPublisher ??
            throw new ArgumentNullException(
                nameof(mediaPublisher));

        _persistenceService =
            persistenceService ??
            throw new ArgumentNullException(
                nameof(persistenceService));

        _cleanupPolicy =
            cleanupPolicy ??
            throw new ArgumentNullException(
                nameof(cleanupPolicy));
    }

    public FinalizationResult Finalize(
        SessionMetadata session,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status ==
            SessionStatus.Completed)
        {
            return CleanupAlreadyCompleted(
                session);
        }

        if (session.Status !=
            SessionStatus.Finalizing)
        {
            return FinalizationResult.Failed(
                FinalizationFailure.InvalidState,
                $"Session must be Finalizing before finalization. Current status: {session.Status}.");
        }

        MediaPublishResult publish =
            _mediaPublisher.Publish(session);

        if (!publish.Succeeded)
        {
            return PersistPublishFailure(
                session,
                completedAt,
                publish);
        }

        try
        {
            _persistenceService.TransitionTo(
                session,
                SessionStatus.Completed,
                completedAt);
        }
        catch (Exception ex)
            when (IsPersistenceException(ex))
        {
            FinalizationFailure failure =
                session.Status ==
                    SessionStatus.Completed
                    ? FinalizationFailure
                        .RecoveryIndexRemoval
                    : FinalizationFailure
                        .CompletedPersistence;

            return FinalizationResult.Failed(
                failure,
                ex.Message,
                publish);
        }

        return CleanupCompleted(
            session,
            publish);
    }

    private FinalizationResult PersistPublishFailure(
        SessionMetadata session,
        DateTimeOffset occurredAt,
        MediaPublishResult publish)
    {
        try
        {
            _persistenceService.TransitionTo(
                session,
                SessionStatus.StopFailed,
                occurredAt);
        }
        catch (Exception ex)
            when (IsPersistenceException(ex))
        {
            return FinalizationResult.Failed(
                FinalizationFailure
                    .StopFailedPersistence,
                ex.Message,
                publish);
        }

        return FinalizationResult.Failed(
            FinalizationFailure.MediaPublication,
            publish.Error ??
                $"Media publication failed: {publish.Outcome}.",
            publish);
    }

    private FinalizationResult CleanupCompleted(
        SessionMetadata session,
        MediaPublishResult publish)
    {
        try
        {
            if (!_cleanupPolicy.TryCleanup(
                    session))
            {
                return FinalizationResult.Failed(
                    FinalizationFailure.Cleanup,
                    "Completed Session was not eligible for cleanup.",
                    publish);
            }
        }
        catch (Exception ex)
            when (IsPersistenceException(ex))
        {
            return FinalizationResult.Failed(
                FinalizationFailure.Cleanup,
                ex.Message,
                publish);
        }

        return FinalizationResult.Completed(
            publish);
    }

    private FinalizationResult CleanupAlreadyCompleted(
        SessionMetadata session)
    {
        try
        {
            if (!_cleanupPolicy.TryCleanup(
                    session))
            {
                return FinalizationResult.Failed(
                    FinalizationFailure.Cleanup,
                    "Completed Session was not eligible for cleanup.");
            }
        }
        catch (Exception ex)
            when (IsPersistenceException(ex))
        {
            return FinalizationResult.Failed(
                FinalizationFailure.Cleanup,
                ex.Message);
        }

        return FinalizationResult.Completed(
            mediaPublishResult: null,
            alreadyCompleted: true);
    }

    private static bool IsPersistenceException(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            System.Text.Json.JsonException;
    }
}
