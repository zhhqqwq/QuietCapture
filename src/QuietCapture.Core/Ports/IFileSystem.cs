using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Ports;

public interface IFileSystem
{
    StorageVolumeInfo GetStorageVolumeInfo(string path);

    void CreateDirectory(string path);

    bool FileExists(string path);

    long GetFileLength(string path);

    string ReadAllText(string path);

    IReadOnlyList<string> EnumerateDirectories(
        string path);

    IReadOnlyList<string> EnumerateFiles(
        string path);

    bool TryReserveFile(string path);

    bool DeleteFileIfEmpty(string path);

    void DeleteDirectoryIfEmpty(string path);

    void WriteAllTextAtomically(
        string path,
        string content);
}
