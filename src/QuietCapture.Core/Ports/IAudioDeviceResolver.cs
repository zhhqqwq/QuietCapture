namespace QuietCapture.Core.Ports;

public interface IAudioDeviceResolver
{
    string? ResolveDefaultSystemAudioDeviceId();

    string? ResolveDefaultMicrophoneDeviceId();

    bool IsSystemAudioDeviceAvailable(
        string deviceId);

    bool IsMicrophoneDeviceAvailable(
        string deviceId);
}
