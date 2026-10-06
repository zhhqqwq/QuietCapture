namespace QuietCapture.Core.Recovery;

public sealed record RecoveryStartupResolution(
    RecoveryCandidate Candidate,
    RecoveryResolutionResult Resolution);

public sealed record RecoveryStartupResult
{
    public RecoveryStartupResult(
        IEnumerable<RecoveryCandidate> remainingCandidates,
        IEnumerable<RecoveryStartupResolution> resolutions,
        IEnumerable<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(
            remainingCandidates);
        ArgumentNullException.ThrowIfNull(
            resolutions);
        ArgumentNullException.ThrowIfNull(
            diagnostics);

        RemainingCandidates =
            remainingCandidates.ToArray();

        Resolutions =
            resolutions.ToArray();

        Diagnostics =
            diagnostics.ToArray();
    }

    public IReadOnlyList<RecoveryCandidate>
        RemainingCandidates { get; }

    public IReadOnlyList<RecoveryStartupResolution>
        Resolutions { get; }

    public IReadOnlyList<string>
        Diagnostics { get; }

    public bool HasOutstandingRecovery =>
        RemainingCandidates.Count != 0 ||
        Resolutions.Any(item =>
            !item.Resolution.Succeeded);
}
