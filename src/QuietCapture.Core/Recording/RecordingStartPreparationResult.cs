using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Recording;

public sealed record RecordingStartPreparationResult
{
    private RecordingStartPreparationResult(
        bool succeeded,
        SessionMetadata? session,
        RecordingStartFailureDetail? failure)
    {
        Succeeded = succeeded;
        Session = session;
        Failure = failure;
    }

    public bool Succeeded { get; }

    public SessionMetadata? Session { get; }

    public RecordingStartFailureDetail? Failure
        { get; }

    internal static RecordingStartPreparationResult Success(
        SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new RecordingStartPreparationResult(
            succeeded: true,
            session,
            failure: null);
    }

    internal static RecordingStartPreparationResult Failed(
        RecordingStartFailureDetail failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new RecordingStartPreparationResult(
            succeeded: false,
            session: null,
            failure);
    }
}
