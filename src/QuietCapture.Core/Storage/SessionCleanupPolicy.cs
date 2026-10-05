using QuietCapture.Core.Ports;
using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Storage;

public sealed class SessionCleanupPolicy
{
    private readonly IFileSystem _fileSystem;
    private readonly RecoveryIndexStore _recoveryIndexStore;

    public SessionCleanupPolicy(
        IFileSystem fileSystem,
        RecoveryIndexStore recoveryIndexStore)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));

        _recoveryIndexStore =
            recoveryIndexStore ??
            throw new ArgumentNullException(
                nameof(recoveryIndexStore));
    }

    public bool TryCleanup(
        SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status is
            SessionStatus.StopFailed or
            SessionStatus.Interrupted or
            SessionStatus.Orphaned)
        {
            return false;
        }

        if (session.Status is not
                SessionStatus.Completed and not
                SessionStatus.FailedToStart)
        {
            return false;
        }

        RecoveryIndexEntry entry =
            RecoveryIndexEntry.FromSession(
                session);

        if (_recoveryIndexStore.Contains(entry))
        {
            return false;
        }

        if (session.Status ==
            SessionStatus.Completed)
        {
            if (_fileSystem.FileExists(
                    session.TempMediaPath))
            {
                return false;
            }

            if (!_fileSystem.FileExists(
                    session.FinalMediaPath) ||
                _fileSystem.GetFileLength(
                    session.FinalMediaPath) <= 0)
            {
                return false;
            }
        }
        else
        {
            if (_fileSystem.FileExists(
                    session.TempMediaPath) &&
                !_fileSystem.DeleteFileIfEmpty(
                    session.TempMediaPath))
            {
                return false;
            }

            if (_fileSystem.FileExists(
                    session.FinalMediaPath) &&
                !_fileSystem.DeleteFileIfEmpty(
                    session.FinalMediaPath))
            {
                return false;
            }
        }

        string metadataPath =
            SessionStore.GetMetadataPath(
                session.WorkingDirectory);

        _fileSystem.DeleteFile(
            metadataPath);

        _fileSystem.DeleteDirectoryIfEmpty(
            session.WorkingDirectory);

        return true;
    }
}
