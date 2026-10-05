using QuietCapture.Core.Models;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Sessions;

public sealed class SessionManager
{
    private readonly IFileSystem _fileSystem;
    private readonly OutputPlanner _outputPlanner;
    private readonly SessionPersistenceService
        _persistenceService;

    public SessionManager(
        IFileSystem fileSystem,
        OutputPlanner outputPlanner,
        SessionPersistenceService persistenceService)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));

        _outputPlanner =
            outputPlanner ??
            throw new ArgumentNullException(
                nameof(outputPlanner));

        _persistenceService =
            persistenceService ??
            throw new ArgumentNullException(
                nameof(persistenceService));
    }

    public SessionMetadata CreateSession(
        CaptureTarget target,
        RecordingOptions options,
        string outputDirectory,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(
            target);
        ArgumentNullException.ThrowIfNull(
            options);

        SessionId sessionId =
            SessionId.New();

        OutputReservation reservation =
            _outputPlanner.Reserve(
                outputDirectory,
                sessionId,
                createdAt);

        OutputPlan plan = reservation.Plan;

        try
        {
            _fileSystem.CreateDirectory(
                plan.WorkingDirectory);

            SessionMetadata session =
                SessionMetadata.Create(
                    sessionId,
                    target,
                    options,
                    plan.WorkingDirectory,
                    plan.TempMediaPath,
                    plan.FinalMediaPath,
                    createdAt);

            _persistenceService
                .RegisterCreatedSession(
                    session);

            return session;
        }
        catch
        {
            CleanupFailedCreation(
                reservation);
            throw;
        }
    }

    public RecoveryIndexEntry CreateRecoveryIndexEntry(
        SessionMetadata session)
    {
        return RecoveryIndexEntry.FromSession(
            session);
    }

    private void CleanupFailedCreation(
        OutputReservation reservation)
    {
        OutputPlan plan =
            reservation.Plan;

        bool metadataExists =
            _fileSystem.FileExists(
                plan.SessionMetadataPath);

        bool preserveWorkingDirectory =
            metadataExists ||
            (_fileSystem.FileExists(
                 plan.TempMediaPath) &&
             _fileSystem.GetFileLength(
                 plan.TempMediaPath) > 0);

        if (!metadataExists)
        {
            _outputPlanner.ReleaseIfUnused(
                reservation);
        }

        if (!preserveWorkingDirectory)
        {
            _fileSystem.DeleteDirectoryIfEmpty(
                plan.WorkingDirectory);
        }
    }
}
