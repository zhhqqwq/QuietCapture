using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G02SystemAudio;

public partial class G02SystemAudioWindow : Window
{
    private Recorder? _recorder;
    private List<DisplayChoice> _displayChoices = new();
    private List<AudioDeviceChoice> _audioDeviceChoices = new();
    private G02RunReport? _runReport;
    private string? _runReportPath;
    private long _audioPacketCount;
    private long _mixedAudioBytes;
    private long _sourcePacketCount;
    private long _sourceAudioBytes;

    public G02SystemAudioWindow()
    {
        InitializeComponent();
        OutputPathTextBox.Text = CreateDefaultOutputPath();
        RefreshSources();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSources();
    }

    private void RefreshSources()
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

            _audioDeviceChoices = Recorder.GetSystemAudioLoopbackDevices()
                .Select(device => new AudioDeviceChoice(
                    device,
                    $"{device.FriendlyName} {(device.IsDefaultDevice ? "[Default] " : string.Empty)}({device.DeviceName})"))
                .ToList();

            DisplayComboBox.ItemsSource = _displayChoices;
            DisplayComboBox.SelectedIndex = _displayChoices.Count > 0 ? 0 : -1;

            AudioDeviceComboBox.ItemsSource = _audioDeviceChoices;
            int defaultAudioIndex = _audioDeviceChoices.FindIndex(choice => choice.Device.IsDefaultDevice);
            AudioDeviceComboBox.SelectedIndex = _audioDeviceChoices.Count == 0
                ? -1
                : defaultAudioIndex >= 0 ? defaultAudioIndex : 0;

            StartButton.IsEnabled =
                _recorder is null &&
                _displayChoices.Count > 0 &&
                _audioDeviceChoices.Count > 0;

            Log($"Displays: {_displayChoices.Count}; loopback devices: {_audioDeviceChoices.Count}");
            foreach (AudioDeviceChoice choice in _audioDeviceChoices)
            {
                Log($"Audio device: default={choice.Device.IsDefaultDevice}; name={choice.Device.FriendlyName}; id={choice.Device.DeviceName}");
            }
        }
        catch (Exception ex)
        {
            Log($"Source enumeration failed: {ex}");
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

        if (DisplayComboBox.SelectedItem is not DisplayChoice displayChoice)
        {
            Log("Select a display.");
            return;
        }

        if (AudioDeviceComboBox.SelectedItem is not AudioDeviceChoice audioChoice)
        {
            Log("Select a system audio device.");
            return;
        }

        string outputPath = Path.GetFullPath(OutputPathTextBox.Text.Trim());
        if (!string.Equals(Path.GetExtension(outputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            Log("Output path must end in .mp4.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        Interlocked.Exchange(ref _audioPacketCount, 0);
        Interlocked.Exchange(ref _mixedAudioBytes, 0);
        Interlocked.Exchange(ref _sourcePacketCount, 0);
        Interlocked.Exchange(ref _sourceAudioBytes, 0);
        AudioPacketTextBlock.Text = "Audio packets: 0";

        string recorderLogPath = Path.ChangeExtension(outputPath, ".log");
        _runReportPath = Path.ChangeExtension(outputPath, ".g0-2.json");
        _runReport = new G02RunReport
        {
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath,
            DisplayDeviceName = displayChoice.Display.DeviceName,
            DisplayFriendlyName = displayChoice.Display.FriendlyName ?? string.Empty,
            AudioDeviceId = audioChoice.Device.DeviceName,
            AudioDeviceFriendlyName = audioChoice.Device.FriendlyName ?? string.Empty,
            AudioDeviceWasDefaultAtEnumeration = audioChoice.Device.IsDefaultDevice
        };

        foreach (AudioDeviceChoice choice in _audioDeviceChoices)
        {
            _runReport.EnumeratedLoopbackDevices.Add(new G02AudioDeviceSnapshot(
                choice.Device.DeviceName,
                choice.Device.FriendlyName ?? string.Empty,
                choice.Device.IsDefaultDevice));
        }

        _runReport.AddEvent("StartRequested");
        TrySaveRunReport();

        var videoSource = new DisplayRecordingSource(displayChoice.Display.DeviceName)
        {
            IsCursorCaptureEnabled = true
        };

        var loopbackSource = new LoopbackAudioSource(audioChoice.Device.DeviceName)
        {
            Volume = 1.0f
        };

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = new List<RecordingSourceBase> { videoSource }
            },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = true,
                Bitrate = AudioBitrate.bitrate_128kbps,
                Channels = AudioChannels.Stereo,
                MasterVolume = 1.0f,
                IsAudioPacketPreviewEnabled = true,
                AudioSources = new List<AudioSourceBase> { loopbackSource }
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

        try
        {
            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnStatusChanged += Recorder_OnStatusChanged;
            _recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
            _recorder.OnRecordingFailed += Recorder_OnRecordingFailed;
            _recorder.OnAudioPacketRecorded += Recorder_OnAudioPacketRecorded;

            Log($"Start: display={displayChoice.Display.DeviceName}; audio={audioChoice.Device.FriendlyName}; audioId={audioChoice.Device.DeviceName}; output={outputPath}");
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
                UpdateReportCounters();
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
                UpdateReportCounters();
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
                UpdateReportCounters();
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
                UpdateReportCounters();
                TrySaveRunReport();
            }
        });
    }

    private void Recorder_OnAudioPacketRecorded(object? sender, AudioDataRecordedEventArgs e)
    {
        long packetCount = Interlocked.Increment(ref _audioPacketCount);

        if (e.AudioData?.Data is { Length: > 0 } mixedData)
        {
            Interlocked.Add(ref _mixedAudioBytes, mixedData.Length);
        }

        if (e.AudioData?.Sources is not null)
        {
            foreach (AudioPacketSource source in e.AudioData.Sources)
            {
                Interlocked.Increment(ref _sourcePacketCount);
                if (source.Data is { Length: > 0 })
                {
                    Interlocked.Add(ref _sourceAudioBytes, source.Data.Length);
                }
            }
        }

        if (packetCount % 50 == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                AudioPacketTextBlock.Text = $"Audio packets: {Interlocked.Read(ref _audioPacketCount)}";
            });
        }
    }

    private void Recorder_OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateReportCounters();
            Log($"Complete: {e.FilePath}; audioPackets={_audioPacketCount}; mixedBytes={_mixedAudioBytes}; sourceBytes={_sourceAudioBytes}");

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
            UpdateReportCounters();
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

    private void UpdateReportCounters()
    {
        if (_runReport is null)
        {
            return;
        }

        _runReport.AudioPacketCount = Interlocked.Read(ref _audioPacketCount);
        _runReport.MixedAudioBytes = Interlocked.Read(ref _mixedAudioBytes);
        _runReport.SourcePacketCount = Interlocked.Read(ref _sourcePacketCount);
        _runReport.SourceAudioBytes = Interlocked.Read(ref _sourceAudioBytes);
    }

    private void SetIdle()
    {
        StateTextBlock.Text = "Idle";
        StartButton.IsEnabled =
            _displayChoices.Count > 0 &&
            _audioDeviceChoices.Count > 0;
        StopButton.IsEnabled = false;
        AudioPacketTextBlock.Text = $"Audio packets: {Interlocked.Read(ref _audioPacketCount)}";
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
                UpdateReportCounters();
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
        string directory = Path.Combine(Path.GetTempPath(), "QuietCaptureSpike", "G0-2");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"system-audio-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.mp4");
    }

    private sealed record DisplayChoice(RecordableDisplay Display, string Label);

    private sealed record AudioDeviceChoice(
        RecordableAudioLoopbackDevice Device,
        string Label);
}
