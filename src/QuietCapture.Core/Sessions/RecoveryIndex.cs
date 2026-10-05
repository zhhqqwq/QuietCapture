namespace QuietCapture.Core.Sessions;

public sealed class RecoveryIndex
{
    private readonly IReadOnlyList<RecoveryIndexEntry>
        _entries;

    public RecoveryIndex(
        IEnumerable<RecoveryIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byWorkingDirectory =
            new Dictionary<string, RecoveryIndexEntry>(
                StringComparer.OrdinalIgnoreCase);

        foreach (RecoveryIndexEntry entry in entries)
        {
            if (byWorkingDirectory.TryGetValue(
                    entry.WorkingDirectory,
                    out RecoveryIndexEntry? existing))
            {
                if (existing.SessionId != entry.SessionId)
                {
                    throw new ArgumentException(
                        "Recovery index contains conflicting session IDs for the same working directory.",
                        nameof(entries));
                }

                continue;
            }

            byWorkingDirectory.Add(
                entry.WorkingDirectory,
                entry);
        }

        _entries =
            byWorkingDirectory.Values.ToArray();
    }

    public IReadOnlyList<RecoveryIndexEntry> Entries =>
        _entries;

    public static RecoveryIndex Empty { get; } =
        new(Array.Empty<RecoveryIndexEntry>());
}
