using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Sessions;

public sealed record FinalizationResult
{
    private FinalizationResult(
        bool succeeded,
        bool alreadyCompleted,
        bool cleanupCompleted,
        FinalizationFailure? failure,
        MediaPublishResult? mediaPublishResult,
        string? error)
    {
        Succeeded = succeeded;
        AlreadyCompleted = alreadyCompleted;
        CleanupCompleted = cleanupCompleted;
        Failure = failure;
        MediaPublishResult = mediaPublishResult;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool AlreadyCompleted { get; }

    public bool CleanupCompleted { get; }

    public FinalizationFailure? Failure { get; }

    public MediaPublishResult? MediaPublishResult { get; }

    public string? Error { get; }

    internal static FinalizationResult Completed(
        MediaPublishResult? mediaPublishResult,
        bool alreadyCompleted = false)
    {
        return new FinalizationResult(
            succeeded: true,
            alreadyCompleted,
            cleanupCompleted: true,
            failure: null,
            mediaPublishResult,
            error: null);
    }

    internal static FinalizationResult Failed(
        FinalizationFailure failure,
        string error,
        MediaPublishResult? mediaPublishResult = null)
    {
        return new FinalizationResult(
            succeeded: false,
            alreadyCompleted: false,
            cleanupCompleted: false,
            failure,
            mediaPublishResult,
            error);
    }
}
