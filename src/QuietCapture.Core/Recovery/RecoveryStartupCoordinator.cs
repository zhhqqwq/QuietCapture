namespace QuietCapture.Core.Recovery;

public sealed class RecoveryStartupCoordinator
{
    private readonly RecoveryService _recoveryService;
    private readonly RecoveryResolutionService
        _resolutionService;

    public RecoveryStartupCoordinator(
        RecoveryService recoveryService,
        RecoveryResolutionService resolutionService)
    {
        _recoveryService =
            recoveryService ??
            throw new ArgumentNullException(
                nameof(recoveryService));

        _resolutionService =
            resolutionService ??
            throw new ArgumentNullException(
                nameof(resolutionService));
    }

    public RecoveryStartupResult Run(
        string recoveryIndexPath,
        IEnumerable<string> knownOutputDirectories)
    {
        if (string.IsNullOrWhiteSpace(
                recoveryIndexPath))
        {
            throw new ArgumentException(
                "Recovery index path must not be empty.",
                nameof(recoveryIndexPath));
        }

        ArgumentNullException.ThrowIfNull(
            knownOutputDirectories);

        string[] outputDirectories =
            NormalizeDistinctDirectories(
                knownOutputDirectories);

        RecoveryScanResult scan =
            _recoveryService.Scan(
                recoveryIndexPath,
                outputDirectories);

        var remaining =
            new List<RecoveryCandidate>();

        var resolutions =
            new List<RecoveryStartupResolution>();

        foreach (RecoveryCandidate candidate
                 in scan.Candidates)
        {
            RecoveryResolutionResult resolution =
                ResolveSafely(candidate);

            resolutions.Add(
                new RecoveryStartupResolution(
                    candidate,
                    resolution));

            if (candidate.Classification !=
                    RecoveryClassification
                        .NoRecoveryRequired ||
                !resolution.Succeeded)
            {
                remaining.Add(candidate);
            }
        }

        return new RecoveryStartupResult(
            remaining,
            resolutions,
            scan.Diagnostics);
    }

    private RecoveryResolutionResult ResolveSafely(
        RecoveryCandidate candidate)
    {
        try
        {
            return _resolutionService.Resolve(
                candidate);
        }
        catch (Exception ex)
            when (IsRecoverableStartupException(ex))
        {
            RecoveryAction action =
                candidate.Classification ==
                    RecoveryClassification
                        .NoRecoveryRequired
                    ? candidate.FromRecoveryIndex
                        ? RecoveryAction
                            .RemoveStaleIndexAndCleanup
                        : RecoveryAction
                            .CleanupNoRecoveryRequired
                    : RecoveryAction.Preserve;

            return RecoveryResolutionResult.Failed(
                action,
                indexEntryRemoved: false,
                ex.Message);
        }
    }

    private static string[] NormalizeDistinctDirectories(
        IEnumerable<string> directories)
    {
        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string fullPath =
                Path.GetFullPath(directory);

            string? root =
                Path.GetPathRoot(fullPath);

            string normalized =
                !string.IsNullOrWhiteSpace(root) &&
                string.Equals(
                    fullPath,
                    root,
                    StringComparison.OrdinalIgnoreCase)
                    ? fullPath
                    : fullPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            result.Add(normalized);
        }

        return result
            .OrderBy(
                path => path,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsRecoverableStartupException(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            ArgumentException or
            System.Text.Json.JsonException;
    }
}
