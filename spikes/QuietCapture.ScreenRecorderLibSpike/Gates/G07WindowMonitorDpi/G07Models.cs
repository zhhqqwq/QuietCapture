using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G07WindowMonitorDpi;

internal enum G07CaptureMode
{
    Window,
    Monitor,
    Area
}

internal enum G07WindowScript
{
    NoActions,
    Lifecycle,
    CrossMonitor
}

internal enum G07DisplayCaptureApi
{
    DesktopDuplication,
    WindowsGraphicsCapture
}

internal sealed record G07PixelRect(
    int Left,
    int Top,
    int Width,
    int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

internal sealed record G07DisplaySnapshot(
    long HMonitor,
    string DeviceName,
    string? RecorderFriendlyName,
    string? DeviceInterfaceId,
    string? DeviceString,
    string? DeviceKey,
    G07PixelRect Bounds,
    G07PixelRect WorkArea,
    bool IsPrimary,
    uint? DpiX,
    uint? DpiY,
    double? ScalePercentX,
    double? ScalePercentY,
    bool HasNegativeCoordinates);

internal sealed record G07WindowSnapshot(
    long Hwnd,
    bool IsValid,
    bool IsMinimized,
    G07PixelRect? Bounds,
    uint? Dpi,
    double? ScalePercent,
    string? MonitorDeviceName);

internal sealed class G07ActionObservation
{
    public string Action { get; init; } = string.Empty;
    public double ScheduledOffsetSeconds { get; init; }
    public double ActualOffsetSeconds { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Detail { get; set; }
    public G07WindowSnapshot? Before { get; set; }
    public G07WindowSnapshot? After { get; set; }
}

internal sealed record G07TimelineEvent(
    DateTimeOffset TimestampUtc,
    double? ElapsedMilliseconds,
    string Name,
    string? Detail,
    G07WindowSnapshot? Window);

internal sealed class G07RunManifest
{
    public string Gate { get; init; } = "G0-7";
    public string RunId { get; init; } = string.Empty;
    public G07CaptureMode CaptureMode { get; init; }
    public G07WindowScript WindowScript { get; init; }
    public G07DisplayCaptureApi DisplayCaptureApi { get; init; }
    public int StepSeconds { get; init; }
    public int NoActionRunSeconds { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } =
        DateTimeOffset.UtcNow;
    public DateTimeOffset? RecordCalledAtUtc { get; set; }
    public DateTimeOffset? RecordingAtUtc { get; set; }
    public DateTimeOffset? StopRequestedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string OsDescription { get; init; } =
        RuntimeInformation.OSDescription;
    public string FrameworkDescription { get; init; } =
        RuntimeInformation.FrameworkDescription;
    public string ProcessArchitecture { get; init; } =
        RuntimeInformation.ProcessArchitecture.ToString();
    public string RecorderAssemblyVersion { get; init; } =
        typeof(Recorder).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public string OutputPath { get; init; } = string.Empty;
    public string RecorderLogPath { get; init; } = string.Empty;

    public string SelectedDisplayDeviceName { get; init; } =
        string.Empty;
    public string? SelectedDisplayFriendlyName { get; init; }

    public int? AreaX { get; init; }
    public int? AreaY { get; init; }
    public int? AreaWidth { get; init; }
    public int? AreaHeight { get; init; }
    public G07PixelRect? ExpectedVirtualAreaRect { get; set; }

    public long? TargetWindowHwnd { get; set; }
    public string? TargetWindowTitle { get; set; }

    public List<G07DisplaySnapshot> DisplaysBefore { get; init; } =
        new();
    public List<G07DisplaySnapshot> DisplaysAfter { get; init; } =
        new();
    public List<G07ActionObservation> Actions { get; init; } =
        new();
    public List<G07TimelineEvent> Timeline { get; init; } =
        new();

    public string Status { get; set; } = "Created";
    public string? Error { get; set; }
    public bool OutputExistsAfterRun { get; set; }
    public long OutputBytesAfterRun { get; set; }

    public void AddEvent(
        string name,
        string? detail = null,
        double? elapsedMilliseconds = null,
        G07WindowSnapshot? window = null)
    {
        Timeline.Add(
            new G07TimelineEvent(
                DateTimeOffset.UtcNow,
                elapsedMilliseconds,
                name,
                detail,
                window));
    }

    public void ObserveOutput()
    {
        var file = new FileInfo(OutputPath);
        OutputExistsAfterRun = file.Exists;
        OutputBytesAfterRun =
            file.Exists ? file.Length : 0;
    }

    public void Save(string path)
    {
        string directory =
            Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "Manifest path has no directory.");

        Directory.CreateDirectory(directory);

        string tempPath = path + ".tmp";
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(
            tempPath,
            JsonSerializer.Serialize(this, options));
        File.Move(
            tempPath,
            path,
            overwrite: true);
    }
}
