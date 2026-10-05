using QuietCapture.Core.Ports;

namespace QuietCapture.Core.Sessions;

public sealed class SessionStore
{
    private const string SessionMetadataFileName =
        "session.json";

    private readonly IFileSystem _fileSystem;

    public SessionStore(IFileSystem fileSystem)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));
    }

    public void Save(SessionMetadata session)
    {
        ArgumentNullException.ThrowIfNull(session);

        SaveDocument(
            session.WorkingDirectory,
            SessionMetadataDocument.FromDomain(
                session));
    }

    public void SaveDocument(
        string workingDirectory,
        SessionMetadataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        string path =
            GetMetadataPath(
                workingDirectory);

        _fileSystem.WriteAllTextAtomically(
            path,
            SessionMetadataJson.Serialize(
                document));
    }

    public SessionMetadataDocument LoadDocument(
        string workingDirectory)
    {
        string path =
            GetMetadataPath(
                workingDirectory);

        return SessionMetadataJson.Deserialize(
            _fileSystem.ReadAllText(path));
    }

    public static string GetMetadataPath(
        string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                workingDirectory))
        {
            throw new ArgumentException(
                "Working directory must not be empty.",
                nameof(workingDirectory));
        }

        return Path.Combine(
            workingDirectory,
            SessionMetadataFileName);
    }
}
