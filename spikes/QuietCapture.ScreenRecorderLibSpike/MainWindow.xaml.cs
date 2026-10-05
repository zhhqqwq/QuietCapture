using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using QuietCapture.ScreenRecorderLibSpike.Gates.G01AreaCapture;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike;

public partial class MainWindow : Window
{
    private Recorder? _recorder;
    private G01RunReport? _runReport;
    private string? _runReportPath;
    private List<DisplayChoice> _displayChoices = new();

    public MainWindow()
    {
        InitializeComponent();
        OutputPathTextBox.Text = CreateDefaultOutputPath();
        RefreshDisplays();
    }

    private void RefreshDisplaysButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        try
        {
            _displayChoices = Recorder.GetDisplays()
                .Select(display => new DisplayChoice(
                    display,
                    string.IsNullOrWhiteSpace(display.FriendlyName)
                        ? display.DeviceName
                        : $"{display.FriendlyName} ({display.DeviceName})"))
                .ToList();

            DisplayComboBox.ItemsSource = _displayChoices;
            DisplayComboBox.SelectedIndex = _displayChoices.Count > 0 ? 0 : -1;
            StartButton.IsEnabled = _displayChoices.Count > 0 && _recorder is null;
            Log($"Displays: {_displayChoices.Count}");
        }
        catch (Exception ex)
        {
            Log($"Display enumeration failed: {ex}");
            StartButton.IsEnabled = false;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".mp4",
            Filter = "MP4 video (*.mp4)|*.mp4",
            FileName = Path.GetFileName(OutputPathTextBox.Text),
            InitialDirectory = Path.GetDirectoryName(OutputPathTextBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null)
        {
            return;
        }

        if (DisplayComboBox.SelectedItem is not DisplayChoice choice)
        {
            Log("Select a display.");
            return;
        }

        if (!TryReadRectangle(out int x, out int y, out int width, out int height))
        {
            return;
        }

        string outputPath = Path.GetFullPath(OutputPathTextBox.Text.Trim());
        if (!string.Equals(Path.GetExtension(outputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            Log("Output path must end in .mp4.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        bool includeCursor = IncludeCursorCheckBox.IsChecked == true;
        string recorderLogPath = Path.ChangeExtension(outputPath, ".log");
        _runReportPath = Path.ChangeExtension(outputPath, ".g0-1.json");
        _runReport = new G01RunReport
        {
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath,
            DisplayDeviceName = choice.Display.DeviceName,
            DisplayFriendlyName = choice.Display.FriendlyName ?? string.Empty,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IncludeCursor = includeCursor
        };
        _runReport.AddEvent("StartRequested");
        TrySaveRunReport();
        var source = new DisplayRecordingSource(choice.Display.DeviceName)
        {
            SourceRect = new ScreenRect(x, y, width, height),
            IsCursorCaptureEnabled = includeCursor
        };

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = new List<RecordingSourceBase> { source }
            },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = new ScreenSize(width, height),
                Stretch = StretchMode.Fill
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = false
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = includeCursor
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

        try
        {
            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnStatusChanged += Recorder_OnStatusChanged;
            _recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
            _recorder.OnRecordingFailed += Recorder_OnRecordingFailed;

            Log($"Start: display={choice.Display.DeviceName}; rect=({x},{y},{width},{height}); cursor={includeCursor}; output={outputPath}");
            StateTextBlock.Text = "Starting";
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            _recorder.Record(outputPath);
        }
        catch (Exception ex)
        {
            Log($"Start failed: {ex}");
            if (_runReport is not null)
            {
                _runReport.Status = "StartFailed";
                _runReport.Error = ex.ToString();
                _runReport.FinishedAtUtc = DateTimeOffset.UtcNow;
                _runReport.AddEvent("StartFailed", ex.Message);
                TrySaveRunReport();
            }

            SetIdle();
            DisposeRecorder();
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder is null)
        {
            return;
        }

        StopButton.IsEnabled = false;
        StateTextBlock.Text = "Stopping";

        try
        {
            Log("Stop requested.");
            if (_runReport is not null)
            {
                _runReport.StopRequestedAtUtc = DateTimeOffset.UtcNow;
                _runReport.AddEvent("StopRequested");
                TrySaveRunReport();
            }

            _recorder.Stop();
        }
        catch (Exception ex)
        {
            Log($"Stop failed: {ex}");
            if (_runReport is not null)
            {
                _runReport.Status = "StopFailed";
                _runReport.Error = ex.ToString();
                _runReport.FinishedAtUtc = DateTimeOffset.UtcNow;
                _runReport.AddEvent("StopFailed", ex.Message);
                TrySaveRunReport();
            }

            SetIdle();
            DisposeRecorder();
        }
    }

    private void Recorder_OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StateTextBlock.Text = e.Status.ToString();
            Log($"Status: {e.Status}");
            if (_runReport is not null)
            {
                _runReport.Status = e.Status.ToString();
                _runReport.AddEvent("StatusChanged", e.Status.ToString());
                TrySaveRunReport();
            }
        });
    }

    private void Recorder_OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Log($"Complete: {e.FilePath}");
            if (_runReport is not null)
            {
                _runReport.Status = "Complete";
                _runReport.FinishedAtUtc = DateTimeOffset.UtcNow;
                _runReport.AddEvent("RecordingComplete", e.FilePath);
                TrySaveRunReport();
            }

            SetIdle();
            DisposeRecorder();
            OutputPathTextBox.Text = CreateDefaultOutputPath();
        });
    }

    private void Recorder_OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Log($"Recording failed: {e.Error}");
            if (_runReport is not null)
            {
                _runReport.Status = "RecordingFailed";
                _runReport.Error = e.Error;
                _runReport.FinishedAtUtc = DateTimeOffset.UtcNow;
                _runReport.AddEvent("RecordingFailed", e.Error);
                TrySaveRunReport();
            }

            SetIdle();
            DisposeRecorder();
        });
    }

    private bool TryReadRectangle(out int x, out int y, out int width, out int height)
    {
        x = 0;
        y = 0;
        width = 0;
        height = 0;

        bool ok =
            int.TryParse(XTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
            int.TryParse(YTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out y) &&
            int.TryParse(WidthTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out width) &&
            int.TryParse(HeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);

        if (!ok)
        {
            Log("X, Y, Width, and Height must be integers.");
            return false;
        }

        if (width <= 0 || height <= 0)
        {
            Log("Width and Height must be positive.");
            return false;
        }

        if ((width & 1) != 0 || (height & 1) != 0)
        {
            Log("Initial H.264 tests require even Width and Height.");
            return false;
        }

        return true;
    }

    private void SetIdle()
    {
        StateTextBlock.Text = "Idle";
        StartButton.IsEnabled = _displayChoices.Count > 0;
        StopButton.IsEnabled = false;
    }

    private void DisposeRecorder()
    {
        if (_recorder is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _recorder = null;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        try
        {
            _recorder?.Stop();
        }
        catch (Exception ex)
        {
            Log($"Stop during window close failed: {ex.Message}");
        }
        finally
        {
            if (_runReport is not null && _runReport.FinishedAtUtc is null)
            {
                _runReport.Status = "WindowClosed";
                _runReport.FinishedAtUtc = DateTimeOffset.UtcNow;
                _runReport.AddEvent("WindowClosed");
                TrySaveRunReport();
            }

            DisposeRecorder();
        }
    }

    private void TrySaveRunReport()
    {
        if (_runReport is null || string.IsNullOrWhiteSpace(_runReportPath))
        {
            return;
        }

        try
        {
            _runReport.Save(_runReportPath);
        }
        catch (Exception ex)
        {
            Log($"Run report write failed: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        string line = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private static string CreateDefaultOutputPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "QuietCaptureSpike", "G0-1");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"area-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.mp4");
    }

    private sealed record DisplayChoice(RecordableDisplay Display, string Label);
}
