using System.Diagnostics;
using System.IO;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G06StabilityStopLatency;

internal static class G06Worker
{
    private static readonly TimeSpan RecordingReadyTimeout =
        TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CompletionTimeout =
        TimeSpan.FromSeconds(60);

    public static async Task<int> RunAsync(
        IReadOnlyDictionary<string, string> arguments)
    {
        string runId = Require(arguments, "run-id");
        string runDirectory = Require(arguments, "run-dir");
        int runSeconds = ParsePositiveInt(arguments, "run-seconds");

        Directory.CreateDirectory(runDirectory);

        string outputPath = Path.Combine(runDirectory, "recording.mp4");
        string recorderLogPath = Path.Combine(runDirectory, "recorder.log");
        string workerManifestPath = Path.Combine(
            runDirectory,
            "worker-manifest.json");
        string readyFlagPath = Path.Combine(
            runDirectory,
            "recording-ready.flag");

        var manifest = new G06WorkerManifest
        {
            RunId = runId,
            RunSeconds = runSeconds,
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath
        };
        manifest.AddEvent("WorkerStarted");
        manifest.Save(workerManifestPath);

        var recordingReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingComplete = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingFailed = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Recorder? recorder = null;

        try
        {
            var options = new RecorderOptions
            {
                SourceOptions = SourceOptions.MainMonitor,
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = false
                },
                MouseOptions = new MouseOptions
                {
                    IsMousePointerEnabled = true
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Framerate = 30,
                    Bitrate = 8_000_000,
                    IsFixedFramerate = true,
                    IsHardwareEncodingEnabled = true,
                    IsFragmentedMp4Enabled = false,
                    IsMp4FastStartEnabled = true
                },
                LogOptions = new LogOptions
                {
                    IsLogEnabled = true,
                    LogFilePath = recorderLogPath,
                    LogSeverityLevel = ScreenRecorderLib.LogLevel.Debug
                }
            };

            recorder = Recorder.CreateRecorder(options);

            recorder.OnStatusChanged += (_, eventArgs) =>
            {
                manifest.Status = eventArgs.Status.ToString();
                manifest.AddEvent(
                    "StatusChanged",
                    eventArgs.Status.ToString());

                if (eventArgs.Status == RecorderStatus.Recording)
                {
                    manifest.RecordingAtUtc = DateTimeOffset.UtcNow;
                    manifest.Save(workerManifestPath);
                    File.WriteAllText(
                        readyFlagPath,
                        manifest.RecordingAtUtc.Value.ToString("O"));
                    recordingReady.TrySetResult(true);
                    return;
                }

                manifest.Save(workerManifestPath);
            };

            recorder.OnRecordingComplete += (_, eventArgs) =>
            {
                long completeTimestamp = Stopwatch.GetTimestamp();
                DateTimeOffset completeUtc = DateTimeOffset.UtcNow;

                manifest.Status = "Complete";
                manifest.RecordingCompleteAtUtc = completeUtc;
                manifest.RecordingCompleteTimestamp = completeTimestamp;
                manifest.FinishedAtUtc = completeUtc;

                if (manifest.StopRequestedTimestamp.HasValue)
                {
                    manifest.StopLatencyMilliseconds =
                        Stopwatch.GetElapsedTime(
                            manifest.StopRequestedTimestamp.Value,
                            completeTimestamp).TotalMilliseconds;
                }

                manifest.AddEvent(
                    "RecordingComplete",
                    $"file={eventArgs.FilePath}; stopLatencyMs={manifest.StopLatencyMilliseconds:F3}");
                manifest.Save(workerManifestPath);
                recordingComplete.TrySetResult(eventArgs.FilePath);
            };

            recorder.OnRecordingFailed += (_, eventArgs) =>
            {
                manifest.Status = "RecordingFailed";
                manifest.Error = eventArgs.Error;
                manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
                manifest.AddEvent(
                    "RecordingFailed",
                    eventArgs.Error);
                manifest.Save(workerManifestPath);
                recordingFailed.TrySetResult(eventArgs.Error);
            };

            manifest.Status = "Starting";
            manifest.AddEvent("RecordCalled");
            manifest.Save(workerManifestPath);
            recorder.Record(outputPath);

            Task readyTimeout = Task.Delay(RecordingReadyTimeout);
            Task firstReady = await Task.WhenAny(
                recordingReady.Task,
                recordingFailed.Task,
                readyTimeout);

            if (firstReady == recordingFailed.Task)
            {
                return 3;
            }

            if (firstReady == readyTimeout)
            {
                manifest.Status = "RecordingReadyTimeout";
                manifest.Error =
                    $"Recorder did not enter Recording within {RecordingReadyTimeout.TotalSeconds:0} seconds.";
                manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
                manifest.AddEvent("RecordingReadyTimeout");
                manifest.Save(workerManifestPath);
                return 4;
            }

            await Task.Delay(TimeSpan.FromSeconds(runSeconds));

            long stopTimestamp = Stopwatch.GetTimestamp();
            DateTimeOffset stopUtc = DateTimeOffset.UtcNow;

            manifest.StopRequestedTimestamp = stopTimestamp;
            manifest.StopRequestedAtUtc = stopUtc;
            manifest.Status = "Stopping";
            manifest.AddEvent("StopRequested");
            manifest.Save(workerManifestPath);

            recorder.Stop();

            Task completionTimeout = Task.Delay(CompletionTimeout);
            Task firstCompletion = await Task.WhenAny(
                recordingComplete.Task,
                recordingFailed.Task,
                completionTimeout);

            if (firstCompletion == recordingComplete.Task)
            {
                return 0;
            }

            if (firstCompletion == recordingFailed.Task)
            {
                return 5;
            }

            manifest.Status = "CompletionTimeout";
            manifest.Error =
                $"Recorder did not complete within {CompletionTimeout.TotalSeconds:0} seconds after Stop.";
            manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
            manifest.AddEvent("CompletionTimeout");
            manifest.Save(workerManifestPath);
            return 6;
        }
        catch (Exception ex)
        {
            manifest.Status = "WorkerException";
            manifest.Error = ex.ToString();
            manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
            manifest.AddEvent(
                "WorkerException",
                ex.Message);
            manifest.Save(workerManifestPath);
            return 10;
        }
        finally
        {
            if (recorder is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static string Require(
        IReadOnlyDictionary<string, string> arguments,
        string name)
    {
        if (!arguments.TryGetValue(name, out string? value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"Missing --{name}=... argument.");
        }

        return value;
    }

    private static int ParsePositiveInt(
        IReadOnlyDictionary<string, string> arguments,
        string name)
    {
        string value = Require(arguments, name);

        return int.TryParse(value, out int result) &&
            result > 0
            ? result
            : throw new ArgumentException(
                $"--{name} must be a positive integer.");
    }
}
