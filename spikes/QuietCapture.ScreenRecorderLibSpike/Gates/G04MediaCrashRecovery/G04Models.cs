using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G04MediaCrashRecovery;

internal enum G04TerminationMode
{
    NormalStop,
    Kill
}

internal sealed class G04ControllerManifest
{
    public string Gate { get; init; } = "G0-4";
    public string RunId { get; init; } = string.Empty;
    public G04TerminationMode TerminationMode { get; init; }
    public bool FragmentedMp4 { get; init; }
    public bool FixedFramerate { get; init; }
    public int TargetFps { get; init; } = 30;
    public int RunSeconds { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? WorkerStartedAtUtc { get; set; }
    public DateTimeOffset? WorkerReadyAtUtc { get; set; }
    public DateTimeOffset? TerminationRequestedAtUtc { get; set; }
    public DateTimeOffset? WorkerExitedAtUtc { get; set; }
    public int? WorkerProcessId { get; set; }
    public int? WorkerExitCode { get; set; }
    public bool WorkerWasKilled { get; set; }
    public string Status { get; set; } = "Created";
    public string RunDirectory { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public string RecorderLogPath { get; init; } = string.Empty;
    public string WorkerManifestPath { get; init; } = string.Empty;
    public string ReadyFlagPath { get; init; } = string.Empty;
    public bool OutputExistsAfterExit { get; set; }
    public long OutputBytesAfterExit { get; set; }
    public string? Error { get; set; }
    public List<G04Event> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G04Event(DateTimeOffset.UtcNow, name, detail));
    }

    public void Save(string path) => G04Json.Save(path, this);
}

internal sealed class G04WorkerManifest
{
    public string Gate { get; init; } = "G0-4";
    public string RunId { get; init; } = string.Empty;
    public G04TerminationMode TerminationMode { get; init; }
    public bool FragmentedMp4 { get; init; }
    public bool FixedFramerate { get; init; }
    public int TargetFps { get; init; } = 30;
    public int RunSeconds { get; init; }
    public string Status { get; set; } = "Created";
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RecordingAtUtc { get; set; }
    public DateTimeOffset? StopRequestedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string OsDescription { get; init; } = RuntimeInformation.OSDescription;
    public string FrameworkDescription { get; init; } = RuntimeInformation.FrameworkDescription;
    public string ProcessArchitecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString();
    public int ProcessId { get; init; } = Environment.ProcessId;
    public string RecorderAssemblyVersion { get; init; } =
        typeof(Recorder).Assembly.GetName().Version?.ToString() ?? "unknown";
    public string OutputPath { get; init; } = string.Empty;
    public string RecorderLogPath { get; init; } = string.Empty;
    public string? Error { get; set; }
    public List<G04Event> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G04Event(DateTimeOffset.UtcNow, name, detail));
    }

    public void Save(string path) => G04Json.Save(path, this);
}

internal sealed record G04Event(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Detail);

internal static class G04Json
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Save<T>(string path, T value)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Manifest path has no directory.");

        Directory.CreateDirectory(directory);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, Options));
        File.Move(tempPath, path, overwrite: true);
    }
}
