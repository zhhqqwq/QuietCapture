using System.Text;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Storage;

namespace QuietCapture.Infrastructure.Windows.Storage;

public sealed class WindowsFileSystem : IFileSystem
{
    private readonly VolumeInfoResolver _volumeInfoResolver;
    private readonly AtomicTextFileWriter _atomicTextFileWriter;
    private readonly AtomicMediaFileMover _atomicMediaFileMover;

    public WindowsFileSystem()
        : this(
            new VolumeInfoResolver(),
            new AtomicTextFileWriter(),
            new AtomicMediaFileMover())
    {
    }

    public WindowsFileSystem(
        VolumeInfoResolver volumeInfoResolver,
        AtomicTextFileWriter atomicTextFileWriter,
        AtomicMediaFileMover atomicMediaFileMover)
    {
        _volumeInfoResolver =
            volumeInfoResolver ??
            throw new ArgumentNullException(
                nameof(volumeInfoResolver));

        _atomicTextFileWriter =
            atomicTextFileWriter ??
            throw new ArgumentNullException(
                nameof(atomicTextFileWriter));

        _atomicMediaFileMover =
            atomicMediaFileMover ??
            throw new ArgumentNullException(
                nameof(atomicMediaFileMover));
    }

    public StorageVolumeInfo GetStorageVolumeInfo(
        string path)
    {
        return _volumeInfoResolver.Resolve(path);
    }

    public void CreateDirectory(
        string path)
    {
        Directory.CreateDirectory(path);
    }

    public bool FileExists(
        string path)
    {
        return File.Exists(path);
    }

    public long GetFileLength(
        string path)
    {
        return new FileInfo(path).Length;
    }

    public string ReadAllText(
        string path)
    {
        return File.ReadAllText(
            path,
            Encoding.UTF8);
    }

    public IReadOnlyList<string> EnumerateDirectories(
        string path)
    {
        return Directory.GetDirectories(
            path,
            "*",
            SearchOption.TopDirectoryOnly);
    }

    public IReadOnlyList<string> EnumerateFiles(
        string path)
    {
        return Directory.GetFiles(
            path,
            "*",
            SearchOption.TopDirectoryOnly);
    }

    public bool TryReserveFile(
        string path)
    {
        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 1,
                    FileOptions.WriteThrough);

            stream.Flush(
                flushToDisk: true);
            return true;
        }
        catch (IOException)
            when (File.Exists(path))
        {
            return false;
        }
    }

    public bool DeleteFileIfEmpty(
        string path)
    {
        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Delete);

            if (stream.Length != 0)
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    public void DeleteFile(
        string path)
    {
        File.Delete(path);
    }

    public void DeleteDirectoryIfEmpty(
        string path)
    {
        try
        {
            Directory.Delete(
                path,
                recursive: false);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    public void MoveFileReplacingEmptyReservation(
        string sourcePath,
        string destinationPath)
    {
        StorageVolumeInfo sourceVolume =
            _volumeInfoResolver.Resolve(
                sourcePath);

        StorageVolumeInfo destinationVolume =
            _volumeInfoResolver.Resolve(
                destinationPath);

        if (!string.Equals(
                sourceVolume.VolumeId,
                destinationVolume.VolumeId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(
                "Source media and final reservation are not on the same volume.");
        }

        _atomicMediaFileMover
            .MoveReplacingEmptyReservation(
                sourcePath,
                destinationPath);
    }

    public void WriteAllTextAtomically(
        string path,
        string content)
    {
        _atomicTextFileWriter.Write(
            path,
            content);
    }
}
