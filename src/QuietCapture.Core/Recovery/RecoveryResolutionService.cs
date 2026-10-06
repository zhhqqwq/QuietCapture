using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Recovery;

public sealed class RecoveryResolutionService
{
    private readonly RecoveryIndexStore _recoveryIndexStore;
    private readonly SessionCleanupPolicy _cleanupPolicy;

    public RecoveryResolutionService(
        RecoveryIndexStore recoveryIndexStore,
        SessionCleanupPolicy cleanupPolicy)
    {
        _recoveryIndexStore =
            recoveryIndexStore ??
            throw new ArgumentNullException(
                nameof(recoveryIndexStore));

        _cleanupPolicy =
            cleanupPolicy ??
            throw new ArgumentNullException(
                nameof(cleanupPolicy));
    }

    public RecoveryResolutionResult Resolve(
        RecoveryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.Classification !=
            RecoveryClassification.NoRecoveryRequired)
        {
            return RecoveryResolutionResult.Preserved();
        }

        if (candidate.Metadata is null ||
            !candidate.SessionId.HasValue ||
            !candidate.PersistedStatus.HasValue)
        {
            return RecoveryResolutionResult.Failed(
                RecoveryAction.Preserve,
                indexEntryRemoved: false,
                "NoRecoveryRequired candidate is missing validated Session metadata.");
        }

        SessionStatus status =
            candidate.PersistedStatus.Value;

        if (status is not
                SessionStatus.Completed and not
                SessionStatus.FailedToStart)
        {
            return RecoveryResolutionResult.Failed(
                RecoveryAction.Preserve,
                indexEntryRemoved: false,
                $"NoRecoveryRequired candidate has unsupported Session status: {status}.");
        }

        SessionMetadata session;

        try
        {
            session =
                SessionMetadata.Restore(
                    candidate.Metadata);
        }
        catch (Exception ex)
            when (ex is
                ArgumentException or
                InvalidOperationException or
                OverflowException)
        {
            return RecoveryResolutionResult.Failed(
                RecoveryAction.Preserve,
                indexEntryRemoved: false,
                ex.Message);
        }

        RecoveryIndexEntry entry =
            RecoveryIndexEntry.FromSession(
                session);

        RecoveryAction action =
            candidate.FromRecoveryIndex
                ? RecoveryAction
                    .RemoveStaleIndexAndCleanup
                : RecoveryAction
                    .CleanupNoRecoveryRequired;

        bool indexEntryRemoved = false;

        if (_recoveryIndexStore.Contains(entry))
        {
            try
            {
                _recoveryIndexStore.Remove(entry);
                indexEntryRemoved = true;
            }
            catch (Exception ex)
                when (IsStorageException(ex))
            {
                return RecoveryResolutionResult.Failed(
                    action,
                    indexEntryRemoved: false,
                    ex.Message);
            }
        }

        try
        {
            if (!_cleanupPolicy.TryCleanup(
                    session))
            {
                return RecoveryResolutionResult.Failed(
                    action,
                    indexEntryRemoved,
                    "Session was not eligible for safe cleanup.");
            }
        }
        catch (Exception ex)
            when (IsStorageException(ex))
        {
            return RecoveryResolutionResult.Failed(
                action,
                indexEntryRemoved,
                ex.Message);
        }

        return RecoveryResolutionResult.Completed(
            action,
            indexEntryRemoved);
    }

    private static bool IsStorageException(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            System.Text.Json.JsonException;
    }
}
