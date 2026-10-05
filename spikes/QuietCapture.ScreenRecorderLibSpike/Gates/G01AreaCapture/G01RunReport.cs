using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G01AreaCapture;

internal sealed class G01RunReport
{
    public string Gate { get; init; } = "G0-1";
    public string Status { get; set; } = "Started";
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StopRequestedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string OutputPath { get; init; } = string.Empty;
    public string RecorderLogPath { get; init; } = string.Empty;
    public string OsDescription { get; init; } = RuntimeInformation.OSDescription;
    public string FrameworkDescription { get; init; } = RuntimeInformation.FrameworkDescription;
    public string ProcessArchitecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string RecorderAssemblyVersion { get; init; } =
        typeof(Recorder).Assembly.GetName().Version?.ToString() ?? "unknown";
    public string DisplayDeviceName { get; init; } = string.Empty;
    public string DisplayFriendlyName { get; init; } = string.Empty;
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IncludeCursor { get; init; }
    public string? Error { get; set; }
    public List<G01RunEvent> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G01RunEvent(DateTimeOffset.UtcNow, name, detail));
    }

    public void Save(string path)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }
}

internal sealed record G01RunEvent(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Detail);
