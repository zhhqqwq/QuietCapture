using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G06StabilityStopLatency;

public partial class G06ControllerWindow : Window
{
    private static readonly TimeSpan WorkerReadyTimeout =
        TimeSpan.FromSeconds(20);

    public G06ControllerWindow()
    {
        InitializeComponent();

        EvidenceRootTextBox.Text = Path.Combine(
            Path.GetTempPath(),
            "QuietCaptureSpike",
            "G0-6");
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadConfiguration(
                out int rounds,
                out int runSeconds,
                out int sampleIntervalMilliseconds))
        {
            return;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            Log("Environment.ProcessPath is unavailable; cannot launch Worker.");
            return;
        }

        bool launchedThroughDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);
        string? entryAssemblyPath =
            Assembly.GetEntryAssembly()?.Location;

        if (launchedThroughDotnetHost &&
            string.IsNullOrWhiteSpace(entryAssemblyPath))
        {
            Log("Entry assembly path is unavailable; cannot launch Worker through dotnet host.");
            return;
        }

        SetRunning(true);

        string batchId =
            $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-r{rounds}-s{runSeconds}-{Guid.NewGuid():N}"[..63];
        string batchDirectory = Path.Combine(
            EvidenceRootTextBox.Text,
            batchId);
        Directory.CreateDirectory(batchDirectory);

        string batchManifestPath = Path.Combine(
            batchDirectory,
            "batch-manifest.json");
        string samplesCsvPath = Path.Combine(
            batchDirectory,
            "samples.csv");
        string summaryPath = Path.Combine(
            batchDirectory,
            "summary.md");

        var batch = new G06BatchManifest
        {
            BatchId = batchId,
            RequestedRounds = rounds,
            RunSeconds = runSeconds,
            SampleIntervalMilliseconds =
                sampleIntervalMilliseconds,
            BatchDirectory = batchDirectory,
            SamplesCsvPath = samplesCsvPath,
            SummaryPath = summaryPath,
            Status = "Running"
        };
        batch.AddEvent("BatchStarted");
        batch.Save(batchManifestPath);

        Log($"Batch {batchId}");
        Log($"Rounds={rounds}; runSeconds={runSeconds}; sampleIntervalMs={sampleIntervalMilliseconds}");
        Log($"Evidence={batchDirectory}");

        try
        {
            await using var csvWriter =
                new StreamWriter(
                    samplesCsvPath,
                    append: false,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };

            await csvWriter.WriteLineAsync(G06Csv.Header);

            for (int runIndex = 1;
                 runIndex <= rounds;
                 runIndex++)
            {
                ProgressTextBlock.Text =
                    $"{runIndex} / {rounds}";
                StateTextBlock.Text =
                    $"Run {runIndex}";

                G06RunObservation observation =
                    await RunOneAsync(
                        runIndex,
                        batchId,
                        batchDirectory,
                        runSeconds,
                        sampleIntervalMilliseconds,
                        processPath,
                        launchedThroughDotnetHost,
                        entryAssemblyPath,
                        csvWriter);

                batch.Runs.Add(observation);
                UpdateBatchStatistics(batch);
                batch.AddEvent(
                    "RunFinished",
                    $"run={runIndex}; status={observation.Status}; stopLatencyMs={observation.StopLatencyMilliseconds:F3}");
                batch.Save(batchManifestPath);
                await File.WriteAllTextAsync(
                    summaryPath,
                    G06Summary.Build(batch));

                Log(
                    $"Run {runIndex}: status={observation.Status}; samples={observation.SampleCount}; privateGrowth={FormatMiB(observation.PrivateBytesGrowthBytes)}; stopLatency={FormatMs(observation.StopLatencyMilliseconds)}");
            }

            batch.Status = batch.Runs.All(run =>
                    string.Equals(
                        run.Status,
                        "Completed",
                        StringComparison.Ordinal))
                ? "Completed"
                : "CompletedWithErrors";
            batch.FinishedAtUtc = DateTimeOffset.UtcNow;
            UpdateBatchStatistics(batch);
            batch.AddEvent(
                "BatchFinished",
                $"status={batch.Status}; p50={batch.StopP50Milliseconds:F3}; p95={batch.StopP95Milliseconds:F3}");
            batch.Save(batchManifestPath);

            await File.WriteAllTextAsync(
                summaryPath,
                G06Summary.Build(batch));

            Log(
                $"Batch finished: {batch.Status}; Stop P50={FormatMs(batch.StopP50Milliseconds)}; P95={FormatMs(batch.StopP95Milliseconds)}");
            Log($"CSV: {samplesCsvPath}");
            Log($"Summary: {summaryPath}");
        }
        catch (Exception ex)
        {
            batch.Status = "ControllerException";
            batch.Error = ex.ToString();
            batch.FinishedAtUtc = DateTimeOffset.UtcNow;
            batch.AddEvent(
                "ControllerException",
                ex.Message);
            UpdateBatchStatistics(batch);
            batch.Save(batchManifestPath);

            await File.WriteAllTextAsync(
                summaryPath,
                G06Summary.Build(batch));

            Log($"G0-6 batch failed: {ex}");
        }
        finally
        {
            SetRunning(false);
        }
    }

    private async Task<G06RunObservation> RunOneAsync(
        int runIndex,
        string batchId,
        string batchDirectory,
        int runSeconds,
        int sampleIntervalMilliseconds,
        string processPath,
        bool launchedThroughDotnetHost,
        string? entryAssemblyPath,
        StreamWriter csvWriter)
    {
        string runId =
            $"{batchId}-run{runIndex:000}";
        string runDirectory =
            Path.Combine(
                batchDirectory,
                $"run-{runIndex:000}");
        Directory.CreateDirectory(runDirectory);

        string workerManifestPath = Path.Combine(
            runDirectory,
            "worker-manifest.json");
        string readyFlagPath = Path.Combine(
            runDirectory,
            "recording-ready.flag");
        string outputPath = Path.Combine(
            runDirectory,
            "recording.mp4");
        string stdoutPath = Path.Combine(
            runDirectory,
            "worker.stdout.log");
        string stderrPath = Path.Combine(
            runDirectory,
            "worker.stderr.log");

        var observation = new G06RunObservation
        {
            RunIndex = runIndex,
            RunId = runId,
            RunDirectory = runDirectory,
            Status = "Starting"
        };

        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (launchedThroughDotnetHost)
            {
                startInfo.ArgumentList.Add(entryAssemblyPath!);
            }

            startInfo.ArgumentList.Add("--gate=g0-6-worker");
            startInfo.ArgumentList.Add($"--run-id={runId}");
            startInfo.ArgumentList.Add($"--run-dir={runDirectory}");
            startInfo.ArgumentList.Add(
                $"--run-seconds={runSeconds}");

            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Worker process did not start.");
            }

            observation.WorkerProcessId = process.Id;
            Log(
                $"Run {runIndex}: Worker PID {process.Id}");

            Task stdoutTask =
                DrainAsync(
                    process.StandardOutput,
                    stdoutPath);
            Task stderrTask =
                DrainAsync(
                    process.StandardError,
                    stderrPath);

            bool ready = await WaitForReadyAsync(
                process,
                readyFlagPath,
                WorkerReadyTimeout);

            if (!ready)
            {
                observation.Status =
                    process.HasExited
                        ? "WorkerExitedBeforeReady"
                        : "WorkerReadyTimeout";

                observation.Error =
                    process.HasExited
                        ? $"Worker exited before Recording. Exit code: {process.ExitCode}."
                        : $"Worker did not enter Recording within {WorkerReadyTimeout.TotalSeconds:0} seconds.";

                if (!process.HasExited)
                {
                    process.Kill(
                        entireProcessTree: true);
                    await process.WaitForExitAsync();
                }

                observation.WorkerExitCode =
                    process.ExitCode;

                await Task.WhenAll(
                    stdoutTask,
                    stderrTask);

                PopulateWorkerResult(
                    observation,
                    workerManifestPath,
                    outputPath,
                    Array.Empty<G06Sample>());

                return observation;
            }

            observation.RecordingReadyObserved = true;
            observation.Status = "Recording";
            Log(
                $"Run {runIndex}: Recording ready; sampling started.");

            var samples =
                new List<G06Sample>();
            var elapsed =
                Stopwatch.StartNew();

            double? previousCpuMilliseconds = null;
            long? previousSampleTimestamp = null;

            while (!process.HasExited)
            {
                G06Sample? sample =
                    TryCreateSample(
                        process,
                        runIndex,
                        runId,
                        outputPath,
                        elapsed.Elapsed.TotalSeconds,
                        previousCpuMilliseconds,
                        previousSampleTimestamp,
                        out double currentCpuMilliseconds,
                        out long currentSampleTimestamp);

                if (sample is not null)
                {
                    samples.Add(sample);
                    await csvWriter.WriteLineAsync(
                        G06Csv.Format(sample));

                    previousCpuMilliseconds =
                        currentCpuMilliseconds;
                    previousSampleTimestamp =
                        currentSampleTimestamp;
                }

                await Task.Delay(
                    sampleIntervalMilliseconds);
            }

            await process.WaitForExitAsync();
            observation.WorkerExitCode =
                process.ExitCode;

            await Task.WhenAll(
                stdoutTask,
                stderrTask);

            PopulateWorkerResult(
                observation,
                workerManifestPath,
                outputPath,
                samples);

            return observation;
        }
        catch (Exception ex)
        {
            observation.Status =
                "ControllerRunException";
            observation.Error = ex.ToString();

            if (process is { HasExited: false })
            {
                try
                {
                    process.Kill(
                        entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
                catch (Exception killException)
                {
                    observation.Error +=
                        Environment.NewLine +
                        "Cleanup kill failed: " +
                        killException;
                }
            }

            if (process is not null &&
                process.HasExited)
            {
                observation.WorkerExitCode =
                    process.ExitCode;
            }

            PopulateWorkerResult(
                observation,
                workerManifestPath,
                outputPath,
                Array.Empty<G06Sample>());

            return observation;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static G06Sample? TryCreateSample(
        Process process,
        int runIndex,
        string runId,
        string outputPath,
        double elapsedSeconds,
        double? previousCpuMilliseconds,
        long? previousSampleTimestamp,
        out double currentCpuMilliseconds,
        out long currentSampleTimestamp)
    {
        currentCpuMilliseconds = 0;
        currentSampleTimestamp =
            Stopwatch.GetTimestamp();

        try
        {
            process.Refresh();

            long workingSetBytes =
                process.WorkingSet64;
            long privateBytes =
                process.PrivateMemorySize64;
            currentCpuMilliseconds =
                process.TotalProcessorTime.TotalMilliseconds;

            double? cpuPercent = null;

            if (previousCpuMilliseconds.HasValue &&
                previousSampleTimestamp.HasValue)
            {
                double wallMilliseconds =
                    Stopwatch.GetElapsedTime(
                        previousSampleTimestamp.Value,
                        currentSampleTimestamp)
                    .TotalMilliseconds;

                if (wallMilliseconds > 0)
                {
                    double cpuDelta =
                        currentCpuMilliseconds -
                        previousCpuMilliseconds.Value;

                    cpuPercent =
                        Math.Max(
                            0,
                            cpuDelta /
                            wallMilliseconds *
                            100d /
                            Environment.ProcessorCount);
                }
            }

            long outputBytes =
                ReadFileLength(outputPath);

            return new G06Sample(
                runIndex,
                runId,
                DateTimeOffset.UtcNow,
                elapsedSeconds,
                workingSetBytes,
                privateBytes,
                currentCpuMilliseconds,
                cpuPercent,
                outputBytes);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static void PopulateWorkerResult(
        G06RunObservation observation,
        string workerManifestPath,
        string outputPath,
        IReadOnlyList<G06Sample> samples)
    {
        observation.SampleCount =
            samples.Count;

        if (samples.Count > 0)
        {
            observation.InitialWorkingSetBytes =
                samples[0].WorkingSetBytes;
            observation.FinalWorkingSetBytes =
                samples[^1].WorkingSetBytes;
            observation.MaxWorkingSetBytes =
                samples.Max(sample =>
                    sample.WorkingSetBytes);

            observation.InitialPrivateBytes =
                samples[0].PrivateBytes;
            observation.FinalPrivateBytes =
                samples[^1].PrivateBytes;
            observation.MaxPrivateBytes =
                samples.Max(sample =>
                    sample.PrivateBytes);
            observation.PrivateBytesGrowthBytes =
                observation.FinalPrivateBytes -
                observation.InitialPrivateBytes;
            observation.PrivateBytesSlopeMiBPerHour =
                G06Statistics.LinearSlopeMiBPerHour(
                    samples);

            double[] cpuValues =
                samples
                    .Where(sample =>
                        sample.CpuPercent.HasValue)
                    .Select(sample =>
                        sample.CpuPercent!.Value)
                    .ToArray();

            if (cpuValues.Length > 0)
            {
                observation.AverageCpuPercent =
                    cpuValues.Average();
                observation.MaxCpuPercent =
                    cpuValues.Max();
            }
        }

        observation.FinalOutputBytes =
            ReadFileLength(outputPath);

        G06WorkerManifest? worker =
            G06Json.Read<G06WorkerManifest>(
                workerManifestPath);

        if (worker is not null)
        {
            observation.WorkerStatus =
                worker.Status;
            observation.StopLatencyMilliseconds =
                worker.StopLatencyMilliseconds;

            if (string.IsNullOrWhiteSpace(
                    observation.Error))
            {
                observation.Error =
                    worker.Error;
            }

            if (observation.Status is
                    "Starting" or "Recording")
            {
                observation.Status =
                    observation.WorkerExitCode == 0 &&
                    string.Equals(
                        worker.Status,
                        "Complete",
                        StringComparison.Ordinal)
                        ? "Completed"
                        : "WorkerFailed";
            }
        }
        else if (observation.Status is
                     "Starting" or "Recording")
        {
            observation.Status =
                "WorkerManifestMissing";
            observation.Error ??=
                "worker-manifest.json is missing or unreadable.";
        }
    }

    private static void UpdateBatchStatistics(
        G06BatchManifest batch)
    {
        double[] stopLatencies =
            batch.Runs
                .Where(run =>
                    run.StopLatencyMilliseconds.HasValue)
                .Select(run =>
                    run.StopLatencyMilliseconds!.Value)
                .ToArray();

        batch.StopP50Milliseconds =
            G06Statistics.Percentile(
                stopLatencies,
                0.50);
        batch.StopP95Milliseconds =
            G06Statistics.Percentile(
                stopLatencies,
                0.95);
    }

    private static async Task DrainAsync(
        StreamReader reader,
        string outputPath)
    {
        string text =
            await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(
            outputPath,
            text);
    }

    private static async Task<bool> WaitForReadyAsync(
        Process process,
        string readyFlagPath,
        TimeSpan timeout)
    {
        DateTimeOffset deadline =
            DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(readyFlagPath))
            {
                return true;
            }

            if (process.HasExited)
            {
                return false;
            }

            await Task.Delay(100);
        }

        return File.Exists(readyFlagPath);
    }

    private static long ReadFileLength(
        string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists
                ? file.Length
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private bool TryReadConfiguration(
        out int rounds,
        out int runSeconds,
        out int sampleIntervalMilliseconds)
    {
        rounds = 0;
        runSeconds = 0;
        sampleIntervalMilliseconds = 0;

        if (!int.TryParse(
                RoundsTextBox.Text,
                out rounds) ||
            rounds < 1 ||
            rounds > 50)
        {
            Log(
                "Rounds must be an integer from 1 to 50.");
            return false;
        }

        if (!int.TryParse(
                RunSecondsTextBox.Text,
                out runSeconds) ||
            runSeconds < 1 ||
            runSeconds > 43200)
        {
            Log(
                "Run seconds must be an integer from 1 to 43200.");
            return false;
        }

        if (!int.TryParse(
                SampleIntervalTextBox.Text,
                out sampleIntervalMilliseconds) ||
            sampleIntervalMilliseconds < 250 ||
            sampleIntervalMilliseconds > 60000)
        {
            Log(
                "Sample interval must be from 250 to 60000 ms.");
            return false;
        }

        return true;
    }

    private void SetRunning(bool running)
    {
        RunButton.IsEnabled = !running;
        RoundsTextBox.IsEnabled = !running;
        RunSecondsTextBox.IsEnabled = !running;
        SampleIntervalTextBox.IsEnabled = !running;
        StateTextBlock.Text =
            running ? "Running" : "Idle";

        if (!running)
        {
            ProgressTextBlock.Text = "0 / 0";
        }
    }

    private void Log(string message)
    {
        string line =
            $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        LogTextBox.AppendText(
            line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private static string FormatMiB(
        long? bytes)
    {
        return bytes.HasValue
            ? $"{bytes.Value / (1024d * 1024d):F1} MiB"
            : "n/a";
    }

    private static string FormatMs(
        double? milliseconds)
    {
        return milliseconds.HasValue
            ? $"{milliseconds.Value:F1} ms"
            : "n/a";
    }
}
