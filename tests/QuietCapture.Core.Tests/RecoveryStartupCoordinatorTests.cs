using QuietCapture.Core.Models;
using QuietCapture.Core.Recovery;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class RecoveryStartupCoordinatorTests
{
    [Fact]
    public void EmptyStartup_ReturnsNoRecoveryWork()
    {
        Fixture fixture =
            CreateFixture("empty");

        RecoveryStartupResult result =
            fixture.Coordinator.Run(
                fixture.IndexPath,
                new[]
                {
                    fixture.OutputDirectory
                });

        Assert.Empty(
            result.RemainingCandidates);
        Assert.Empty(
            result.Resolutions);
        Assert.Empty(
            result.Diagnostics);
        Assert.False(
            result.HasOutstandingRecovery);
    }

    [Fact]
    public void CompletedSession_IsAutomaticallyResolvedAtStartup()
    {
        Fixture fixture =
            CreateFixture("completed");

        SessionMetadata session =
            CreateCompletedSession(
                fixture,
                slot: 1);

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                session.WorkingDirectory));

        RecoveryStartupResult result =
            fixture.Coordinator.Run(
                fixture.IndexPath,
                new[]
                {
                    fixture.OutputDirectory
                });

        Assert.Empty(
            result.RemainingCandidates);

        RecoveryStartupResolution resolution =
            Assert.Single(
                result.Resolutions);

        Assert.Equal(
            RecoveryClassification.NoRecoveryRequired,
            resolution.Candidate.Classification);
        Assert.True(
            resolution.Resolution.Succeeded);
        Assert.True(
            resolution.Resolution.CleanupCompleted);

        Assert.False(
            fixture.FileSystem.DirectoryExists(
                session.WorkingDirectory));

        Assert.True(
            fixture.FileSystem.FileExists(
                session.FinalMediaPath));
    }

    [Fact]
    public void MixedCandidateSet_AutoResolvesOnlyCompleted()
    {
        Fixture fixture =
            CreateFixture("mixed");

        SessionMetadata completed =
            CreateCompletedSession(
                fixture,
                slot: 1);

        SessionMetadata interrupted =
            CreateInterruptedSession(
                fixture,
                slot: 2);

        SessionMetadata stopFailed =
            CreateStopFailedSession(
                fixture,
                slot: 3);

        SessionMetadata orphaned =
            CreateOrphanedSession(
                fixture,
                slot: 4);

        RecoveryStartupResult result =
            fixture.Coordinator.Run(
                fixture.IndexPath,
                new[]
                {
                    fixture.OutputDirectory,
                    fixture.OutputDirectory +
                        Path.DirectorySeparatorChar,
                    Path.Combine(
                        fixture.OutputDirectory,
                        ".")
                });

        Assert.Equal(
            4,
            result.Resolutions.Count);

        Assert.Equal(
            3,
            result.RemainingCandidates.Count);

        Assert.Equal(
            new[]
            {
                RecoveryClassification.Interrupted,
                RecoveryClassification.StopFailed,
                RecoveryClassification.Orphaned
            },
            result.RemainingCandidates
                .Select(candidate =>
                    candidate.Classification)
                .OrderBy(value => value)
                .ToArray());

        Assert.False(
            fixture.FileSystem.DirectoryExists(
                completed.WorkingDirectory));

        Assert.True(
            fixture.FileSystem.DirectoryExists(
                interrupted.WorkingDirectory));
        Assert.True(
            fixture.FileSystem.DirectoryExists(
                stopFailed.WorkingDirectory));
        Assert.True(
            fixture.FileSystem.DirectoryExists(
                orphaned.WorkingDirectory));

        Assert.True(
            result.HasOutstandingRecovery);
    }

    [Fact]
    public void Interrupted_IsPreservedAtStartup()
    {
        Fixture fixture =
            CreateFixture("interrupted");

        SessionMetadata session =
            CreateInterruptedSession(
                fixture,
                slot: 1);

        string partialBefore =
            fixture.FileSystem.ReadAllText(
                session.TempMediaPath);

        RecoveryStartupResult result =
            RunStartup(fixture);

        RecoveryCandidate candidate =
            Assert.Single(
                result.RemainingCandidates);

        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);

        Assert.Equal(
            partialBefore,
            fixture.FileSystem.ReadAllText(
                session.TempMediaPath));

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    session)));
    }

    [Fact]
    public void StopFailed_IsPreservedAtStartup()
    {
        Fixture fixture =
            CreateFixture("stop-failed");

        SessionMetadata session =
            CreateStopFailedSession(
                fixture,
                slot: 1);

        RecoveryStartupResult result =
            RunStartup(fixture);

        RecoveryCandidate candidate =
            Assert.Single(
                result.RemainingCandidates);

        Assert.Equal(
            RecoveryClassification.StopFailed,
            candidate.Classification);

        Assert.Equal(
            "partial-stop-failed",
            fixture.FileSystem.ReadAllText(
                session.TempMediaPath));

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    session)));
    }

    [Fact]
    public void Orphaned_IsPreservedAtStartup()
    {
        Fixture fixture =
            CreateFixture("orphaned");

        SessionMetadata session =
            CreateOrphanedSession(
                fixture,
                slot: 1);

        RecoveryStartupResult result =
            RunStartup(fixture);

        RecoveryCandidate candidate =
            Assert.Single(
                result.RemainingCandidates);

        Assert.Equal(
            RecoveryClassification.Orphaned,
            candidate.Classification);

        Assert.True(
            fixture.FileSystem.FileExists(
                SessionStore.GetMetadataPath(
                    session.WorkingDirectory)));

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    session)));
    }

    [Fact]
    public void CorruptRecoveryIndex_IsSurfacedAsDiagnostic()
    {
        Fixture fixture =
            CreateFixture("corrupt-index");

        fixture.FileSystem.AddFile(
            fixture.IndexPath,
            "{ definitely not valid json");

        RecoveryStartupResult result =
            fixture.Coordinator.Run(
                fixture.IndexPath,
                Array.Empty<string>());

        Assert.Empty(
            result.RemainingCandidates);
        Assert.Empty(
            result.Resolutions);

        string diagnostic =
            Assert.Single(
                result.Diagnostics);

        Assert.Contains(
            "Recovery index could not be read",
            diagnostic,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolutionFailure_IsSurfacedAndCandidateRemains()
    {
        Fixture fixture =
            CreateFixture("resolution-failure");

        SessionMetadata session =
            CreateCompletedSession(
                fixture,
                slot: 1);

        string metadataPath =
            SessionStore.GetMetadataPath(
                session.WorkingDirectory);

        fixture.FileSystem.DeleteFileFailureFactory =
            path =>
                PathsEqual(
                    path,
                    metadataPath)
                    ? new IOException(
                        "Injected startup cleanup failure.")
                    : null;

        RecoveryStartupResult result =
            RunStartup(fixture);

        RecoveryCandidate candidate =
            Assert.Single(
                result.RemainingCandidates);

        RecoveryStartupResolution resolution =
            Assert.Single(
                result.Resolutions);

        Assert.Equal(
            candidate,
            resolution.Candidate);

        Assert.False(
            resolution.Resolution.Succeeded);
        Assert.Equal(
            RecoveryAction.CleanupNoRecoveryRequired,
            resolution.Resolution.Action);

        Assert.True(
            result.HasOutstandingRecovery);

        Assert.True(
            fixture.FileSystem.FileExists(
                metadataPath));

        Assert.True(
            fixture.FileSystem.FileExists(
                session.FinalMediaPath));
    }

    [Fact]
    public void RepeatedStartup_IsIdempotentAfterAutomaticCleanup()
    {
        Fixture fixture =
            CreateFixture("repeat-startup");

        SessionMetadata session =
            CreateCompletedSession(
                fixture,
                slot: 1);

        RecoveryStartupResult first =
            RunStartup(fixture);

        RecoveryStartupResult second =
            RunStartup(fixture);

        Assert.Empty(
            first.RemainingCandidates);
        Assert.Single(
            first.Resolutions);

        Assert.Empty(
            second.RemainingCandidates);
        Assert.Empty(
            second.Resolutions);
        Assert.Empty(
            second.Diagnostics);
        Assert.False(
            second.HasOutstandingRecovery);

        Assert.True(
            fixture.FileSystem.FileExists(
                session.FinalMediaPath));

        Assert.False(
            fixture.FileSystem.DirectoryExists(
                session.WorkingDirectory));
    }

    private static RecoveryStartupResult RunStartup(
        Fixture fixture)
    {
        return fixture.Coordinator.Run(
            fixture.IndexPath,
            new[]
            {
                fixture.OutputDirectory
            });
    }

    private static SessionMetadata CreateCompletedSession(
        Fixture fixture,
        int slot)
    {
        SessionMetadata session =
            CreateSession(
                fixture,
                slot);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            Timestamp(slot, 1));

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            Timestamp(slot, 2));

        fixture.FileSystem.AddFile(
            session.TempMediaPath,
            "video-data");

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Finalizing,
            Timestamp(slot, 3),
            StopReason.UserRequested);

        Assert.True(
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(session)
            .Succeeded);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Completed,
            Timestamp(slot, 4));

        Assert.False(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    session)));

        return session;
    }

    private static SessionMetadata CreateInterruptedSession(
        Fixture fixture,
        int slot)
    {
        SessionMetadata session =
            CreateSession(
                fixture,
                slot);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            Timestamp(slot, 1));

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            Timestamp(slot, 2));

        fixture.FileSystem.AddFile(
            session.TempMediaPath,
            "partial-interrupted");

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Interrupted,
            Timestamp(slot, 3),
            StopReason.SystemSuspend);

        return session;
    }

    private static SessionMetadata CreateStopFailedSession(
        Fixture fixture,
        int slot)
    {
        SessionMetadata session =
            CreateSession(
                fixture,
                slot);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Starting,
            Timestamp(slot, 1));

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Recording,
            Timestamp(slot, 2));

        fixture.FileSystem.AddFile(
            session.TempMediaPath,
            "partial-stop-failed");

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.StopFailed,
            Timestamp(slot, 3),
            StopReason.BackendFailure);

        return session;
    }

    private static SessionMetadata CreateOrphanedSession(
        Fixture fixture,
        int slot)
    {
        SessionMetadata session =
            CreateSession(
                fixture,
                slot);

        fixture.Persistence.TransitionTo(
            session,
            SessionStatus.Orphaned,
            Timestamp(slot, 1));

        return session;
    }

    private static SessionMetadata CreateSession(
        Fixture fixture,
        int slot)
    {
        return fixture.Manager.CreateSession(
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
            fixture.OutputDirectory,
            Timestamp(slot, 0));
    }

    private static DateTimeOffset Timestamp(
        int slot,
        int seconds)
    {
        return new DateTimeOffset(
                2026,
                10,
                6,
                6,
                slot,
                0,
                TimeSpan.Zero)
            .AddSeconds(seconds);
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
                "RecoveryStartupTests",
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
            new SessionStore(
                fileSystem);

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
                new OutputPlanner(fileSystem),
                persistence);

        var scanner =
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator());

        var cleanup =
            new SessionCleanupPolicy(
                fileSystem,
                indexStore);

        var resolution =
            new RecoveryResolutionService(
                indexStore,
                cleanup);

        return new Fixture(
            fileSystem,
            indexStore,
            persistence,
            manager,
            new RecoveryStartupCoordinator(
                scanner,
                resolution),
            outputDirectory,
            indexPath);
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
        RecoveryIndexStore IndexStore,
        SessionPersistenceService Persistence,
        SessionManager Manager,
        RecoveryStartupCoordinator Coordinator,
        string OutputDirectory,
        string IndexPath);
}
