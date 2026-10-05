using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Tests;

public sealed class SessionMetadataJsonTests
{
    [Fact]
    public void SerializeDeserialize_UsesStablePrimitiveDocumentShape()
    {
        DateTimeOffset createdAt =
            new(
                2026,
                10,
                5,
                1,
                0,
                0,
                TimeSpan.Zero);

        SessionMetadata session =
            SessionMetadata.Create(
                SessionId.New(),
                new WindowCaptureTarget(
                    new nint(12345)),
                new RecordingOptions(
                    new PixelSize(
                        1920,
                        1080),
                    60,
                    new VideoQualityPreset(
                        "high"),
                    recordSystemAudio: false,
                    recordMicrophone: false,
                    systemAudioDeviceId: null,
                    microphoneDeviceId: null),
                @"D:\Recordings\.screenrecorder\sessions\abc",
                @"D:\Recordings\.screenrecorder\sessions\abc\recording.partial.mp4",
                @"D:\Recordings\2026-10-05_01-00-00.mp4",
                createdAt);

        string json =
            SessionMetadataJson.Serialize(
                session);

        SessionMetadataDocument document =
            SessionMetadataJson.Deserialize(
                json);

        Assert.Equal(
            SessionMetadataDocument
                .CurrentSchemaVersion,
            document.SchemaVersion);
        Assert.Equal(
            "Window",
            document.Target.Kind);
        Assert.Equal(
            12345,
            document.Target.Hwnd);
        Assert.Equal(
            1920,
            document.Options.OutputWidth);
        Assert.Equal(
            1080,
            document.Options.OutputHeight);
        Assert.Equal(
            60,
            document.Options.FrameRate);
        Assert.Equal(
            "high",
            document.Options.QualityPresetId);
    }
}
