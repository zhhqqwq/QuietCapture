using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G05CaptureExclusion;

internal enum G05CaptureMode
{
    Area,
    Monitor,
    Window
}

internal sealed class G05RunManifest
{
    public string Gate { get; init; } = "G0-5";
    public string RunId { get; init; } = string.Empty;
    public G05CaptureMode CaptureMode { get; init; }
    public bool ExclusionRequested { get; init; }
    public int RunSeconds { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RecordCalledAtUtc { get; set; }
    public DateTimeOffset? RecordingAtUtc { get; set; }
    public DateTimeOffset? OverlaysShownAtUtc { get; set; }
    public DateTimeOffset? StopRequestedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string OsDescription { get; init; } = RuntimeInformation.OSDescription;
    public string FrameworkDescription { get; init; } = RuntimeInformation.FrameworkDescription;
    public string ProcessArchitecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string RecorderAssemblyVersion { get; init; } =
        typeof(Recorder).Assembly.GetName().Version?.ToString() ?? "unknown";

    public string OutputPath { get; init; } = string.Empty;
    public string RecorderLogPath { get; init; } = string.Empty;
    public string ReferenceScreenshotPath { get; init; } = string.Empty;

    public string? DisplayDeviceName { get; set; }
    public string? DisplayFriendlyName { get; set; }
    public long? TargetWindowHwnd { get; set; }
    public string? TargetWindowTitle { get; set; }

    public int? AreaX { get; set; }
    public int? AreaY { get; set; }
    public int? AreaWidth { get; set; }
    public int? AreaHeight { get; set; }

    public G05WindowPreparationResult? StatusWindowPreparation { get; set; }
    public G05WindowPreparationResult? BorderWindowPreparation { get; set; }
    public G05AffinityReadResult? StatusWindowAffinityAfterShow { get; set; }
    public G05AffinityReadResult? BorderWindowAffinityAfterShow { get; set; }

    public string Status { get; set; } = "Created";
    public string? Error { get; set; }
    public bool OutputExistsAfterRun { get; set; }
    public long OutputBytesAfterRun { get; set; }
    public List<G05RunEvent> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G05RunEvent(DateTimeOffset.UtcNow, name, detail));
    }

    public void ObserveOutput()
    {
        var file = new FileInfo(OutputPath);
        OutputExistsAfterRun = file.Exists;
        OutputBytesAfterRun = file.Exists ? file.Length : 0;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Manifest path has no directory."));

        string tempPath = path + ".tmp";
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(
            tempPath,
            JsonSerializer.Serialize(this, options));
        File.Move(tempPath, path, overwrite: true);
    }
}

internal sealed record G05RunEvent(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Detail);
