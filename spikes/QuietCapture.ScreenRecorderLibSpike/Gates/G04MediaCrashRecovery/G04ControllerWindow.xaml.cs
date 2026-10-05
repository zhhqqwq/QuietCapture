using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G04MediaCrashRecovery;

public partial class G04ControllerWindow : Window
{
    private static readonly TimeSpan WorkerReadyTimeout = TimeSpan.FromSeconds(20);

    public G04ControllerWindow()
    {
        InitializeComponent();
        EvidenceRootTextBox.Text = Path.Combine(
            Path.GetTempPath(),
            "QuietCaptureSpike",
            "G0-4");
    }

    private async void NormalStopButton_Click(object sender, RoutedEventArgs e)
    {
        await RunExperimentAsync(G04TerminationMode.NormalStop);
    }

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        await RunExperimentAsync(G04TerminationMode.Kill);
    }

    private async Task RunExperimentAsync(G04TerminationMode mode)
    {
        if (!TryReadConfiguration(out bool fragmented, out bool fixedFramerate, out int runSeconds))
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
        string? entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;

        if (launchedThroughDotnetHost && string.IsNullOrWhiteSpace(entryAssemblyPath))
        {
            Log("Current entry assembly path is unavailable; cannot launch Worker through dotnet host.");
            return;
        }

        SetRunning(true);

        string runId =
            $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{mode.ToString().ToLowerInvariant()}-frag{BoolDigit(fragmented)}-fixed{BoolDigit(fixedFramerate)}-{Guid.NewGuid():N}"[..63];

        string evidenceRoot = EvidenceRootTextBox.Text;
        string runDirectory = Path.Combine(evidenceRoot, runId);
        Directory.CreateDirectory(runDirectory);

        string controllerManifestPath = Path.Combine(runDirectory, "controller-manifest.json");
        string workerManifestPath = Path.Combine(runDirectory, "worker-manifest.json");
        string outputPath = Path.Combine(runDirectory, "recording.partial.mp4");
        string recorderLogPath = Path.Combine(runDirectory, "recorder.log");
        string readyFlagPath = Path.Combine(runDirectory, "recording-ready.flag");
        string workerStdoutPath = Path.Combine(runDirectory, "worker.stdout.log");
        string workerStderrPath = Path.Combine(runDirectory, "worker.stderr.log");

        var manifest = new G04ControllerManifest
        {
            RunId = runId,
            TerminationMode = mode,
            FragmentedMp4 = fragmented,
            FixedFramerate = fixedFramerate,
            RunSeconds = runSeconds,
            RunDirectory = runDirectory,
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath,
            WorkerManifestPath = workerManifestPath,
            ReadyFlagPath = readyFlagPath
        };
        manifest.AddEvent("ControllerRunCreated");
        manifest.Save(controllerManifestPath);

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

            startInfo.ArgumentList.Add("--gate=g0-4-worker");
            startInfo.ArgumentList.Add($"--run-id={runId}");
            startInfo.ArgumentList.Add($"--run-dir={runDirectory}");
            startInfo.ArgumentList.Add($"--termination-mode={mode}");
            startInfo.ArgumentList.Add($"--fragmented={fragmented}");
            startInfo.ArgumentList.Add($"--fixed-fps={fixedFramerate}");
            startInfo.ArgumentList.Add($"--run-seconds={runSeconds}");

            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Worker process did not start.");
            }

            manifest.WorkerProcessId = process.Id;
            manifest.WorkerStartedAtUtc = DateTimeOffset.UtcNow;
            manifest.Status = "WorkerStarted";
            manifest.AddEvent("WorkerStarted", $"pid={process.Id}");
            manifest.Save(controllerManifestPath);

            Log($"Run {runId}");
            Log($"Worker PID: {process.Id}");
            Log($"Config: fragmented={fragmented}; fixedFps={fixedFramerate}; mode={mode}; seconds={runSeconds}");
            Log($"Evidence: {runDirectory}");

            Task stdoutTask = DrainAsync(process.StandardOutput, workerStdoutPath);
            Task stderrTask = DrainAsync(process.StandardError, workerStderrPath);

            bool ready = await WaitForReadyAsync(process, readyFlagPath, WorkerReadyTimeout);
            if (!ready)
            {
                manifest.Status = process.HasExited ? "WorkerExitedBeforeReady" : "WorkerReadyTimeout";
                manifest.Error = process.HasExited
                    ? $"Worker exited before Recording. Exit code: {process.ExitCode}."
                    : $"Worker did not create recording-ready.flag within {WorkerReadyTimeout.TotalSeconds:0} seconds.";
                manifest.AddEvent(manifest.Status, manifest.Error);
                manifest.Save(controllerManifestPath);

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    manifest.WorkerWasKilled = true;
                    manifest.TerminationRequestedAtUtc = DateTimeOffset.UtcNow;
                    manifest.AddEvent("ControllerKilledUnreadyWorker");
                    await process.WaitForExitAsync();
                }

                await Task.WhenAll(stdoutTask, stderrTask);
                FinalizeObservation(manifest, process, controllerManifestPath);
                return;
            }

            manifest.WorkerReadyAtUtc = DateTimeOffset.UtcNow;
            manifest.Status = "Recording";
            manifest.AddEvent("WorkerReady");
            manifest.Save(controllerManifestPath);
            StateTextBlock.Text = "Recording";
            Log("Worker reached RecorderStatus.Recording.");

            if (mode == G04TerminationMode.Kill)
            {
                await Task.Delay(TimeSpan.FromSeconds(runSeconds));

                if (!process.HasExited)
                {
                    manifest.TerminationRequestedAtUtc = DateTimeOffset.UtcNow;
                    manifest.Status = "KillingWorker";
                    manifest.AddEvent("KillRequested");
                    manifest.Save(controllerManifestPath);

                    Log("Terminating Worker process.");
                    process.Kill(entireProcessTree: true);
                    manifest.WorkerWasKilled = true;
                }

                await process.WaitForExitAsync();
            }
            else
            {
                TimeSpan normalWait = TimeSpan.FromSeconds(runSeconds + 70);
                Task exitTask = process.WaitForExitAsync();
                Task timeoutTask = Task.Delay(normalWait);
                Task first = await Task.WhenAny(exitTask, timeoutTask);

                if (first == timeoutTask && !process.HasExited)
                {
                    manifest.Status = "ControllerWaitTimeout";
                    manifest.Error = $"Worker did not exit within {normalWait.TotalSeconds:0} seconds.";
                    manifest.TerminationRequestedAtUtc = DateTimeOffset.UtcNow;
                    manifest.AddEvent("ControllerWaitTimeout", manifest.Error);
                    manifest.Save(controllerManifestPath);

                    process.Kill(entireProcessTree: true);
                    manifest.WorkerWasKilled = true;
                    await process.WaitForExitAsync();
                }
            }

            await Task.WhenAll(stdoutTask, stderrTask);
            FinalizeObservation(manifest, process, controllerManifestPath);

            Log($"Worker exit code: {manifest.WorkerExitCode}");
            Log($"Output exists: {manifest.OutputExistsAfterExit}; bytes={manifest.OutputBytesAfterExit}");
            Log($"Controller manifest: {controllerManifestPath}");
        }
        catch (Exception ex)
        {
            manifest.Status = "ControllerException";
            manifest.Error = ex.ToString();
            manifest.AddEvent("ControllerException", ex.Message);

            if (process is { HasExited: false })
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    manifest.WorkerWasKilled = true;
                    manifest.TerminationRequestedAtUtc ??= DateTimeOffset.UtcNow;
                    await process.WaitForExitAsync();
                }
                catch (Exception killException)
                {
                    manifest.AddEvent("CleanupKillFailed", killException.Message);
                }
            }

            if (process is not null)
            {
                FinalizeObservation(manifest, process, controllerManifestPath);
            }
            else
            {
                ObserveOutput(manifest);
                manifest.Save(controllerManifestPath);
            }

            Log($"Controller failed: {ex}");
        }
        finally
        {
            process?.Dispose();
            SetRunning(false);
        }
    }

    private static async Task DrainAsync(StreamReader reader, string outputPath)
    {
        string text = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(outputPath, text);
    }

    private static async Task<bool> WaitForReadyAsync(Process process, string readyFlagPath, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

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

    private static void FinalizeObservation(
        G04ControllerManifest manifest,
        Process process,
        string controllerManifestPath)
    {
        manifest.WorkerExitedAtUtc = DateTimeOffset.UtcNow;
        manifest.WorkerExitCode = process.HasExited ? process.ExitCode : null;

        ObserveOutput(manifest);

        if (manifest.Status is not "ControllerException" and not "ControllerWaitTimeout")
        {
            manifest.Status = manifest.WorkerWasKilled
                ? "WorkerKilled"
                : manifest.WorkerExitCode == 0
                    ? "WorkerCompleted"
                    : "WorkerExitedWithError";
        }

        manifest.AddEvent(
            "WorkerExited",
            $"exitCode={manifest.WorkerExitCode}; killed={manifest.WorkerWasKilled}; outputBytes={manifest.OutputBytesAfterExit}");
        manifest.Save(controllerManifestPath);
    }

    private static void ObserveOutput(G04ControllerManifest manifest)
    {
        var file = new FileInfo(manifest.OutputPath);
        manifest.OutputExistsAfterExit = file.Exists;
        manifest.OutputBytesAfterExit = file.Exists ? file.Length : 0;
    }

    private bool TryReadConfiguration(out bool fragmented, out bool fixedFramerate, out int runSeconds)
    {
        fragmented = ReadBoolCombo(FragmentedComboBox);
        fixedFramerate = ReadBoolCombo(FixedFramerateComboBox);

        if (!int.TryParse(RunSecondsTextBox.Text, out runSeconds) || runSeconds < 1 || runSeconds > 3600)
        {
            Log("Record seconds must be an integer from 1 to 3600.");
            return false;
        }

        return true;
    }

    private static bool ReadBoolCombo(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem item &&
            bool.TryParse(item.Content?.ToString(), out bool value))
        {
            return value;
        }

        return false;
    }

    private void SetRunning(bool running)
    {
        NormalStopButton.IsEnabled = !running;
        KillButton.IsEnabled = !running;
        FragmentedComboBox.IsEnabled = !running;
        FixedFramerateComboBox.IsEnabled = !running;
        RunSecondsTextBox.IsEnabled = !running;
        StateTextBlock.Text = running ? "Starting" : "Idle";
    }

    private void Log(string message)
    {
        string line = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private static int BoolDigit(bool value) => value ? 1 : 0;
}
