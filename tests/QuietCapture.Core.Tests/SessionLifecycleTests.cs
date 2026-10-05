using QuietCapture.Core.Models;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Tests;

public sealed class SessionLifecycleTests
{
    [Fact]
    public void NormalLifecycle_RecordsStartAndFinishTimes()
    {
        DateTimeOffset created =
            new(
                2026,
                10,
                5,
                1,
                0,
                0,
                TimeSpan.Zero);
        DateTimeOffset started =
            created.AddSeconds(2);
        DateTimeOffset finished =
            created.AddMinutes(1);

        SessionMetadata session =
            CreateSession(created);

        session.TransitionTo(
            SessionStatus.Starting,
            created.AddSeconds(1));
        session.TransitionTo(
            SessionStatus.Recording,
            started);
        session.TransitionTo(
            SessionStatus.Finalizing,
            finished.AddSeconds(-1),
            StopReason.UserRequested);
        session.TransitionTo(
            SessionStatus.Completed,
            finished);

        Assert.Equal(
            SessionStatus.Completed,
            session.Status);
        Assert.Equal(
            started,
            session.StartedAt);
        Assert.Equal(
            finished,
            session.FinishedAt);
        Assert.Equal(
            StopReason.UserRequested,
            session.StopReason);
    }

    [Fact]
    public void TerminalSession_CannotReturnToRecording()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;
        SessionMetadata session =
            CreateSession(now);

        session.TransitionTo(
            SessionStatus.Starting,
            now.AddSeconds(1));
        session.TransitionTo(
            SessionStatus.FailedToStart,
            now.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(
            () =>
                session.TransitionTo(
                    SessionStatus.Recording,
                    now.AddSeconds(3)));
    }

    [Fact]
    public void StopFailed_IsTerminalAndPreservesFirstStopReason()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;
        SessionMetadata session =
            CreateSession(now);

        session.TransitionTo(
            SessionStatus.Starting,
            now.AddSeconds(1));
        session.TransitionTo(
            SessionStatus.Recording,
            now.AddSeconds(2));
        session.TransitionTo(
            SessionStatus.Finalizing,
            now.AddSeconds(3),
            StopReason.DiskSpaceLow);
        session.TransitionTo(
            SessionStatus.StopFailed,
            now.AddSeconds(4),
            StopReason.BackendFailure);

        Assert.Equal(
            SessionStatus.StopFailed,
            session.Status);
        Assert.Equal(
            StopReason.DiskSpaceLow,
            session.StopReason);
        Assert.True(
            SessionLifecycle.IsTerminal(
                session.Status));
    }

    private static SessionMetadata CreateSession(
        DateTimeOffset createdAt)
    {
        var target =
            new AreaCaptureTarget(
                "DISPLAY-1",
                new PixelRect(
                    0,
                    0,
                    1280,
                    720));

        var options =
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
                microphoneDeviceId: null);

        return SessionMetadata.Create(
            SessionId.New(),
            target,
            options,
            @"D:\Recordings\.screenrecorder\sessions\test",
            @"D:\Recordings\.screenrecorder\sessions\test\recording.partial.mp4",
            @"D:\Recordings\final.mp4",
            createdAt);
    }
}
