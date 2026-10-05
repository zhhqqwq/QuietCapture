namespace QuietCapture.Core.Sessions;

public sealed record RecoveryIndexEntry
{
    public RecoveryIndexEntry(
        SessionId sessionId,
        string workingDirectory)
    {
        if (sessionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID must not be empty.",
                nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(
                workingDirectory))
        {
            throw new ArgumentException(
                "Working directory must not be empty.",
                nameof(workingDirectory));
        }

        SessionId = sessionId;
        WorkingDirectory = workingDirectory;
    }

    public SessionId SessionId { get; }

    public string WorkingDirectory { get; }

    public static RecoveryIndexEntry FromSession(
        SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(
            session);

        return new RecoveryIndexEntry(
            session.Id,
            session.WorkingDirectory);
    }
}
