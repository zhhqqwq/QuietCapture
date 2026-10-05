using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G03SystemAudioMicrophone;

public partial class G03SystemAudioMicrophoneWindow : Window
{
    private Recorder? _recorder;
    private List<DisplayChoice> _displayChoices = new();
    private List<LoopbackDeviceChoice> _loopbackChoices = new();
    private List<MicrophoneDeviceChoice> _microphoneChoices = new();
    private G03RunReport? _runReport;
    private string? _runReportPath;
    private string? _loopbackSourceId;
    private string? _microphoneSourceId;

    private long _audioPacketCount;
    private long _mixedAudioBytes;
    private long _loopbackPacketCount;
    private long _loopbackAudioBytes;
    private long _microphonePacketCount;
    private long _microphoneAudioBytes;
    private long _unknownSourcePacketCount;
    private long _unknownSourceAudioBytes;

    public G03SystemAudioMicrophoneWindow()
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

            _loopbackChoices = Recorder.GetSystemAudioLoopbackDevices()
                .Select(device => new LoopbackDeviceChoice(
                    device,
                    $"{device.FriendlyName} {(device.IsDefaultDevice ? "[Default] " : string.Empty)}({device.DeviceName})"))
                .ToList();

            _microphoneChoices = Recorder.GetSystemAudioCaptureDevices()
                .Select(device => new MicrophoneDeviceChoice(
                    device,
                    $"{device.FriendlyName} {(device.IsDefaultDevice ? "[Default] " : string.Empty)}({device.DeviceName})"))
                .ToList();

            DisplayComboBox.ItemsSource = _displayChoices;
            DisplayComboBox.SelectedIndex = _displayChoices.Count > 0 ? 0 : -1;

            LoopbackDeviceComboBox.ItemsSource = _loopbackChoices;
            LoopbackDeviceComboBox.SelectedIndex = ResolveDefaultIndex(
                _loopbackChoices.Select(choice => choice.Device.IsDefaultDevice).ToList());

            MicrophoneDeviceComboBox.ItemsSource = _microphoneChoices;
            MicrophoneDeviceComboBox.SelectedIndex = ResolveDefaultIndex(
                _microphoneChoices.Select(choice => choice.Device.IsDefaultDevice).ToList());

            StartButton.IsEnabled =
                _recorder is null &&
                _displayChoices.Count > 0 &&
                _loopbackChoices.Count > 0 &&
                _microphoneChoices.Count > 0;

            Log($"Displays: {_displayChoices.Count}; loopback devices: {_loopbackChoices.Count}; microphones: {_microphoneChoices.Count}");

            foreach (LoopbackDeviceChoice choice in _loopbackChoices)
            {
                Log($"Loopback: default={choice.Device.IsDefaultDevice}; name={choice.Device.FriendlyName}; id={choice.Device.DeviceName}");
            }

            foreach (MicrophoneDeviceChoice choice in _microphoneChoices)
            {
                Log($"Microphone: default={choice.Device.IsDefaultDevice}; name={choice.Device.FriendlyName}; id={choice.Device.DeviceName}");
            }
        }
        catch (Exception ex)
        {
            Log($"Source enumeration failed: {ex}");
            StartButton.IsEnabled = false;
        }
    }

    private static int ResolveDefaultIndex(IReadOnlyList<bool> defaults)
    {
        if (defaults.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < defaults.Count; i++)
        {
            if (defaults[i])
            {
                return i;
            }
        }

        return 0;
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

        if (LoopbackDeviceComboBox.SelectedItem is not LoopbackDeviceChoice loopbackChoice)
        {
            Log("Select a system audio device.");
            return;
        }

        if (MicrophoneDeviceComboBox.SelectedItem is not MicrophoneDeviceChoice microphoneChoice)
        {
            Log("Select a microphone device.");
            return;
        }

        string outputPath = Path.GetFullPath(OutputPathTextBox.Text.Trim());
        if (!string.Equals(Path.GetExtension(outputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            Log("Output path must end in .mp4.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        ResetCounters();
        PacketTextBlock.Text = "Packets: loopback 0 / mic 0";

        var loopbackSource = new LoopbackAudioSource(loopbackChoice.Device.DeviceName)
        {
            Volume = 1.0f
        };

        var microphoneSource = new CaptureAudioSource(microphoneChoice.Device.DeviceName)
        {
            Volume = 1.0f
        };

        _loopbackSourceId = loopbackSource.ID;
        _microphoneSourceId = microphoneSource.ID;

        string recorderLogPath = Path.ChangeExtension(outputPath, ".log");
        _runReportPath = Path.ChangeExtension(outputPath, ".g0-3.json");
        _runReport = new G03RunReport
        {
            OutputPath = outputPath,
            RecorderLogPath = recorderLogPath,
            DisplayDeviceName = displayChoice.Display.DeviceName,
            DisplayFriendlyName = displayChoice.Display.FriendlyName ?? string.Empty,
            LoopbackDeviceId = loopbackChoice.Device.DeviceName,
            LoopbackDeviceFriendlyName = loopbackChoice.Device.FriendlyName ?? string.Empty,
            LoopbackDeviceWasDefaultAtEnumeration = loopbackChoice.Device.IsDefaultDevice,
            LoopbackSourceId = _loopbackSourceId,
            MicrophoneDeviceId = microphoneChoice.Device.DeviceName,
            MicrophoneDeviceFriendlyName = microphoneChoice.Device.FriendlyName ?? string.Empty,
            MicrophoneDeviceWasDefaultAtEnumeration = microphoneChoice.Device.IsDefaultDevice,
            MicrophoneSourceId = _microphoneSourceId
        };

        foreach (LoopbackDeviceChoice choice in _loopbackChoices)
        {
            _runReport.EnumeratedLoopbackDevices.Add(new G03AudioDeviceSnapshot(
                choice.Device.DeviceName,
                choice.Device.FriendlyName ?? string.Empty,
                choice.Device.IsDefaultDevice));
        }

        foreach (MicrophoneDeviceChoice choice in _microphoneChoices)
        {
            _runReport.EnumeratedCaptureDevices.Add(new G03AudioDeviceSnapshot(
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
                AudioSources = new List<AudioSourceBase>
                {
                    loopbackSource,
                    microphoneSource
                }
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

            Log($"Start: display={displayChoice.Display.DeviceName}; loopback={loopbackChoice.Device.FriendlyName}; loopbackId={loopbackChoice.Device.DeviceName}; mic={microphoneChoice.Device.FriendlyName}; micId={microphoneChoice.Device.DeviceName}; output={outputPath}");
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
                int length = source.Data?.Length ?? 0;

                if (string.Equals(source.Id, _loopbackSourceId, StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _loopbackPacketCount);
                    Interlocked.Add(ref _loopbackAudioBytes, length);
                }
                else if (string.Equals(source.Id, _microphoneSourceId, StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _microphonePacketCount);
                    Interlocked.Add(ref _microphoneAudioBytes, length);
                }
                else
                {
                    Interlocked.Increment(ref _unknownSourcePacketCount);
                    Interlocked.Add(ref _unknownSourceAudioBytes, length);
                }
            }
        }

        if (packetCount % 50 == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                PacketTextBlock.Text =
                    $"Packets: loopback {Interlocked.Read(ref _loopbackPacketCount)} / mic {Interlocked.Read(ref _microphonePacketCount)}";
            });
        }
    }

    private void Recorder_OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateReportCounters();
            Log($"Complete: {e.FilePath}; mixedPackets={_audioPacketCount}; loopbackPackets={_loopbackPacketCount}; micPackets={_microphonePacketCount}; unknownPackets={_unknownSourcePacketCount}");

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

    private void ResetCounters()
    {
        Interlocked.Exchange(ref _audioPacketCount, 0);
        Interlocked.Exchange(ref _mixedAudioBytes, 0);
        Interlocked.Exchange(ref _loopbackPacketCount, 0);
        Interlocked.Exchange(ref _loopbackAudioBytes, 0);
        Interlocked.Exchange(ref _microphonePacketCount, 0);
        Interlocked.Exchange(ref _microphoneAudioBytes, 0);
        Interlocked.Exchange(ref _unknownSourcePacketCount, 0);
        Interlocked.Exchange(ref _unknownSourceAudioBytes, 0);
    }

    private void UpdateReportCounters()
    {
        if (_runReport is null)
        {
            return;
        }

        _runReport.AudioPacketCount = Interlocked.Read(ref _audioPacketCount);
        _runReport.MixedAudioBytes = Interlocked.Read(ref _mixedAudioBytes);
        _runReport.LoopbackPacketCount = Interlocked.Read(ref _loopbackPacketCount);
        _runReport.LoopbackAudioBytes = Interlocked.Read(ref _loopbackAudioBytes);
        _runReport.MicrophonePacketCount = Interlocked.Read(ref _microphonePacketCount);
        _runReport.MicrophoneAudioBytes = Interlocked.Read(ref _microphoneAudioBytes);
        _runReport.UnknownSourcePacketCount = Interlocked.Read(ref _unknownSourcePacketCount);
        _runReport.UnknownSourceAudioBytes = Interlocked.Read(ref _unknownSourceAudioBytes);
    }

    private void SetIdle()
    {
        StateTextBlock.Text = "Idle";
        StartButton.IsEnabled =
            _displayChoices.Count > 0 &&
            _loopbackChoices.Count > 0 &&
            _microphoneChoices.Count > 0;
        StopButton.IsEnabled = false;
        PacketTextBlock.Text =
            $"Packets: loopback {Interlocked.Read(ref _loopbackPacketCount)} / mic {Interlocked.Read(ref _microphonePacketCount)}";
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
        string directory = Path.Combine(Path.GetTempPath(), "QuietCaptureSpike", "G0-3");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"system-audio-mic-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.mp4");
    }

    private sealed record DisplayChoice(RecordableDisplay Display, string Label);

    private sealed record LoopbackDeviceChoice(
        RecordableAudioLoopbackDevice Device,
        string Label);

    private sealed record MicrophoneDeviceChoice(
        RecordableAudioCaptureDevice Device,
        string Label);
}
