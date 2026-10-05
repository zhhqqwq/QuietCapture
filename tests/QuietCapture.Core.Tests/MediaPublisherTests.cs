using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class MediaPublisherTests
{
    [Fact]
    public void Publish_MovesPartialIntoReservedFinalPath()
    {
        Fixture fixture =
            CreateFinalizingFixture(
                "publish-success",
                partialContent: "video-bytes");

        MediaPublishResult result =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.True(result.Succeeded);
        Assert.Equal(
            MediaPublishOutcome.Published,
            result.Outcome);
        Assert.False(
            fixture.FileSystem.FileExists(
                fixture.Plan.TempMediaPath));
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Plan.FinalMediaPath));
        Assert.Equal(
            "video-bytes".Length,
            fixture.FileSystem.GetFileLength(
                fixture.Plan.FinalMediaPath));
    }

    [Fact]
    public void Publish_RejectsNonEmptyFinalReservationWithoutOverwrite()
    {
        Fixture fixture =
            CreateFinalizingFixture(
                "non-empty-final",
                partialContent: "partial-content");

        fixture.FileSystem.AddFile(
            fixture.Plan.FinalMediaPath,
            "existing-final");

        MediaPublishResult result =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.Equal(
            MediaPublishOutcome.FinalReservationNotEmpty,
            result.Outcome);
        Assert.Equal(
            "existing-final",
            fixture.FileSystem.ReadAllText(
                fixture.Plan.FinalMediaPath));
        Assert.Equal(
            "partial-content",
            fixture.FileSystem.ReadAllText(
                fixture.Plan.TempMediaPath));
    }

    [Fact]
    public void Publish_MissingPartial_IsReported()
    {
        Fixture fixture =
            CreateFinalizingFixture(
                "missing-partial",
                partialContent: null);

        MediaPublishResult result =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.Equal(
            MediaPublishOutcome.MissingPartial,
            result.Outcome);
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Plan.FinalMediaPath));
        Assert.Equal(
            0,
            fixture.FileSystem.GetFileLength(
                fixture.Plan.FinalMediaPath));
    }

    [Fact]
    public void Publish_ZeroBytePartial_IsReported()
    {
        Fixture fixture =
            CreateFinalizingFixture(
                "zero-partial",
                partialContent: string.Empty);

        MediaPublishResult result =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.Equal(
            MediaPublishOutcome.EmptyPartial,
            result.Outcome);
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Plan.TempMediaPath));
        Assert.Equal(
            0,
            fixture.FileSystem.GetFileLength(
                fixture.Plan.TempMediaPath));
    }

    [Fact]
    public void Publish_MoveFailure_PreservesPartialAndReservation()
    {
        Fixture fixture =
            CreateFinalizingFixture(
                "move-failure",
                partialContent: "keep-partial");

        fixture.FileSystem.MoveFileFailureFactory =
            (_, _) =>
                new IOException(
                    "Injected media move failure.");

        MediaPublishResult result =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.Equal(
            MediaPublishOutcome.Failed,
            result.Outcome);
        Assert.Equal(
            "keep-partial",
            fixture.FileSystem.ReadAllText(
                fixture.Plan.TempMediaPath));
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Plan.FinalMediaPath));
        Assert.Equal(
            0,
            fixture.FileSystem.GetFileLength(
                fixture.Plan.FinalMediaPath));
    }

    [Fact]
    public void Publish_MissingFinalReservation_IsReported()
    {
        Fixture fixture =
            CreateFinalizingFixture(
                "missing-reservation",
                partialContent: "partial");

        fixture.FileSystem.DeleteFile(
            fixture.Plan.FinalMediaPath);

        MediaPublishResult result =
            new MediaPublisher(
                fixture.FileSystem)
            .Publish(
                fixture.Session);

        Assert.Equal(
            MediaPublishOutcome.MissingFinalReservation,
            result.Outcome);
        Assert.True(
            fixture.FileSystem.FileExists(
                fixture.Plan.TempMediaPath));
    }

    private static Fixture CreateFinalizingFixture(
        string suffix,
        string? partialContent)
    {
        var fileSystem =
            new FakeFileSystem();

        string outputDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "MediaPublisherTests",
                suffix,
                Guid.NewGuid().ToString("N"));

        DateTimeOffset createdAt =
            new(
                2026,
                10,
                6,
                2,
                0,
                0,
                TimeSpan.Zero);

        SessionId sessionId =
            SessionId.New();

        OutputPlan plan =
            OutputPlan.Create(
                outputDirectory,
                sessionId,
                OutputFileNamePolicy.CreateFileName(
                    createdAt));

        fileSystem.AddDirectory(
            plan.WorkingDirectory);

        Assert.True(
            fileSystem.TryReserveFile(
                plan.FinalMediaPath));

        if (partialContent is not null)
        {
            fileSystem.AddFile(
                plan.TempMediaPath,
                partialContent);
        }

        SessionMetadata session =
            SessionMetadata.Create(
                sessionId,
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
                plan.WorkingDirectory,
                plan.TempMediaPath,
                plan.FinalMediaPath,
                createdAt);

        session.TransitionTo(
            SessionStatus.Starting,
            createdAt.AddSeconds(1));
        session.TransitionTo(
            SessionStatus.Recording,
            createdAt.AddSeconds(2));
        session.TransitionTo(
            SessionStatus.Finalizing,
            createdAt.AddSeconds(3),
            StopReason.UserRequested);

        return new Fixture(
            fileSystem,
            plan,
            session);
    }

    private sealed record Fixture(
        FakeFileSystem FileSystem,
        OutputPlan Plan,
        SessionMetadata Session);
}
