namespace QuietCapture.Core.Models;

public readonly record struct VideoQualityPreset
{
    public VideoQualityPreset(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Quality preset ID must not be empty.",
                nameof(id));
        }

        Id = id;
    }

    public string Id { get; }

    public override string ToString() => Id;
}

public sealed record RecordingOptions
{
    public RecordingOptions(
        PixelSize outputSize,
        int frameRate,
        VideoQualityPreset quality,
        bool recordSystemAudio,
        bool recordMicrophone,
        string? systemAudioDeviceId,
        string? microphoneDeviceId)
    {
        if (frameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameRate),
                frameRate,
                "Target frame rate must be positive.");
        }

        if (string.IsNullOrWhiteSpace(quality.Id))
        {
            throw new ArgumentException(
                "A valid quality preset is required.",
                nameof(quality));
        }

        ValidateAudioBinding(
            recordSystemAudio,
            systemAudioDeviceId,
            nameof(systemAudioDeviceId),
            "system-audio");

        ValidateAudioBinding(
            recordMicrophone,
            microphoneDeviceId,
            nameof(microphoneDeviceId),
            "microphone");

        OutputSize = outputSize;
        FrameRate = frameRate;
        Quality = quality;
        RecordSystemAudio = recordSystemAudio;
        RecordMicrophone = recordMicrophone;
        SystemAudioDeviceId =
            NormalizeDisabledDeviceId(
                recordSystemAudio,
                systemAudioDeviceId);
        MicrophoneDeviceId =
            NormalizeDisabledDeviceId(
                recordMicrophone,
                microphoneDeviceId);
    }

    public PixelSize OutputSize { get; }

    public int FrameRate { get; }

    public VideoQualityPreset Quality { get; }

    public bool RecordSystemAudio { get; }

    public bool RecordMicrophone { get; }

    public string? SystemAudioDeviceId { get; }

    public string? MicrophoneDeviceId { get; }

    private static void ValidateAudioBinding(
        bool enabled,
        string? deviceId,
        string parameterName,
        string sourceName)
    {
        if (enabled &&
            string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException(
                $"A concrete {sourceName} device ID is required when that source is enabled.",
                parameterName);
        }
    }

    private static string? NormalizeDisabledDeviceId(
        bool enabled,
        string? deviceId)
    {
        return enabled
            ? deviceId
            : null;
    }
}
