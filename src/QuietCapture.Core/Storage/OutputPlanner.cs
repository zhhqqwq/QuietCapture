using QuietCapture.Core.Ports;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Storage;

public sealed class OutputPlanner
{
    private readonly IFileSystem _fileSystem;

    public OutputPlanner(IFileSystem fileSystem)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));
    }

    public OutputReservation Reserve(
        string outputDirectory,
        SessionId sessionId,
        DateTimeOffset timestamp,
        int maximumCollisionIndex =
            OutputFileNamePolicy.DefaultMaximumCollisionIndex)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException(
                "Output directory must not be empty.",
                nameof(outputDirectory));
        }

        if (sessionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID must not be empty.",
                nameof(sessionId));
        }

        if (maximumCollisionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCollisionIndex));
        }

        StorageVolumeInfo outputVolume =
            _fileSystem.GetStorageVolumeInfo(
                outputDirectory);

        if (!outputVolume.IsWritable)
        {
            throw new InvalidOperationException(
                "The output volume is not writable.");
        }

        if (outputVolume.IsFat32)
        {
            throw new InvalidOperationException(
                "FAT32 is not supported for recording output.");
        }

        for (int collisionIndex = 0;
             collisionIndex <= maximumCollisionIndex;
             collisionIndex++)
        {
            string fileName =
                OutputFileNamePolicy.CreateFileName(
                    timestamp,
                    collisionIndex);

            OutputPlan plan =
                OutputPlan.Create(
                    outputDirectory,
                    sessionId,
                    fileName);

            StorageVolumeInfo workingVolume =
                _fileSystem.GetStorageVolumeInfo(
                    plan.WorkingDirectory);

            if (!string.Equals(
                    outputVolume.VolumeId,
                    workingVolume.VolumeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Session working directory and final output must be on the same volume.");
            }

            if (_fileSystem.TryReserveFile(
                    plan.FinalMediaPath))
            {
                return new OutputReservation(
                    plan,
                    outputVolume,
                    collisionIndex);
            }
        }

        throw new InvalidOperationException(
            "No final output path could be reserved within the configured collision range.");
    }

    public bool ReleaseIfUnused(
        OutputReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(
            reservation);

        return _fileSystem.DeleteFileIfEmpty(
            reservation.Plan.FinalMediaPath);
    }
}
