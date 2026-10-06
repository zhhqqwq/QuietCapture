using QuietCapture.Core.Models;
using QuietCapture.Core.Settings;

namespace QuietCapture.Core.Preflight;

public sealed record RecordingIntent
{
    public RecordingIntent(
        CaptureTarget target,
        PixelSize outputSize,
        string? outputDirectory,
        int targetFrameRate,
        string qualityPresetId,
        bool recordSystemAudio,
        bool recordMicrophone,
        AudioDevicePreference systemAudioDevicePreference,
        AudioDevicePreference microphoneDevicePreference)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(
            systemAudioDevicePreference);
        ArgumentNullException.ThrowIfNull(
            microphoneDevicePreference);

        if (targetFrameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFrameRate),
                targetFrameRate,
                "Target frame rate must be positive.");
        }

        if (string.IsNullOrWhiteSpace(
                qualityPresetId))
        {
            throw new ArgumentException(
                "Quality preset ID must not be empty.",
                nameof(qualityPresetId));
        }

        Target = target;
        OutputSize = outputSize;
        OutputDirectory =
            string.IsNullOrWhiteSpace(
                outputDirectory)
                ? null
                : Path.GetFullPath(
                    outputDirectory);
        TargetFrameRate = targetFrameRate;
        QualityPresetId = qualityPresetId;
        RecordSystemAudio = recordSystemAudio;
        RecordMicrophone = recordMicrophone;
        SystemAudioDevicePreference =
            systemAudioDevicePreference;
        MicrophoneDevicePreference =
            microphoneDevicePreference;
    }

    public CaptureTarget Target { get; }

    public PixelSize OutputSize { get; }

    public string? OutputDirectory { get; }

    public int TargetFrameRate { get; }

    public string QualityPresetId { get; }

    public bool RecordSystemAudio { get; }

    public bool RecordMicrophone { get; }

    public AudioDevicePreference
        SystemAudioDevicePreference { get; }

    public AudioDevicePreference
        MicrophoneDevicePreference { get; }

    public static RecordingIntent FromSettings(
        CaptureTarget target,
        PixelSize outputSize,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new RecordingIntent(
            target,
            outputSize,
            settings.OutputDirectories
                .CurrentOutputDirectory,
            settings.TargetFrameRate,
            settings.QualityPresetId,
            settings.RecordSystemAudioByDefault,
            settings.RecordMicrophoneByDefault,
            settings.SystemAudioDevicePreference,
            settings.MicrophoneDevicePreference);
    }
}
