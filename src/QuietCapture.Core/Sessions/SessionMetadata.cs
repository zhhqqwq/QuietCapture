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

    internal static SessionMetadata Restore(
        SessionMetadataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!Guid.TryParse(
                document.SessionId,
                out Guid sessionGuid) ||
            sessionGuid == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Persisted Session ID is invalid.");
        }

        if (!Enum.TryParse(
                document.Status,
                ignoreCase: false,
                out SessionStatus status) ||
            !Enum.IsDefined(status))
        {
            throw new InvalidOperationException(
                "Persisted Session status is invalid.");
        }

        StopReason? stopReason = null;

        if (document.StopReason is not null)
        {
            if (!Enum.TryParse(
                    document.StopReason,
                    ignoreCase: false,
                    out StopReason parsedStopReason) ||
                !Enum.IsDefined(parsedStopReason))
            {
                throw new InvalidOperationException(
                    "Persisted StopReason is invalid.");
            }

            stopReason = parsedStopReason;
        }

        CaptureTarget target =
            RestoreTarget(document.Target);

        RecordingOptions options =
            new(
                new PixelSize(
                    document.Options.OutputWidth,
                    document.Options.OutputHeight),
                document.Options.FrameRate,
                new VideoQualityPreset(
                    document.Options.QualityPresetId),
                document.Options.RecordSystemAudio,
                document.Options.RecordMicrophone,
                document.Options.SystemAudioDeviceId,
                document.Options.MicrophoneDeviceId);

        var session =
            new SessionMetadata(
                new SessionId(sessionGuid),
                target,
                options,
                document.WorkingDirectory,
                document.TempMediaPath,
                document.FinalMediaPath,
                document.CreatedAt)
            {
                StartedAt = document.StartedAt,
                FinishedAt = document.FinishedAt,
                Status = status,
                StopReason = stopReason
            };

        return session;
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

    private static CaptureTarget RestoreTarget(
        CaptureTargetDocument target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Kind switch
        {
            "Area" =>
                new AreaCaptureTarget(
                    target.MonitorId ??
                        throw new InvalidOperationException(
                            "Persisted Area target monitor ID is missing."),
                    new PixelRect(
                        target.X ??
                            throw new InvalidOperationException(
                                "Persisted Area target X is missing."),
                        target.Y ??
                            throw new InvalidOperationException(
                                "Persisted Area target Y is missing."),
                        target.Width ??
                            throw new InvalidOperationException(
                                "Persisted Area target Width is missing."),
                        target.Height ??
                            throw new InvalidOperationException(
                                "Persisted Area target Height is missing."))),

            "Window" =>
                new WindowCaptureTarget(
                    new IntPtr(
                        target.Hwnd ??
                            throw new InvalidOperationException(
                                "Persisted Window target HWND is missing."))),

            "Monitor" =>
                new MonitorCaptureTarget(
                    target.MonitorId ??
                        throw new InvalidOperationException(
                            "Persisted Monitor target ID is missing.")),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported persisted capture target kind: {target.Kind}.")
        };
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
