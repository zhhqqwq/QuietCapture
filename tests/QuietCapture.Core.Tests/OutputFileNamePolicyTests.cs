using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Tests;

public sealed class OutputFileNamePolicyTests
{
    private static readonly DateTimeOffset Timestamp =
        new(
            2026,
            10,
            5,
            14,
            32,
            18,
            TimeSpan.FromHours(9));

    [Fact]
    public void CreateFileName_UsesFrozenTimestampFormat()
    {
        string result =
            OutputFileNamePolicy.CreateFileName(
                Timestamp);

        Assert.Equal(
            "2026-10-05_14-32-18.mp4",
            result);
    }

    [Fact]
    public void ChooseAvailableFileName_AppendsThreeDigitCollisionSuffix()
    {
        string[] occupied =
        {
            "2026-10-05_14-32-18.mp4",
            "2026-10-05_14-32-18_001.mp4"
        };

        string result =
            OutputFileNamePolicy
                .ChooseAvailableFileName(
                    Timestamp,
                    occupied);

        Assert.Equal(
            "2026-10-05_14-32-18_002.mp4",
            result);
    }

    [Fact]
    public void OutputPlan_PutsWorkingMediaBelowOutputVolumeTree()
    {
        string outputDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCaptureOutput");

        var sessionId =
            new SessionId(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"));

        OutputPlan plan =
            OutputPlan.Create(
                outputDirectory,
                sessionId,
                "2026-10-05_14-32-18.mp4");

        string expectedWorkingDirectory =
            Path.Combine(
                outputDirectory,
                ".screenrecorder",
                "sessions",
                sessionId.ToString());

        Assert.Equal(
            expectedWorkingDirectory,
            plan.WorkingDirectory);
        Assert.Equal(
            Path.Combine(
                expectedWorkingDirectory,
                "recording.partial.mp4"),
            plan.TempMediaPath);
        Assert.Equal(
            Path.Combine(
                outputDirectory,
                "2026-10-05_14-32-18.mp4"),
            plan.FinalMediaPath);
    }
}
