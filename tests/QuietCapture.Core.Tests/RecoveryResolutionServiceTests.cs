using QuietCapture.Core.Models;
using QuietCapture.Core.Recovery;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class RecoveryResolutionServiceTests
{
    [Fact]
    public void CompletedStaleIndex_RemovesIndexThenCleansSession()
    {
        Fixture fixture =
            CreateFixture("completed-stale-index");

        RecoveryCandidate candidate =
            CreateCompletedStaleIndexCandidate(
                fixture);

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.RemoveStaleIndexAndCleanup,
            result.Action);
        Assert.True(result.IndexEntryRemoved);
        Assert.True(result.CleanupCompleted);

        Assert.False(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.False(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));
    }

    [Fact]
    public void NoRecoveryRequiredWithoutIndex_RetriesSafeCleanup()
    {
        Fixture fixture =
            CreateFixture("cleanup-retry");

        CompleteSessionWithoutCleanup(
            fixture);

        RecoveryCandidate candidate =
            Assert.Single(
                fixture.Scanner.Scan(
                    fixture.IndexPath,
                    new[]
                    {
                        fixture.OutputDirectory
                    })
                .Candidates);

        Assert.Equal(
            RecoveryClassification.NoRecoveryRequired,
            candidate.Classification);
        Assert.False(
            candidate.FromRecoveryIndex);
        Assert.True(
            candidate.FromDirectoryScan);

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.CleanupNoRecoveryRequired,
            result.Action);
        Assert.False(result.IndexEntryRemoved);
        Assert.True(result.CleanupCompleted);
        Assert.False(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
    }

    [Fact]
    public void Interrupted_IsPreservedWithoutAutomaticDeletion()
    {
        Fixture fixture =
            CreateFixture("interrupted");

        AdvanceToRecording(
            fixture);

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "partial-media");

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Interrupted,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.SystemSuspend);

        RecoveryCandidate candidate =
            ScanSingle(fixture);

        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);

        string metadataPath =
            SessionStore.GetMetadataPath(
                fixture.Session.WorkingDirectory);

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.Preserve,
            result.Action);
        Assert.False(result.CleanupCompleted);

        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Session.TempMediaPath));
        Assert.True(
            fixture.FileSystem.FileExists(
                metadataPath));
        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));
    }

    [Fact]
    public void StopFailed_IsPreservedWithoutAutomaticDeletion()
    {
        Fixture fixture =
            CreateFixture("stop-failed");

        AdvanceToRecording(
            fixture);

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "partial-media");

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.StopFailed,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.BackendFailure);

        RecoveryCandidate candidate =
            ScanSingle(fixture);

        Assert.Equal(
            RecoveryClassification.StopFailed,
            candidate.Classification);

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.Preserve,
            result.Action);

        Assert.Equal(
            "partial-media",
            fixture.FileSystem.ReadAllText(
                fixture.Session.TempMediaPath));
        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));
    }

    [Fact]
    public void OrphanedMissingMetadata_IsPreserved()
    {
        Fixture fixture =
            CreateFixture("orphaned");

        string metadataPath =
            SessionStore.GetMetadataPath(
                fixture.Session.WorkingDirectory);

        fixture.FileSystem.DeleteFile(
            metadataPath);

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "partial-media");

        RecoveryCandidate candidate =
            ScanSingle(fixture);

        Assert.Equal(
            RecoveryClassification.Orphaned,
            candidate.Classification);

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.Preserve,
            result.Action);

        Assert.Equal(
            "partial-media",
            fixture.FileSystem.ReadAllText(
                fixture.Session.TempMediaPath));
        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));
    }

    [Fact]
    public void MissingWorkingDirectoryFromIndex_IsPreservedAsOrphaned()
    {
        var fileSystem =
            new FakeFileSystem();

        string root =
            CreateRoot("missing-working-directory");

        string indexPath =
            Path.Combine(
                root,
                "AppData",
                "recovery-index.json");

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var entry =
            new RecoveryIndexEntry(
                SessionId.New(),
                Path.Combine(
                    root,
                    "missing-volume",
                    "session"));

        indexStore.Add(entry);

        var scanner =
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator());

        RecoveryCandidate candidate =
            Assert.Single(
                scanner.Scan(
                    indexPath,
                    Array.Empty<string>())
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Orphaned,
            candidate.Classification);

        var resolution =
            new RecoveryResolutionService(
                indexStore,
                new SessionCleanupPolicy(
                    fileSystem,
                    indexStore));

        RecoveryResolutionResult result =
            resolution.Resolve(candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.Preserve,
            result.Action);
        Assert.True(
            indexStore.Contains(entry));
    }

    [Fact]
    public void InaccessibleWorkingDirectory_IsPreservedAsOrphaned()
    {
        var fileSystem =
            new FakeFileSystem();

        string root =
            CreateRoot("inaccessible-directory");

        string indexPath =
            Path.Combine(
                root,
                "AppData",
                "recovery-index.json");

        string workingDirectory =
            Path.Combine(
                root,
                "Output",
                ".screenrecorder",
                "sessions",
                Guid.NewGuid().ToString("N"));

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var entry =
            new RecoveryIndexEntry(
                SessionId.New(),
                workingDirectory);

        indexStore.Add(entry);

        fileSystem.EnumerateFilesFailureFactory =
            path =>
                PathsEqual(
                    path,
                    workingDirectory)
                    ? new UnauthorizedAccessException(
                        "Injected inaccessible directory.")
                    : null;

        var scanner =
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator());

        RecoveryCandidate candidate =
            Assert.Single(
                scanner.Scan(
                    indexPath,
                    Array.Empty<string>())
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Orphaned,
            candidate.Classification);

        var resolution =
            new RecoveryResolutionService(
                indexStore,
                new SessionCleanupPolicy(
                    fileSystem,
                    indexStore));

        RecoveryResolutionResult result =
            resolution.Resolve(candidate);

        Assert.True(result.Succeeded);
        Assert.Equal(
            RecoveryAction.Preserve,
            result.Action);
        Assert.True(
            indexStore.Contains(entry));
    }

    [Fact]
    public void Resolution_IsIdempotentAfterSuccessfulCleanup()
    {
        Fixture fixture =
            CreateFixture("idempotent");

        RecoveryCandidate candidate =
            CreateCompletedStaleIndexCandidate(
                fixture);

        RecoveryResolutionResult first =
            fixture.Resolution.Resolve(
                candidate);

        RecoveryResolutionResult second =
            fixture.Resolution.Resolve(
                candidate);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        Assert.True(first.IndexEntryRemoved);
        Assert.False(second.IndexEntryRemoved);

        Assert.True(first.CleanupCompleted);
        Assert.True(second.CleanupCompleted);

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));
    }

    [Fact]
    public void IndexRemovalFailure_PreservesAllRecoveryEvidence()
    {
        Fixture fixture =
            CreateFixture("index-removal-failure");

        RecoveryCandidate candidate =
            CreateCompletedStaleIndexCandidate(
                fixture);

        string metadataPath =
            SessionStore.GetMetadataPath(
                fixture.Session.WorkingDirectory);

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                PathsEqual(
                    path,
                    fixture.IndexPath)
                    ? new IOException(
                        "Injected resolution index failure.")
                    : null;

        string metadataBefore =
            fixture.FileSystem.ReadAllText(
                metadataPath);
        string finalBefore =
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath);

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.False(result.Succeeded);
        Assert.Equal(
            RecoveryAction.RemoveStaleIndexAndCleanup,
            result.Action);
        Assert.False(result.IndexEntryRemoved);
        Assert.False(result.CleanupCompleted);

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.Equal(
            metadataBefore,
            fixture.FileSystem.ReadAllText(
                metadataPath));

        Assert.Equal(
            finalBefore,
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                fixture.Session.WorkingDirectory));
    }

    [Fact]
    public void CleanupFailureAfterIndexRemoval_PreservesSessionEvidence()
    {
        Fixture fixture =
            CreateFixture("cleanup-failure");

        RecoveryCandidate candidate =
            CreateCompletedStaleIndexCandidate(
                fixture);

        string metadataPath =
            SessionStore.GetMetadataPath(
                fixture.Session.WorkingDirectory);

        fixture.FileSystem.AddFile(
            Path.Combine(
                fixture.Session.WorkingDirectory,
                "diagnostic.log"),
            "keep");

        RecoveryResolutionResult result =
            fixture.Resolution.Resolve(
                candidate);

        Assert.False(result.Succeeded);
        Assert.True(result.IndexEntryRemoved);
        Assert.False(result.CleanupCompleted);

        Assert.False(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));

        Assert.True(
            fixture.FileSystem.FileExists(
                metadataPath));

        Assert.True(
            fixture.FileSystem.FileExists(
                Path.Combine(
                    fixture.Session.WorkingDirectory,
                    "diagnostic.log")));

        Assert.Equal(
            "video-data",
            fixture.FileSystem.ReadAllText(
                fixture.Session.FinalMediaPath));
    }

    private static RecoveryCandidate CreateCompletedStaleIndexCandidate(
        Fixture fixture)
    {
        AdvanceToFinalizing(
            fixture);

        Assert.True(
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session)
            .Succeeded);

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                PathsEqual(
                    path,
                    fixture.IndexPath)
                    ? new IOException(
                        "Injected stale-index creation.")
                    : null;

        Assert.Throws<IOException>(
            () =>
                fixture.Persistence.TransitionTo(
                    fixture.Session,
                    SessionStatus.Completed,
                    fixture.CompletedAt));

        fixture.FileSystem.AtomicWriteFailureFactory =
            null;

        RecoveryCandidate candidate =
            ScanSingle(fixture);

        Assert.Equal(
            RecoveryClassification.NoRecoveryRequired,
            candidate.Classification);
        Assert.Equal(
            SessionStatus.Completed,
            candidate.PersistedStatus);
        Assert.True(
            candidate.FromRecoveryIndex);

        return candidate;
    }

    private static void CompleteSessionWithoutCleanup(
        Fixture fixture)
    {
        AdvanceToFinalizing(
            fixture);

        Assert.True(
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session)
            .Succeeded);

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Completed,
            fixture.CompletedAt);

        Assert.False(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    fixture.Session)));
    }

    private static void AdvanceToFinalizing(
        Fixture fixture)
    {
        AdvanceToRecording(
            fixture);

        fixture.FileSystem.AddFile(
            fixture.Session.TempMediaPath,
            "video-data");

        fixture.Persistence.TransitionTo(
            fixture.Session,
            SessionStatus.Finalizing,
            fixture.CreatedAt.AddSeconds(3),
            StopReason.UserRequested);
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

    private static RecoveryCandidate ScanSingle(
        Fixture fixture)
    {
        return Assert.Single(
            fixture.Scanner.Scan(
                fixture.IndexPath,
                new[]
                {
                    fixture.OutputDirectory
                })
            .Candidates);
    }

    private static Fixture CreateFixture(
        string suffix)
    {
        var fileSystem =
            new FakeFileSystem();

        string root =
            CreateRoot(suffix);

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
                new OutputPlanner(fileSystem),
                persistence);

        DateTimeOffset createdAt =
            new(
                2026,
                10,
                6,
                5,
                0,
                0,
                TimeSpan.Zero);

        SessionMetadata session =
            manager.CreateSession(
                new MonitorCaptureTarget(
                    "DISPLAY-1"),
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

        var cleanup =
            new SessionCleanupPolicy(
                fileSystem,
                indexStore);

        return new Fixture(
            fileSystem,
            store,
            indexStore,
            persistence,
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator()),
            new RecoveryResolutionService(
                indexStore,
                cleanup),
            session,
            outputDirectory,
            indexPath,
            createdAt,
            createdAt.AddSeconds(4));
    }

    private static string CreateRoot(
        string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuietCapture",
            "RecoveryResolutionTests",
            suffix,
            Guid.NewGuid().ToString("N"));
    }

    private static bool PathsEqual(
        string first,
        string second)
    {
        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Fixture(
        FakeFileSystem FileSystem,
        SessionStore Store,
        RecoveryIndexStore IndexStore,
        SessionPersistenceService Persistence,
        RecoveryService Scanner,
        RecoveryResolutionService Resolution,
        SessionMetadata Session,
        string OutputDirectory,
        string IndexPath,
        DateTimeOffset CreatedAt,
        DateTimeOffset CompletedAt);
}
