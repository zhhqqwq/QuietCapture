namespace QuietCapture.Core.Sessions;

internal static class SessionLifecycle
{
    private static readonly IReadOnlyDictionary<
        SessionStatus,
        IReadOnlySet<SessionStatus>> AllowedTransitions =
        new Dictionary<
            SessionStatus,
            IReadOnlySet<SessionStatus>>
        {
            [SessionStatus.Created] =
                new HashSet<SessionStatus>
                {
                    SessionStatus.Starting,
                    SessionStatus.Interrupted,
                    SessionStatus.Orphaned
                },

            [SessionStatus.Starting] =
                new HashSet<SessionStatus>
                {
                    SessionStatus.Recording,
                    SessionStatus.FailedToStart,
                    SessionStatus.Interrupted,
                    SessionStatus.Orphaned
                },

            [SessionStatus.Recording] =
                new HashSet<SessionStatus>
                {
                    SessionStatus.Finalizing,
                    SessionStatus.Interrupted,
                    SessionStatus.StopFailed,
                    SessionStatus.Orphaned
                },

            [SessionStatus.Finalizing] =
                new HashSet<SessionStatus>
                {
                    SessionStatus.Completed,
                    SessionStatus.Interrupted,
                    SessionStatus.StopFailed,
                    SessionStatus.Orphaned
                },

            [SessionStatus.Completed] =
                new HashSet<SessionStatus>(),

            [SessionStatus.FailedToStart] =
                new HashSet<SessionStatus>(),

            [SessionStatus.Interrupted] =
                new HashSet<SessionStatus>(),

            [SessionStatus.StopFailed] =
                new HashSet<SessionStatus>(),

            [SessionStatus.Orphaned] =
                new HashSet<SessionStatus>()
        };

    public static bool CanTransition(
        SessionStatus current,
        SessionStatus next)
    {
        return current != next &&
            AllowedTransitions[current]
                .Contains(next);
    }

    public static void EnsureTransition(
        SessionStatus current,
        SessionStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException(
                $"Illegal session lifecycle transition: {current} -> {next}.");
        }
    }

    public static bool IsTerminal(
        SessionStatus status)
    {
        return AllowedTransitions[status].Count == 0;
    }
}
