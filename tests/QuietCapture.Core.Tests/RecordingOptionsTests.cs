using QuietCapture.Core.Models;

namespace QuietCapture.Core.Tests;

public sealed class RecordingOptionsTests
{
    [Fact]
    public void EnabledAudioSource_RequiresConcreteDeviceId()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new RecordingOptions(
                    new PixelSize(
                        1280,
                        720),
                    30,
                    new VideoQualityPreset(
                        "balanced"),
                    recordSystemAudio: true,
                    recordMicrophone: false,
                    systemAudioDeviceId: null,
                    microphoneDeviceId: null));
    }

    [Fact]
    public void DisabledAudioSource_DropsUnusedDeviceId()
    {
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
                systemAudioDeviceId: "unused-output",
                microphoneDeviceId: "unused-mic");

        Assert.Null(
            options.SystemAudioDeviceId);
        Assert.Null(
            options.MicrophoneDeviceId);
    }
}
