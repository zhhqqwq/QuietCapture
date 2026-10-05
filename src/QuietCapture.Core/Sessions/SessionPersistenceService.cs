using QuietCapture.Core.Models;

namespace QuietCapture.Core.Sessions;

public sealed class SessionPersistenceService
{
    private readonly SessionStore _sessionStore;
    private readonly RecoveryIndexStore _recoveryIndexStore;

    public SessionPersistenceService(
        SessionStore sessionStore,
        RecoveryIndexStore recoveryIndexStore)
    {
        _sessionStore =
            sessionStore ??
            throw new ArgumentNullException(
                nameof(sessionStore));

        _recoveryIndexStore =
            recoveryIndexStore ??
            throw new ArgumentNullException(
                nameof(recoveryIndexStore));
    }

    public void RegisterCreatedSession(
        SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status !=
            SessionStatus.Created)
        {
            throw new InvalidOperationException(
                "Only a Created Session can be registered.");
        }

        _sessionStore.Save(session);

        _recoveryIndexStore.Add(
            RecoveryIndexEntry.FromSession(
                session));
    }

    public void TransitionTo(
        SessionMetadata session,
        SessionStatus nextStatus,
        DateTimeOffset occurredAt,
        StopReason? stopReason = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        SessionLifecycle.EnsureTransition(
            session.Status,
            nextStatus);

        SessionMetadataDocument proposed =
            SessionMetadataDocument.FromTransition(
                session,
                nextStatus,
                occurredAt,
                stopReason);

        _sessionStore.SaveDocument(
            session.WorkingDirectory,
            proposed);

        session.TransitionTo(
            nextStatus,
            occurredAt,
            stopReason);

        if (CanRemoveRecoveryIndex(
                nextStatus))
        {
            _recoveryIndexStore.Remove(
                RecoveryIndexEntry.FromSession(
                    session));
        }
    }

    private static bool CanRemoveRecoveryIndex(
        SessionStatus status)
    {
        return status is
            SessionStatus.Completed or
            SessionStatus.FailedToStart;
    }
}
