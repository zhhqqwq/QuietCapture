using QuietCapture.Core.Ports;

namespace QuietCapture.Core.Sessions;

public sealed class RecoveryIndexStore
{
    private readonly object _gate = new();
    private readonly IFileSystem _fileSystem;
    private readonly string _indexPath;

    public RecoveryIndexStore(
        IFileSystem fileSystem,
        string indexPath)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));

        if (string.IsNullOrWhiteSpace(indexPath))
        {
            throw new ArgumentException(
                "Recovery index path must not be empty.",
                nameof(indexPath));
        }

        _indexPath =
            Path.GetFullPath(indexPath);
    }

    public string IndexPath => _indexPath;

    public RecoveryIndex Load()
    {
        lock (_gate)
        {
            return LoadUnsafe();
        }
    }

    public bool Contains(
        RecoveryIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            RecoveryIndex current =
                LoadUnsafe();

            return current.Entries.Any(existing =>
                existing.SessionId ==
                    entry.SessionId &&
                PathsEqual(
                    existing.WorkingDirectory,
                    entry.WorkingDirectory));
        }
    }

    public void Add(
        RecoveryIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            RecoveryIndex current =
                LoadUnsafe();

            RecoveryIndexEntry? sameDirectory =
                current.Entries.FirstOrDefault(
                    existing =>
                        PathsEqual(
                            existing.WorkingDirectory,
                            entry.WorkingDirectory));

            if (sameDirectory is not null)
            {
                if (sameDirectory.SessionId ==
                    entry.SessionId)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Recovery index already maps the working directory to another Session.");
            }

            SaveUnsafe(
                new RecoveryIndex(
                    current.Entries.Append(entry)));
        }
    }

    public void Remove(
        RecoveryIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            RecoveryIndex current =
                LoadUnsafe();

            RecoveryIndexEntry[] retained =
                current.Entries
                    .Where(existing =>
                        !(
                            existing.SessionId ==
                                entry.SessionId &&
                            PathsEqual(
                                existing.WorkingDirectory,
                                entry.WorkingDirectory)))
                    .ToArray();

            if (retained.Length ==
                current.Entries.Count)
            {
                return;
            }

            SaveUnsafe(
                new RecoveryIndex(retained));
        }
    }

    private RecoveryIndex LoadUnsafe()
    {
        if (!_fileSystem.FileExists(
                _indexPath))
        {
            return RecoveryIndex.Empty;
        }

        return RecoveryIndexJson.Deserialize(
            _fileSystem.ReadAllText(
                _indexPath));
    }

    private void SaveUnsafe(
        RecoveryIndex index)
    {
        string? directory =
            Path.GetDirectoryName(
                _indexPath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            _fileSystem.CreateDirectory(
                directory);
        }

        _fileSystem.WriteAllTextAtomically(
            _indexPath,
            RecoveryIndexJson.Serialize(index));
    }

    private static bool PathsEqual(
        string first,
        string second)
    {
        return string.Equals(
            Normalize(first),
            Normalize(second),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(
        string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }
}
