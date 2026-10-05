using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G05CaptureExclusion;

public partial class G05StatusWindow : Window
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;

    public G05StatusWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Normal,
            (_, _) => UpdateTimer(),
            Dispatcher);
        _timer.Stop();
    }

    public void StartTimer()
    {
        _stopwatch.Restart();
        _timer.Start();
        UpdateTimer();
    }

    public void StopTimer()
    {
        _timer.Stop();
        _stopwatch.Stop();
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        TimeSpan elapsed = _stopwatch.Elapsed;
        TimerTextBlock.Text =
            $"REC {elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}";
    }
}
