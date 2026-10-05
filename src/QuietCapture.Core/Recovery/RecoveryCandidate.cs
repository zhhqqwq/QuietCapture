using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Recovery;

public sealed record RecoveryCandidate
{
    public RecoveryCandidate(
        string workingDirectory,
        RecoveryClassification classification,
        SessionId? sessionId,
        SessionStatus? persistedStatus,
        bool fromRecoveryIndex,
        bool fromDirectoryScan,
        bool partialMediaExists,
        long partialMediaBytes,
        SessionMetadataDocument? metadata,
        string? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(
                workingDirectory))
        {
            throw new ArgumentException(
                "Working directory must not be empty.",
                nameof(workingDirectory));
        }

        if (partialMediaBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partialMediaBytes));
        }

        WorkingDirectory =
            workingDirectory;
        Classification =
            classification;
        SessionId =
            sessionId;
        PersistedStatus =
            persistedStatus;
        FromRecoveryIndex =
            fromRecoveryIndex;
        FromDirectoryScan =
            fromDirectoryScan;
        PartialMediaExists =
            partialMediaExists;
        PartialMediaBytes =
            partialMediaBytes;
        Metadata =
            metadata;
        Diagnostic =
            diagnostic;
    }

    public string WorkingDirectory { get; }

    public RecoveryClassification Classification { get; }

    public SessionId? SessionId { get; }

    public SessionStatus? PersistedStatus { get; }

    public bool FromRecoveryIndex { get; }

    public bool FromDirectoryScan { get; }

    public bool PartialMediaExists { get; }

    public long PartialMediaBytes { get; }

    public bool HasNonEmptyPartialMedia =>
        PartialMediaExists &&
        PartialMediaBytes > 0;

    public SessionMetadataDocument? Metadata { get; }

    public string? Diagnostic { get; }
}
