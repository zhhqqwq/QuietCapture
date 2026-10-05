using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class SessionCleanupPolicyTests
{
    [Fact]
    public void CompletedSession_CleansMetadataAndWorkingDirectoryAfterPublish()
    {
        Fixture fixture =
            CreateFixture("completed-cleanup");

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "video");

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));
        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));
        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Finalizing,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.UserRequested);

        MediaPublishResult publish =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.True(publish.Succeeded);

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Completed,
            fixture.CreatedAt.AddSeconds(4));

        bool cleaned =
            fixture.Cleanup.TryCleanup(
                fixture.Session);

        Assert.True(cleaned);
        Assert.False(
            fixture.FileSystem.FileExists(
                SessionStore.GetMetadataPath(
                    fixture.Session.WorkingDirectory)));
        Assert.False(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Session.FinalMediaPath));
        Assert.True(
            fixture.FileSystem.GetFileLength(
                fixture.Session.FinalMediaPath) > 0);
    }

    [Fact]
    public void StopFailed_KeepsWorkingDirectoryAndMetadata()
    {
        Fixture fixture =
            CreateFixture("stop-failed");

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "partial-video");

        AdvanceToRecording(
            fixture);

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.StopFailed,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.BackendFailure);

        Assert.False(
            fixture.Cleanup.TryCleanup(
                fixture.Session));

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Session.TempMediaPath));
        Assert.True(
            fixture.FileSystem.FileExists(
                SessionStore.GetMetadataPath(
                    fixture.Session.WorkingDirectory)));
    }

    [Fact]
    public void Interrupted_KeepsWorkingDirectoryAndMetadata()
    {
        Fixture fixture =
            CreateFixture("interrupted");

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "partial-video");

        AdvanceToRecording(
            fixture);

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Interrupted,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.SystemSuspend);

        Assert.False(
            fixture.Cleanup.TryCleanup(
                fixture.Session));

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Session.TempMediaPath));
    }

    [Fact]
    public void Cleanup_WaitsUntilRecoveryIndexRemovalSucceeds()
    {
        Fixture fixture =
            CreateFixture("cleanup-ordering");

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "video");

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));
        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));
        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Finalizing,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.UserRequested);

        Assert.True(
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session)
            .Succeeded);

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(
                        fixture.IndexPath),
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected index remove failure.")
                    : null;

        Assert.Throws<IOException>(
            () =>
                fixture.Persistence.TransitionTo(
                    fixture.Session,
                    SessionStatus.Completed,
                    fixture.CreatedAt.AddSeconds(4)));

        Assert.Equal(
            SessionStatus.Completed,
            fixture.Session.Status);

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.False(
            fixture.Cleanup.TryCleanup(
                fixture.Session));

        Assert.True(
            fixture.FileSystem.FileExists(
                SessionStore.GetMetadataPath(
                    fixture.Session.WorkingDirectory)));

        fixture.IndexStore.Remove(
            RecoveryIndexEntry.FromSession(
                fixture.Session));

        Assert.True(
            fixture.Cleanup.TryCleanup(
                fixture.Session));

        Assert.False(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
    }

    private static void AdvanceToRecording(
        Fixture fixture)
    {
        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));
        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));
    }

    private static Fixture CreateFixture(
        string suffix)
    {
        var fileSystem =
            new FakeFileSystem();

        string root =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "CleanupPolicyTests",
                suffix,
                Guid.NewGuid().ToString("N"));

        string outputDirectory =
            Path.Combine(
                root,
                "Output");

        string indexPath =
            Path.Combine(
                root,
                "AppData",
                "recovery-index.json");

        var store =
            new SessionStore(fileSystem);

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var persistence =
            new SessionPersistenceService(
                store,
                indexStore);

        var manager =
            new SessionManager(
                fileSystem,
                new OutputPlanner(
                    fileSystem),
                persistence);

        DateTimeOffset createdAt =
            new(
                2026,
                10,
                6,
                3,
                0,
                0,
                TimeSpan.Zero);

        SessionMetadata session =
            manager.CreateSession(
                new AreaCaptureTarget(
                    "DISPLAY-1",
                    new PixelRect(
                        0,
                        0,
                        1280,
                        720)),
                new RecordingOptions(
                    new PixelSize(
                        1280,
                        720),
                    30,
                    new VideoQualityPreset(
                        "balanced"),
                    recordSystemAudio: false,
                    recordMicrophone: false,
                    systemAudioDeviceId: null,
                    microphoneDeviceId: null),
                outputDirectory,
                createdAt);

        return new Fixture(
            fileSystem,
            indexStore,
            persistence,
            new SessionCleanupPolicy(
                fileSystem,
                indexStore),
            session,
            indexPath,
            createdAt);
    }

    private sealed record Fixture(
        FakeFileSystem FileSystem,
        RecoveryIndexStore IndexStore,
        SessionPersistenceService Persistence,
        SessionCleanupPolicy Cleanup,
        SessionMetadata Session,
        string IndexPath,
        DateTimeOffset CreatedAt);
}
