using QuietCapture.Core.Models;
using QuietCapture.Core.State;

namespace QuietCapture.Core.Tests;

public sealed class PendingStopLatchTests
{
    [Fact]
    public void FirstPendingStopReasonWinsUntilConsumed()
    {
        var latch = new PendingStopLatch();

        Assert.True(
            latch.TryRequest(
                StopReason.UserRequested));

        Assert.False(
            latch.TryRequest(
                StopReason.BackendFailure));

        Assert.Equal(
            StopReason.UserRequested,
            latch.Reason);
    }

    [Fact]
    public void Consume_ReturnsReasonAndClearsLatch()
    {
        var latch = new PendingStopLatch();

        latch.TryRequest(
            StopReason.SessionLocked);

        StopReason? reason =
            latch.Consume();

        Assert.Equal(
            StopReason.SessionLocked,
            reason);
        Assert.False(
            latch.HasPendingStop);
        Assert.Null(
            latch.Consume());
    }

    [Fact]
    public void StartingStop_RemainsStartingUntilStartOutcomeIsKnown()
    {
        var stateMachine =
            new AppStateMachine(
                AppState.Starting);
        var latch =
            new PendingStopLatch();

        latch.TryRequest(
            StopReason.UserRequested);

        Assert.Equal(
            AppState.Starting,
            stateMachine.CurrentState);

        stateMachine.TransitionTo(
            AppState.Recording);

        StopReason? pending =
            latch.Consume();

        Assert.Equal(
            StopReason.UserRequested,
            pending);

        stateMachine.TransitionTo(
            AppState.Stopping);

        Assert.Equal(
            AppState.Stopping,
            stateMachine.CurrentState);
    }
}
