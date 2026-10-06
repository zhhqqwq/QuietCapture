using QuietCapture.Core.Ports;

namespace QuietCapture.Infrastructure.Windows.Audio;

public sealed class WindowsAudioDeviceResolver
    : IAudioDeviceResolver
{
    private readonly AudioEndpointEnumerator
        _enumerator;

    public WindowsAudioDeviceResolver()
        : this(
            new AudioEndpointEnumerator())
    {
    }

    public WindowsAudioDeviceResolver(
        AudioEndpointEnumerator enumerator)
    {
        _enumerator =
            enumerator ??
            throw new ArgumentNullException(
                nameof(enumerator));
    }

    public string? ResolveDefaultSystemAudioDeviceId()
    {
        return _enumerator
            .GetDefaultRenderEndpoint()
            ?.Id;
    }

    public string? ResolveDefaultMicrophoneDeviceId()
    {
        return _enumerator
            .GetDefaultCaptureEndpoint()
            ?.Id;
    }

    public bool IsSystemAudioDeviceAvailable(
        string deviceId)
    {
        return ContainsActiveEndpoint(
            deviceId,
            _enumerator
                .EnumerateRenderEndpoints());
    }

    public bool IsMicrophoneDeviceAvailable(
        string deviceId)
    {
        return ContainsActiveEndpoint(
            deviceId,
            _enumerator
                .EnumerateCaptureEndpoints());
    }

    private static bool ContainsActiveEndpoint(
        string deviceId,
        IReadOnlyList<AudioEndpointSnapshot>
            endpoints)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return endpoints.Any(endpoint =>
            endpoint.IsActive &&
            string.Equals(
                endpoint.Id,
                deviceId,
                StringComparison.OrdinalIgnoreCase));
    }
}
