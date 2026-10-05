using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G03SystemAudioMicrophone;

internal sealed class G03RunReport
{
    public string Gate { get; init; } = "G0-3";
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

    public string LoopbackDeviceId { get; init; } = string.Empty;
    public string LoopbackDeviceFriendlyName { get; init; } = string.Empty;
    public bool LoopbackDeviceWasDefaultAtEnumeration { get; init; }
    public string LoopbackSourceId { get; init; } = string.Empty;

    public string MicrophoneDeviceId { get; init; } = string.Empty;
    public string MicrophoneDeviceFriendlyName { get; init; } = string.Empty;
    public bool MicrophoneDeviceWasDefaultAtEnumeration { get; init; }
    public string MicrophoneSourceId { get; init; } = string.Empty;

    public long AudioPacketCount { get; set; }
    public long MixedAudioBytes { get; set; }
    public long LoopbackPacketCount { get; set; }
    public long LoopbackAudioBytes { get; set; }
    public long MicrophonePacketCount { get; set; }
    public long MicrophoneAudioBytes { get; set; }
    public long UnknownSourcePacketCount { get; set; }
    public long UnknownSourceAudioBytes { get; set; }

    public string? Error { get; set; }
    public List<G03AudioDeviceSnapshot> EnumeratedLoopbackDevices { get; init; } = new();
    public List<G03AudioDeviceSnapshot> EnumeratedCaptureDevices { get; init; } = new();
    public List<G03RunEvent> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G03RunEvent(DateTimeOffset.UtcNow, name, detail));
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

internal sealed record G03AudioDeviceSnapshot(
    string DeviceId,
    string FriendlyName,
    bool IsDefaultDevice);

internal sealed record G03RunEvent(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Detail);
