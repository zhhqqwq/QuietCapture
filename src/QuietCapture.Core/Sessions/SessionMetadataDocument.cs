using QuietCapture.Core.Models;

namespace QuietCapture.Core.Sessions;

public sealed record SessionMetadataDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public string SessionId { get; init; } =
        string.Empty;

    public string Status { get; init; } =
        string.Empty;

    public string? StopReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string WorkingDirectory { get; init; } =
        string.Empty;

    public string TempMediaPath { get; init; } =
        string.Empty;

    public string FinalMediaPath { get; init; } =
        string.Empty;

    public CaptureTargetDocument Target { get; init; } =
        new();

    public RecordingOptionsDocument Options { get; init; } =
        new();

    public static SessionMetadataDocument FromDomain(
        SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(
            session);

        return new SessionMetadataDocument
        {
            SessionId =
                session.Id.Value.ToString("D"),
            Status =
                session.Status.ToString(),
            StopReason =
                session.StopReason?.ToString(),
            CreatedAt =
                session.CreatedAt,
            StartedAt =
                session.StartedAt,
            FinishedAt =
                session.FinishedAt,
            WorkingDirectory =
                session.WorkingDirectory,
            TempMediaPath =
                session.TempMediaPath,
            FinalMediaPath =
                session.FinalMediaPath,
            Target =
                CaptureTargetDocument.FromDomain(
                    session.Target),
            Options =
                RecordingOptionsDocument.FromDomain(
                    session.Options)
        };
    }
}

public sealed record CaptureTargetDocument
{
    public string Kind { get; init; } =
        string.Empty;

    public string? MonitorId { get; init; }

    public long? Hwnd { get; init; }

    public int? X { get; init; }

    public int? Y { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public static CaptureTargetDocument FromDomain(
        CaptureTarget target)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        return target switch
        {
            AreaCaptureTarget area =>
                new CaptureTargetDocument
                {
                    Kind = "Area",
                    MonitorId = area.MonitorId,
                    X = area.Bounds.X,
                    Y = area.Bounds.Y,
                    Width = area.Bounds.Width,
                    Height = area.Bounds.Height
                },

            WindowCaptureTarget window =>
                new CaptureTargetDocument
                {
                    Kind = "Window",
                    Hwnd = window.Hwnd.ToInt64()
                },

            MonitorCaptureTarget monitor =>
                new CaptureTargetDocument
                {
                    Kind = "Monitor",
                    MonitorId = monitor.MonitorId
                },

            _ => throw new NotSupportedException(
                $"Unsupported capture target type: {target.GetType().FullName}.")
        };
    }
}

public sealed record RecordingOptionsDocument
{
    public int OutputWidth { get; init; }

    public int OutputHeight { get; init; }

    public int FrameRate { get; init; }

    public string QualityPresetId { get; init; } =
        string.Empty;

    public bool RecordSystemAudio { get; init; }

    public bool RecordMicrophone { get; init; }

    public string? SystemAudioDeviceId { get; init; }

    public string? MicrophoneDeviceId { get; init; }

    public static RecordingOptionsDocument FromDomain(
        RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        return new RecordingOptionsDocument
        {
            OutputWidth =
                options.OutputSize.Width,
            OutputHeight =
                options.OutputSize.Height,
            FrameRate =
                options.FrameRate,
            QualityPresetId =
                options.Quality.Id,
            RecordSystemAudio =
                options.RecordSystemAudio,
            RecordMicrophone =
                options.RecordMicrophone,
            SystemAudioDeviceId =
                options.SystemAudioDeviceId,
            MicrophoneDeviceId =
                options.MicrophoneDeviceId
        };
    }
}
