using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G06StabilityStopLatency;

internal sealed class G06WorkerManifest
{
    public string Gate { get; init; } = "G0-6";
    public string RunId { get; init; } = string.Empty;
    public int RunSeconds { get; init; }
    public string Status { get; set; } = "Created";
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RecordingAtUtc { get; set; }
    public DateTimeOffset? StopRequestedAtUtc { get; set; }
    public DateTimeOffset? RecordingCompleteAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public long? StopRequestedTimestamp { get; set; }
    public long? RecordingCompleteTimestamp { get; set; }
    public double? StopLatencyMilliseconds { get; set; }
    public string OsDescription { get; init; } = RuntimeInformation.OSDescription;
    public string FrameworkDescription { get; init; } = RuntimeInformation.FrameworkDescription;
    public string ProcessArchitecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString();
    public int ProcessId { get; init; } = Environment.ProcessId;
    public string RecorderAssemblyVersion { get; init; } =
        typeof(Recorder).Assembly.GetName().Version?.ToString() ?? "unknown";
    public string OutputPath { get; init; } = string.Empty;
    public string RecorderLogPath { get; init; } = string.Empty;
    public string? Error { get; set; }
    public List<G06Event> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G06Event(DateTimeOffset.UtcNow, name, detail));
    }

    public void Save(string path) => G06Json.Save(path, this);
}

internal sealed class G06BatchManifest
{
    public string Gate { get; init; } = "G0-6";
    public string BatchId { get; init; } = string.Empty;
    public int RequestedRounds { get; init; }
    public int RunSeconds { get; init; }
    public int SampleIntervalMilliseconds { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string Status { get; set; } = "Created";
    public string BatchDirectory { get; init; } = string.Empty;
    public string SamplesCsvPath { get; init; } = string.Empty;
    public string SummaryPath { get; init; } = string.Empty;
    public double? StopP50Milliseconds { get; set; }
    public double? StopP95Milliseconds { get; set; }
    public string? Error { get; set; }
    public List<G06RunObservation> Runs { get; init; } = new();
    public List<G06Event> Events { get; init; } = new();

    public void AddEvent(string name, string? detail = null)
    {
        Events.Add(new G06Event(DateTimeOffset.UtcNow, name, detail));
    }

    public void Save(string path) => G06Json.Save(path, this);
}

internal sealed class G06RunObservation
{
    public int RunIndex { get; init; }
    public string RunId { get; init; } = string.Empty;
    public string RunDirectory { get; init; } = string.Empty;
    public int? WorkerProcessId { get; set; }
    public int? WorkerExitCode { get; set; }
    public string Status { get; set; } = "Created";
    public bool RecordingReadyObserved { get; set; }
    public int SampleCount { get; set; }
    public long? InitialWorkingSetBytes { get; set; }
    public long? FinalWorkingSetBytes { get; set; }
    public long? MaxWorkingSetBytes { get; set; }
    public long? InitialPrivateBytes { get; set; }
    public long? FinalPrivateBytes { get; set; }
    public long? MaxPrivateBytes { get; set; }
    public long? PrivateBytesGrowthBytes { get; set; }
    public double? PrivateBytesSlopeMiBPerHour { get; set; }
    public double? AverageCpuPercent { get; set; }
    public double? MaxCpuPercent { get; set; }
    public long? FinalOutputBytes { get; set; }
    public double? StopLatencyMilliseconds { get; set; }
    public string? WorkerStatus { get; set; }
    public string? Error { get; set; }
}

internal sealed record G06Sample(
    int RunIndex,
    string RunId,
    DateTimeOffset TimestampUtc,
    double ElapsedSeconds,
    long WorkingSetBytes,
    long PrivateBytes,
    double TotalProcessorTimeMilliseconds,
    double? CpuPercent,
    long OutputBytes);

internal sealed record G06Event(
    DateTimeOffset TimestampUtc,
    string Name,
    string? Detail);

internal static class G06Statistics
{
    public static double? Percentile(
        IEnumerable<double> source,
        double percentile)
    {
        double[] values = source
            .Where(double.IsFinite)
            .OrderBy(value => value)
            .ToArray();

        if (values.Length == 0)
        {
            return null;
        }

        if (values.Length == 1)
        {
            return values[0];
        }

        double position = (values.Length - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return values[lower];
        }

        double fraction = position - lower;
        return values[lower] +
            ((values[upper] - values[lower]) * fraction);
    }

    public static double? LinearSlopeMiBPerHour(
        IReadOnlyList<G06Sample> samples)
    {
        if (samples.Count < 2)
        {
            return null;
        }

        double meanX = samples.Average(sample => sample.ElapsedSeconds);
        double meanY = samples.Average(sample => (double)sample.PrivateBytes);

        double numerator = 0;
        double denominator = 0;

        foreach (G06Sample sample in samples)
        {
            double dx = sample.ElapsedSeconds - meanX;
            double dy = sample.PrivateBytes - meanY;
            numerator += dx * dy;
            denominator += dx * dx;
        }

        if (denominator <= 0)
        {
            return null;
        }

        double bytesPerSecond = numerator / denominator;
        return bytesPerSecond * 3600d / (1024d * 1024d);
    }
}

internal static class G06Csv
{
    public static string Header =>
        "run_index,run_id,timestamp_utc,elapsed_seconds,working_set_bytes,private_bytes,total_processor_time_ms,cpu_percent,output_bytes";

    public static string Format(G06Sample sample)
    {
        return string.Join(",",
            sample.RunIndex.ToString(CultureInfo.InvariantCulture),
            Escape(sample.RunId),
            Escape(sample.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
            sample.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture),
            sample.WorkingSetBytes.ToString(CultureInfo.InvariantCulture),
            sample.PrivateBytes.ToString(CultureInfo.InvariantCulture),
            sample.TotalProcessorTimeMilliseconds.ToString("F3", CultureInfo.InvariantCulture),
            sample.CpuPercent?.ToString("F3", CultureInfo.InvariantCulture) ?? string.Empty,
            sample.OutputBytes.ToString(CultureInfo.InvariantCulture));
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\n') &&
            !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

internal static class G06Summary
{
    public static string Build(G06BatchManifest batch)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# G0-6 Batch Summary");
        builder.AppendLine();
        builder.AppendLine($"- Batch: `{batch.BatchId}`");
        builder.AppendLine($"- Status: **{batch.Status}**");
        builder.AppendLine($"- Requested rounds: {batch.RequestedRounds}");
        builder.AppendLine($"- Run duration: {batch.RunSeconds} seconds");
        builder.AppendLine($"- Sample interval: {batch.SampleIntervalMilliseconds} ms");
        builder.AppendLine($"- Stop P50: {FormatMs(batch.StopP50Milliseconds)}");
        builder.AppendLine($"- Stop P95: {FormatMs(batch.StopP95Milliseconds)}");
        builder.AppendLine();
        builder.AppendLine("| Run | Status | Samples | Private start | Private final | Private growth | Slope MiB/h | CPU avg | CPU max | Output | Stop latency |");
        builder.AppendLine("| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (G06RunObservation run in batch.Runs)
        {
            builder.AppendLine(
                $"| {run.RunIndex} | {run.Status} | {run.SampleCount} | {FormatBytes(run.InitialPrivateBytes)} | {FormatBytes(run.FinalPrivateBytes)} | {FormatBytes(run.PrivateBytesGrowthBytes)} | {FormatNumber(run.PrivateBytesSlopeMiBPerHour)} | {FormatPercent(run.AverageCpuPercent)} | {FormatPercent(run.MaxCpuPercent)} | {FormatBytes(run.FinalOutputBytes)} | {FormatMs(run.StopLatencyMilliseconds)} |");
        }

        builder.AppendLine();
        builder.AppendLine("Stop percentiles use linear interpolation over successful runs with a measured Stop→RecordingComplete latency.");
        builder.AppendLine("Private-memory slope is an ordinary least-squares fit over periodic samples from each run.");

        return builder.ToString();
    }

    private static string FormatBytes(long? value)
    {
        return value.HasValue
            ? $"{value.Value / (1024d * 1024d):F1} MiB"
            : "n/a";
    }

    private static string FormatMs(double? value)
    {
        return value.HasValue ? $"{value.Value:F1} ms" : "n/a";
    }

    private static string FormatPercent(double? value)
    {
        return value.HasValue ? $"{value.Value:F1}%" : "n/a";
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue ? $"{value.Value:F2}" : "n/a";
    }
}

internal static class G06Json
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Save<T>(string path, T value)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("JSON path has no directory.");

        Directory.CreateDirectory(directory);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, Options));
        File.Move(tempPath, path, overwrite: true);
    }

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            Options);
    }
}
