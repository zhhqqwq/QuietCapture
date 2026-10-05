namespace QuietCapture.Core.State;

public interface IAppStateMachine
{
    AppState CurrentState { get; }

    event EventHandler<AppStateChangedEventArgs>? StateChanged;

    bool CanTransitionTo(AppState nextState);

    void TransitionTo(AppState nextState);
}

public sealed class AppStateChangedEventArgs : EventArgs
{
    public AppStateChangedEventArgs(
        AppState previousState,
        AppState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public AppState PreviousState { get; }

    public AppState CurrentState { get; }
}
