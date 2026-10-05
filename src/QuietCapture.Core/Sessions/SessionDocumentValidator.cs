using QuietCapture.Core.Models;

namespace QuietCapture.Core.Sessions;

public sealed class SessionDocumentValidator
{
    public SessionDocumentValidationResult Validate(
        SessionMetadataDocument document,
        string expectedWorkingDirectory)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(
                expectedWorkingDirectory))
        {
            throw new ArgumentException(
                "Expected working directory must not be empty.",
                nameof(expectedWorkingDirectory));
        }

        if (document.SchemaVersion !=
            SessionMetadataDocument.CurrentSchemaVersion)
        {
            return Invalid(
                $"Unsupported session schema version: {document.SchemaVersion}.");
        }

        if (!Guid.TryParse(
                document.SessionId,
                out Guid sessionGuid) ||
            sessionGuid == Guid.Empty)
        {
            return Invalid(
                "Session ID is invalid.");
        }

        if (!Enum.TryParse(
                document.Status,
                ignoreCase: false,
                out SessionStatus status) ||
            !Enum.IsDefined(status))
        {
            return Invalid(
                "Session status is invalid.");
        }

        if (document.StopReason is not null &&
            (!Enum.TryParse(
                document.StopReason,
                ignoreCase: false,
                out StopReason stopReason) ||
             !Enum.IsDefined(stopReason)))
        {
            return Invalid(
                "Stop reason is invalid.");
        }

        if (!PathsEqual(
                document.WorkingDirectory,
                expectedWorkingDirectory))
        {
            return Invalid(
                "Persisted working directory does not match the scanned directory.");
        }

        if (!IsFileInsideDirectory(
                document.TempMediaPath,
                expectedWorkingDirectory))
        {
            return Invalid(
                "Temporary media path is not inside the Session working directory.");
        }

        if (string.IsNullOrWhiteSpace(
                document.FinalMediaPath))
        {
            return Invalid(
                "Final media path is missing.");
        }

        string? targetError =
            ValidateTarget(document.Target);

        if (targetError is not null)
        {
            return Invalid(targetError);
        }

        string? optionsError =
            ValidateOptions(document.Options);

        if (optionsError is not null)
        {
            return Invalid(optionsError);
        }

        if ((status is
                 SessionStatus.Recording or
                 SessionStatus.Finalizing or
                 SessionStatus.Completed or
                 SessionStatus.Interrupted or
                 SessionStatus.StopFailed) &&
            document.StartedAt is null)
        {
            return Invalid(
                "Persisted status requires StartedAt.");
        }

        if (SessionLifecycle.IsTerminal(status) &&
            document.FinishedAt is null)
        {
            return Invalid(
                "Terminal Session status requires FinishedAt.");
        }

        return new SessionDocumentValidationResult(
            true,
            new SessionId(sessionGuid),
            status,
            null);
    }

    private static string? ValidateTarget(
        CaptureTargetDocument target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Kind switch
        {
            "Area" =>
                string.IsNullOrWhiteSpace(
                    target.MonitorId)
                    ? "Area target monitor ID is missing."
                    : ValidatePositiveSize(
                        target.Width,
                        target.Height,
                        "Area target"),

            "Window" =>
                !target.Hwnd.HasValue ||
                target.Hwnd.Value == 0
                    ? "Window target HWND is invalid."
                    : null,

            "Monitor" =>
                string.IsNullOrWhiteSpace(
                    target.MonitorId)
                    ? "Monitor target ID is missing."
                    : null,

            _ =>
                "Capture target kind is invalid."
        };
    }

    private static string? ValidateOptions(
        RecordingOptionsDocument options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? sizeError =
            ValidatePositiveSize(
                options.OutputWidth,
                options.OutputHeight,
                "Output");

        if (sizeError is not null)
        {
            return sizeError;
        }

        if (options.FrameRate <= 0)
        {
            return "Frame rate must be positive.";
        }

        if (string.IsNullOrWhiteSpace(
                options.QualityPresetId))
        {
            return "Quality preset ID is missing.";
        }

        if (options.RecordSystemAudio &&
            string.IsNullOrWhiteSpace(
                options.SystemAudioDeviceId))
        {
            return "Enabled system audio is missing a concrete device ID.";
        }

        if (options.RecordMicrophone &&
            string.IsNullOrWhiteSpace(
                options.MicrophoneDeviceId))
        {
            return "Enabled microphone is missing a concrete device ID.";
        }

        return null;
    }

    private static string? ValidatePositiveSize(
        int? width,
        int? height,
        string name)
    {
        return !width.HasValue ||
            !height.HasValue ||
            width.Value <= 0 ||
            height.Value <= 0
            ? $"{name} dimensions must be positive."
            : null;
    }

    private static string? ValidatePositiveSize(
        int width,
        int height,
        string name)
    {
        return width <= 0 ||
            height <= 0
            ? $"{name} dimensions must be positive."
            : null;
    }

    private static bool IsFileInsideDirectory(
        string filePath,
        string directory)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string? parent =
            Path.GetDirectoryName(
                Path.GetFullPath(filePath));

        return parent is not null &&
            PathsEqual(
                parent,
                directory);
    }

    private static bool PathsEqual(
        string first,
        string second)
    {
        if (string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(first)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static SessionDocumentValidationResult Invalid(
        string error)
    {
        return new SessionDocumentValidationResult(
            false,
            null,
            null,
            error);
    }
}

public sealed record SessionDocumentValidationResult(
    bool IsValid,
    SessionId? SessionId,
    SessionStatus? Status,
    string? Error);
