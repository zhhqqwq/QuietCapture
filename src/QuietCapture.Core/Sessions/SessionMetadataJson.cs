using System.Text.Json;

namespace QuietCapture.Core.Sessions;

public static class SessionMetadataJson
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public static string Serialize(
        SessionMetadata session)
    {
        return JsonSerializer.Serialize(
            SessionMetadataDocument.FromDomain(
                session),
            Options);
    }

    public static SessionMetadataDocument Deserialize(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                "Session metadata JSON must not be empty.",
                nameof(json));
        }

        SessionMetadataDocument? document =
            JsonSerializer.Deserialize<
                SessionMetadataDocument>(
                json,
                Options);

        return document ??
            throw new InvalidOperationException(
                "Session metadata JSON did not contain a document.");
    }
}
