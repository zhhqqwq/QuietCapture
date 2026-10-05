using QuietCapture.Core.Models;
using QuietCapture.Core.Recovery;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class RecoveryServiceTests
{
    [Fact]
    public void CreatedResidual_IsClassifiedInterrupted()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("created");

        SeededSession seeded =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.Created);

        RecoveryCandidate candidate =
            Assert.Single(
                Scan(
                    fileSystem,
                    outputDirectory)
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);
        Assert.Equal(
            SessionStatus.Created,
            candidate.PersistedStatus);
        Assert.Equal(
            seeded.Session.Id,
            candidate.SessionId);
    }

    [Fact]
    public void RecordingResidual_IsClassifiedInterrupted()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("recording");

        SeedSession(
            fileSystem,
            outputDirectory,
            SessionStatus.Recording,
            partialContent: "video-data");

        RecoveryCandidate candidate =
            Assert.Single(
                Scan(
                    fileSystem,
                    outputDirectory)
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);
        Assert.Equal(
            SessionStatus.Recording,
            candidate.PersistedStatus);
        Assert.True(
            candidate.HasNonEmptyPartialMedia);
    }

    [Fact]
    public void FinalizingResidual_IsClassifiedInterrupted()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("finalizing");

        SeedSession(
            fileSystem,
            outputDirectory,
            SessionStatus.Finalizing,
            partialContent: "video-data");

        RecoveryCandidate candidate =
            Assert.Single(
                Scan(
                    fileSystem,
                    outputDirectory)
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);
        Assert.Equal(
            SessionStatus.Finalizing,
            candidate.PersistedStatus);
        Assert.True(
            candidate.HasNonEmptyPartialMedia);
    }

    [Fact]
    public void StopFailed_IsPreservedAndClassifiedStopFailed()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("stop-failed");

        SeededSession seeded =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.StopFailed,
                partialContent: "keep-this-media");

        long beforeBytes =
            fileSystem.GetFileLength(
                seeded.Plan.TempMediaPath);

        RecoveryCandidate candidate =
            Assert.Single(
                Scan(
                    fileSystem,
                    outputDirectory)
                .Candidates);

        Assert.Equal(
            RecoveryClassification.StopFailed,
            candidate.Classification);
        Assert.True(
            candidate.HasNonEmptyPartialMedia);
        Assert.True(
            fileSystem.FileExists(
                seeded.Plan.TempMediaPath));
        Assert.Equal(
            beforeBytes,
            fileSystem.GetFileLength(
                seeded.Plan.TempMediaPath));
    }

    [Fact]
    public void MissingSessionJson_IsOrphaned()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("missing-metadata");

        SeededSession seeded =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.Recording,
                partialContent: "video-data",
                writeMetadata: false);

        RecoveryCandidate candidate =
            Assert.Single(
                Scan(
                    fileSystem,
                    outputDirectory)
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Orphaned,
            candidate.Classification);
        Assert.Null(
            candidate.PersistedStatus);
        Assert.Contains(
            "session.json is missing",
            candidate.Diagnostic,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(
            fileSystem.FileExists(
                seeded.Plan.TempMediaPath));
    }

    [Fact]
    public void CorruptMetadata_IsOrphaned()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("corrupt-metadata");

        SeededSession seeded =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.Recording,
                partialContent: "video-data",
                corruptMetadata: true);

        RecoveryCandidate candidate =
            Assert.Single(
                Scan(
                    fileSystem,
                    outputDirectory)
                .Candidates);

        Assert.Equal(
            RecoveryClassification.Orphaned,
            candidate.Classification);
        Assert.Null(
            candidate.Metadata);
        Assert.Contains(
            "unreadable",
            candidate.Diagnostic,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(
            fileSystem.FileExists(
                seeded.Plan.TempMediaPath));
    }

    [Fact]
    public void Scan_NeverDeletesNonEmptyPartialMedia()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("preserve-partial");

        SeededSession seeded =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.Created,
                partialContent: "partial-must-remain");

        string[] filesBefore =
            fileSystem.FilePaths
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        long bytesBefore =
            fileSystem.GetFileLength(
                seeded.Plan.TempMediaPath);

        _ = Scan(
            fileSystem,
            outputDirectory);

        string[] filesAfter =
            fileSystem.FilePaths
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        Assert.Equal(
            filesBefore,
            filesAfter);
        Assert.Equal(
            bytesBefore,
            fileSystem.GetFileLength(
                seeded.Plan.TempMediaPath));
    }

    [Fact]
    public void RecoveryIndexAndDirectoryScan_AreMergedByWorkingDirectory()
    {
        var fileSystem = new FakeFileSystem();
        string outputDirectory =
            CreateOutputDirectory("merge");

        SeededSession indexed =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.Recording,
                partialContent: "indexed");

        SeededSession scanOnly =
            SeedSession(
                fileSystem,
                outputDirectory,
                SessionStatus.Finalizing,
                partialContent: "scan-only",
                collisionIndex: 1);

        string recoveryIndexPath =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "recovery-index-merge.json");

        var index =
            new RecoveryIndex(
                new[]
                {
                    RecoveryIndexEntry.FromSession(
                        indexed.Session)
                });

        fileSystem.AddFile(
            recoveryIndexPath,
            RecoveryIndexJson.Serialize(
                index));

        RecoveryScanResult result =
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator())
            .Scan(
                recoveryIndexPath,
                new[]
                {
                    outputDirectory
                });

        Assert.Equal(
            2,
            result.Candidates.Count);

        RecoveryCandidate indexedCandidate =
            Assert.Single(
                result.Candidates.Where(candidate =>
                    candidate.SessionId ==
                    indexed.Session.Id));

        Assert.True(
            indexedCandidate.FromRecoveryIndex);
        Assert.True(
            indexedCandidate.FromDirectoryScan);

        RecoveryCandidate scanCandidate =
            Assert.Single(
                result.Candidates.Where(candidate =>
                    candidate.SessionId ==
                    scanOnly.Session.Id));

        Assert.False(
            scanCandidate.FromRecoveryIndex);
        Assert.True(
            scanCandidate.FromDirectoryScan);
    }

    private static RecoveryScanResult Scan(
        FakeFileSystem fileSystem,
        string outputDirectory)
    {
        string recoveryIndexPath =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                Guid.NewGuid().ToString("N"),
                "recovery-index.json");

        return new RecoveryService(
            fileSystem,
            new SessionDocumentValidator())
            .Scan(
                recoveryIndexPath,
                new[]
                {
                    outputDirectory
                });
    }

    private static SeededSession SeedSession(
        FakeFileSystem fileSystem,
        string outputDirectory,
        SessionStatus status,
        string? partialContent = null,
        bool writeMetadata = true,
        bool corruptMetadata = false,
        int collisionIndex = 0)
    {
        DateTimeOffset createdAt =
            new(
                2026,
                10,
                5,
                1,
                2,
                3,
                TimeSpan.Zero);

        SessionId sessionId =
            SessionId.New();

        OutputPlan plan =
            OutputPlan.Create(
                outputDirectory,
                sessionId,
                OutputFileNamePolicy.CreateFileName(
                    createdAt,
                    collisionIndex));

        fileSystem.AddDirectory(
            plan.WorkingDirectory);

        SessionMetadata session =
            SessionMetadata.Create(
                sessionId,
                new AreaCaptureTarget(
                    "DISPLAY-1",
                    new PixelRect(
                        -1000,
                        100,
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
                plan.WorkingDirectory,
                plan.TempMediaPath,
                plan.FinalMediaPath,
                createdAt);

        AdvanceSession(
            session,
            status,
            createdAt);

        if (writeMetadata)
        {
            fileSystem.AddFile(
                plan.SessionMetadataPath,
                corruptMetadata
                    ? "{ this is not valid json"
                    : SessionMetadataJson.Serialize(
                        session));
        }

        if (partialContent is not null)
        {
            fileSystem.AddFile(
                plan.TempMediaPath,
                partialContent);
        }

        return new SeededSession(
            session,
            plan);
    }

    private static void AdvanceSession(
        SessionMetadata session,
        SessionStatus targetStatus,
        DateTimeOffset createdAt)
    {
        if (targetStatus ==
            SessionStatus.Created)
        {
            return;
        }

        session.TransitionTo(
            SessionStatus.Starting,
            createdAt.AddSeconds(1));

        if (targetStatus ==
            SessionStatus.Starting)
        {
            return;
        }

        if (targetStatus ==
            SessionStatus.FailedToStart)
        {
            session.TransitionTo(
                SessionStatus.FailedToStart,
                createdAt.AddSeconds(2));
            return;
        }

        session.TransitionTo(
            SessionStatus.Recording,
            createdAt.AddSeconds(2));

        if (targetStatus ==
            SessionStatus.Recording)
        {
            return;
        }

        session.TransitionTo(
            SessionStatus.Finalizing,
            createdAt.AddSeconds(3),
            StopReason.UserRequested);

        if (targetStatus ==
            SessionStatus.Finalizing)
        {
            return;
        }

        session.TransitionTo(
            targetStatus,
            createdAt.AddSeconds(4),
            StopReason.UserRequested);
    }

    private static string CreateOutputDirectory(
        string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuietCapture",
            "RecoveryTests",
            suffix,
            Guid.NewGuid().ToString("N"));
    }

    private sealed record SeededSession(
        SessionMetadata Session,
        OutputPlan Plan);
}
