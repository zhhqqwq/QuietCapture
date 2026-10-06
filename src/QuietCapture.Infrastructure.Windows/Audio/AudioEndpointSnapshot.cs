namespace QuietCapture.Infrastructure.Windows.Audio;

public enum AudioEndpointFlow
{
    Render,
    Capture
}

[Flags]
public enum AudioEndpointState
{
    None = 0,
    Active = 0x00000001,
    Disabled = 0x00000002,
    NotPresent = 0x00000004,
    Unplugged = 0x00000008
}

public sealed record AudioEndpointSnapshot
{
    public AudioEndpointSnapshot(
        string id,
        string? friendlyName,
        AudioEndpointFlow flow,
        AudioEndpointState state)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Audio endpoint ID must not be empty.",
                nameof(id));
        }

        Id = id;
        FriendlyName =
            string.IsNullOrWhiteSpace(friendlyName)
                ? null
                : friendlyName;
        Flow = flow;
        State = state;
    }

    public string Id { get; }

    public string? FriendlyName { get; }

    public AudioEndpointFlow Flow { get; }

    public AudioEndpointState State { get; }

    public bool IsActive =>
        (State & AudioEndpointState.Active) != 0;
}
