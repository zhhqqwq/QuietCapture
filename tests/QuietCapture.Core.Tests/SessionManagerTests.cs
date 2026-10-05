using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class SessionManagerTests
{
    [Fact]
    public void CreateSession_ReservesOutputCreatesMetadataAndReturnsCreatedSession()
    {
        var fileSystem =
            new FakeFileSystem();
        var manager =
            CreateManager(fileSystem);

        DateTimeOffset createdAt =
            new(
                2026,
                10,
                5,
                1,
                2,
                3,
                TimeSpan.Zero);

        SessionMetadata session =
            manager.CreateSession(
                CreateTarget(),
                CreateOptions(),
                CreateOutputDirectory(
                    "create-success"),
                createdAt);

        Assert.Equal(
            SessionStatus.Created,
            session.Status);
        Assert.Equal(
            createdAt,
            session.CreatedAt);

        Assert.True(
            fileSystem.DirectoryExists(
                session.WorkingDirectory));

        Assert.True(
            fileSystem.FileExists(
                session.FinalMediaPath));

        Assert.Equal(
            0,
            fileSystem.GetFileLength(
                session.FinalMediaPath));

        string metadataPath =
            Path.Combine(
                session.WorkingDirectory,
                "session.json");

        string json =
            fileSystem.ReadAllText(
                metadataPath);

        SessionMetadataDocument document =
            SessionMetadataJson.Deserialize(
                json);

        Assert.Equal(
            SessionMetadataDocument
                .CurrentSchemaVersion,
            document.SchemaVersion);

        Assert.Equal(
            session.Id.Value.ToString("D"),
            document.SessionId);

        Assert.Equal(
            "Created",
            document.Status);

        Assert.Equal(
            "Area",
            document.Target.Kind);

        Assert.Equal(
            "DISPLAY-1",
            document.Target.MonitorId);

        Assert.Equal(
            -1200,
            document.Target.X);

        RecoveryIndexEntry recovery =
            manager.CreateRecoveryIndexEntry(
                session);

        Assert.Equal(
            session.Id,
            recovery.SessionId);
        Assert.Equal(
            session.WorkingDirectory,
            recovery.WorkingDirectory);
    }

    [Fact]
    public void MetadataWriteFailure_ReleasesEmptyFinalReservation()
    {
        var fileSystem =
            new FakeFileSystem
            {
                AtomicWriteFailureFactory = path =>
                    path.EndsWith(
                        "session.json",
                        StringComparison.OrdinalIgnoreCase)
                        ? new IOException(
                            "Injected metadata failure.")
                        : null
            };

        var manager =
            CreateManager(fileSystem);

        string outputDirectory =
            CreateOutputDirectory(
                "metadata-failure");

        Assert.Throws<IOException>(
            () =>
                manager.CreateSession(
                    CreateTarget(),
                    CreateOptions(),
                    outputDirectory,
                    DateTimeOffset.UtcNow));

        Assert.DoesNotContain(
            fileSystem.FilePaths,
            path =>
                path.EndsWith(
                    ".mp4",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MetadataWriteFailure_PreservesNonEmptyPartialMedia()
    {
        var fileSystem =
            new FakeFileSystem();

        fileSystem.BeforeAtomicWrite = path =>
        {
            if (!path.EndsWith(
                    "session.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string workingDirectory =
                Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException();

            fileSystem.AddFile(
                Path.Combine(
                    workingDirectory,
                    "recording.partial.mp4"),
                "non-empty-partial");
        };

        fileSystem.AtomicWriteFailureFactory = path =>
            path.EndsWith(
                "session.json",
                StringComparison.OrdinalIgnoreCase)
                ? new IOException(
                    "Injected metadata failure.")
                : null;

        var manager =
            CreateManager(fileSystem);

        Assert.Throws<IOException>(
            () =>
                manager.CreateSession(
                    CreateTarget(),
                    CreateOptions(),
                    CreateOutputDirectory(
                        "preserve-partial"),
                    DateTimeOffset.UtcNow));

        string partialPath =
            Assert.Single(
                fileSystem.FilePaths.Where(
                    path =>
                        path.EndsWith(
                            "recording.partial.mp4",
                            StringComparison.OrdinalIgnoreCase)));

        Assert.True(
            fileSystem.GetFileLength(
                partialPath) > 0);

        string workingDirectory =
            Path.GetDirectoryName(
                partialPath)
            ?? throw new InvalidOperationException();

        Assert.True(
            fileSystem.DirectoryExists(
                workingDirectory));

        Assert.DoesNotContain(
            fileSystem.FilePaths,
            path =>
                path.EndsWith(
                    ".mp4",
                    StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(
                    "recording.partial.mp4",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SerializedMetadata_PreservesResolvedAudioBindings()
    {
        var fileSystem =
            new FakeFileSystem();
        var manager =
            CreateManager(fileSystem);

        SessionMetadata session =
            manager.CreateSession(
                CreateTarget(),
                new RecordingOptions(
                    new PixelSize(
                        1280,
                        720),
                    30,
                    new VideoQualityPreset(
                        "balanced"),
                    recordSystemAudio: true,
                    recordMicrophone: true,
                    systemAudioDeviceId:
                        "render-device-42",
                    microphoneDeviceId:
                        "capture-device-17"),
                CreateOutputDirectory(
                    "audio-metadata"),
                DateTimeOffset.UtcNow);

        string json =
            fileSystem.ReadAllText(
                Path.Combine(
                    session.WorkingDirectory,
                    "session.json"));

        SessionMetadataDocument document =
            SessionMetadataJson.Deserialize(
                json);

        Assert.True(
            document.Options.RecordSystemAudio);
        Assert.True(
            document.Options.RecordMicrophone);
        Assert.Equal(
            "render-device-42",
            document.Options.SystemAudioDeviceId);
        Assert.Equal(
            "capture-device-17",
            document.Options.MicrophoneDeviceId);
    }

    private static SessionManager CreateManager(
        FakeFileSystem fileSystem)
    {
        string indexPath =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "SessionManagerTests",
                Guid.NewGuid().ToString("N"),
                "recovery-index.json");

        var persistence =
            new SessionPersistenceService(
                new SessionStore(fileSystem),
                new RecoveryIndexStore(
                    fileSystem,
                    indexPath));

        return new SessionManager(
            fileSystem,
            new OutputPlanner(fileSystem),
            persistence);
    }

    private static CaptureTarget CreateTarget()
    {
        return new AreaCaptureTarget(
            "DISPLAY-1",
            new PixelRect(
                -1200,
                80,
                1280,
                720));
    }

    private static RecordingOptions CreateOptions()
    {
        return new RecordingOptions(
            new PixelSize(
                1280,
                720),
            30,
            new VideoQualityPreset(
                "balanced"),
            recordSystemAudio: false,
            recordMicrophone: false,
            systemAudioDeviceId: null,
            microphoneDeviceId: null);
    }

    private static string CreateOutputDirectory(
        string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuietCapture",
            suffix);
    }
}
