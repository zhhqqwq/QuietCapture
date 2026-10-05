using QuietCapture.Core.Ports;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Storage;

public sealed class MediaPublisher
{
    private readonly IFileSystem _fileSystem;

    public MediaPublisher(IFileSystem fileSystem)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));
    }

    public MediaPublishResult Publish(
        SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status !=
            SessionStatus.Finalizing)
        {
            throw new InvalidOperationException(
                "Media can only be published while the Session is Finalizing.");
        }

        if (!_fileSystem.FileExists(
                session.TempMediaPath))
        {
            return new MediaPublishResult(
                MediaPublishOutcome.MissingPartial,
                0,
                "Partial media file is missing.");
        }

        long partialBytes =
            _fileSystem.GetFileLength(
                session.TempMediaPath);

        if (partialBytes <= 0)
        {
            return new MediaPublishResult(
                MediaPublishOutcome.EmptyPartial,
                partialBytes,
                "Partial media file is empty.");
        }

        if (!_fileSystem.FileExists(
                session.FinalMediaPath))
        {
            return new MediaPublishResult(
                MediaPublishOutcome.MissingFinalReservation,
                partialBytes,
                "Final-path reservation is missing.");
        }

        long finalBytes =
            _fileSystem.GetFileLength(
                session.FinalMediaPath);

        if (finalBytes != 0)
        {
            return new MediaPublishResult(
                MediaPublishOutcome.FinalReservationNotEmpty,
                partialBytes,
                "Final-path reservation is not empty.");
        }

        StorageVolumeInfo sourceVolume =
            _fileSystem.GetStorageVolumeInfo(
                session.TempMediaPath);

        StorageVolumeInfo destinationVolume =
            _fileSystem.GetStorageVolumeInfo(
                session.FinalMediaPath);

        if (!string.Equals(
                sourceVolume.VolumeId,
                destinationVolume.VolumeId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new MediaPublishResult(
                MediaPublishOutcome.DifferentVolume,
                partialBytes,
                "Partial media and final output are not on the same volume.");
        }

        try
        {
            _fileSystem
                .MoveFileReplacingEmptyReservation(
                    session.TempMediaPath,
                    session.FinalMediaPath);

            return new MediaPublishResult(
                MediaPublishOutcome.Published,
                partialBytes,
                null);
        }
        catch (Exception ex)
            when (ex is
                IOException or
                UnauthorizedAccessException)
        {
            return new MediaPublishResult(
                MediaPublishOutcome.Failed,
                partialBytes,
                ex.Message);
        }
    }
}
