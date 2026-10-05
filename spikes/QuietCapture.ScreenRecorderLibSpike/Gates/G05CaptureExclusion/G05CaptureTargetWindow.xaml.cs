using System.Windows;
using System.Windows.Threading;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G05CaptureExclusion;

public partial class G05CaptureTargetWindow : Window
{
    private readonly DispatcherTimer _timer;

    public G05CaptureTargetWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Normal,
            (_, _) => UpdateClock(),
            Dispatcher);
        _timer.Start();
        UpdateClock();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void UpdateClock()
    {
        ClockTextBlock.Text =
            $"Target clock: {DateTimeOffset.Now:HH:mm:ss.fff}";
    }
}
