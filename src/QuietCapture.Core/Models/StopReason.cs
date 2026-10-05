namespace QuietCapture.Core.Models;

public enum StopReason
{
    UserRequested,
    DiskSpaceLow,
    BackendFailure,
    DeviceDisconnected,
    TargetUnavailable,
    TargetInvalidated,
    SessionLocked,
    SystemSuspend,
    DisplayTopologyChanged,
    ApplicationExit,
    SystemShutdown,
    SystemLogoff
}
