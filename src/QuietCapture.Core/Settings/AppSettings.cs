namespace QuietCapture.Core.Settings;

public sealed record AppSettings
{
    public const int DefaultTargetFrameRate = 30;
    public const string DefaultQualityPresetId =
        "balanced";

    public AppSettings(
        OutputDirectoryHistory outputDirectories,
        int targetFrameRate,
        string qualityPresetId,
        bool recordSystemAudioByDefault,
        bool recordMicrophoneByDefault,
        AudioDevicePreference systemAudioDevicePreference,
        AudioDevicePreference microphoneDevicePreference)
    {
        ArgumentNullException.ThrowIfNull(
            outputDirectories);
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

        OutputDirectories = outputDirectories;
        TargetFrameRate = targetFrameRate;
        QualityPresetId = qualityPresetId;
        RecordSystemAudioByDefault =
            recordSystemAudioByDefault;
        RecordMicrophoneByDefault =
            recordMicrophoneByDefault;
        SystemAudioDevicePreference =
            systemAudioDevicePreference;
        MicrophoneDevicePreference =
            microphoneDevicePreference;
    }

    public OutputDirectoryHistory OutputDirectories
        { get; }

    public int TargetFrameRate { get; }

    public string QualityPresetId { get; }

    public bool RecordSystemAudioByDefault { get; }

    public bool RecordMicrophoneByDefault { get; }

    public AudioDevicePreference
        SystemAudioDevicePreference { get; }

    public AudioDevicePreference
        MicrophoneDevicePreference { get; }

    public static AppSettings CreateDefault()
    {
        return new AppSettings(
            OutputDirectoryHistory.Empty,
            DefaultTargetFrameRate,
            DefaultQualityPresetId,
            recordSystemAudioByDefault: false,
            recordMicrophoneByDefault: false,
            AudioDevicePreference.Default,
            AudioDevicePreference.Default);
    }

    public AppSettings WithOutputDirectories(
        OutputDirectoryHistory outputDirectories)
    {
        return new AppSettings(
            outputDirectories,
            TargetFrameRate,
            QualityPresetId,
            RecordSystemAudioByDefault,
            RecordMicrophoneByDefault,
            SystemAudioDevicePreference,
            MicrophoneDevicePreference);
    }
}
