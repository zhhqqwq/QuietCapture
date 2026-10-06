using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class SessionFinalizationServiceTests
{
    [Fact]
    public void Finalize_HappyPathPublishesCompletesAndCleans()
    {
        Fixture fixture =
            CreateFixture("happy-path");

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.True(result.Succeeded);
        Assert.False(result.AlreadyCompleted);
        Assert.True(result.CleanupCompleted);
        Assert.Null(result.Failure);

        Assert.Equal(
            SessionStatus.Completed,
            fixture.Session.Status);

        Assert.False(
            fixture.FileSystem.FileExists(
                fixture.Session.TempMediaPath));

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));

        Assert.False(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.False(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
    }

    [Fact]
    public void Finalize_PublishFailurePreservesPartialAndPersistsStopFailed()
    {
        Fixture fixture =
            CreateFixture("publish-failure");

        fixture.FileSystem.MoveFileFailureFactory =
            (_, _) =>
                new IOException(
                    "Injected publish failure.");

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.False(result.Succeeded);
        Assert.Equal(
            FinalizationFailure.MediaPublication,
            result.Failure);

        Assert.Equal(
            SessionStatus.StopFailed,
            fixture.Session.Status);

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.TempMediaPath));

        Assert.Equal(
            0,
            fixture.FileSystem.GetFileLength(
                fixture.Session.FinalMediaPath));

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.Equal(
            "StopFailed",
            fixture.Store
                .LoadDocument(
                    fixture.Session.WorkingDirectory)
                .Status);
    }

    [Fact]
    public void Finalize_StopFailedPersistenceFailureStillPreservesPartial()
    {
        Fixture fixture =
            CreateFixture(
                "stop-failed-persistence");

        fixture.FileSystem.MoveFileFailureFactory =
            (_, _) =>
                new IOException(
                    "Injected publish failure.");

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                path.EndsWith(
                    "session.json",
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected StopFailed metadata failure.")
                    : null;

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.False(result.Succeeded);
        Assert.Equal(
            FinalizationFailure.StopFailedPersistence,
            result.Failure);

        Assert.Equal(
            SessionStatus.Finalizing,
            fixture.Session.Status);

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.TempMediaPath));

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.Equal(
            "Finalizing",
            fixture.Store
                .LoadDocument(
                    fixture.Session.WorkingDirectory)
                .Status);

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));
    }

    [Fact]
    public void Finalize_CompletedMetadataFailurePreservesPublishedMedia()
    {
        Fixture fixture =
            CreateFixture(
                "completed-metadata-failure");

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                path.EndsWith(
                    "session.json",
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected Completed metadata failure.")
                    : null;

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.False(result.Succeeded);
        Assert.Equal(
            FinalizationFailure.CompletedPersistence,
            result.Failure);

        Assert.Equal(
            SessionStatus.Finalizing,
            fixture.Session.Status);

        Assert.False(
            fixture.FileSystem.FileExists(
                fixture.Session.TempMediaPath));

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.Equal(
            "Finalizing",
            fixture.Store
                .LoadDocument(
                    fixture.Session.WorkingDirectory)
                .Status);

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));
    }

    [Fact]
    public void Finalize_IndexRemovalFailureLeavesCompletedMetadataAndFinalMedia()
    {
        Fixture fixture =
            CreateFixture(
                "index-remove-failure");

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

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.False(result.Succeeded);
        Assert.Equal(
            FinalizationFailure.RecoveryIndexRemoval,
            result.Failure);

        Assert.Equal(
            SessionStatus.Completed,
            fixture.Session.Status);

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.Equal(
            "Completed",
            fixture.Store
                .LoadDocument(
                    fixture.Session.WorkingDirectory)
                .Status);

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
    }

    [Fact]
    public void Finalize_CleanupFailureLeavesCompletedEvidenceAndFinalMedia()
    {
        Fixture fixture =
            CreateFixture(
                "cleanup-failure");

        string metadataPath =
            SessionStore.GetMetadataPath(
                fixture.Session.WorkingDirectory);

        fixture.FileSystem.DeleteFileFailureFactory =
            path =>
                string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(
                        metadataPath),
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected cleanup failure.")
                    : null;

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.False(result.Succeeded);
        Assert.Equal(
            FinalizationFailure.Cleanup,
            result.Failure);

        Assert.Equal(
            SessionStatus.Completed,
            fixture.Session.Status);

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));

        Assert.True(
            fixture.FileSystem.FileExists(
                metadataPath));

        Assert.False(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
    }

    [Fact]
    public void Finalize_UnknownWorkingFileBlocksCleanupWithoutDeletingMetadata()
    {
        Fixture fixture =
            CreateFixture(
                "unknown-working-file");

        string metadataPath =
            SessionStore.GetMetadataPath(
                fixture.Session.WorkingDirectory);

        fixture.FileSystem.AddFile(
            Path.Combine(
                fixture.Session.WorkingDirectory,
                "session.log"),
            "diagnostic");

        FinalizationResult result =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.False(result.Succeeded);
        Assert.Equal(
            FinalizationFailure.Cleanup,
            result.Failure);

        Assert.Equal(
            SessionStatus.Completed,
            fixture.Session.Status);

        Assert.True(
            fixture.FileSystem.FileExists(
                metadataPath));

        Assert.True(
            fixture.FileSystem.FileExists(
                Path.Combine(
                    fixture.Session.WorkingDirectory,
                    "session.log")));

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));
    }

    [Fact]
    public void Finalize_RepeatedAfterSuccessIsIdempotent()
    {
        Fixture fixture =
            CreateFixture(
                "repeat-finalize");

        FinalizationResult first =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt);

        Assert.True(first.Succeeded);

        long finalBytes =
            fixture.FileSystem.GetFileLength(
                fixture.Session.FinalMediaPath);

        FinalizationResult second =
            fixture.Finalizer.Finalize(
                fixture.Session,
                fixture.CompletedAt.AddSeconds(1));

        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyCompleted);
        Assert.True(second.CleanupCompleted);
        Assert.Equal(
            SessionStatus.Completed,
            fixture.Session.Status);
        Assert.Equal(
            finalBytes,
            fixture.FileSystem.GetFileLength(
                fixture.Session.FinalMediaPath));
        Assert.Null(
            second.MediaPublishResult);
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
                "FinalizationTests",
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
            new SessionStore(
                fileSystem);

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

        var cleanup =
            new SessionCleanupPolicy(
                fileSystem,
                indexStore);

        var finalizer =
            new SessionFinalizationService(
                new MediaPublisher(
                    fileSystem),
                persistence,
                cleanup);

        DateTimeOffset createdAt =
            new(
                2026,
                10,
                6,
                4,
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

        persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            createdAt.AddSeconds(1));

        persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            createdAt.AddSeconds(2));

        persistence.TransitionTo(
            session,
            SessionStatus.Finalizing,
            createdAt.AddSeconds(3),
            StopReason.UserRequested);

        fileSystem.AddFile(
            session.TempMediaPath,
            "video-data");

        return new Fixture(
            fileSystem,
            store,
            indexStore,
            finalizer,
            session,
            indexPath,
            createdAt.AddSeconds(4));
    }

    private sealed record Fixture(
        FakeFileSystem FileSystem,
        SessionStore Store,
        RecoveryIndexStore IndexStore,
        SessionFinalizationService Finalizer,
        SessionMetadata Session,
        string IndexPath,
        DateTimeOffset CompletedAt);
}
