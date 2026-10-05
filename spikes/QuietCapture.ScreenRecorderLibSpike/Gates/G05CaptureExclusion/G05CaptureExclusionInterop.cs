using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuietCapture.ScreenRecorderLibSpike.Gates.G05CaptureExclusion;

internal static class G05CaptureExclusionInterop
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;

    public const uint WdaNone = 0x00000000;
    public const uint WdaExcludeFromCapture = 0x00000011;

    public static G05WindowPreparationResult Prepare(
        Window window,
        string role,
        bool exclusionRequested)
    {
        nint hwnd = new WindowInteropHelper(window).EnsureHandle();

        long stylesBefore = GetWindowLongPtrW(hwnd, GwlExStyle).ToInt64();
        long requestedStyles =
            stylesBefore |
            WsExTransparent |
            WsExToolWindow |
            WsExNoActivate;

        KernelSetLastError(0);
        nint previousStyle = SetWindowLongPtrW(
            hwnd,
            GwlExStyle,
            new nint(requestedStyles));
        int styleError = previousStyle == 0
            ? Marshal.GetLastWin32Error()
            : 0;

        long stylesAfter = GetWindowLongPtrW(hwnd, GwlExStyle).ToInt64();

        uint requestedAffinity = exclusionRequested
            ? WdaExcludeFromCapture
            : WdaNone;

        bool setAffinitySucceeded =
            SetWindowDisplayAffinity(hwnd, requestedAffinity);
        int setAffinityError = setAffinitySucceeded
            ? 0
            : Marshal.GetLastWin32Error();

        G05AffinityReadResult readResult =
            ReadAffinity(hwnd, role);

        return new G05WindowPreparationResult(
            role,
            hwnd.ToInt64(),
            exclusionRequested,
            requestedAffinity,
            setAffinitySucceeded,
            setAffinityError,
            readResult.Succeeded,
            readResult.Error,
            readResult.Affinity,
            stylesBefore,
            stylesAfter,
            styleError);
    }

    public static G05AffinityReadResult ReadAffinity(
        Window window,
        string role)
    {
        nint hwnd = new WindowInteropHelper(window).EnsureHandle();
        return ReadAffinity(hwnd, role);
    }

    public static void FlushDwm()
    {
        int result = DwmFlush();
        if (result != 0)
        {
            throw new Win32Exception(result, "DwmFlush failed.");
        }
    }

    private static G05AffinityReadResult ReadAffinity(
        nint hwnd,
        string role)
    {
        bool succeeded =
            GetWindowDisplayAffinity(hwnd, out uint affinity);
        int error = succeeded
            ? 0
            : Marshal.GetLastWin32Error();

        return new G05AffinityReadResult(
            role,
            hwnd.ToInt64(),
            succeeded,
            error,
            affinity);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(
        nint hWnd,
        uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(
        nint hWnd,
        out uint pdwAffinity);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongPtrW",
        SetLastError = true)]
    private static extern nint GetWindowLongPtrW(
        nint hWnd,
        int nIndex);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongPtrW",
        SetLastError = true)]
    private static extern nint SetWindowLongPtrW(
        nint hWnd,
        int nIndex,
        nint dwNewLong);

    [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
    private static extern void KernelSetLastError(uint dwErrCode);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}

internal sealed record G05WindowPreparationResult(
    string Role,
    long Hwnd,
    bool ExclusionRequested,
    uint RequestedAffinity,
    bool SetAffinitySucceeded,
    int SetAffinityError,
    bool GetAffinitySucceeded,
    int GetAffinityError,
    uint VerifiedAffinity,
    long ExtendedStyleBefore,
    long ExtendedStyleAfter,
    int SetExtendedStyleError)
{
    public bool IsReady =>
        SetExtendedStyleError == 0 &&
        SetAffinitySucceeded &&
        GetAffinitySucceeded &&
        VerifiedAffinity == RequestedAffinity;
}

internal sealed record G05AffinityReadResult(
    string Role,
    long Hwnd,
    bool Succeeded,
    int Error,
    uint Affinity);
