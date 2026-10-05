namespace QuietCapture.Core.Storage;

public sealed record OutputReservation(
    OutputPlan Plan,
    StorageVolumeInfo Volume,
    int CollisionIndex);
