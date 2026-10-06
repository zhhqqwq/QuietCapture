namespace QuietCapture.Core.Sessions;

public enum FinalizationFailure
{
    InvalidState,
    MediaPublication,
    StopFailedPersistence,
    CompletedPersistence,
    RecoveryIndexRemoval,
    Cleanup
}
