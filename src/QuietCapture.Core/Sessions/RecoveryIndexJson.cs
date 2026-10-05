using System.Text.Json;

namespace QuietCapture.Core.Sessions;

public static class RecoveryIndexJson
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public static string Serialize(
        RecoveryIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        var document =
            new RecoveryIndexDocument
            {
                Entries =
                    index.Entries
                        .Select(entry =>
                            new RecoveryIndexEntryDocument
                            {
                                SessionId =
                                    entry.SessionId.Value
                                        .ToString("D"),
                                WorkingDirectory =
                                    entry.WorkingDirectory
                            })
                        .ToList()
            };

        return JsonSerializer.Serialize(
            document,
            Options);
    }

    public static RecoveryIndex Deserialize(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                "Recovery index JSON must not be empty.",
                nameof(json));
        }

        RecoveryIndexDocument? document =
            JsonSerializer.Deserialize<
                RecoveryIndexDocument>(
                json,
                Options);

        if (document is null)
        {
            throw new InvalidOperationException(
                "Recovery index JSON did not contain a document.");
        }

        if (document.SchemaVersion !=
            CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported recovery index schema version: {document.SchemaVersion}.");
        }

        var entries =
            new List<RecoveryIndexEntry>();

        foreach (RecoveryIndexEntryDocument entry
                 in document.Entries)
        {
            if (!Guid.TryParse(
                    entry.SessionId,
                    out Guid sessionGuid) ||
                sessionGuid == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Recovery index contains an invalid session ID.");
            }

            entries.Add(
                new RecoveryIndexEntry(
                    new SessionId(sessionGuid),
                    entry.WorkingDirectory));
        }

        return new RecoveryIndex(entries);
    }

    private sealed record RecoveryIndexDocument
    {
        public int SchemaVersion { get; init; } =
            CurrentSchemaVersion;

        public List<RecoveryIndexEntryDocument> Entries
            { get; init; } = new();
    }

    private sealed record RecoveryIndexEntryDocument
    {
        public string SessionId { get; init; } =
            string.Empty;

        public string WorkingDirectory { get; init; } =
            string.Empty;
    }
}
