using System.IO;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G04MediaCrashRecovery;

internal static class G04Worker
{
    private static readonly TimeSpan RecordingReadyTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(45);

    public static async Task<int> RunAsync(IReadOnlyDictionary<string, string> arguments)
    {
        string runId = Require(arguments, "run-id");
        string runDirectory = Require(arguments, "run-dir");
        G04TerminationMode terminationMode = ParseTerminationMode(arguments);
        bool fragmented = ParseBool(arguments, "fragmented");
        bool fixedFramerate = ParseBool(arguments, "fixed-fps");
        int runSeconds = ParsePositiveInt(arguments, "run-seconds");

        Directory.CreateDirectory(runDirectory);

        string outputPath = Path.Combine(runDirectory, "recording.partial.mp4");
        string recorderLogPath = Path.Combine(runDirectory, "recorder.log");
        string workerManifestPath = Path.Combine(runDirectory, "worker-manifest.json");
        string readyFlagPath = Path.Combine(runDirectory, "recording-ready.flag");

        var manifest = new G04WorkerManifest
        {
            RunId = runId,
            TerminationMode = terminationMode,
            FragmentedMp4 = fragmented,
            FixedFramerate = fixedFramerate,
            RunSeconds = runSeconds,
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath
        };
        manifest.AddEvent("WorkerStarted");
        manifest.Save(workerManifestPath);

        var recordingReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingFinished = new TaskCompletionSource<string>(
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
                    IsFixedFramerate = fixedFramerate,
                    IsHardwareEncodingEnabled = true,
                    IsFragmentedMp4Enabled = fragmented,
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

            recorder.OnStatusChanged += (_, e) =>
            {
                manifest.Status = e.Status.ToString();
                manifest.AddEvent("StatusChanged", e.Status.ToString());

                if (e.Status == RecorderStatus.Recording)
                {
                    manifest.RecordingAtUtc = DateTimeOffset.UtcNow;
                    manifest.Save(workerManifestPath);
                    File.WriteAllText(readyFlagPath, DateTimeOffset.UtcNow.ToString("O"));
                    recordingReady.TrySetResult(true);
                    return;
                }

                manifest.Save(workerManifestPath);
            };

            recorder.OnRecordingComplete += (_, e) =>
            {
                manifest.Status = "Complete";
                manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
                manifest.AddEvent("RecordingComplete", e.FilePath);
                manifest.Save(workerManifestPath);
                recordingFinished.TrySetResult(e.FilePath);
            };

            recorder.OnRecordingFailed += (_, e) =>
            {
                manifest.Status = "RecordingFailed";
                manifest.Error = e.Error;
                manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
                manifest.AddEvent("RecordingFailed", e.Error);
                manifest.Save(workerManifestPath);
                recordingFailed.TrySetResult(e.Error);
            };

            manifest.Status = "Starting";
            manifest.AddEvent("RecordCalled");
            manifest.Save(workerManifestPath);
            recorder.Record(outputPath);

            Task readyTimeoutTask = Task.Delay(RecordingReadyTimeout);
            Task firstReady = await Task.WhenAny(
                recordingReady.Task,
                recordingFailed.Task,
                readyTimeoutTask);

            if (firstReady == recordingFailed.Task)
            {
                return 3;
            }

            if (firstReady == readyTimeoutTask)
            {
                manifest.Status = "RecordingReadyTimeout";
                manifest.Error = $"Recorder did not enter Recording within {RecordingReadyTimeout.TotalSeconds:0} seconds.";
                manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
                manifest.AddEvent("RecordingReadyTimeout");
                manifest.Save(workerManifestPath);
                return 4;
            }

            if (terminationMode == G04TerminationMode.Kill)
            {
                manifest.Status = "WaitingForExternalKill";
                manifest.AddEvent("WaitingForExternalKill");
                manifest.Save(workerManifestPath);

                await Task.Delay(Timeout.InfiniteTimeSpan);
                return 20;
            }

            await Task.Delay(TimeSpan.FromSeconds(runSeconds));

            manifest.StopRequestedAtUtc = DateTimeOffset.UtcNow;
            manifest.Status = "Stopping";
            manifest.AddEvent("StopRequested");
            manifest.Save(workerManifestPath);
            recorder.Stop();

            Task completionTimeoutTask = Task.Delay(CompletionTimeout);
            Task firstCompletion = await Task.WhenAny(
                recordingFinished.Task,
                recordingFailed.Task,
                completionTimeoutTask);

            if (firstCompletion == recordingFinished.Task)
            {
                return 0;
            }

            if (firstCompletion == recordingFailed.Task)
            {
                return 5;
            }

            manifest.Status = "CompletionTimeout";
            manifest.Error = $"Recorder did not complete within {CompletionTimeout.TotalSeconds:0} seconds after Stop.";
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
            manifest.AddEvent("WorkerException", ex.Message);
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

    private static string Require(IReadOnlyDictionary<string, string> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing --{name}=... argument.");
        }

        return value;
    }

    private static G04TerminationMode ParseTerminationMode(
        IReadOnlyDictionary<string, string> arguments)
    {
        string value = Require(arguments, "termination-mode");
        return Enum.TryParse(value, ignoreCase: true, out G04TerminationMode mode)
            ? mode
            : throw new ArgumentException(
                "--termination-mode must be NormalStop or Kill.");
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> arguments, string name)
    {
        string value = Require(arguments, name);
        return bool.TryParse(value, out bool result)
            ? result
            : throw new ArgumentException($"--{name} must be true or false.");
    }

    private static int ParsePositiveInt(IReadOnlyDictionary<string, string> arguments, string name)
    {
        string value = Require(arguments, name);
        return int.TryParse(value, out int result) && result > 0
            ? result
            : throw new ArgumentException($"--{name} must be a positive integer.");
    }
}
