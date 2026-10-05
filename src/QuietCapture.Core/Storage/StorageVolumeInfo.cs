namespace QuietCapture.Core.Storage;

public sealed record StorageVolumeInfo
{
    public StorageVolumeInfo(
        string volumeId,
        string fileSystem,
        long availableBytes,
        bool isWritable)
    {
        if (string.IsNullOrWhiteSpace(volumeId))
        {
            throw new ArgumentException(
                "Volume ID must not be empty.",
                nameof(volumeId));
        }

        if (string.IsNullOrWhiteSpace(fileSystem))
        {
            throw new ArgumentException(
                "File system must not be empty.",
                nameof(fileSystem));
        }

        if (availableBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableBytes));
        }

        VolumeId = volumeId;
        FileSystem = fileSystem;
        AvailableBytes = availableBytes;
        IsWritable = isWritable;
    }

    public string VolumeId { get; }

    public string FileSystem { get; }

    public long AvailableBytes { get; }

    public bool IsWritable { get; }

    public bool IsFat32 =>
        string.Equals(
            FileSystem,
            "FAT32",
            StringComparison.OrdinalIgnoreCase);
}
