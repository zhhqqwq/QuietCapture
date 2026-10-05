using QuietCapture.Core.Storage;
using QuietCapture.Infrastructure.Windows.Storage;

namespace QuietCapture.IntegrationTests.Windows.Storage;

public sealed class WindowsFileSystemIntegrationTests
{
    [Fact]
    public void TryReserveFile_UsesAtomicCreateNewSemantics()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string path =
            Path.Combine(
                scope.Path,
                "reserved.mp4");

        Assert.True(
            fileSystem.TryReserveFile(path));
        Assert.False(
            fileSystem.TryReserveFile(path));
        Assert.True(
            File.Exists(path));
        Assert.Equal(
            0,
            new FileInfo(path).Length);
    }

    [Fact]
    public async Task ConcurrentDuplicateReservation_HasExactlyOneWinner()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string path =
            Path.Combine(
                scope.Path,
                "race.mp4");

        Task<bool>[] tasks =
            Enumerable.Range(0, 32)
                .Select(_ =>
                    Task.Run(() =>
                        fileSystem.TryReserveFile(path)))
                .ToArray();

        bool[] results =
            await Task.WhenAll(tasks);

        Assert.Equal(
            1,
            results.Count(result => result));
        Assert.Equal(
            31,
            results.Count(result => !result));
        Assert.True(
            File.Exists(path));
    }

    [Fact]
    public void AtomicMetadataReplace_ReplacesExistingCompleteFile()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string path =
            Path.Combine(
                scope.Path,
                "session.json");

        File.WriteAllText(
            path,
            "old");

        fileSystem.WriteAllTextAtomically(
            path,
            "{\"status\":\"Recording\"}");

        Assert.Equal(
            "{\"status\":\"Recording\"}",
            File.ReadAllText(path));

        Assert.Empty(
            Directory.GetFiles(
                scope.Path,
                "*.tmp",
                SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void FailedAtomicReplace_PreservesExistingFile()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string path =
            Path.Combine(
                scope.Path,
                "session.json");

        File.WriteAllText(
            path,
            "old-complete-content");

        using var lockStream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        Assert.Throws<IOException>(
            () =>
                fileSystem.WriteAllTextAtomically(
                    path,
                    "new-content"));

        Assert.Equal(
            "old-complete-content",
            File.ReadAllText(path));

        Assert.Empty(
            Directory.GetFiles(
                scope.Path,
                "*.tmp",
                SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void VolumeInfo_UsesSameIdentityForPathsOnSameVolume()
    {
        using var scope =
            TestDirectoryScope.Create();

        string nested =
            Path.Combine(
                scope.Path,
                "nested",
                "future",
                "session");
        Directory.CreateDirectory(
            Path.Combine(
                scope.Path,
                "nested"));

        var resolver =
            new VolumeInfoResolver();

        StorageVolumeInfo rootInfo =
            resolver.Resolve(scope.Path);
        StorageVolumeInfo nestedInfo =
            resolver.Resolve(nested);

        Assert.Equal(
            rootInfo.VolumeId,
            nestedInfo.VolumeId,
            ignoreCase: true);
        Assert.False(
            string.IsNullOrWhiteSpace(
                rootInfo.FileSystem));
        Assert.True(
            rootInfo.AvailableBytes > 0);
        Assert.True(
            rootInfo.IsWritable);
    }

    [Fact]
    public void DirectoryAndFileEnumeration_ReturnOnlyDirectChildren()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string childA =
            Path.Combine(
                scope.Path,
                "child-a");
        string childB =
            Path.Combine(
                scope.Path,
                "child-b");
        string nested =
            Path.Combine(
                childA,
                "nested");

        Directory.CreateDirectory(childA);
        Directory.CreateDirectory(childB);
        Directory.CreateDirectory(nested);

        string directFile =
            Path.Combine(
                scope.Path,
                "direct.txt");
        string nestedFile =
            Path.Combine(
                nested,
                "nested.txt");

        File.WriteAllText(
            directFile,
            "direct");
        File.WriteAllText(
            nestedFile,
            "nested");

        IReadOnlyList<string> directories =
            fileSystem.EnumerateDirectories(
                scope.Path);
        IReadOnlyList<string> files =
            fileSystem.EnumerateFiles(
                scope.Path);

        Assert.Equal(
            2,
            directories.Count);
        Assert.Contains(
            childA,
            directories);
        Assert.Contains(
            childB,
            directories);

        Assert.Single(files);
        Assert.Contains(
            directFile,
            files);
    }

    [Fact]
    public void EmptyOnlyCleanup_DeletesOnlyEmptyFileAndDirectory()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string emptyFile =
            Path.Combine(
                scope.Path,
                "empty.mp4");
        string nonEmptyFile =
            Path.Combine(
                scope.Path,
                "non-empty.mp4");

        Assert.True(
            fileSystem.TryReserveFile(
                emptyFile));

        File.WriteAllText(
            nonEmptyFile,
            "media");

        Assert.True(
            fileSystem.DeleteFileIfEmpty(
                emptyFile));
        Assert.False(
            File.Exists(emptyFile));

        Assert.False(
            fileSystem.DeleteFileIfEmpty(
                nonEmptyFile));
        Assert.True(
            File.Exists(nonEmptyFile));

        string emptyDirectory =
            Path.Combine(
                scope.Path,
                "empty-dir");
        string nonEmptyDirectory =
            Path.Combine(
                scope.Path,
                "non-empty-dir");

        Directory.CreateDirectory(
            emptyDirectory);
        Directory.CreateDirectory(
            nonEmptyDirectory);

        File.WriteAllText(
            Path.Combine(
                nonEmptyDirectory,
                "keep.txt"),
            "keep");

        fileSystem.DeleteDirectoryIfEmpty(
            emptyDirectory);
        fileSystem.DeleteDirectoryIfEmpty(
            nonEmptyDirectory);

        Assert.False(
            Directory.Exists(
                emptyDirectory));
        Assert.True(
            Directory.Exists(
                nonEmptyDirectory));
    }

    [Fact]
    public void MoveFileReplacingEmptyReservation_PublishesMedia()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string source =
            Path.Combine(
                scope.Path,
                "recording.partial.mp4");
        string destination =
            Path.Combine(
                scope.Path,
                "final.mp4");

        File.WriteAllText(
            source,
            "video-data");

        Assert.True(
            fileSystem.TryReserveFile(
                destination));

        fileSystem.MoveFileReplacingEmptyReservation(
            source,
            destination);

        Assert.False(
            File.Exists(source));
        Assert.Equal(
            "video-data",
            File.ReadAllText(destination));
    }

    [Fact]
    public void MoveFileReplacingEmptyReservation_RejectsNonEmptyDestination()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string source =
            Path.Combine(
                scope.Path,
                "recording.partial.mp4");
        string destination =
            Path.Combine(
                scope.Path,
                "final.mp4");

        File.WriteAllText(
            source,
            "partial-media");
        File.WriteAllText(
            destination,
            "existing-final");

        Assert.Throws<IOException>(
            () =>
                fileSystem.MoveFileReplacingEmptyReservation(
                    source,
                    destination));

        Assert.Equal(
            "partial-media",
            File.ReadAllText(source));
        Assert.Equal(
            "existing-final",
            File.ReadAllText(destination));
    }

    [Fact]
    public void MoveFileFailure_PreservesSourceAndEmptyReservation()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string source =
            Path.Combine(
                scope.Path,
                "recording.partial.mp4");
        string destination =
            Path.Combine(
                scope.Path,
                "final.mp4");

        File.WriteAllText(
            source,
            "partial-media");

        Assert.True(
            fileSystem.TryReserveFile(
                destination));

        using var sourceLock =
            new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        Assert.Throws<IOException>(
            () =>
                fileSystem.MoveFileReplacingEmptyReservation(
                    source,
                    destination));

        Assert.True(
            File.Exists(source));
        Assert.Equal(
            0,
            new FileInfo(destination).Length);
    }

    [Fact]
    public void DeleteFile_RemovesMetadataFile()
    {
        using var scope =
            TestDirectoryScope.Create();
        var fileSystem =
            new WindowsFileSystem();

        string path =
            Path.Combine(
                scope.Path,
                "session.json");

        File.WriteAllText(
            path,
            "{}");

        fileSystem.DeleteFile(path);

        Assert.False(
            File.Exists(path));
    }

    private sealed class TestDirectoryScope : IDisposable
    {
        private TestDirectoryScope(
            string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestDirectoryScope Create()
        {
            string path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "QuietCapture",
                    "WindowsStorageIntegration",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);

            return new TestDirectoryScope(path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
