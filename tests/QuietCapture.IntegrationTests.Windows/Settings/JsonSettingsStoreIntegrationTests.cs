using QuietCapture.Core.Settings;
using QuietCapture.Infrastructure.Windows.Settings;
using QuietCapture.Infrastructure.Windows.Storage;

namespace QuietCapture.IntegrationTests.Windows.Settings;

public sealed class JsonSettingsStoreIntegrationTests
{
    [Fact]
    public void Settings_RoundTripPreservesUserIntent()
    {
        using var scope =
            TestDirectoryScope.Create();

        string settingsPath =
            Path.Combine(
                scope.Path,
                "ScreenRecorder",
                "settings.json");

        var fileSystem =
            new WindowsFileSystem();

        var store =
            new JsonSettingsStore(
                fileSystem,
                settingsPath);

        string current =
            Path.Combine(
                scope.Path,
                "Recordings");

        string previous =
            Path.Combine(
                scope.Path,
                "OldRecordings");

        var settings =
            new AppSettings(
                new OutputDirectoryHistory(
                    current,
                    new[]
                    {
                        previous
                    }),
                targetFrameRate: 60,
                qualityPresetId: "high",
                recordSystemAudioByDefault: true,
                recordMicrophoneByDefault: false,
                AudioDevicePreference.Specific(
                    "preferred-render-device"),
                AudioDevicePreference.Default);

        store.Save(settings);

        AppSettings loaded =
            Assert.IsType<AppSettings>(
                store.Load());

        Assert.Equal(
            60,
            loaded.TargetFrameRate);
        Assert.Equal(
            "high",
            loaded.QualityPresetId);

        Assert.True(
            loaded.RecordSystemAudioByDefault);
        Assert.False(
            loaded.RecordMicrophoneByDefault);

        Assert.Equal(
            Path.GetFullPath(current),
            loaded.OutputDirectories
                .CurrentOutputDirectory);

        Assert.Equal(
            new[]
            {
                Path.GetFullPath(current),
                Path.GetFullPath(previous)
            },
            loaded.OutputDirectories
                .KnownDirectories);

        Assert.Equal(
            AudioDevicePreferenceKind.Specific,
            loaded.SystemAudioDevicePreference.Kind);

        Assert.Equal(
            "preferred-render-device",
            loaded.SystemAudioDevicePreference
                .PreferredDeviceId);

        Assert.True(
            loaded.MicrophoneDevicePreference
                .UsesDefaultDevice);

        Assert.Null(
            loaded.MicrophoneDevicePreference
                .PreferredDeviceId);
    }

    [Fact]
    public void CorruptSettings_FallBackToFirstRunDefaults()
    {
        using var scope =
            TestDirectoryScope.Create();

        string settingsPath =
            Path.Combine(
                scope.Path,
                "settings.json");

        File.WriteAllText(
            settingsPath,
            "{ not valid json");

        var service =
            new SettingsService(
                new JsonSettingsStore(
                    new WindowsFileSystem(),
                    settingsPath));

        AppSettings settings =
            service.LoadOrDefault();

        Assert.Null(
            settings.OutputDirectories
                .CurrentOutputDirectory);
        Assert.Equal(
            AppSettings.DefaultTargetFrameRate,
            settings.TargetFrameRate);
        Assert.Equal(
            AppSettings.DefaultQualityPresetId,
            settings.QualityPresetId);
        Assert.True(
            settings.SystemAudioDevicePreference
                .UsesDefaultDevice);
        Assert.True(
            settings.MicrophoneDevicePreference
                .UsesDefaultDevice);
    }

    [Fact]
    public void AtomicSettingsUpdate_FailurePreservesPreviousCompleteFile()
    {
        using var scope =
            TestDirectoryScope.Create();

        string settingsPath =
            Path.Combine(
                scope.Path,
                "settings.json");

        var fileSystem =
            new WindowsFileSystem();

        var store =
            new JsonSettingsStore(
                fileSystem,
                settingsPath);

        AppSettings first =
            AppSettings.CreateDefault();

        store.Save(first);

        using (var lockStream =
               new FileStream(
                   settingsPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            var updated =
                new AppSettings(
                    first.OutputDirectories,
                    targetFrameRate: 60,
                    qualityPresetId: "high",
                    first.RecordSystemAudioByDefault,
                    first.RecordMicrophoneByDefault,
                    first.SystemAudioDevicePreference,
                    first.MicrophoneDevicePreference);

            Assert.Throws<IOException>(
                () =>
                    store.Save(updated));
        }

        AppSettings afterFailure =
            Assert.IsType<AppSettings>(
                store.Load());

        Assert.Equal(
            AppSettings.DefaultTargetFrameRate,
            afterFailure.TargetFrameRate);

        Assert.Equal(
            AppSettings.DefaultQualityPresetId,
            afterFailure.QualityPresetId);

        Assert.Empty(
            Directory.GetFiles(
                scope.Path,
                "*.tmp",
                SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void PersistedSettings_DoNotContainBackendOrCfrPolicyFields()
    {
        using var scope =
            TestDirectoryScope.Create();

        string settingsPath =
            Path.Combine(
                scope.Path,
                "settings.json");

        var store =
            new JsonSettingsStore(
                new WindowsFileSystem(),
                settingsPath);

        store.Save(
            AppSettings.CreateDefault());

        string json =
            File.ReadAllText(
                settingsPath)
            .ToLowerInvariant();

        Assert.DoesNotContain(
            "backend",
            json);
        Assert.DoesNotContain(
            "screenrecorderlib",
            json);
        Assert.DoesNotContain(
            "cfr",
            json);
        Assert.DoesNotContain(
            "fixedframerate",
            json);
        Assert.DoesNotContain(
            "fragment",
            json);
    }

    private sealed class TestDirectoryScope
        : IDisposable
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
                    "SettingsIntegration",
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
