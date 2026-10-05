using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G02SystemAudio;

internal sealed class G02RunReport
{
    public string Gate { get; init; } = "G0-2";
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
    public string AudioDeviceId { get; init; } = string.Empty;
    public string AudioDeviceFriendlyName { get; init; } = string.Empty;
    public bool AudioDeviceWasDefaultAtEnumeration { get; init; }
    public long AudioPacketCount { get; set; }
    public long MixedAudioBytes { get; set; }
    public long SourcePacketCount { get; set; }
    public long SourceAudioBytes { get; set; }
    public string? Error { get; set; }
    public List<G02AudioDeviceSnapshot> EnumeratedLoopbackDevices { get; init; } = new();
    public List<G02RunEvent> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G02RunEvent(DateTimeOffset.UtcNow, name, detail));
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

internal sealed record G02AudioDeviceSnapshot(
    string DeviceId,
    string FriendlyName,
    bool IsDefaultDevice);

internal sealed record G02RunEvent(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Detail);
