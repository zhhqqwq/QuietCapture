namespace QuietCapture.Core.Recovery;

public sealed record RecoveryResolutionResult(
    RecoveryAction Action,
    bool Succeeded,
    bool IndexEntryRemoved,
    bool CleanupCompleted,
    string? Error)
{
    public static RecoveryResolutionResult Preserved()
    {
        return new RecoveryResolutionResult(
            RecoveryAction.Preserve,
            Succeeded: true,
            IndexEntryRemoved: false,
            CleanupCompleted: false,
            Error: null);
    }

    public static RecoveryResolutionResult Failed(
        RecoveryAction action,
        bool indexEntryRemoved,
        string error)
    {
        return new RecoveryResolutionResult(
            action,
            Succeeded: false,
            IndexEntryRemoved: indexEntryRemoved,
            CleanupCompleted: false,
            Error: error);
    }

    public static RecoveryResolutionResult Completed(
        RecoveryAction action,
        bool indexEntryRemoved)
    {
        return new RecoveryResolutionResult(
            action,
            Succeeded: true,
            IndexEntryRemoved: indexEntryRemoved,
            CleanupCompleted: true,
            Error: null);
    }
}
