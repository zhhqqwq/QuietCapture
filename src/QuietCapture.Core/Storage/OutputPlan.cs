using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Storage;

public sealed record OutputPlan
{
    private const string SessionRootDirectoryName =
        ".screenrecorder";
    private const string SessionsDirectoryName =
        "sessions";
    private const string PartialMediaFileName =
        "recording.partial.mp4";
    private const string SessionMetadataFileName =
        "session.json";

    private OutputPlan(
        string outputDirectory,
        string workingDirectory,
        string tempMediaPath,
        string sessionMetadataPath,
        string finalMediaPath)
    {
        OutputDirectory = outputDirectory;
        WorkingDirectory = workingDirectory;
        TempMediaPath = tempMediaPath;
        SessionMetadataPath = sessionMetadataPath;
        FinalMediaPath = finalMediaPath;
    }

    public string OutputDirectory { get; }

    public string WorkingDirectory { get; }

    public string TempMediaPath { get; }

    public string SessionMetadataPath { get; }

    public string FinalMediaPath { get; }

    public static OutputPlan Create(
        string outputDirectory,
        SessionId sessionId,
        string finalFileName)
    {
        if (sessionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID must not be empty.",
                nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException(
                "Output directory must not be empty.",
                nameof(outputDirectory));
        }

        if (string.IsNullOrWhiteSpace(finalFileName))
        {
            throw new ArgumentException(
                "Final file name must not be empty.",
                nameof(finalFileName));
        }

        if (!string.Equals(
                Path.GetFileName(finalFileName),
                finalFileName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Final file name must not contain a directory component.",
                nameof(finalFileName));
        }

        string workingDirectory =
            Path.Combine(
                outputDirectory,
                SessionRootDirectoryName,
                SessionsDirectoryName,
                sessionId.ToString());

        return new OutputPlan(
            outputDirectory,
            workingDirectory,
            Path.Combine(
                workingDirectory,
                PartialMediaFileName),
            Path.Combine(
                workingDirectory,
                SessionMetadataFileName),
            Path.Combine(
                outputDirectory,
                finalFileName));
    }
}
