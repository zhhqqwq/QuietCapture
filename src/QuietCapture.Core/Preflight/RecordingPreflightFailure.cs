namespace QuietCapture.Core.Preflight;

public enum RecordingPreflightFailure
{
    MissingOutputDirectory,
    OutputVolumeUnavailable,
    OutputVolumeNotWritable,
    Fat32OutputNotSupported,
    DefaultSystemAudioDeviceUnavailable,
    DefaultMicrophoneDeviceUnavailable,
    SpecificSystemAudioDeviceUnavailable,
    SpecificMicrophoneDeviceUnavailable
}
