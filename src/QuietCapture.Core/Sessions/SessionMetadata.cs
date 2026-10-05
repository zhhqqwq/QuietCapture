using QuietCapture.Core.Models;

namespace QuietCapture.Core.Sessions;

public sealed class SessionMetadata
{
    private SessionMetadata(
        SessionId id,
        CaptureTarget target,
        RecordingOptions options,
        string workingDirectory,
        string tempMediaPath,
        string finalMediaPath,
        DateTimeOffset createdAt)
    {
        Id = id;
        Target = target;
        Options = options;
        WorkingDirectory = RequirePath(
            workingDirectory,
            nameof(workingDirectory));
        TempMediaPath = RequirePath(
            tempMediaPath,
            nameof(tempMediaPath));
        FinalMediaPath = RequirePath(
            finalMediaPath,
            nameof(finalMediaPath));
        CreatedAt = createdAt;
        Status = SessionStatus.Created;
    }

    public SessionId Id { get; }

    public CaptureTarget Target { get; }

    public RecordingOptions Options { get; }

    public string WorkingDirectory { get; }

    public string TempMediaPath { get; }

    public string FinalMediaPath { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public SessionStatus Status { get; private set; }

    public StopReason? StopReason { get; private set; }

    public static SessionMetadata Create(
        SessionId id,
        CaptureTarget target,
        RecordingOptions options,
        string workingDirectory,
        string tempMediaPath,
        string finalMediaPath,
        DateTimeOffset createdAt)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID must not be empty.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        return new SessionMetadata(
            id,
            target,
            options,
            workingDirectory,
            tempMediaPath,
            finalMediaPath,
            createdAt);
    }

    internal void TransitionTo(
        SessionStatus nextStatus,
        DateTimeOffset occurredAt,
        StopReason? stopReason = null)
    {
        SessionLifecycle.EnsureTransition(
            Status,
            nextStatus);

        if (nextStatus == SessionStatus.Recording &&
            StartedAt is null)
        {
            StartedAt = occurredAt;
        }

        if (stopReason.HasValue)
        {
            StopReason ??= stopReason;
        }

        Status = nextStatus;

        if (SessionLifecycle.IsTerminal(nextStatus))
        {
            FinishedAt = occurredAt;
        }
    }

    private static string RequirePath(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Session path must not be empty.",
                parameterName);
        }

        return value;
    }
}
