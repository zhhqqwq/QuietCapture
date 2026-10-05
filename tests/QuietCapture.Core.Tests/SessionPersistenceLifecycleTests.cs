using QuietCapture.Core.Models;
using QuietCapture.Core.Recovery;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class SessionPersistenceLifecycleTests
{
    [Fact]
    public void MetadataTransitions_ArePersistedAndCompletedRemovesIndex()
    {
        Fixture fixture =
            CreateFixture("transition-persistence");

        SessionMetadata session =
            fixture.Manager.CreateSession(
                CreateTarget(),
                CreateOptions(),
                fixture.OutputDirectory,
                fixture.CreatedAt);

        Assert.Equal(
            "Created",
            fixture.Store
                .LoadDocument(
                    session.WorkingDirectory)
                .Status);

        Assert.Single(
            fixture.IndexStore.Load().Entries);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));

        Assert.Equal(
            "Starting",
            fixture.Store
                .LoadDocument(
                    session.WorkingDirectory)
                .Status);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));

        SessionMetadataDocument recording =
            fixture.Store.LoadDocument(
                session.WorkingDirectory);

        Assert.Equal(
            "Recording",
            recording.Status);
        Assert.Equal(
            fixture.CreatedAt.AddSeconds(2),
            recording.StartedAt);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Finalizing,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.UserRequested);

        SessionMetadataDocument finalizing =
            fixture.Store.LoadDocument(
                session.WorkingDirectory);

        Assert.Equal(
            "Finalizing",
            finalizing.Status);
        Assert.Equal(
            StopReason.UserRequested.ToString(),
            finalizing.StopReason);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Completed,
            fixture.CreatedAt.AddSeconds(4));

        SessionMetadataDocument completed =
            fixture.Store.LoadDocument(
                session.WorkingDirectory);

        Assert.Equal(
            "Completed",
            completed.Status);
        Assert.Equal(
            fixture.CreatedAt.AddSeconds(4),
            completed.FinishedAt);
        Assert.Empty(
            fixture.IndexStore.Load().Entries);
    }

    [Fact]
    public void FailedMetadataUpdate_DoesNotAdvanceInMemoryState()
    {
        Fixture fixture =
            CreateFixture("metadata-failure");

        SessionMetadata session =
            fixture.Manager.CreateSession(
                CreateTarget(),
                CreateOptions(),
                fixture.OutputDirectory,
                fixture.CreatedAt);

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                path.EndsWith(
                    "session.json",
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected session metadata failure.")
                    : null;

        Assert.Throws<IOException>(
            () =>
                fixture.Persistence.TransitionTo(
                    session,
                    SessionStatus.Starting,
                    fixture.CreatedAt.AddSeconds(1)));

        Assert.Equal(
            SessionStatus.Created,
            session.Status);

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.Equal(
            "Created",
            fixture.Store
                .LoadDocument(
                    session.WorkingDirectory)
                .Status);

        Assert.Single(
            fixture.IndexStore.Load().Entries);
    }

    [Fact]
    public void RecoveryIndexStore_AddRemove_IsAtomicLogicalLifecycle()
    {
        var fileSystem =
            new FakeFileSystem();

        string indexPath =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "RecoveryIndexStoreTests",
                Guid.NewGuid().ToString("N"),
                "recovery-index.json");

        var store =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var entry =
            new RecoveryIndexEntry(
                SessionId.New(),
                Path.Combine(
                    Path.GetTempPath(),
                    "QuietCapture",
                    "sessions",
                    Guid.NewGuid().ToString("N")));

        store.Add(entry);
        store.Add(entry);

        RecoveryIndex afterAdd =
            store.Load();

        Assert.Single(
            afterAdd.Entries);

        store.Remove(entry);

        Assert.Empty(
            store.Load().Entries);
    }

    [Fact]
    public void IndexRemoveFailure_LeavesTerminalMetadataAndStaleIndex()
    {
        Fixture fixture =
            CreateFixture("index-remove-failure");

        SessionMetadata session =
            fixture.Manager.CreateSession(
                CreateTarget(),
                CreateOptions(),
                fixture.OutputDirectory,
                fixture.CreatedAt);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));
        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));
        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Finalizing,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.UserRequested);

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
                    session,
                    SessionStatus.Completed,
                    fixture.CreatedAt.AddSeconds(4)));

        Assert.Equal(
            SessionStatus.Completed,
            session.Status);

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.Equal(
            "Completed",
            fixture.Store
                .LoadDocument(
                    session.WorkingDirectory)
                .Status);

        RecoveryIndex stale =
            fixture.IndexStore.Load();

        RecoveryIndexEntry entry =
            Assert.Single(
                stale.Entries);

        Assert.Equal(
            session.Id,
            entry.SessionId);
    }

    [Fact]
    public void CrashBetweenSessionAndIndexWrites_IsRecoverableByDirectoryScan()
    {
        Fixture fixture =
            CreateFixture("crash-between-writes");

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(
                        fixture.IndexPath),
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected index add failure.")
                    : null;

        Assert.Throws<IOException>(
            () =>
                fixture.Manager.CreateSession(
                    CreateTarget(),
                    CreateOptions(),
                    fixture.OutputDirectory,
                    fixture.CreatedAt));

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        string metadataPath =
            Assert.Single(
                fixture.FileSystem.FilePaths
                    .Where(path =>
                        path.EndsWith(
                            "session.json",
                            StringComparison.OrdinalIgnoreCase)));

        string workingDirectory =
            Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException();

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                workingDirectory));

        Assert.Contains(
            fixture.FileSystem.FilePaths,
            path =>
                path.EndsWith(
                    ".mp4",
                    StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(
                    "recording.partial.mp4",
                    StringComparison.OrdinalIgnoreCase));

        RecoveryScanResult result =
            new RecoveryService(
                fixture.FileSystem,
                new SessionDocumentValidator())
            .Scan(
                fixture.IndexPath,
                new[]
                {
                    fixture.OutputDirectory
                });

        RecoveryCandidate candidate =
            Assert.Single(
                result.Candidates);

        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);
        Assert.Equal(
            SessionStatus.Created,
            candidate.PersistedStatus);
        Assert.False(
            candidate.FromRecoveryIndex);
        Assert.True(
            candidate.FromDirectoryScan);
    }

    [Fact]
    public void RecoveryAfterStaleIndex_UsesTerminalMetadataAsTruth()
    {
        Fixture fixture =
            CreateFixture("stale-index-recovery");

        SessionMetadata session =
            fixture.Manager.CreateSession(
                CreateTarget(),
                CreateOptions(),
                fixture.OutputDirectory,
                fixture.CreatedAt);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));
        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));
        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Finalizing,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.UserRequested);

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(
                        fixture.IndexPath),
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected stale-index failure.")
                    : null;

        Assert.Throws<IOException>(
            () =>
                fixture.Persistence.TransitionTo(
                    session,
                    SessionStatus.Completed,
                    fixture.CreatedAt.AddSeconds(4)));

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        RecoveryScanResult result =
            new RecoveryService(
                fixture.FileSystem,
                new SessionDocumentValidator())
            .Scan(
                fixture.IndexPath,
                new[]
                {
                    fixture.OutputDirectory
                });

        RecoveryCandidate candidate =
            Assert.Single(
                result.Candidates);

        Assert.Equal(
            RecoveryClassification.NoRecoveryRequired,
            candidate.Classification);
        Assert.Equal(
            SessionStatus.Completed,
            candidate.PersistedStatus);
        Assert.True(
            candidate.FromRecoveryIndex);
        Assert.True(
            candidate.FromDirectoryScan);
    }

    [Fact]
    public void StopFailed_RemainsIndexedForRecovery()
    {
        Fixture fixture =
            CreateFixture("stop-failed-index");

        SessionMetadata session =
            fixture.Manager.CreateSession(
                CreateTarget(),
                CreateOptions(),
                fixture.OutputDirectory,
                fixture.CreatedAt);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            fixture.CreatedAt.AddSeconds(1));
        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            fixture.CreatedAt.AddSeconds(2));
        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.StopFailed,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.BackendFailure);

        RecoveryIndexEntry entry =
            Assert.Single(
                fixture.IndexStore.Load().Entries);

        Assert.Equal(
            session.Id,
            entry.SessionId);

        RecoveryCandidate candidate =
            Assert.Single(
                new RecoveryService(
                    fixture.FileSystem,
                    new SessionDocumentValidator())
                .Scan(
                    fixture.IndexPath,
                    new[]
                    {
                        fixture.OutputDirectory
                    })
                .Candidates);

        Assert.Equal(
            RecoveryClassification.StopFailed,
            candidate.Classification);
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
                "PersistenceLifecycleTests",
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

        var sessionStore =
            new SessionStore(fileSystem);

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var persistence =
            new SessionPersistenceService(
                sessionStore,
                indexStore);

        var manager =
            new SessionManager(
                fileSystem,
                new OutputPlanner(
                    fileSystem),
                persistence);

        return new Fixture(
            fileSystem,
            sessionStore,
            indexStore,
            persistence,
            manager,
            outputDirectory,
            indexPath,
            new DateTimeOffset(
                2026,
                10,
                6,
                1,
                0,
                0,
                TimeSpan.Zero));
    }

    private static CaptureTarget CreateTarget()
    {
        return new AreaCaptureTarget(
            "DISPLAY-1",
            new PixelRect(
                0,
                0,
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

    private sealed record Fixture(
        FakeFileSystem FileSystem,
        SessionStore Store,
        RecoveryIndexStore IndexStore,
        SessionPersistenceService Persistence,
        SessionManager Manager,
        string OutputDirectory,
        string IndexPath,
        DateTimeOffset CreatedAt);
}
