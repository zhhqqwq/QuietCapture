namespace QuietCapture.Core.Settings;

public enum AudioDevicePreferenceKind
{
    Default,
    Specific
}

public sealed record AudioDevicePreference
{
    private AudioDevicePreference(
        AudioDevicePreferenceKind kind,
        string? preferredDeviceId)
    {
        if (kind ==
                AudioDevicePreferenceKind.Specific &&
            string.IsNullOrWhiteSpace(
                preferredDeviceId))
        {
            throw new ArgumentException(
                "A specific audio-device preference requires a device ID.",
                nameof(preferredDeviceId));
        }

        Kind = kind;
        PreferredDeviceId =
            kind ==
                AudioDevicePreferenceKind.Specific
                ? preferredDeviceId
                : null;
    }

    public AudioDevicePreferenceKind Kind { get; }

    public string? PreferredDeviceId { get; }

    public bool UsesDefaultDevice =>
        Kind == AudioDevicePreferenceKind.Default;

    public static AudioDevicePreference Default { get; } =
        new(
            AudioDevicePreferenceKind.Default,
            preferredDeviceId: null);

    public static AudioDevicePreference Specific(
        string deviceId)
    {
        return new AudioDevicePreference(
            AudioDevicePreferenceKind.Specific,
            deviceId);
    }
}
