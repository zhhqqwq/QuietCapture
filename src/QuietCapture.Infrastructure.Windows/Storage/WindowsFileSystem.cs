using System.Text;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Storage;

namespace QuietCapture.Infrastructure.Windows.Storage;

public sealed class WindowsFileSystem : IFileSystem
{
    private readonly VolumeInfoResolver _volumeInfoResolver;
    private readonly AtomicTextFileWriter _atomicTextFileWriter;

    public WindowsFileSystem()
        : this(
            new VolumeInfoResolver(),
            new AtomicTextFileWriter())
    {
    }

    public WindowsFileSystem(
        VolumeInfoResolver volumeInfoResolver,
        AtomicTextFileWriter atomicTextFileWriter)
    {
        _volumeInfoResolver =
            volumeInfoResolver ??
            throw new ArgumentNullException(
                nameof(volumeInfoResolver));

        _atomicTextFileWriter =
            atomicTextFileWriter ??
            throw new ArgumentNullException(
                nameof(atomicTextFileWriter));
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

    public void WriteAllTextAtomically(
        string path,
        string content)
    {
        _atomicTextFileWriter.Write(
            path,
            content);
    }
}
