using System.Collections.Concurrent;
using System.Text;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Tests.Fakes;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, byte[]> _files =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, byte> _directories =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _moveGate = new();

    public Func<string, StorageVolumeInfo>? VolumeResolver { get; set; }

    public Action<string>? BeforeAtomicWrite { get; set; }

    public Func<string, Exception?>? AtomicWriteFailureFactory { get; set; }

    public Func<string, string, Exception?>? MoveFileFailureFactory { get; set; }

    public Func<string, Exception?>? DeleteFileFailureFactory { get; set; }

    public IReadOnlyCollection<string> FilePaths =>
        _files.Keys.ToArray();

    public IReadOnlyCollection<string> DirectoryPaths =>
        _directories.Keys.ToArray();

    public StorageVolumeInfo GetStorageVolumeInfo(string path)
    {
        if (VolumeResolver is not null)
        {
            return VolumeResolver(path);
        }

        string fullPath = Normalize(path);
        string root =
            Path.GetPathRoot(fullPath)
            ?? "FAKE";

        return new StorageVolumeInfo(
            root,
            "NTFS",
            512L * 1024 * 1024 * 1024,
            isWritable: true);
    }

    public void CreateDirectory(string path)
    {
        _directories[Normalize(path)] = 0;
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(
            Normalize(path));
    }

    public long GetFileLength(string path)
    {
        string normalized =
            Normalize(path);

        if (!_files.TryGetValue(
                normalized,
                out byte[]? bytes))
        {
            throw new FileNotFoundException(
                "Fake file not found.",
                normalized);
        }

        return bytes.LongLength;
    }

    public string ReadAllText(string path)
    {
        string normalized =
            Normalize(path);

        if (!_files.TryGetValue(
                normalized,
                out byte[]? bytes))
        {
            throw new FileNotFoundException(
                "Fake file not found.",
                normalized);
        }

        return Encoding.UTF8.GetString(
            bytes);
    }

    public IReadOnlyList<string> EnumerateDirectories(
        string path)
    {
        string normalized =
            Normalize(path);

        return _directories.Keys
            .Where(directory =>
                IsDirectChild(
                    directory,
                    normalized))
            .OrderBy(
                directory => directory,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> EnumerateFiles(
        string path)
    {
        string normalized =
            Normalize(path);

        return _files.Keys
            .Where(file =>
                IsDirectChild(
                    file,
                    normalized))
            .OrderBy(
                file => file,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryReserveFile(string path)
    {
        return _files.TryAdd(
            Normalize(path),
            Array.Empty<byte>());
    }

    public bool DeleteFileIfEmpty(string path)
    {
        string normalized =
            Normalize(path);

        while (_files.TryGetValue(
                   normalized,
                   out byte[]? bytes))
        {
            if (bytes.Length != 0)
            {
                return false;
            }

            var collection =
                (ICollection<KeyValuePair<string, byte[]>>)
                _files;

            if (collection.Remove(
                    new KeyValuePair<string, byte[]>(
                        normalized,
                        bytes)))
            {
                return true;
            }
        }

        return false;
    }

    public void DeleteFile(string path)
    {
        string normalized =
            Normalize(path);

        Exception? failure =
            DeleteFileFailureFactory?.Invoke(
                normalized);

        if (failure is not null)
        {
            throw failure;
        }

        _files.TryRemove(
            normalized,
            out _);
    }

    public void DeleteDirectoryIfEmpty(string path)
    {
        string normalized =
            Normalize(path);
        string prefix =
            normalized.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        bool containsFile =
            _files.Keys.Any(file =>
                file.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase));

        bool containsDirectory =
            _directories.Keys.Any(directory =>
                !string.Equals(
                    directory,
                    normalized,
                    StringComparison.OrdinalIgnoreCase) &&
                directory.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase));

        if (!containsFile &&
            !containsDirectory)
        {
            _directories.TryRemove(
                normalized,
                out _);
        }
    }

    public void MoveFileReplacingEmptyReservation(
        string sourcePath,
        string destinationPath)
    {
        string source =
            Normalize(sourcePath);
        string destination =
            Normalize(destinationPath);

        lock (_moveGate)
        {
            Exception? failure =
                MoveFileFailureFactory?.Invoke(
                    source,
                    destination);

            if (failure is not null)
            {
                throw failure;
            }

            if (!_files.TryGetValue(
                    source,
                    out byte[]? sourceBytes))
            {
                throw new FileNotFoundException(
                    "Fake source file not found.",
                    source);
            }

            if (!_files.TryGetValue(
                    destination,
                    out byte[]? destinationBytes))
            {
                throw new FileNotFoundException(
                    "Fake destination reservation not found.",
                    destination);
            }

            if (destinationBytes.Length != 0)
            {
                throw new IOException(
                    "Fake destination reservation is not empty.");
            }

            _files[destination] =
                sourceBytes;

            _files.TryRemove(
                source,
                out _);
        }
    }

    public void WriteAllTextAtomically(
        string path,
        string content)
    {
        string normalized =
            Normalize(path);

        BeforeAtomicWrite?.Invoke(
            normalized);

        Exception? failure =
            AtomicWriteFailureFactory?.Invoke(
                normalized);

        if (failure is not null)
        {
            throw failure;
        }

        _files[normalized] =
            Encoding.UTF8.GetBytes(content);
    }

    public void AddFile(
        string path,
        string content)
    {
        _files[Normalize(path)] =
            Encoding.UTF8.GetBytes(content);
    }

    public bool DirectoryExists(string path)
    {
        return _directories.ContainsKey(
            Normalize(path));
    }

    public void AddDirectory(string path)
    {
        _directories[Normalize(path)] = 0;
    }

    private static bool IsDirectChild(
        string candidate,
        string parent)
    {
        string? candidateParent =
            Path.GetDirectoryName(candidate);

        return candidateParent is not null &&
            string.Equals(
                candidateParent.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                parent.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Path must not be empty.",
                nameof(path));
        }

        return Path.GetFullPath(path);
    }
}
