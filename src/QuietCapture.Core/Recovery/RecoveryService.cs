using QuietCapture.Core.Ports;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Recovery;

public sealed class RecoveryService
{
    private const string SessionRootDirectoryName =
        ".screenrecorder";
    private const string SessionsDirectoryName =
        "sessions";
    private const string SessionMetadataFileName =
        "session.json";
    private const string PartialMediaFileName =
        "recording.partial.mp4";

    private readonly IFileSystem _fileSystem;
    private readonly SessionDocumentValidator _validator;

    public RecoveryService(
        IFileSystem fileSystem,
        SessionDocumentValidator validator)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));

        _validator =
            validator ??
            throw new ArgumentNullException(
                nameof(validator));
    }

    public RecoveryScanResult Scan(
        string recoveryIndexPath,
        IEnumerable<string> outputDirectories)
    {
        if (string.IsNullOrWhiteSpace(
                recoveryIndexPath))
        {
            throw new ArgumentException(
                "Recovery index path must not be empty.",
                nameof(recoveryIndexPath));
        }

        ArgumentNullException.ThrowIfNull(
            outputDirectories);

        var diagnostics =
            new List<string>();

        var seeds =
            new Dictionary<string, RecoverySeed>(
                StringComparer.OrdinalIgnoreCase);

        LoadIndexSeeds(
            recoveryIndexPath,
            seeds,
            diagnostics);

        foreach (string outputDirectory
                 in outputDirectories
                     .Where(path =>
                         !string.IsNullOrWhiteSpace(path))
                     .Distinct(
                         StringComparer.OrdinalIgnoreCase))
        {
            ScanOutputDirectory(
                outputDirectory,
                seeds,
                diagnostics);
        }

        var candidates =
            seeds.Values
                .OrderBy(seed =>
                    seed.WorkingDirectory,
                    StringComparer.OrdinalIgnoreCase)
                .Select(InspectSeed)
                .ToArray();

        return new RecoveryScanResult(
            candidates,
            diagnostics);
    }

    private void LoadIndexSeeds(
        string recoveryIndexPath,
        IDictionary<string, RecoverySeed> seeds,
        ICollection<string> diagnostics)
    {
        if (!_fileSystem.FileExists(
                recoveryIndexPath))
        {
            return;
        }

        try
        {
            RecoveryIndex index =
                RecoveryIndexJson.Deserialize(
                    _fileSystem.ReadAllText(
                        recoveryIndexPath));

            foreach (RecoveryIndexEntry entry
                     in index.Entries)
            {
                string key =
                    NormalizeDirectory(
                        entry.WorkingDirectory);

                if (!seeds.TryGetValue(
                        key,
                        out RecoverySeed? seed))
                {
                    seed =
                        new RecoverySeed(
                            key);
                    seeds.Add(
                        key,
                        seed);
                }

                seed.FromRecoveryIndex = true;
                seed.IndexSessionId =
                    entry.SessionId;
            }
        }
        catch (Exception ex)
            when (ex is
                IOException or
                UnauthorizedAccessException or
                System.Text.Json.JsonException or
                InvalidOperationException or
                ArgumentException)
        {
            diagnostics.Add(
                $"Recovery index could not be read: {ex.Message}");
        }
    }

    private void ScanOutputDirectory(
        string outputDirectory,
        IDictionary<string, RecoverySeed> seeds,
        ICollection<string> diagnostics)
    {
        string sessionsRoot =
            Path.Combine(
                outputDirectory,
                SessionRootDirectoryName,
                SessionsDirectoryName);

        IReadOnlyList<string> directories;

        try
        {
            directories =
                _fileSystem.EnumerateDirectories(
                    sessionsRoot);
        }
        catch (Exception ex)
            when (ex is
                IOException or
                UnauthorizedAccessException)
        {
            diagnostics.Add(
                $"Session root could not be scanned: {sessionsRoot}: {ex.Message}");
            return;
        }

        foreach (string workingDirectory
                 in directories)
        {
            string key =
                NormalizeDirectory(
                    workingDirectory);

            if (!seeds.TryGetValue(
                    key,
                    out RecoverySeed? seed))
            {
                seed =
                    new RecoverySeed(
                        key);
                seeds.Add(
                    key,
                    seed);
            }

            seed.FromDirectoryScan = true;
        }
    }

    private RecoveryCandidate InspectSeed(
        RecoverySeed seed)
    {
        string metadataPath =
            Path.Combine(
                seed.WorkingDirectory,
                SessionMetadataFileName);

        PartialObservation conventionalPartial =
            ObservePartial(
                Path.Combine(
                    seed.WorkingDirectory,
                    PartialMediaFileName));

        IReadOnlyList<string> files;

        try
        {
            files =
                _fileSystem.EnumerateFiles(
                    seed.WorkingDirectory);
        }
        catch (Exception ex)
            when (ex is
                IOException or
                UnauthorizedAccessException)
        {
            return CreateCandidate(
                seed,
                RecoveryClassification.Orphaned,
                seed.IndexSessionId,
                null,
                conventionalPartial,
                null,
                $"Working directory could not be enumerated: {ex.Message}");
        }

        bool metadataExists =
            files.Any(path =>
                string.Equals(
                    Path.GetFileName(path),
                    SessionMetadataFileName,
                    StringComparison.OrdinalIgnoreCase)) ||
            _fileSystem.FileExists(
                metadataPath);

        if (!metadataExists)
        {
            return CreateCandidate(
                seed,
                RecoveryClassification.Orphaned,
                seed.IndexSessionId,
                null,
                conventionalPartial,
                null,
                "session.json is missing.");
        }

        SessionMetadataDocument document;

        try
        {
            document =
                SessionMetadataJson.Deserialize(
                    _fileSystem.ReadAllText(
                        metadataPath));
        }
        catch (Exception ex)
            when (ex is
                IOException or
                UnauthorizedAccessException or
                System.Text.Json.JsonException or
                InvalidOperationException or
                ArgumentException)
        {
            return CreateCandidate(
                seed,
                RecoveryClassification.Orphaned,
                seed.IndexSessionId,
                null,
                conventionalPartial,
                null,
                $"session.json is unreadable: {ex.Message}");
        }

        SessionDocumentValidationResult validation =
            _validator.Validate(
                document,
                seed.WorkingDirectory);

        if (!validation.IsValid ||
            !validation.SessionId.HasValue ||
            !validation.Status.HasValue)
        {
            return CreateCandidate(
                seed,
                RecoveryClassification.Orphaned,
                seed.IndexSessionId,
                null,
                conventionalPartial,
                document,
                validation.Error ??
                    "session.json validation failed.");
        }

        if (seed.IndexSessionId.HasValue &&
            seed.IndexSessionId.Value !=
                validation.SessionId.Value)
        {
            return CreateCandidate(
                seed,
                RecoveryClassification.Orphaned,
                validation.SessionId,
                validation.Status,
                ObservePartial(
                    document.TempMediaPath),
                document,
                "Recovery-index SessionId does not match session.json.");
        }

        PartialObservation partial =
            ObservePartial(
                document.TempMediaPath);

        RecoveryClassification classification =
            Classify(
                validation.Status.Value,
                partial.Bytes);

        string? diagnostic =
            classification ==
                RecoveryClassification.Orphaned &&
            SessionLifecycle.IsTerminal(
                validation.Status.Value) &&
            partial.Bytes > 0
                ? "Terminal Session has non-empty partial media."
                : null;

        return CreateCandidate(
            seed,
            classification,
            validation.SessionId,
            validation.Status,
            partial,
            document,
            diagnostic);
    }

    private PartialObservation ObservePartial(
        string path)
    {
        try
        {
            if (!_fileSystem.FileExists(path))
            {
                return new PartialObservation(
                    false,
                    0);
            }

            return new PartialObservation(
                true,
                _fileSystem.GetFileLength(path));
        }
        catch (Exception ex)
            when (ex is
                IOException or
                UnauthorizedAccessException)
        {
            return new PartialObservation(
                true,
                0);
        }
    }

    private static RecoveryClassification Classify(
        SessionStatus status,
        long partialBytes)
    {
        return status switch
        {
            SessionStatus.Created or
            SessionStatus.Starting or
            SessionStatus.Recording or
            SessionStatus.Finalizing or
            SessionStatus.Interrupted =>
                RecoveryClassification.Interrupted,

            SessionStatus.StopFailed =>
                RecoveryClassification.StopFailed,

            SessionStatus.Orphaned =>
                RecoveryClassification.Orphaned,

            SessionStatus.Completed or
            SessionStatus.FailedToStart =>
                partialBytes > 0
                    ? RecoveryClassification.Orphaned
                    : RecoveryClassification.NoRecoveryRequired,

            _ =>
                RecoveryClassification.Orphaned
        };
    }

    private static RecoveryCandidate CreateCandidate(
        RecoverySeed seed,
        RecoveryClassification classification,
        SessionId? sessionId,
        SessionStatus? status,
        PartialObservation partial,
        SessionMetadataDocument? metadata,
        string? diagnostic)
    {
        return new RecoveryCandidate(
            seed.WorkingDirectory,
            classification,
            sessionId,
            status,
            seed.FromRecoveryIndex,
            seed.FromDirectoryScan,
            partial.Exists,
            partial.Bytes,
            metadata,
            diagnostic);
    }

    private static string NormalizeDirectory(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Working directory must not be empty.",
                nameof(path));
        }

        return Path.GetFullPath(path)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private sealed class RecoverySeed
    {
        public RecoverySeed(
            string workingDirectory)
        {
            WorkingDirectory =
                workingDirectory;
        }

        public string WorkingDirectory { get; }

        public bool FromRecoveryIndex { get; set; }

        public bool FromDirectoryScan { get; set; }

        public SessionId? IndexSessionId { get; set; }
    }

    private readonly record struct PartialObservation(
        bool Exists,
        long Bytes);
}

public sealed record RecoveryScanResult(
    IReadOnlyList<RecoveryCandidate> Candidates,
    IReadOnlyList<string> Diagnostics);
