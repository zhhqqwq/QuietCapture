using QuietCapture.Core.Preflight;

namespace QuietCapture.Core.Recording;

public enum RecordingStartFailure
{
    Preflight,
    SessionCreation
}

public sealed record RecordingStartFailureDetail(
    RecordingStartFailure Kind,
    RecordingPreflightFailure? PreflightFailure,
    string Error);
