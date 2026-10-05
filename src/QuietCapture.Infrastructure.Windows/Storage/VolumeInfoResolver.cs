using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using QuietCapture.Core.Storage;

namespace QuietCapture.Infrastructure.Windows.Storage;

public sealed class VolumeInfoResolver
{
    private const uint FileReadOnlyVolume =
        0x00080000;

    public StorageVolumeInfo Resolve(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Path must not be empty.",
                nameof(path));
        }

        string fullPath =
            Path.GetFullPath(path);

        string anchor =
            FindExistingAnchor(fullPath);

        string volumePath =
            GetVolumePath(anchor);

        VolumeDetails details =
            GetVolumeDetails(volumePath);

        long availableBytes =
            GetAvailableBytes(anchor);

        string volumeId =
            TryGetVolumeGuid(volumePath)
            ?? BuildFallbackVolumeId(
                volumePath,
                details.SerialNumber);

        return new StorageVolumeInfo(
            volumeId,
            details.FileSystem,
            availableBytes,
            isWritable:
                (details.FileSystemFlags &
                 FileReadOnlyVolume) == 0);
    }

    private static string FindExistingAnchor(
        string fullPath)
    {
        string current = fullPath;

        while (!Directory.Exists(current) &&
               !File.Exists(current))
        {
            string? parent =
                Path.GetDirectoryName(current);

            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(
                    parent,
                    current,
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        if (Directory.Exists(current) ||
            File.Exists(current))
        {
            return current;
        }

        string? root =
            Path.GetPathRoot(fullPath);

        if (string.IsNullOrWhiteSpace(root))
        {
            throw new DirectoryNotFoundException(
                $"Unable to resolve a volume for path: {fullPath}");
        }

        return root;
    }

    private static string GetVolumePath(
        string anchor)
    {
        var buffer =
            new StringBuilder(1024);

        if (!GetVolumePathNameW(
                anchor,
                buffer,
                buffer.Capacity))
        {
            throw CreateWin32IOException(
                "GetVolumePathName failed.",
                anchor);
        }

        return buffer.ToString();
    }

    private static VolumeDetails GetVolumeDetails(
        string volumePath)
    {
        var volumeName =
            new StringBuilder(261);
        var fileSystemName =
            new StringBuilder(261);

        if (!GetVolumeInformationW(
                volumePath,
                volumeName,
                volumeName.Capacity,
                out uint serialNumber,
                out _,
                out uint fileSystemFlags,
                fileSystemName,
                fileSystemName.Capacity))
        {
            throw CreateWin32IOException(
                "GetVolumeInformation failed.",
                volumePath);
        }

        string fileSystem =
            fileSystemName.ToString();

        if (string.IsNullOrWhiteSpace(
                fileSystem))
        {
            throw new IOException(
                $"Volume filesystem is unavailable for: {volumePath}");
        }

        return new VolumeDetails(
            serialNumber,
            fileSystemFlags,
            fileSystem);
    }

    private static long GetAvailableBytes(
        string path)
    {
        if (!GetDiskFreeSpaceExW(
                path,
                out ulong freeBytesAvailable,
                out _,
                out _))
        {
            throw CreateWin32IOException(
                "GetDiskFreeSpaceEx failed.",
                path);
        }

        return freeBytesAvailable >
            long.MaxValue
            ? long.MaxValue
            : (long)freeBytesAvailable;
    }

    private static string? TryGetVolumeGuid(
        string volumePath)
    {
        var buffer =
            new StringBuilder(1024);

        if (!GetVolumeNameForVolumeMountPointW(
                EnsureTrailingSeparator(
                    volumePath),
                buffer,
                buffer.Capacity))
        {
            return null;
        }

        string value =
            buffer.ToString();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private static string BuildFallbackVolumeId(
        string volumePath,
        uint serialNumber)
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"MOUNT:{EnsureTrailingSeparator(volumePath)}|SERIAL:{serialNumber:X8}");
    }

    private static string EnsureTrailingSeparator(
        string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path +
              Path.DirectorySeparatorChar;
    }

    private static IOException CreateWin32IOException(
        string message,
        string path)
    {
        int error =
            Marshal.GetLastWin32Error();

        return new IOException(
            $"{message} Path: {path}",
            new Win32Exception(error));
    }

    private sealed record VolumeDetails(
        uint SerialNumber,
        uint FileSystemFlags,
        string FileSystem);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNameW(
        string fileName,
        StringBuilder volumePathName,
        int bufferLength);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPointW(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationW(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(
        string directoryName,
        out ulong freeBytesAvailable,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);
}
