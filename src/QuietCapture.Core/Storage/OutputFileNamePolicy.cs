using System.Globalization;

namespace QuietCapture.Core.Storage;

public static class OutputFileNamePolicy
{
    public const int DefaultMaximumCollisionIndex = 9999;

    public static string ChooseAvailableFileName(
        DateTimeOffset timestamp,
        IEnumerable<string> occupiedFileNames,
        int maximumCollisionIndex =
            DefaultMaximumCollisionIndex)
    {
        ArgumentNullException.ThrowIfNull(
            occupiedFileNames);

        if (maximumCollisionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCollisionIndex));
        }

        var occupied = new HashSet<string>(
            occupiedFileNames,
            StringComparer.OrdinalIgnoreCase);

        for (int collisionIndex = 0;
             collisionIndex <= maximumCollisionIndex;
             collisionIndex++)
        {
            string candidate =
                CreateFileName(
                    timestamp,
                    collisionIndex);

            if (!occupied.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "No available recording file name remains within the configured collision range.");
    }

    public static string CreateFileName(
        DateTimeOffset timestamp,
        int collisionIndex = 0)
    {
        if (collisionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collisionIndex));
        }

        string baseName =
            timestamp.ToString(
                "yyyy-MM-dd_HH-mm-ss",
                CultureInfo.InvariantCulture);

        return collisionIndex == 0
            ? $"{baseName}.mp4"
            : $"{baseName}_{collisionIndex:000}.mp4";
    }
}
