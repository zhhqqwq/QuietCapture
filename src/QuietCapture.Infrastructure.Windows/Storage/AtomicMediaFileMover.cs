using System.ComponentModel;
using System.Runtime.InteropServices;

namespace QuietCapture.Infrastructure.Windows.Storage;

public sealed class AtomicMediaFileMover
{
    private const uint MoveFileWriteThrough =
        0x00000008;

    public void MoveReplacingEmptyReservation(
        string sourcePath,
        string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException(
                "Source path must not be empty.",
                nameof(sourcePath));
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException(
                "Destination path must not be empty.",
                nameof(destinationPath));
        }

        string source =
            Path.GetFullPath(sourcePath);
        string destination =
            Path.GetFullPath(destinationPath);

        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "Source media file does not exist.",
                source);
        }

        ConsumeEmptyReservation(
            destination);

        if (MoveFileExW(
                source,
                destination,
                MoveFileWriteThrough))
        {
            return;
        }

        int error =
            Marshal.GetLastWin32Error();

        TryRestoreEmptyReservation(
            destination);

        throw new IOException(
            $"Same-volume media move failed: {source} -> {destination}",
            new Win32Exception(error));
    }

    private static void ConsumeEmptyReservation(
        string destination)
    {
        using (var reservationLock =
               new FileStream(
                   destination,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read |
                   FileShare.Delete))
        {
            if (reservationLock.Length != 0)
            {
                throw new IOException(
                    "Final-path reservation is not empty.");
            }

            File.Delete(destination);
        }

        if (File.Exists(destination))
        {
            throw new IOException(
                "Final-path reservation could not be consumed.");
        }
    }

    private static void TryRestoreEmptyReservation(
        string destination)
    {
        try
        {
            using var stream =
                new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 1,
                    FileOptions.WriteThrough);

            stream.Flush(
                flushToDisk: true);
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
