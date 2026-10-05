namespace QuietCapture.Core.Sessions;

public enum SessionStatus
{
    Created,
    Starting,
    Recording,
    Finalizing,
    Completed,
    FailedToStart,
    Interrupted,
    StopFailed,
    Orphaned
}
