using QuietCapture.Core.Sessions;

namespace QuietCapture.Core.Tests;

public sealed class RecoveryIndexJsonTests
{
    [Fact]
    public void SerializeDeserialize_UsesMinimalStableEntryShape()
    {
        var sessionId =
            new SessionId(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"));

        var index =
            new RecoveryIndex(
                new[]
                {
                    new RecoveryIndexEntry(
                        sessionId,
                        @"D:\Recordings\.screenrecorder\sessions\1111")
                });

        string json =
            RecoveryIndexJson.Serialize(
                index);

        Assert.Contains(
            "\"sessionId\": \"11111111-2222-3333-4444-555555555555\"",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"value\"",
            json,
            StringComparison.OrdinalIgnoreCase);

        RecoveryIndex roundTrip =
            RecoveryIndexJson.Deserialize(
                json);

        RecoveryIndexEntry entry =
            Assert.Single(
                roundTrip.Entries);

        Assert.Equal(
            sessionId,
            entry.SessionId);
        Assert.Equal(
            @"D:\Recordings\.screenrecorder\sessions\1111",
            entry.WorkingDirectory);
    }

    [Fact]
    public void RecoveryIndex_DeduplicatesIdenticalWorkingDirectoryEntry()
    {
        var sessionId =
            SessionId.New();

        var index =
            new RecoveryIndex(
                new[]
                {
                    new RecoveryIndexEntry(
                        sessionId,
                        @"D:\Sessions\one"),
                    new RecoveryIndexEntry(
                        sessionId,
                        @"D:\Sessions\one")
                });

        Assert.Single(
            index.Entries);
    }
}
