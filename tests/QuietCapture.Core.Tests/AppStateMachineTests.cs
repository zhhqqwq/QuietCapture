using QuietCapture.Core.State;

namespace QuietCapture.Core.Tests;

public sealed class AppStateMachineTests
{
    [Fact]
    public void NormalRecordingFlow_UsesExpectedStateSequence()
    {
        var machine = new AppStateMachine();

        machine.TransitionTo(AppState.Selecting);
        machine.TransitionTo(AppState.Ready);
        machine.TransitionTo(AppState.Starting);
        machine.TransitionTo(AppState.Recording);
        machine.TransitionTo(AppState.Stopping);
        machine.TransitionTo(AppState.Idle);

        Assert.Equal(
            AppState.Idle,
            machine.CurrentState);
    }

    [Fact]
    public void Starting_CannotTransitionDirectlyToStopping()
    {
        var machine =
            new AppStateMachine(
                AppState.Starting);

        Assert.False(
            machine.CanTransitionTo(
                AppState.Stopping));

        Assert.Throws<InvalidOperationException>(
            () =>
                machine.TransitionTo(
                    AppState.Stopping));
    }

    [Fact]
    public void StateChanged_ReportsPreviousAndCurrentState()
    {
        var machine = new AppStateMachine();
        AppStateChangedEventArgs? observed = null;

        machine.StateChanged +=
            (_, eventArgs) =>
                observed = eventArgs;

        machine.TransitionTo(
            AppState.Selecting);

        Assert.NotNull(observed);
        Assert.Equal(
            AppState.Idle,
            observed.PreviousState);
        Assert.Equal(
            AppState.Selecting,
            observed.CurrentState);
    }

    [Fact]
    public void Stopping_CanEnterFaulted_WhenBackendSafetyIsUnknown()
    {
        var machine =
            new AppStateMachine(
                AppState.Stopping);

        machine.TransitionTo(
            AppState.Faulted);

        Assert.Equal(
            AppState.Faulted,
            machine.CurrentState);
    }
}
