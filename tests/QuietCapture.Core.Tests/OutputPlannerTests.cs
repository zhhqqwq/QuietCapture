using QuietCapture.Core.Sessions;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class OutputPlannerTests
{
    private static readonly DateTimeOffset Timestamp =
        new(
            2026,
            10,
            5,
            14,
            32,
            18,
            TimeSpan.Zero);

    [Fact]
    public void Reserve_CreatesFinalPathReservationAndSameVolumePlan()
    {
        var fileSystem =
            new FakeFileSystem();
        var planner =
            new OutputPlanner(fileSystem);

        string outputDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "reserve-same-volume");

        OutputReservation reservation =
            planner.Reserve(
                outputDirectory,
                SessionId.New(),
                Timestamp);

        Assert.True(
            fileSystem.FileExists(
                reservation.Plan.FinalMediaPath));
        Assert.Equal(
            0,
            fileSystem.GetFileLength(
                reservation.Plan.FinalMediaPath));

        Assert.StartsWith(
            Path.GetFullPath(outputDirectory),
            Path.GetFullPath(
                reservation.Plan.WorkingDirectory),
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            reservation.Volume.VolumeId,
            fileSystem
                .GetStorageVolumeInfo(
                    reservation.Plan.WorkingDirectory)
                .VolumeId);
    }

    [Fact]
    public void Reserve_RejectsWhenWorkingPathResolvesToDifferentVolume()
    {
        var fileSystem =
            new FakeFileSystem
            {
                VolumeResolver = path =>
                {
                    bool isWorkingPath =
                        path.Contains(
                            ".screenrecorder",
                            StringComparison.OrdinalIgnoreCase);

                    return new StorageVolumeInfo(
                        isWorkingPath ? "VOL-B" : "VOL-A",
                        "NTFS",
                        10_000_000_000,
                        isWritable: true);
                }
            };

        var planner =
            new OutputPlanner(fileSystem);

        Assert.Throws<InvalidOperationException>(
            () =>
                planner.Reserve(
                    @"D:\Recordings",
                    SessionId.New(),
                    Timestamp));
    }

    [Fact]
    public void Reserve_RejectsFat32()
    {
        var fileSystem =
            new FakeFileSystem
            {
                VolumeResolver = _ =>
                    new StorageVolumeInfo(
                        "VOL-A",
                        "FAT32",
                        10_000_000_000,
                        isWritable: true)
            };

        var planner =
            new OutputPlanner(fileSystem);

        Assert.Throws<InvalidOperationException>(
            () =>
                planner.Reserve(
                    @"D:\Recordings",
                    SessionId.New(),
                    Timestamp));
    }

    [Fact]
    public async Task ConcurrentReservations_NeverReturnDuplicateFinalPath()
    {
        var fileSystem =
            new FakeFileSystem();
        var planner =
            new OutputPlanner(fileSystem);

        string outputDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "reservation-race");

        Task<OutputReservation>[] tasks =
            Enumerable.Range(0, 32)
                .Select(_ =>
                    Task.Run(() =>
                        planner.Reserve(
                            outputDirectory,
                            SessionId.New(),
                            Timestamp)))
                .ToArray();

        OutputReservation[] reservations =
            await Task.WhenAll(tasks);

        string[] paths =
            reservations
                .Select(reservation =>
                    reservation.Plan.FinalMediaPath)
                .ToArray();

        Assert.Equal(
            paths.Length,
            paths.Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.Equal(
            32,
            paths.Length);

        Assert.Contains(
            paths,
            path =>
                path.EndsWith(
                    "2026-10-05_14-32-18.mp4",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            paths,
            path =>
                path.EndsWith(
                    "2026-10-05_14-32-18_001.mp4",
                    StringComparison.OrdinalIgnoreCase));
    }
}
