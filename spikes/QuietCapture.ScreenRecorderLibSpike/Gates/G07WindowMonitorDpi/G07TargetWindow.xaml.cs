using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G07WindowMonitorDpi;

public partial class G07TargetWindow : Window
{
    private readonly DispatcherTimer _timer;

    public G07TargetWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Normal,
            (_, _) => UpdateLabels(),
            Dispatcher);
        _timer.Start();
        UpdateLabels();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void UpdateLabels()
    {
        ClockTextBlock.Text =
            $"Clock: {DateTimeOffset.Now:HH:mm:ss.fff}";

        nint hwnd =
            new WindowInteropHelper(this).Handle;

        if (hwnd != 0)
        {
            G07WindowSnapshot snapshot =
                G07Interop.CaptureWindow(
                    hwnd,
                    Array.Empty<G07DisplaySnapshot>());

            DpiTextBlock.Text =
                snapshot.Dpi.HasValue
                    ? $"Window DPI: {snapshot.Dpi} ({snapshot.ScalePercent:F0}%)"
                    : "Window DPI: n/a";
        }
        else
        {
            DpiTextBlock.Text = "Window DPI: pending";
        }
    }
}
