namespace QuietCapture.Core.Storage;

public enum MediaPublishOutcome
{
    Published,
    MissingPartial,
    EmptyPartial,
    MissingFinalReservation,
    FinalReservationNotEmpty,
    DifferentVolume,
    Failed
}

public sealed record MediaPublishResult(
    MediaPublishOutcome Outcome,
    long PartialBytes,
    string? Error)
{
    public bool Succeeded =>
        Outcome == MediaPublishOutcome.Published;
}
