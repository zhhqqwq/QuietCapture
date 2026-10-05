using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G07WindowMonitorDpi;

public partial class G07ControllerWindow : Window
{
    private static readonly TimeSpan RecordingReadyTimeout =
        TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CompletionTimeout =
        TimeSpan.FromSeconds(45);

    private List<G07DisplaySnapshot> _displaySnapshots = new();
    private List<DisplayChoice> _displayChoices = new();

    public G07ControllerWindow()
    {
        InitializeComponent();

        EvidenceRootTextBox.Text = Path.Combine(
            Path.GetTempPath(),
            "QuietCaptureSpike",
            "G0-7");

        RefreshDisplays();
    }

    private void RefreshDisplaysButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        try
        {
            List<RecordableDisplay> recorderDisplays =
                Recorder.GetDisplays().ToList();

            _displaySnapshots =
                G07Interop.CaptureDisplays(
                    recorderDisplays).ToList();

            _displayChoices =
                _displaySnapshots
                    .Select(snapshot =>
                        new DisplayChoice(
                            snapshot,
                            BuildDisplayLabel(snapshot)))
                    .ToList();

            DisplayComboBox.ItemsSource =
                _displayChoices;
            DisplayComboBox.SelectedIndex =
                _displayChoices.Count > 0 ? 0 : -1;

            int negativeCount =
                _displaySnapshots.Count(display =>
                    display.HasNegativeCoordinates);

            DisplaySummaryTextBlock.Text =
                $"Displays: {_displaySnapshots.Count}; negative-origin monitors: {negativeCount}";

            RunButton.IsEnabled =
                _displayChoices.Count > 0;

            Log(
                $"Display snapshot: {_displaySnapshots.Count} monitor(s).");

            foreach (G07DisplaySnapshot display in _displaySnapshots)
            {
                Log(
                    $"Display {display.DeviceName}: bounds={FormatRect(display.Bounds)}; work={FormatRect(display.WorkArea)}; dpi={FormatDpi(display)}; primary={display.IsPrimary}; deviceId={display.DeviceInterfaceId ?? "n/a"}");
            }
        }
        catch (Exception ex)
        {
            _displaySnapshots.Clear();
            _displayChoices.Clear();
            DisplayComboBox.ItemsSource = null;
            DisplaySummaryTextBlock.Text =
                "Display enumeration failed";
            RunButton.IsEnabled = false;
            Log($"Display enumeration failed: {ex}");
        }
    }

    private async void RunButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DisplayComboBox.SelectedItem
            is not DisplayChoice selectedChoice)
        {
            Log("Select a display.");
            return;
        }

        if (!TryReadConfiguration(
                out G07CaptureMode mode,
                out G07DisplayCaptureApi displayApi,
                out G07WindowScript script,
                out int stepSeconds,
                out int noActionSeconds,
                out int areaX,
                out int areaY,
                out int areaWidth,
                out int areaHeight))
        {
            return;
        }

        if (mode != G07CaptureMode.Window &&
            script != G07WindowScript.NoActions)
        {
            Log(
                $"{mode} mode does not use window actions; script forced to NoActions.");
            script = G07WindowScript.NoActions;
        }

        SetRunning(true);

        string runId =
            $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{mode.ToString().ToLowerInvariant()}-{displayApi.ToString().ToLowerInvariant()}-{script.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}"[..63];

        string runDirectory =
            Path.Combine(
                EvidenceRootTextBox.Text,
                runId);
        Directory.CreateDirectory(runDirectory);

        string outputPath =
            Path.Combine(
                runDirectory,
                "capture.mp4");
        string recorderLogPath =
            Path.Combine(
                runDirectory,
                "recorder.log");
        string manifestPath =
            Path.Combine(
                runDirectory,
                "run-manifest.json");

        List<G07DisplaySnapshot> displaysBefore =
            G07Interop.CaptureDisplays(
                Recorder.GetDisplays()).ToList();

        G07DisplaySnapshot selectedDisplay =
            displaysBefore.FirstOrDefault(display =>
                string.Equals(
                    display.DeviceName,
                    selectedChoice.Snapshot.DeviceName,
                    StringComparison.OrdinalIgnoreCase))
            ?? selectedChoice.Snapshot;

        var manifest = new G07RunManifest
        {
            RunId = runId,
            CaptureMode = mode,
            WindowScript = script,
            DisplayCaptureApi = displayApi,
            StepSeconds = stepSeconds,
            NoActionRunSeconds = noActionSeconds,
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath,
            SelectedDisplayDeviceName =
                selectedDisplay.DeviceName,
            SelectedDisplayFriendlyName =
                selectedDisplay.RecorderFriendlyName,
            AreaX =
                mode == G07CaptureMode.Area
                    ? areaX
                    : null,
            AreaY =
                mode == G07CaptureMode.Area
                    ? areaY
                    : null,
            AreaWidth =
                mode == G07CaptureMode.Area
                    ? areaWidth
                    : null,
            AreaHeight =
                mode == G07CaptureMode.Area
                    ? areaHeight
                    : null
        };

        manifest.DisplaysBefore.AddRange(
            displaysBefore);

        if (mode == G07CaptureMode.Area)
        {
            manifest.ExpectedVirtualAreaRect =
                new G07PixelRect(
                    selectedDisplay.Bounds.Left + areaX,
                    selectedDisplay.Bounds.Top + areaY,
                    areaWidth,
                    areaHeight);
        }

        object manifestGate = new();
        var runClock = new Stopwatch();

        void SaveManifest(
            Action<G07RunManifest>? update = null)
        {
            lock (manifestGate)
            {
                update?.Invoke(manifest);
                manifest.Save(manifestPath);
            }
        }

        manifest.AddEvent(
            "RunCreated",
            $"mode={mode}; displayApi={displayApi}; script={script}");
        SaveManifest();

        G07TargetWindow? targetWindow = null;
        nint targetHwnd = 0;
        Recorder? recorder = null;

        var recordingReady =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingComplete =
            new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingFailed =
            new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            if (mode == G07CaptureMode.Window)
            {
                targetWindow = new G07TargetWindow();

                targetHwnd =
                    G07Interop.EnsureTargetHandle(
                        targetWindow);

                G07PixelRect initialRect =
                    ComputeInitialWindowRect(
                        selectedDisplay);

                G07Interop.MoveResizeWindow(
                    targetHwnd,
                    initialRect);

                targetWindow.Show();
                G07Interop.FlushDwm();

                G07WindowSnapshot targetSnapshot =
                    G07Interop.CaptureWindow(
                        targetHwnd,
                        displaysBefore);

                SaveManifest(m =>
                {
                    m.TargetWindowHwnd =
                        targetHwnd.ToInt64();
                    m.TargetWindowTitle =
                        targetWindow.Title;
                    m.AddEvent(
                        "TargetWindowShown",
                        $"hwnd=0x{targetHwnd.ToInt64():X}",
                        null,
                        targetSnapshot);
                });
            }

            RecordingSourceBase source =
                BuildRecordingSource(
                    mode,
                    displayApi,
                    selectedDisplay.DeviceName,
                    targetHwnd,
                    areaX,
                    areaY,
                    areaWidth,
                    areaHeight);

            var outputOptions =
                new OutputOptions
                {
                    RecorderMode =
                        RecorderMode.Video
                };

            if (mode == G07CaptureMode.Area)
            {
                outputOptions.OutputFrameSize =
                    new ScreenSize(
                        areaWidth,
                        areaHeight);
            }

            var options =
                new RecorderOptions
                {
                    SourceOptions =
                        new SourceOptions
                        {
                            RecordingSources =
                                new List<RecordingSourceBase>
                                {
                                    source
                                }
                        },
                    OutputOptions =
                        outputOptions,
                    AudioOptions =
                        new AudioOptions
                        {
                            IsAudioEnabled = false
                        },
                    MouseOptions =
                        new MouseOptions
                        {
                            IsMousePointerEnabled = true
                        },
                    VideoEncoderOptions =
                        new VideoEncoderOptions
                        {
                            Framerate = 30,
                            Bitrate = 8_000_000,
                            IsFixedFramerate = true,
                            IsHardwareEncodingEnabled = true,
                            IsFragmentedMp4Enabled = false,
                            IsMp4FastStartEnabled = true
                        },
                    LogOptions =
                        new LogOptions
                        {
                            IsLogEnabled = true,
                            LogFilePath = recorderLogPath,
                            LogSeverityLevel =
                                ScreenRecorderLib.LogLevel.Debug
                        }
                };

            recorder =
                Recorder.CreateRecorder(options);

            recorder.OnStatusChanged += (_, eventArgs) =>
            {
                double? elapsed =
                    runClock.IsRunning
                        ? runClock.Elapsed.TotalMilliseconds
                        : null;

                SaveManifest(m =>
                {
                    m.Status =
                        eventArgs.Status.ToString();

                    if (eventArgs.Status ==
                        RecorderStatus.Recording)
                    {
                        m.RecordingAtUtc =
                            DateTimeOffset.UtcNow;
                    }

                    m.AddEvent(
                        "RecorderStatus",
                        eventArgs.Status.ToString(),
                        elapsed,
                        targetHwnd != 0
                            ? G07Interop.CaptureWindow(
                                targetHwnd,
                                G07Interop.CaptureDisplays(
                                    Recorder.GetDisplays()))
                            : null);
                });

                if (eventArgs.Status ==
                    RecorderStatus.Recording)
                {
                    recordingReady.TrySetResult(true);
                }
            };

            recorder.OnRecordingComplete += (_, eventArgs) =>
            {
                SaveManifest(m =>
                {
                    m.Status = "Complete";
                    m.FinishedAtUtc =
                        DateTimeOffset.UtcNow;
                    m.AddEvent(
                        "RecordingComplete",
                        eventArgs.FilePath,
                        runClock.IsRunning
                            ? runClock.Elapsed.TotalMilliseconds
                            : null);
                });

                recordingComplete.TrySetResult(
                    eventArgs.FilePath);
            };

            recorder.OnRecordingFailed += (_, eventArgs) =>
            {
                SaveManifest(m =>
                {
                    m.Status = "RecordingFailed";
                    m.Error = eventArgs.Error;
                    m.FinishedAtUtc =
                        DateTimeOffset.UtcNow;
                    m.AddEvent(
                        "RecordingFailed",
                        eventArgs.Error,
                        runClock.IsRunning
                            ? runClock.Elapsed.TotalMilliseconds
                            : null);
                });

                recordingFailed.TrySetResult(
                    eventArgs.Error);
            };

            Log($"Run {runId}");
            Log(
                $"Mode={mode}; display={selectedDisplay.DeviceName}; API={displayApi}; script={script}");
            Log(
                $"Display bounds={FormatRect(selectedDisplay.Bounds)}; work={FormatRect(selectedDisplay.WorkArea)}; dpi={FormatDpi(selectedDisplay)}");

            if (mode == G07CaptureMode.Area)
            {
                Log(
                    $"Area source-local=({areaX},{areaY},{areaWidth},{areaHeight}); expected virtual={FormatRect(manifest.ExpectedVirtualAreaRect!)}");
            }

            Hide();
            G07Interop.FlushDwm();

            SaveManifest(m =>
            {
                m.RecordCalledAtUtc =
                    DateTimeOffset.UtcNow;
                m.Status = "Starting";
                m.AddEvent("RecordCalled");
            });

            recorder.Record(outputPath);

            Task readyTimeout =
                Task.Delay(
                    RecordingReadyTimeout);
            Task firstReady =
                await Task.WhenAny(
                    recordingReady.Task,
                    recordingFailed.Task,
                    readyTimeout);

            if (firstReady ==
                recordingFailed.Task)
            {
                Log(
                    $"Recording failed before ready: {await recordingFailed.Task}");
                return;
            }

            if (firstReady == readyTimeout)
            {
                SaveManifest(m =>
                {
                    m.Status =
                        "RecordingReadyTimeout";
                    m.Error =
                        $"Recorder did not enter Recording within {RecordingReadyTimeout.TotalSeconds:0} seconds.";
                    m.FinishedAtUtc =
                        DateTimeOffset.UtcNow;
                    m.AddEvent(
                        "RecordingReadyTimeout");
                });

                Log(
                    "Recorder did not reach Recording before timeout.");
                return;
            }

            runClock.Restart();

            SaveManifest(m =>
            {
                m.AddEvent(
                    "ActionClockStarted",
                    null,
                    0,
                    targetHwnd != 0
                        ? G07Interop.CaptureWindow(
                            targetHwnd,
                            displaysBefore)
                        : null);
            });

            if (mode ==
                G07CaptureMode.Window)
            {
                await ExecuteWindowScriptAsync(
                    script,
                    targetWindow!,
                    targetHwnd,
                    selectedDisplay,
                    displaysBefore,
                    stepSeconds,
                    runClock,
                    manifest,
                    SaveManifest,
                    recordingFailed,
                    recordingComplete);
            }
            else
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        noActionSeconds));
            }

            if (!recordingFailed.Task.IsCompleted &&
                !recordingComplete.Task.IsCompleted)
            {
                SaveManifest(m =>
                {
                    m.StopRequestedAtUtc =
                        DateTimeOffset.UtcNow;
                    m.Status = "Stopping";
                    m.AddEvent(
                        "StopRequested",
                        null,
                        runClock.Elapsed.TotalMilliseconds,
                        targetHwnd != 0
                            ? G07Interop.CaptureWindow(
                                targetHwnd,
                                G07Interop.CaptureDisplays(
                                    Recorder.GetDisplays()))
                            : null);
                });

                recorder.Stop();
            }

            Task completionTimeout =
                Task.Delay(
                    CompletionTimeout);

            Task firstCompletion =
                await Task.WhenAny(
                    recordingComplete.Task,
                    recordingFailed.Task,
                    completionTimeout);

            if (firstCompletion ==
                recordingFailed.Task)
            {
                Log(
                    $"Recording failed: {await recordingFailed.Task}");
            }
            else if (firstCompletion ==
                completionTimeout)
            {
                SaveManifest(m =>
                {
                    m.Status =
                        "CompletionTimeout";
                    m.Error =
                        $"Recorder did not finish within {CompletionTimeout.TotalSeconds:0} seconds.";
                    m.FinishedAtUtc =
                        DateTimeOffset.UtcNow;
                    m.AddEvent(
                        "CompletionTimeout",
                        null,
                        runClock.Elapsed.TotalMilliseconds);
                });

                Log(
                    "Recording completion timed out.");
            }
            else
            {
                Log(
                    $"Recording complete: {await recordingComplete.Task}");
            }
        }
        catch (Exception ex)
        {
            SaveManifest(m =>
            {
                m.Status =
                    "ControllerException";
                m.Error =
                    ex.ToString();
                m.FinishedAtUtc =
                    DateTimeOffset.UtcNow;
                m.AddEvent(
                    "ControllerException",
                    ex.Message,
                    runClock.IsRunning
                        ? runClock.Elapsed.TotalMilliseconds
                        : null);
            });

            Log($"G0-7 run failed: {ex}");
        }
        finally
        {
            try
            {
                if (targetWindow?.IsLoaded == true)
                {
                    targetWindow.Close();
                }
            }
            finally
            {
                if (recorder is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                List<G07DisplaySnapshot> displaysAfter;

                try
                {
                    displaysAfter =
                        G07Interop.CaptureDisplays(
                            Recorder.GetDisplays())
                        .ToList();
                }
                catch (Exception snapshotException)
                {
                    displaysAfter =
                        new List<G07DisplaySnapshot>();

                    SaveManifest(m =>
                        m.AddEvent(
                            "FinalDisplaySnapshotFailed",
                            snapshotException.Message,
                            runClock.IsRunning
                                ? runClock.Elapsed.TotalMilliseconds
                                : null));
                }

                SaveManifest(m =>
                {
                    m.DisplaysAfter.Clear();
                    m.DisplaysAfter.AddRange(
                        displaysAfter);
                    m.ObserveOutput();

                    if (m.FinishedAtUtc is null)
                    {
                        m.FinishedAtUtc =
                            DateTimeOffset.UtcNow;
                    }

                    m.AddEvent(
                        "RunFinalized",
                        $"outputExists={m.OutputExistsAfterRun}; outputBytes={m.OutputBytesAfterRun}; displaysAfter={displaysAfter.Count}",
                        runClock.IsRunning
                            ? runClock.Elapsed.TotalMilliseconds
                            : null);
                });

                if (!IsVisible)
                {
                    Show();
                }

                Activate();
                SetRunning(false);

                Log(
                    $"Final output: exists={manifest.OutputExistsAfterRun}; bytes={manifest.OutputBytesAfterRun}");
                Log(
                    $"Manifest: {manifestPath}");
            }
        }
    }

    private static async Task ExecuteWindowScriptAsync(
        G07WindowScript script,
        G07TargetWindow targetWindow,
        nint targetHwnd,
        G07DisplaySnapshot selectedDisplay,
        IReadOnlyList<G07DisplaySnapshot> displays,
        int stepSeconds,
        Stopwatch clock,
        G07RunManifest manifest,
        Action<Action<G07RunManifest>?> saveManifest,
        TaskCompletionSource<string> recordingFailed,
        TaskCompletionSource<string> recordingComplete)
    {
        if (script ==
            G07WindowScript.NoActions)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(
                    manifest.NoActionRunSeconds));
            return;
        }

        var actions =
            BuildActions(
                script,
                targetWindow,
                targetHwnd,
                selectedDisplay,
                displays,
                stepSeconds);

        foreach (ScheduledAction action in actions)
        {
            if (recordingFailed.Task.IsCompleted ||
                recordingComplete.Task.IsCompleted)
            {
                saveManifest(m =>
                    m.AddEvent(
                        "ScriptAborted",
                        "Recorder completed or failed before all actions ran.",
                        clock.Elapsed.TotalMilliseconds,
                        G07Interop.CaptureWindow(
                            targetHwnd,
                            G07Interop.CaptureDisplays(
                                Recorder.GetDisplays()))));
                break;
            }

            TimeSpan due =
                TimeSpan.FromSeconds(
                    action.ScheduledOffsetSeconds);
            TimeSpan remaining =
                due - clock.Elapsed;

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }

            IReadOnlyList<G07DisplaySnapshot> currentDisplays =
                G07Interop.CaptureDisplays(
                    Recorder.GetDisplays());

            var observation =
                new G07ActionObservation
                {
                    Action = action.Name,
                    ScheduledOffsetSeconds =
                        action.ScheduledOffsetSeconds,
                    ActualOffsetSeconds =
                        clock.Elapsed.TotalSeconds,
                    Before =
                        G07Interop.CaptureWindow(
                            targetHwnd,
                            currentDisplays)
                };

            try
            {
                action.Execute();
                G07Interop.FlushDwm();

                currentDisplays =
                    G07Interop.CaptureDisplays(
                        Recorder.GetDisplays());

                observation.After =
                    G07Interop.CaptureWindow(
                        targetHwnd,
                        currentDisplays);
                observation.Status =
                    action.StatusAfterExecution;
                observation.Detail =
                    action.Detail;

                saveManifest(m =>
                {
                    m.Actions.Add(observation);
                    m.AddEvent(
                        $"Action:{action.Name}",
                        observation.Detail,
                        clock.Elapsed.TotalMilliseconds,
                        observation.After);
                });
            }
            catch (Exception ex)
            {
                observation.Status = "Failed";
                observation.Detail =
                    ex.ToString();

                saveManifest(m =>
                {
                    m.Actions.Add(observation);
                    m.AddEvent(
                        $"ActionFailed:{action.Name}",
                        ex.Message,
                        clock.Elapsed.TotalMilliseconds,
                        G07Interop.CaptureWindow(
                            targetHwnd,
                            currentDisplays));
                });

                break;
            }
        }

        await Task.Delay(
            TimeSpan.FromSeconds(
                stepSeconds));
    }

    private static IReadOnlyList<ScheduledAction> BuildActions(
        G07WindowScript script,
        G07TargetWindow targetWindow,
        nint targetHwnd,
        G07DisplaySnapshot selectedDisplay,
        IReadOnlyList<G07DisplaySnapshot> displays,
        int stepSeconds)
    {
        if (script ==
            G07WindowScript.Lifecycle)
        {
            G07PixelRect moveRect =
                ComputeMoveRect(
                    selectedDisplay);

            G07PixelRect resizeRect =
                ComputeResizeRect(
                    selectedDisplay);

            return new[]
            {
                new ScheduledAction(
                    "Move",
                    stepSeconds,
                    () =>
                        G07Interop.MoveResizeWindow(
                            targetHwnd,
                            moveRect),
                    "Executed",
                    $"rect={FormatRect(moveRect)}"),

                new ScheduledAction(
                    "Resize",
                    stepSeconds * 2d,
                    () =>
                        G07Interop.MoveResizeWindow(
                            targetHwnd,
                            resizeRect),
                    "Executed",
                    $"rect={FormatRect(resizeRect)}"),

                new ScheduledAction(
                    "Minimize",
                    stepSeconds * 3d,
                    () =>
                        G07Interop.MinimizeWindow(
                            targetHwnd),
                    "Executed",
                    null),

                new ScheduledAction(
                    "Restore",
                    stepSeconds * 4d,
                    () =>
                        G07Interop.RestoreWindow(
                            targetHwnd),
                    "Executed",
                    null),

                new ScheduledAction(
                    "Destroy",
                    stepSeconds * 5d,
                    targetWindow.Close,
                    "Executed",
                    "WPF target Close() called")
            };
        }

        G07DisplaySnapshot? alternate =
            displays.FirstOrDefault(display =>
                !string.Equals(
                    display.DeviceName,
                    selectedDisplay.DeviceName,
                    StringComparison.OrdinalIgnoreCase));

        if (alternate is null)
        {
            return new[]
            {
                new ScheduledAction(
                    "MoveBetweenMonitors",
                    stepSeconds,
                    () => { },
                    "Skipped",
                    "Only one display is available.")
            };
        }

        G07PixelRect alternateRect =
            ComputeMoveRect(
                alternate);
        G07PixelRect returnRect =
            ComputeMoveRect(
                selectedDisplay);

        return new[]
        {
            new ScheduledAction(
                "MoveToAlternateMonitor",
                stepSeconds,
                () =>
                    G07Interop.MoveResizeWindow(
                        targetHwnd,
                        alternateRect),
                "Executed",
                $"target={alternate.DeviceName}; rect={FormatRect(alternateRect)}"),

            new ScheduledAction(
                "MoveBackToSelectedMonitor",
                stepSeconds * 2d,
                () =>
                    G07Interop.MoveResizeWindow(
                        targetHwnd,
                        returnRect),
                "Executed",
                $"target={selectedDisplay.DeviceName}; rect={FormatRect(returnRect)}")
        };
    }

    private static RecordingSourceBase BuildRecordingSource(
        G07CaptureMode mode,
        G07DisplayCaptureApi displayApi,
        string deviceName,
        nint targetHwnd,
        int areaX,
        int areaY,
        int areaWidth,
        int areaHeight)
    {
        if (mode ==
            G07CaptureMode.Window)
        {
            if (targetHwnd == 0)
            {
                throw new InvalidOperationException(
                    "Target window HWND is unavailable.");
            }

            return new WindowRecordingSource(
                targetHwnd)
            {
                IsCursorCaptureEnabled = true,
                IsBorderRequired = false
            };
        }

        var displaySource =
            new DisplayRecordingSource(
                deviceName)
            {
                RecorderApi =
                    displayApi ==
                    G07DisplayCaptureApi.DesktopDuplication
                        ? RecorderApi.DesktopDuplication
                        : RecorderApi.WindowsGraphicsCapture,
                IsCursorCaptureEnabled = true,
                IsBorderRequired = false
            };

        if (mode ==
            G07CaptureMode.Area)
        {
            displaySource.SourceRect =
                new ScreenRect(
                    areaX,
                    areaY,
                    areaWidth,
                    areaHeight);
            displaySource.OutputSize =
                new ScreenSize(
                    areaWidth,
                    areaHeight);
        }

        return displaySource;
    }

    private bool TryReadConfiguration(
        out G07CaptureMode mode,
        out G07DisplayCaptureApi displayApi,
        out G07WindowScript script,
        out int stepSeconds,
        out int noActionSeconds,
        out int areaX,
        out int areaY,
        out int areaWidth,
        out int areaHeight)
    {
        mode =
            ParseEnumCombo(
                CaptureModeComboBox,
                G07CaptureMode.Window);

        displayApi =
            ParseEnumCombo(
                DisplayApiComboBox,
                G07DisplayCaptureApi.DesktopDuplication);

        script =
            ParseEnumCombo(
                WindowScriptComboBox,
                G07WindowScript.NoActions);

        stepSeconds =
            noActionSeconds =
            areaX =
            areaY =
            areaWidth =
            areaHeight = 0;

        if (!int.TryParse(
                StepSecondsTextBox.Text,
                out stepSeconds) ||
            stepSeconds < 1 ||
            stepSeconds > 60)
        {
            Log(
                "Step seconds must be an integer from 1 to 60.");
            return false;
        }

        if (!int.TryParse(
                NoActionSecondsTextBox.Text,
                out noActionSeconds) ||
            noActionSeconds < 1 ||
            noActionSeconds > 300)
        {
            Log(
                "No-action seconds must be an integer from 1 to 300.");
            return false;
        }

        bool areaValid =
            int.TryParse(
                AreaXTextBox.Text,
                out areaX) &&
            int.TryParse(
                AreaYTextBox.Text,
                out areaY) &&
            int.TryParse(
                AreaWidthTextBox.Text,
                out areaWidth) &&
            int.TryParse(
                AreaHeightTextBox.Text,
                out areaHeight);

        if (!areaValid)
        {
            Log(
                "Area X, Y, Width, and Height must be integers.");
            return false;
        }

        if (mode ==
            G07CaptureMode.Area)
        {
            if (areaWidth <= 0 ||
                areaHeight <= 0)
            {
                Log(
                    "Area Width and Height must be positive.");
                return false;
            }

            if ((areaWidth & 1) != 0 ||
                (areaHeight & 1) != 0)
            {
                Log(
                    "Area Width and Height must be even for H.264.");
                return false;
            }
        }

        return true;
    }

    private static T ParseEnumCombo<T>(
        ComboBox comboBox,
        T fallback)
        where T : struct, Enum
    {
        if (comboBox.SelectedItem
                is ComboBoxItem item &&
            Enum.TryParse(
                item.Content?.ToString(),
                ignoreCase: true,
                out T value))
        {
            return value;
        }

        return fallback;
    }

    private static G07PixelRect ComputeInitialWindowRect(
        G07DisplaySnapshot display)
    {
        int width =
            Math.Min(
                900,
                Math.Max(
                    480,
                    display.WorkArea.Width - 160));

        int height =
            Math.Min(
                560,
                Math.Max(
                    320,
                    display.WorkArea.Height - 160));

        return new G07PixelRect(
            display.WorkArea.Left +
                Math.Max(
                    40,
                    (display.WorkArea.Width - width) / 2),
            display.WorkArea.Top +
                Math.Max(
                    40,
                    (display.WorkArea.Height - height) / 2),
            width,
            height);
    }

    private static G07PixelRect ComputeMoveRect(
        G07DisplaySnapshot display)
    {
        int width =
            Math.Min(
                760,
                Math.Max(
                    420,
                    display.WorkArea.Width - 220));

        int height =
            Math.Min(
                480,
                Math.Max(
                    280,
                    display.WorkArea.Height - 220));

        return new G07PixelRect(
            display.WorkArea.Left + 80,
            display.WorkArea.Top + 80,
            width,
            height);
    }

    private static G07PixelRect ComputeResizeRect(
        G07DisplaySnapshot display)
    {
        int width =
            Math.Min(
                Math.Max(
                    560,
                    display.WorkArea.Width * 2 / 3),
                Math.Max(
                    560,
                    display.WorkArea.Width - 120));

        int height =
            Math.Min(
                Math.Max(
                    360,
                    display.WorkArea.Height * 2 / 3),
                Math.Max(
                    360,
                    display.WorkArea.Height - 120));

        return new G07PixelRect(
            display.WorkArea.Left + 60,
            display.WorkArea.Top + 60,
            width,
            height);
    }

    private void SetRunning(
        bool running)
    {
        RunButton.IsEnabled =
            !running &&
            _displayChoices.Count > 0;
        CaptureModeComboBox.IsEnabled =
            !running;
        DisplayApiComboBox.IsEnabled =
            !running;
        DisplayComboBox.IsEnabled =
            !running;
        WindowScriptComboBox.IsEnabled =
            !running;
        StepSecondsTextBox.IsEnabled =
            !running;
        NoActionSecondsTextBox.IsEnabled =
            !running;
        AreaXTextBox.IsEnabled =
            !running;
        AreaYTextBox.IsEnabled =
            !running;
        AreaWidthTextBox.IsEnabled =
            !running;
        AreaHeightTextBox.IsEnabled =
            !running;
        StateTextBlock.Text =
            running ? "Running" : "Idle";
    }

    private void Log(
        string message)
    {
        string line =
            $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        LogTextBox.AppendText(
            line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private static string BuildDisplayLabel(
        G07DisplaySnapshot display)
    {
        string name =
            string.IsNullOrWhiteSpace(
                display.RecorderFriendlyName)
                ? display.DeviceName
                : $"{display.RecorderFriendlyName} ({display.DeviceName})";

        return $"{name}  bounds={FormatRect(display.Bounds)}  dpi={FormatDpi(display)}";
    }

    private static string FormatDpi(
        G07DisplaySnapshot display)
    {
        return display.DpiX.HasValue &&
            display.DpiY.HasValue
            ? $"{display.DpiX}x{display.DpiY} ({display.ScalePercentX:F0}%)"
            : "n/a";
    }

    private static string FormatRect(
        G07PixelRect rect)
    {
        return $"({rect.Left},{rect.Top},{rect.Width},{rect.Height})";
    }

    private sealed record DisplayChoice(
        G07DisplaySnapshot Snapshot,
        string Label);

    private sealed record ScheduledAction(
        string Name,
        double ScheduledOffsetSeconds,
        Action Execute,
        string StatusAfterExecution,
        string? Detail);
}
