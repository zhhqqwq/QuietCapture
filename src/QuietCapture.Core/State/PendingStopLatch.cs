using QuietCapture.Core.Models;

namespace QuietCapture.Core.State;

public sealed class PendingStopLatch
{
    public StopReason? Reason { get; private set; }

    public bool HasPendingStop =>
        Reason.HasValue;

    public bool TryRequest(StopReason reason)
    {
        if (Reason.HasValue)
        {
            return false;
        }

        Reason = reason;
        return true;
    }

    public StopReason? Consume()
    {
        StopReason? reason = Reason;
        Reason = null;
        return reason;
    }

    public void Clear()
    {
        Reason = null;
    }
}
