using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenRecorderLib;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G07WindowMonitorDpi;

internal static class G07Interop
{
    private const uint MonitorInfoPrimary = 0x00000001;
    private const uint EddGetDeviceInterfaceName = 0x00000001;
    private const uint MonitorDefaultToNearest = 0x00000002;

    private const int SwMinimize = 6;
    private const int SwRestore = 9;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public static IReadOnlyList<G07DisplaySnapshot> CaptureDisplays(
        IEnumerable<RecordableDisplay> recorderDisplays)
    {
        var recorderNames = recorderDisplays
            .GroupBy(
                display => display.DeviceName,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().FriendlyName ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var result = new List<G07DisplaySnapshot>();

        MonitorEnumProc callback =
            (nint hMonitor, nint hdcMonitor, ref NativeRect monitorRect, nint data) =>
            {
                var info = new MonitorInfoEx
                {
                    cbSize = Marshal.SizeOf<MonitorInfoEx>()
                };

                if (!GetMonitorInfoW(hMonitor, ref info))
                {
                    return true;
                }

                uint? dpiX = null;
                uint? dpiY = null;

                int dpiResult =
                    GetDpiForMonitor(
                        hMonitor,
                        MonitorDpiType.EffectiveDpi,
                        out uint rawDpiX,
                        out uint rawDpiY);

                if (dpiResult == 0)
                {
                    dpiX = rawDpiX;
                    dpiY = rawDpiY;
                }

                string deviceName =
                    info.szDevice ?? string.Empty;

                string? deviceInterfaceId = null;
                string? deviceString = null;
                string? deviceKey = null;

                var device = new DisplayDevice
                {
                    cb = Marshal.SizeOf<DisplayDevice>()
                };

                if (EnumDisplayDevicesW(
                        deviceName,
                        0,
                        ref device,
                        EddGetDeviceInterfaceName))
                {
                    deviceInterfaceId =
                        EmptyToNull(device.DeviceID);
                    deviceString =
                        EmptyToNull(device.DeviceString);
                    deviceKey =
                        EmptyToNull(device.DeviceKey);
                }

                recorderNames.TryGetValue(
                    deviceName,
                    out string? recorderFriendlyName);

                result.Add(
                    new G07DisplaySnapshot(
                        hMonitor.ToInt64(),
                        deviceName,
                        EmptyToNull(recorderFriendlyName),
                        deviceInterfaceId,
                        deviceString,
                        deviceKey,
                        ToPixelRect(info.rcMonitor),
                        ToPixelRect(info.rcWork),
                        (info.dwFlags & MonitorInfoPrimary) != 0,
                        dpiX,
                        dpiY,
                        dpiX.HasValue
                            ? dpiX.Value / 96d * 100d
                            : null,
                        dpiY.HasValue
                            ? dpiY.Value / 96d * 100d
                            : null,
                        info.rcMonitor.Left < 0 ||
                        info.rcMonitor.Top < 0));
                return true;
            };

        if (!EnumDisplayMonitors(
                nint.Zero,
                nint.Zero,
                callback,
                nint.Zero))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "EnumDisplayMonitors failed.");
        }

        return result
            .OrderByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.Left)
            .ThenBy(display => display.Bounds.Top)
            .ToArray();
    }

    public static nint EnsureTargetHandle(Window window)
    {
        return new WindowInteropHelper(window).EnsureHandle();
    }

    public static G07WindowSnapshot CaptureWindow(
        nint hwnd,
        IReadOnlyList<G07DisplaySnapshot> displays)
    {
        bool valid = IsWindow(hwnd);

        if (!valid)
        {
            return new G07WindowSnapshot(
                hwnd.ToInt64(),
                false,
                false,
                null,
                null,
                null,
                null);
        }

        G07PixelRect? rect = null;

        if (GetWindowRect(hwnd, out NativeRect nativeRect))
        {
            rect = ToPixelRect(nativeRect);
        }

        uint dpi = GetDpiForWindow(hwnd);
        nint hMonitor = MonitorFromWindow(
            hwnd,
            MonitorDefaultToNearest);

        G07DisplaySnapshot? display =
            displays.FirstOrDefault(item =>
                item.HMonitor == hMonitor.ToInt64());

        return new G07WindowSnapshot(
            hwnd.ToInt64(),
            true,
            IsIconic(hwnd),
            rect,
            dpi == 0 ? null : dpi,
            dpi == 0 ? null : dpi / 96d * 100d,
            display?.DeviceName);
    }

    public static void MoveResizeWindow(
        nint hwnd,
        G07PixelRect rect)
    {
        if (!SetWindowPos(
                hwnd,
                nint.Zero,
                rect.Left,
                rect.Top,
                rect.Width,
                rect.Height,
                SwpNoZOrder | SwpNoActivate))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "SetWindowPos failed.");
        }

        FlushDwm();
    }

    public static void MinimizeWindow(nint hwnd)
    {
        ShowWindow(hwnd, SwMinimize);
        FlushDwm();
    }

    public static void RestoreWindow(nint hwnd)
    {
        ShowWindow(hwnd, SwRestore);
        FlushDwm();
    }

    public static void FlushDwm()
    {
        int result = DwmFlush();

        if (result != 0)
        {
            throw new Win32Exception(
                result,
                "DwmFlush failed.");
        }
    }

    private static G07PixelRect ToPixelRect(
        NativeRect rect)
    {
        return new G07PixelRect(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private enum MonitorDpiType
    {
        EffectiveDpi = 0
    }

    private delegate bool MonitorEnumProc(
        nint hMonitor,
        nint hdcMonitor,
        ref NativeRect monitorRect,
        nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 128)]
        public string DeviceString;

        public uint StateFlags;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint hdc,
        nint clipRect,
        MonitorEnumProc callback,
        nint data);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(
        nint hMonitor,
        ref MonitorInfoEx monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint hMonitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevicesW(
        string? lpDevice,
        uint deviceNumber,
        ref DisplayDevice displayDevice,
        uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(
        nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        nint hwnd,
        out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(
        nint hwnd,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(
        nint hwnd,
        int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
