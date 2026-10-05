namespace QuietCapture.Core.Models;

public abstract record CaptureTarget;

public sealed record AreaCaptureTarget : CaptureTarget
{
    public AreaCaptureTarget(
        string monitorId,
        PixelRect bounds)
    {
        MonitorId = RequireId(
            monitorId,
            nameof(monitorId));
        Bounds = bounds;
    }

    public string MonitorId { get; }

    public PixelRect Bounds { get; }

    private static string RequireId(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Monitor ID must not be empty.",
                parameterName);
        }

        return value;
    }
}

public sealed record WindowCaptureTarget : CaptureTarget
{
    public WindowCaptureTarget(nint hwnd)
    {
        if (hwnd == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hwnd),
                "Window handle must not be zero.");
        }

        Hwnd = hwnd;
    }

    public nint Hwnd { get; }
}

public sealed record MonitorCaptureTarget : CaptureTarget
{
    public MonitorCaptureTarget(string monitorId)
    {
        if (string.IsNullOrWhiteSpace(monitorId))
        {
            throw new ArgumentException(
                "Monitor ID must not be empty.",
                nameof(monitorId));
        }

        MonitorId = monitorId;
    }

    public string MonitorId { get; }
}
