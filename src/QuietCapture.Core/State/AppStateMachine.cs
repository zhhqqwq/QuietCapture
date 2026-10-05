namespace QuietCapture.Core.State;

public sealed class AppStateMachine : IAppStateMachine
{
    private static readonly IReadOnlyDictionary<
        AppState,
        IReadOnlySet<AppState>> AllowedTransitions =
        new Dictionary<AppState, IReadOnlySet<AppState>>
        {
            [AppState.Idle] =
                new HashSet<AppState>
                {
                    AppState.Selecting
                },

            [AppState.Selecting] =
                new HashSet<AppState>
                {
                    AppState.Idle,
                    AppState.Ready
                },

            [AppState.Ready] =
                new HashSet<AppState>
                {
                    AppState.Idle,
                    AppState.Starting
                },

            [AppState.Starting] =
                new HashSet<AppState>
                {
                    AppState.Idle,
                    AppState.Ready,
                    AppState.Recording
                },

            [AppState.Recording] =
                new HashSet<AppState>
                {
                    AppState.Stopping
                },

            [AppState.Stopping] =
                new HashSet<AppState>
                {
                    AppState.Idle,
                    AppState.Faulted
                },

            [AppState.Faulted] =
                new HashSet<AppState>
                {
                    AppState.Idle
                }
        };

    public AppStateMachine(
        AppState initialState = AppState.Idle)
    {
        CurrentState = initialState;
    }

    public AppState CurrentState { get; private set; }

    public event EventHandler<AppStateChangedEventArgs>?
        StateChanged;

    public bool CanTransitionTo(AppState nextState)
    {
        return nextState != CurrentState &&
            AllowedTransitions[CurrentState]
                .Contains(nextState);
    }

    public void TransitionTo(AppState nextState)
    {
        if (!CanTransitionTo(nextState))
        {
            throw new InvalidOperationException(
                $"Illegal application state transition: {CurrentState} -> {nextState}.");
        }

        AppState previous = CurrentState;
        CurrentState = nextState;

        StateChanged?.Invoke(
            this,
            new AppStateChangedEventArgs(
                previous,
                nextState));
    }
}
