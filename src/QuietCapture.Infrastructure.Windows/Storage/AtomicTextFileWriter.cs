using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace QuietCapture.Infrastructure.Windows.Storage;

public sealed class AtomicTextFileWriter
{
    private const uint MoveFileReplaceExisting =
        0x00000001;
    private const uint MoveFileWriteThrough =
        0x00000008;

    public void Write(
        string path,
        string content)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Path must not be empty.",
                nameof(path));
        }

        ArgumentNullException.ThrowIfNull(content);

        string fullPath =
            Path.GetFullPath(path);

        string directory =
            Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException(
                "Path must have a parent directory.",
                nameof(path));

        string tempPath =
            Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            WriteTempFile(
                tempPath,
                content);

            if (!MoveFileExW(
                    tempPath,
                    fullPath,
                    MoveFileReplaceExisting |
                    MoveFileWriteThrough))
            {
                int error =
                    Marshal.GetLastWin32Error();

                throw new IOException(
                    $"Atomic replace failed for: {fullPath}",
                    new Win32Exception(error));
            }
        }
        finally
        {
            TryDeleteTemp(tempPath);
        }
    }

    private static void WriteTempFile(
        string tempPath,
        string content)
    {
        using var stream =
            new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);

        using (var writer =
               new StreamWriter(
                   stream,
                   new UTF8Encoding(
                       encoderShouldEmitUTF8Identifier:
                           false),
                   bufferSize: 4096,
                   leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        stream.Flush(
            flushToDisk: true);
    }

    private static void TryDeleteTemp(
        string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(
        string existingFileName,
        string newFileName,
        uint flags);
}
