using QuietCapture.Core.Models;
using QuietCapture.Core.Recovery;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Settings;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void FirstRun_ReturnsBackendIndependentDefaults()
    {
        var store =
            new InMemorySettingsStore();

        AppSettings settings =
            new SettingsService(store)
                .LoadOrDefault();

        Assert.Null(
            settings.OutputDirectories
                .CurrentOutputDirectory);
        Assert.Empty(
            settings.OutputDirectories
                .KnownDirectories);

        Assert.Equal(
            30,
            settings.TargetFrameRate);
        Assert.Equal(
            "balanced",
            settings.QualityPresetId);

        Assert.False(
            settings.RecordSystemAudioByDefault);
        Assert.False(
            settings.RecordMicrophoneByDefault);

        Assert.True(
            settings.SystemAudioDevicePreference
                .UsesDefaultDevice);
        Assert.True(
            settings.MicrophoneDevicePreference
                .UsesDefaultDevice);

        Assert.Null(
            settings.SystemAudioDevicePreference
                .PreferredDeviceId);
        Assert.Null(
            settings.MicrophoneDevicePreference
                .PreferredDeviceId);
    }

    [Fact]
    public void OutputDirectoryHistory_NormalizesAndDeduplicates()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "SettingsHistory",
                Guid.NewGuid().ToString("N"));

        string first =
            Path.Combine(
                root,
                "First");

        string second =
            Path.Combine(
                root,
                "Second");

        var history =
            new OutputDirectoryHistory(
                first +
                    Path.DirectorySeparatorChar,
                new[]
                {
                    first,
                    first +
                        Path.DirectorySeparatorChar,
                    second,
                    Path.Combine(
                        second,
                        "."),
                    second.ToUpperInvariant()
                });

        Assert.Equal(
            Path.GetFullPath(first),
            history.CurrentOutputDirectory);

        Assert.Equal(
            2,
            history.KnownDirectories.Count);

        Assert.Equal(
            Path.GetFullPath(first),
            history.KnownDirectories[0]);

        Assert.Equal(
            Path.GetFullPath(second),
            history.KnownDirectories[1]);
    }

    [Fact]
    public void SetCurrentOutputDirectory_PersistsAndMovesCurrentToFront()
    {
        var store =
            new InMemorySettingsStore();

        var service =
            new SettingsService(store);

        string root =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "SettingsCurrent",
                Guid.NewGuid().ToString("N"));

        string first =
            Path.Combine(root, "A");
        string second =
            Path.Combine(root, "B");

        AppSettings settings =
            service.SetCurrentOutputDirectory(
                AppSettings.CreateDefault(),
                first);

        settings =
            service.SetCurrentOutputDirectory(
                settings,
                second);

        Assert.Equal(
            Path.GetFullPath(second),
            settings.OutputDirectories
                .CurrentOutputDirectory);

        Assert.Equal(
            new[]
            {
                Path.GetFullPath(second),
                Path.GetFullPath(first)
            },
            settings.OutputDirectories
                .KnownDirectories);

        Assert.Same(
            settings,
            store.Stored);
    }

    [Fact]
    public void DefaultDeviceIntent_RemainsUnresolvedInSettings()
    {
        AppSettings defaults =
            AppSettings.CreateDefault();

        Assert.Equal(
            AudioDevicePreferenceKind.Default,
            defaults.SystemAudioDevicePreference.Kind);
        Assert.Null(
            defaults.SystemAudioDevicePreference
                .PreferredDeviceId);

        AudioDevicePreference specific =
            AudioDevicePreference.Specific(
                "device-preference-id");

        var settings =
            new AppSettings(
                defaults.OutputDirectories,
                defaults.TargetFrameRate,
                defaults.QualityPresetId,
                recordSystemAudioByDefault: true,
                recordMicrophoneByDefault: false,
                specific,
                AudioDevicePreference.Default);

        Assert.Equal(
            "device-preference-id",
            settings.SystemAudioDevicePreference
                .PreferredDeviceId);

        Assert.True(
            settings.MicrophoneDevicePreference
                .UsesDefaultDevice);
        Assert.Null(
            settings.MicrophoneDevicePreference
                .PreferredDeviceId);
    }

    [Fact]
    public void SettingsTypes_DoNotContainBackendOrCfrPolicyFields()
    {
        string[] propertyNames =
            new[]
            {
                typeof(AppSettings),
                typeof(AudioDevicePreference),
                typeof(OutputDirectoryHistory)
            }
            .SelectMany(type =>
                type.GetProperties())
            .Select(property =>
                property.Name)
            .ToArray();

        string combined =
            string.Join(
                "|",
                propertyNames)
            .ToLowerInvariant();

        Assert.DoesNotContain(
            "backend",
            combined);
        Assert.DoesNotContain(
            "cfr",
            combined);
        Assert.DoesNotContain(
            "fixedframerate",
            combined);
        Assert.DoesNotContain(
            "fragment",
            combined);
    }

    [Fact]
    public void CurrentOutputRoot_FeedsStartupRecoveryDirectoryScan()
    {
        var fileSystem =
            new FakeFileSystem();

        string root =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "SettingsRecovery",
                Guid.NewGuid().ToString("N"));

        string outputDirectory =
            Path.Combine(
                root,
                "Output");

        string indexPath =
            Path.Combine(
                root,
                "AppData",
                "recovery-index.json");

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var persistence =
            new SessionPersistenceService(
                new SessionStore(fileSystem),
                indexStore);

        var manager =
            new SessionManager(
                fileSystem,
                new OutputPlanner(fileSystem),
                persistence);

        SessionMetadata session =
            manager.CreateSession(
                new MonitorCaptureTarget(
                    "DISPLAY-1"),
                new RecordingOptions(
                    new PixelSize(
                        1280,
                        720),
                    30,
                    new VideoQualityPreset(
                        "balanced"),
                    recordSystemAudio: false,
                    recordMicrophone: false,
                    systemAudioDeviceId: null,
                    microphoneDeviceId: null),
                outputDirectory,
                new DateTimeOffset(
                    2026,
                    10,
                    6,
                    7,
                    0,
                    0,
                    TimeSpan.Zero));

        indexStore.Remove(
            RecoveryIndexEntry.FromSession(
                session));

        Assert.False(
            indexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    session)));

        var settingsStore =
            new InMemorySettingsStore();

        var settingsService =
            new SettingsService(
                settingsStore);

        AppSettings settings =
            settingsService
                .SetCurrentOutputDirectory(
                    AppSettings.CreateDefault(),
                    outputDirectory);

        var scanner =
            new RecoveryService(
                fileSystem,
                new SessionDocumentValidator());

        var cleanup =
            new SessionCleanupPolicy(
                fileSystem,
                indexStore);

        var coordinator =
            new RecoveryStartupCoordinator(
                scanner,
                new RecoveryResolutionService(
                    indexStore,
                    cleanup));

        RecoveryStartupResult result =
            coordinator.Run(
                indexPath,
                settingsService
                    .GetKnownOutputDirectories(
                        settings));

        RecoveryCandidate candidate =
            Assert.Single(
                result.RemainingCandidates);

        Assert.Equal(
            session.Id,
            candidate.SessionId);
        Assert.Equal(
            RecoveryClassification.Interrupted,
            candidate.Classification);
        Assert.False(
            candidate.FromRecoveryIndex);
        Assert.True(
            candidate.FromDirectoryScan);
    }

    private sealed class InMemorySettingsStore
        : ISettingsStore
    {
        public AppSettings? Stored { get; private set; }

        public AppSettings? Load()
        {
            return Stored;
        }

        public void Save(AppSettings settings)
        {
            Stored = settings;
        }
    }
}
