using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G05CaptureExclusion;

public partial class G05ControllerWindow : Window
{
    private static readonly TimeSpan RecordingReadyTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(45);

    private RecordableDisplay? _primaryDisplay;

    public G05ControllerWindow()
    {
        InitializeComponent();

        EvidenceRootTextBox.Text = Path.Combine(
            Path.GetTempPath(),
            "QuietCaptureSpike",
            "G0-5");

        RefreshPrimaryDisplay();
    }

    private void RefreshDisplayButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPrimaryDisplay();
    }

    private void RefreshPrimaryDisplay()
    {
        try
        {
            List<RecordableDisplay> displays = Recorder.GetDisplays().ToList();
            string? mainDeviceName = DisplayRecordingSource.MainMonitor?.DeviceName;

            _primaryDisplay = displays.FirstOrDefault(display =>
                string.Equals(
                    display.DeviceName,
                    mainDeviceName,
                    StringComparison.OrdinalIgnoreCase))
                ?? displays.FirstOrDefault();

            DisplayTextBox.Text = _primaryDisplay is null
                ? "No display available"
                : $"{_primaryDisplay.FriendlyName} ({_primaryDisplay.DeviceName})";

            RunButton.IsEnabled = _primaryDisplay is not null;
            Log($"Primary display: {DisplayTextBox.Text}");
        }
        catch (Exception ex)
        {
            _primaryDisplay = null;
            DisplayTextBox.Text = "Display enumeration failed";
            RunButton.IsEnabled = false;
            Log($"Display enumeration failed: {ex}");
        }
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_primaryDisplay is null)
        {
            Log("No primary display is available.");
            return;
        }

        if (!TryReadConfiguration(
                out G05CaptureMode mode,
                out bool exclusionRequested,
                out int runSeconds,
                out int areaX,
                out int areaY,
                out int areaWidth,
                out int areaHeight))
        {
            return;
        }

        SetRunning(true);

        string runId =
            $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{mode.ToString().ToLowerInvariant()}-exclude{(exclusionRequested ? 1 : 0)}-{Guid.NewGuid():N}"[..63];

        string runDirectory = Path.Combine(
            EvidenceRootTextBox.Text,
            runId);
        Directory.CreateDirectory(runDirectory);

        string outputPath = Path.Combine(runDirectory, "capture.mp4");
        string recorderLogPath = Path.Combine(runDirectory, "recorder.log");
        string manifestPath = Path.Combine(runDirectory, "run-manifest.json");
        string referenceScreenshotPath = Path.Combine(
            runDirectory,
            "reference-screen.png");

        var manifest = new G05RunManifest
        {
            RunId = runId,
            CaptureMode = mode,
            ExclusionRequested = exclusionRequested,
            RunSeconds = runSeconds,
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath,
            ReferenceScreenshotPath = referenceScreenshotPath,
            DisplayDeviceName = _primaryDisplay.DeviceName,
            DisplayFriendlyName = _primaryDisplay.FriendlyName,
            AreaX = mode == G05CaptureMode.Area ? areaX : null,
            AreaY = mode == G05CaptureMode.Area ? areaY : null,
            AreaWidth = mode == G05CaptureMode.Area ? areaWidth : null,
            AreaHeight = mode == G05CaptureMode.Area ? areaHeight : null
        };

        object manifestGate = new();

        void SaveManifest(Action<G05RunManifest>? update = null)
        {
            lock (manifestGate)
            {
                update?.Invoke(manifest);
                manifest.Save(manifestPath);
            }
        }

        manifest.AddEvent("RunCreated");
        SaveManifest();

        G05StatusWindow? statusWindow = null;
        G05RecordingBorderWindow? borderWindow = null;
        G05CaptureTargetWindow? targetWindow = null;
        Recorder? recorder = null;

        try
        {
            statusWindow = new G05StatusWindow();
            borderWindow = new G05RecordingBorderWindow();
            PositionOverlays(statusWindow, borderWindow, mode);

            G05WindowPreparationResult statusPreparation =
                G05CaptureExclusionInterop.Prepare(
                    statusWindow,
                    "StatusWindow",
                    exclusionRequested);

            G05WindowPreparationResult borderPreparation =
                G05CaptureExclusionInterop.Prepare(
                    borderWindow,
                    "RecordingBorderWindow",
                    exclusionRequested);

            SaveManifest(m =>
            {
                m.StatusWindowPreparation = statusPreparation;
                m.BorderWindowPreparation = borderPreparation;
                m.Status = "OverlaysPrepared";
                m.AddEvent(
                    "OverlaysPrepared",
                    $"statusReady={statusPreparation.IsReady}; borderReady={borderPreparation.IsReady}");
            });

            if (!statusPreparation.IsReady || !borderPreparation.IsReady)
            {
                SaveManifest(m =>
                {
                    m.Status = "OverlayPreparationFailed";
                    m.Error =
                        $"Status ready={statusPreparation.IsReady}; Border ready={borderPreparation.IsReady}.";
                    m.FinishedAtUtc = DateTimeOffset.UtcNow;
                    m.AddEvent("OverlayPreparationFailed", m.Error);
                });

                Log("Overlay preparation failed; recording was not started.");
                return;
            }

            nint targetWindowHwnd = 0;

            if (mode == G05CaptureMode.Window)
            {
                targetWindow = new G05CaptureTargetWindow
                {
                    Left = 120,
                    Top = 120
                };

                targetWindowHwnd =
                    new WindowInteropHelper(targetWindow).EnsureHandle();

                targetWindow.Show();
                G05CaptureExclusionInterop.FlushDwm();

                SaveManifest(m =>
                {
                    m.TargetWindowHwnd = targetWindowHwnd.ToInt64();
                    m.TargetWindowTitle = targetWindow.Title;
                    m.AddEvent(
                        "TargetWindowShown",
                        $"hwnd=0x{targetWindowHwnd.ToInt64():X}");
                });

                PositionOverlays(
                    statusWindow,
                    borderWindow,
                    mode,
                    targetWindow);
            }

            RecordingSourceBase source = BuildRecordingSource(
                mode,
                _primaryDisplay,
                targetWindowHwnd,
                areaX,
                areaY,
                areaWidth,
                areaHeight);

            var outputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video
            };

            if (mode == G05CaptureMode.Area)
            {
                outputOptions.OutputFrameSize =
                    new ScreenSize(areaWidth, areaHeight);
            }

            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources =
                        new List<RecordingSourceBase> { source }
                },
                OutputOptions = outputOptions,
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

            var recordingReady =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingComplete =
                new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingFailed =
                new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            recorder = Recorder.CreateRecorder(options);

            recorder.OnStatusChanged += (_, eventArgs) =>
            {
                SaveManifest(m =>
                {
                    m.Status = eventArgs.Status.ToString();
                    m.AddEvent(
                        "StatusChanged",
                        eventArgs.Status.ToString());

                    if (eventArgs.Status == RecorderStatus.Recording)
                    {
                        m.RecordingAtUtc = DateTimeOffset.UtcNow;
                    }
                });

                if (eventArgs.Status == RecorderStatus.Recording)
                {
                    recordingReady.TrySetResult(true);
                }
            };

            recorder.OnRecordingComplete += (_, eventArgs) =>
            {
                SaveManifest(m =>
                {
                    m.Status = "Complete";
                    m.FinishedAtUtc = DateTimeOffset.UtcNow;
                    m.AddEvent(
                        "RecordingComplete",
                        eventArgs.FilePath);
                });

                recordingComplete.TrySetResult(eventArgs.FilePath);
            };

            recorder.OnRecordingFailed += (_, eventArgs) =>
            {
                SaveManifest(m =>
                {
                    m.Status = "RecordingFailed";
                    m.Error = eventArgs.Error;
                    m.FinishedAtUtc = DateTimeOffset.UtcNow;
                    m.AddEvent(
                        "RecordingFailed",
                        eventArgs.Error);
                });

                recordingFailed.TrySetResult(eventArgs.Error);
            };

            Log($"Run {runId}");
            Log($"Mode={mode}; exclusion={exclusionRequested}; seconds={runSeconds}");
            Log($"Output={outputPath}");
            Log("Overlay HWNDs prepared before Show.");

            Hide();
            G05CaptureExclusionInterop.FlushDwm();

            SaveManifest(m =>
            {
                m.RecordCalledAtUtc = DateTimeOffset.UtcNow;
                m.Status = "Starting";
                m.AddEvent("RecordCalled");
            });

            recorder.Record(outputPath);

            Task readyTimeout =
                Task.Delay(RecordingReadyTimeout);
            Task firstReady = await Task.WhenAny(
                recordingReady.Task,
                recordingFailed.Task,
                readyTimeout);

            if (firstReady == recordingFailed.Task)
            {
                string error = await recordingFailed.Task;
                Log($"Recording failed before overlays were shown: {error}");
                return;
            }

            if (firstReady == readyTimeout)
            {
                SaveManifest(m =>
                {
                    m.Status = "RecordingReadyTimeout";
                    m.Error =
                        $"Recorder did not enter Recording within {RecordingReadyTimeout.TotalSeconds:0} seconds.";
                    m.FinishedAtUtc = DateTimeOffset.UtcNow;
                    m.AddEvent("RecordingReadyTimeout");
                });

                Log("Recorder did not reach Recording before timeout.");
                return;
            }

            statusWindow.Show();
            borderWindow.Show();
            statusWindow.StartTimer();
            G05CaptureExclusionInterop.FlushDwm();

            G05AffinityReadResult statusAfterShow =
                G05CaptureExclusionInterop.ReadAffinity(
                    statusWindow,
                    "StatusWindow");

            G05AffinityReadResult borderAfterShow =
                G05CaptureExclusionInterop.ReadAffinity(
                    borderWindow,
                    "RecordingBorderWindow");

            SaveManifest(m =>
            {
                m.OverlaysShownAtUtc = DateTimeOffset.UtcNow;
                m.StatusWindowAffinityAfterShow = statusAfterShow;
                m.BorderWindowAffinityAfterShow = borderAfterShow;
                m.Status = "OverlaysVisible";
                m.AddEvent(
                    "OverlaysShown",
                    $"statusAffinity=0x{statusAfterShow.Affinity:X}; borderAffinity=0x{borderAfterShow.Affinity:X}");
            });

            Log("Recorder reached Recording; overlays are now visible.");
            Log(
                $"Post-Show affinity: status=0x{statusAfterShow.Affinity:X}; border=0x{borderAfterShow.Affinity:X}");

            await Task.Delay(TimeSpan.FromSeconds(runSeconds));

            SaveManifest(m =>
            {
                m.StopRequestedAtUtc = DateTimeOffset.UtcNow;
                m.Status = "Stopping";
                m.AddEvent("StopRequested");
            });

            StateTextBlock.Text = "Stopping";
            statusWindow.StopTimer();
            recorder.Stop();

            Task completionTimeout =
                Task.Delay(CompletionTimeout);
            Task firstCompletion = await Task.WhenAny(
                recordingComplete.Task,
                recordingFailed.Task,
                completionTimeout);

            if (firstCompletion == recordingFailed.Task)
            {
                string error = await recordingFailed.Task;
                Log($"Recording failed: {error}");
            }
            else if (firstCompletion == completionTimeout)
            {
                SaveManifest(m =>
                {
                    m.Status = "CompletionTimeout";
                    m.Error =
                        $"Recorder did not complete within {CompletionTimeout.TotalSeconds:0} seconds after Stop.";
                    m.FinishedAtUtc = DateTimeOffset.UtcNow;
                    m.AddEvent("CompletionTimeout");
                });

                Log("Recording completion timed out.");
            }
            else
            {
                Log($"Recording complete: {await recordingComplete.Task}");
            }
        }
        catch (Exception ex)
        {
            SaveManifest(m =>
            {
                m.Status = "ControllerException";
                m.Error = ex.ToString();
                m.FinishedAtUtc = DateTimeOffset.UtcNow;
                m.AddEvent(
                    "ControllerException",
                    ex.Message);
            });

            Log($"G0-5 run failed: {ex}");
        }
        finally
        {
            try
            {
                statusWindow?.StopTimer();

                if (statusWindow?.IsLoaded == true)
                {
                    statusWindow.Close();
                }

                if (borderWindow?.IsLoaded == true)
                {
                    borderWindow.Close();
                }

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

                SaveManifest(m =>
                {
                    m.ObserveOutput();

                    if (m.FinishedAtUtc is null)
                    {
                        m.FinishedAtUtc = DateTimeOffset.UtcNow;
                    }

                    m.AddEvent(
                        "RunFinalized",
                        $"outputExists={m.OutputExistsAfterRun}; outputBytes={m.OutputBytesAfterRun}");
                });

                if (!IsVisible)
                {
                    Show();
                }

                Activate();
                SetRunning(false);

                Log(
                    $"Final output: exists={manifest.OutputExistsAfterRun}; bytes={manifest.OutputBytesAfterRun}");
                Log($"Manifest: {manifestPath}");
                Log(
                    $"Optional external reference screenshot path: {referenceScreenshotPath}");
            }
        }
    }

    private static RecordingSourceBase BuildRecordingSource(
        G05CaptureMode mode,
        RecordableDisplay display,
        nint targetWindowHwnd,
        int areaX,
        int areaY,
        int areaWidth,
        int areaHeight)
    {
        return mode switch
        {
            G05CaptureMode.Area =>
                new DisplayRecordingSource(display.DeviceName)
                {
                    SourceRect = new ScreenRect(
                        areaX,
                        areaY,
                        areaWidth,
                        areaHeight),
                    OutputSize =
                        new ScreenSize(areaWidth, areaHeight),
                    IsCursorCaptureEnabled = true
                },

            G05CaptureMode.Monitor =>
                new DisplayRecordingSource(display.DeviceName)
                {
                    IsCursorCaptureEnabled = true
                },

            G05CaptureMode.Window when targetWindowHwnd != 0 =>
                new WindowRecordingSource(targetWindowHwnd)
                {
                    IsCursorCaptureEnabled = true
                },

            G05CaptureMode.Window =>
                throw new InvalidOperationException(
                    "Window capture target HWND is unavailable."),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Unsupported G0-5 capture mode.")
        };
    }

    private static void PositionOverlays(
        G05StatusWindow statusWindow,
        G05RecordingBorderWindow borderWindow,
        G05CaptureMode mode,
        G05CaptureTargetWindow? targetWindow = null)
    {
        if (mode == G05CaptureMode.Window &&
            targetWindow is not null)
        {
            borderWindow.Left = targetWindow.Left + 18;
            borderWindow.Top = targetWindow.Top + 48;
            borderWindow.Width = targetWindow.Width - 36;
            borderWindow.Height = targetWindow.Height - 72;

            statusWindow.Left = targetWindow.Left + 70;
            statusWindow.Top = targetWindow.Top + 100;
            return;
        }

        borderWindow.Left = 70;
        borderWindow.Top = 70;
        borderWindow.Width = 560;
        borderWindow.Height = 340;

        statusWindow.Left = 120;
        statusWindow.Top = 120;
    }

    private bool TryReadConfiguration(
        out G05CaptureMode mode,
        out bool exclusionRequested,
        out int runSeconds,
        out int areaX,
        out int areaY,
        out int areaWidth,
        out int areaHeight)
    {
        areaX = 0;
        areaY = 0;
        areaWidth = 0;
        areaHeight = 0;

        mode = ParseCaptureMode();
        exclusionRequested =
            ExclusionCheckBox.IsChecked == true;

        if (!int.TryParse(RunSecondsTextBox.Text, out runSeconds) ||
            runSeconds < 1 ||
            runSeconds > 300)
        {
            Log("Record seconds must be an integer from 1 to 300.");
            areaX = areaY = areaWidth = areaHeight = 0;
            return false;
        }

        bool areaValid =
            int.TryParse(AreaXTextBox.Text, out areaX) &&
            int.TryParse(AreaYTextBox.Text, out areaY) &&
            int.TryParse(AreaWidthTextBox.Text, out areaWidth) &&
            int.TryParse(AreaHeightTextBox.Text, out areaHeight);

        if (!areaValid)
        {
            Log("Area X, Y, Width, and Height must be integers.");
            return false;
        }

        if (mode == G05CaptureMode.Area)
        {
            if (areaWidth <= 0 || areaHeight <= 0)
            {
                Log("Area Width and Height must be positive.");
                return false;
            }

            if ((areaWidth & 1) != 0 ||
                (areaHeight & 1) != 0)
            {
                Log("Area Width and Height must be even for the H.264 test.");
                return false;
            }
        }

        return true;
    }

    private G05CaptureMode ParseCaptureMode()
    {
        if (CaptureModeComboBox.SelectedItem is ComboBoxItem item &&
            Enum.TryParse(
                item.Content?.ToString(),
                ignoreCase: true,
                out G05CaptureMode mode))
        {
            return mode;
        }

        return G05CaptureMode.Area;
    }

    private void SetRunning(bool running)
    {
        RunButton.IsEnabled =
            !running && _primaryDisplay is not null;
        CaptureModeComboBox.IsEnabled = !running;
        ExclusionCheckBox.IsEnabled = !running;
        AreaXTextBox.IsEnabled = !running;
        AreaYTextBox.IsEnabled = !running;
        AreaWidthTextBox.IsEnabled = !running;
        AreaHeightTextBox.IsEnabled = !running;
        RunSecondsTextBox.IsEnabled = !running;
        StateTextBlock.Text = running ? "Running" : "Idle";
    }

    private void Log(string message)
    {
        string line =
            $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        LogTextBox.AppendText(
            line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
