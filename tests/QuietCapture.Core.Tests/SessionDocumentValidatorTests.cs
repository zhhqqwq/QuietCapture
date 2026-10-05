using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Tests;

public sealed class SessionDocumentValidatorTests
{
    [Fact]
    public void Validate_RejectsWorkingDirectoryMismatch()
    {
        SessionMetadata session =
            SessionMetadata.Create(
                SessionId.New(),
                new MonitorCaptureTarget(
                    "DISPLAY-1"),
                new RecordingOptions(
                    new PixelSize(
                        1920,
                        1080),
                    30,
                    new VideoQualityPreset(
                        "balanced"),
                    recordSystemAudio: false,
                    recordMicrophone: false,
                    systemAudioDeviceId: null,
                    microphoneDeviceId: null),
                @"D:\Recordings\.screenrecorder\sessions\one",
                @"D:\Recordings\.screenrecorder\sessions\one\recording.partial.mp4",
                @"D:\Recordings\final.mp4",
                DateTimeOffset.UtcNow);

        SessionMetadataDocument document =
            SessionMetadataDocument.FromDomain(
                session);

        SessionDocumentValidationResult result =
            new SessionDocumentValidator()
                .Validate(
                    document,
                    @"D:\Recordings\.screenrecorder\sessions\other");

        Assert.False(
            result.IsValid);
        Assert.Contains(
            "working directory",
            result.Error,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsEnabledAudioWithoutConcreteDeviceId()
    {
        var document =
            new SessionMetadataDocument
            {
                SessionId =
                    Guid.NewGuid().ToString("D"),
                Status =
                    SessionStatus.Created.ToString(),
                CreatedAt =
                    DateTimeOffset.UtcNow,
                WorkingDirectory =
                    @"D:\Recordings\.screenrecorder\sessions\one",
                TempMediaPath =
                    @"D:\Recordings\.screenrecorder\sessions\one\recording.partial.mp4",
                FinalMediaPath =
                    @"D:\Recordings\final.mp4",
                Target =
                    new CaptureTargetDocument
                    {
                        Kind = "Monitor",
                        MonitorId = "DISPLAY-1"
                    },
                Options =
                    new RecordingOptionsDocument
                    {
                        OutputWidth = 1920,
                        OutputHeight = 1080,
                        FrameRate = 30,
                        QualityPresetId =
                            "balanced",
                        RecordSystemAudio = true,
                        SystemAudioDeviceId = null
                    }
            };

        SessionDocumentValidationResult result =
            new SessionDocumentValidator()
                .Validate(
                    document,
                    document.WorkingDirectory);

        Assert.False(
            result.IsValid);
        Assert.Contains(
            "device ID",
            result.Error,
            StringComparison.OrdinalIgnoreCase);
    }
}
