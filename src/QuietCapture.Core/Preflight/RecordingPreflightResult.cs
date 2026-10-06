using QuietCapture.Core.Models;

namespace QuietCapture.Core.Preflight;

public sealed record RecordingPreflightResult
{
    private RecordingPreflightResult(
        bool succeeded,
        CaptureTarget? target,
        string? outputDirectory,
        RecordingOptions? options,
        RecordingPreflightFailure? failure,
        string? error)
    {
        Succeeded = succeeded;
        Target = target;
        OutputDirectory = outputDirectory;
        Options = options;
        Failure = failure;
        Error = error;
    }

    public bool Succeeded { get; }

    public CaptureTarget? Target { get; }

    public string? OutputDirectory { get; }

    public RecordingOptions? Options { get; }

    public RecordingPreflightFailure? Failure
        { get; }

    public string? Error { get; }

    internal static RecordingPreflightResult Success(
        CaptureTarget target,
        string outputDirectory,
        RecordingOptions options)
    {
        return new RecordingPreflightResult(
            succeeded: true,
            target,
            outputDirectory,
            options,
            failure: null,
            error: null);
    }

    internal static RecordingPreflightResult Failed(
        RecordingPreflightFailure failure,
        string error)
    {
        return new RecordingPreflightResult(
            succeeded: false,
            target: null,
            outputDirectory: null,
            options: null,
            failure,
            error);
    }
}
