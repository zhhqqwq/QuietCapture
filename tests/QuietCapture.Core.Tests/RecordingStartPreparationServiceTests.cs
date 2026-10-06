using QuietCapture.Core.Models;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Preflight;
using QuietCapture.Core.Recording;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Settings;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class RecordingStartPreparationServiceTests
{
    [Fact]
    public void SuccessfulPreparation_CreatesCreatedSession()
    {
        Fixture fixture =
            CreateFixture("success");

        AppSettings settings =
            CreateSettings(
                fixture.OutputDirectory);

        RecordingStartPreparationResult result =
            fixture.Service.Prepare(
                settings,
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        Assert.True(result.Succeeded);
        Assert.Null(result.Failure);

        SessionMetadata session =
            Assert.IsType<SessionMetadata>(
                result.Session);

        Assert.Equal(
            SessionStatus.Created,
            session.Status);

        Assert.True(
            fixture.FileSystem.FileExists(
                session.FinalMediaPath));

        Assert.True(
            fixture.FileSystem.FileExists(
                SessionStore.GetMetadataPath(
                    session.WorkingDirectory)));

        Assert.True(
            fixture.IndexStore.Contains(
                RecoveryIndexEntry.FromSession(
                    session)));
    }

    [Fact]
    public void PreflightFailure_CreatesNoSession()
    {
        Fixture fixture =
            CreateFixture("preflight-failure");

        AppSettings settings =
            AppSettings.CreateDefault();

        RecordingStartPreparationResult result =
            fixture.Service.Prepare(
                settings,
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);

        RecordingStartFailureDetail failure =
            Assert.IsType<RecordingStartFailureDetail>(
                result.Failure);

        Assert.Equal(
            RecordingStartFailure.Preflight,
            failure.Kind);

        Assert.Equal(
            RecordingPreflightFailure
                .MissingOutputDirectory,
            failure.PreflightFailure);
    }

    [Fact]
    public void PreflightFailure_CreatesNoFinalPathReservation()
    {
        Fixture fixture =
            CreateFixture("no-reservation");

        fixture.FileSystem.VolumeResolver =
            _ =>
                new StorageVolumeInfo(
                    "VOL-A",
                    "FAT32",
                    100_000_000,
                    isWritable: true);

        AppSettings settings =
            CreateSettings(
                fixture.OutputDirectory);

        int before =
            fixture.FileSystem.FilePaths.Count;

        RecordingStartPreparationResult result =
            fixture.Service.Prepare(
                settings,
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        Assert.False(result.Succeeded);

        Assert.Equal(
            RecordingStartFailure.Preflight,
            result.Failure?.Kind);

        Assert.Equal(
            RecordingPreflightFailure
                .Fat32OutputNotSupported,
            result.Failure?.PreflightFailure);

        Assert.Equal(
            before,
            fixture.FileSystem.FilePaths.Count);

        Assert.DoesNotContain(
            fixture.FileSystem.FilePaths,
            path =>
                path.EndsWith(
                    ".mp4",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SessionCreationFailure_IsSurfaced()
    {
        Fixture fixture =
            CreateFixture("session-failure");

        fixture.FileSystem.AtomicWriteFailureFactory =
            path =>
                path.EndsWith(
                    "session.json",
                    StringComparison.OrdinalIgnoreCase)
                    ? new IOException(
                        "Injected session creation failure.")
                    : null;

        RecordingStartPreparationResult result =
            fixture.Service.Prepare(
                CreateSettings(
                    fixture.OutputDirectory),
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);

        Assert.Equal(
            RecordingStartFailure.SessionCreation,
            result.Failure?.Kind);

        Assert.Null(
            result.Failure?.PreflightFailure);

        Assert.Contains(
            "Injected session creation failure",
            result.Failure?.Error);

        Assert.DoesNotContain(
            fixture.FileSystem.FilePaths,
            path =>
                path.EndsWith(
                    ".mp4",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SettingsCurrentOutputDirectory_IsUsedForSessionPaths()
    {
        Fixture fixture =
            CreateFixture("settings-output");

        string selectedOutput =
            Path.Combine(
                fixture.Root,
                "SelectedOutput");

        RecordingStartPreparationResult result =
            fixture.Service.Prepare(
                CreateSettings(
                    selectedOutput),
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        SessionMetadata session =
            Assert.IsType<SessionMetadata>(
                result.Session);

        Assert.True(result.Succeeded);

        string normalizedOutput =
            Path.GetFullPath(
                selectedOutput);

        Assert.StartsWith(
            normalizedOutput +
                Path.DirectorySeparatorChar,
            session.FinalMediaPath,
            StringComparison.OrdinalIgnoreCase);

        Assert.StartsWith(
            Path.Combine(
                normalizedOutput,
                ".screenrecorder",
                "sessions") +
                Path.DirectorySeparatorChar,
            session.WorkingDirectory,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConcreteAudioIds_ArePersistedInSessionJson()
    {
        Fixture fixture =
            CreateFixture("persist-audio");

        fixture.Audio.DefaultSystemAudioIds.Enqueue(
            "render-concrete");
        fixture.Audio.DefaultMicrophoneIds.Enqueue(
            "capture-concrete");

        AppSettings settings =
            CreateSettings(
                fixture.OutputDirectory,
                recordSystemAudio: true,
                recordMicrophone: true);

        RecordingStartPreparationResult result =
            fixture.Service.Prepare(
                settings,
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        SessionMetadata session =
            Assert.IsType<SessionMetadata>(
                result.Session);

        SessionMetadataDocument document =
            fixture.SessionStore.LoadDocument(
                session.WorkingDirectory);

        Assert.Equal(
            "render-concrete",
            session.Options.SystemAudioDeviceId);

        Assert.Equal(
            "capture-concrete",
            session.Options.MicrophoneDeviceId);

        Assert.Equal(
            "render-concrete",
            document.Options.SystemAudioDeviceId);

        Assert.Equal(
            "capture-concrete",
            document.Options.MicrophoneDeviceId);

        Assert.True(
            document.Options.RecordSystemAudio);
        Assert.True(
            document.Options.RecordMicrophone);
    }

    [Fact]
    public void RepeatedPreparation_ReResolvesDefaultDeviceEachTime()
    {
        Fixture fixture =
            CreateFixture("fresh-device-resolution");

        fixture.Audio.DefaultSystemAudioIds.Enqueue(
            "render-A");
        fixture.Audio.DefaultSystemAudioIds.Enqueue(
            "render-B");

        AppSettings settings =
            CreateSettings(
                fixture.OutputDirectory,
                recordSystemAudio: true,
                recordMicrophone: false);

        RecordingStartPreparationResult first =
            fixture.Service.Prepare(
                settings,
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt);

        RecordingStartPreparationResult second =
            fixture.Service.Prepare(
                settings,
                CreateTarget(),
                new PixelSize(1280, 720),
                fixture.CreatedAt.AddSeconds(1));

        SessionMetadata firstSession =
            Assert.IsType<SessionMetadata>(
                first.Session);

        SessionMetadata secondSession =
            Assert.IsType<SessionMetadata>(
                second.Session);

        Assert.Equal(
            "render-A",
            firstSession.Options
                .SystemAudioDeviceId);

        Assert.Equal(
            "render-B",
            secondSession.Options
                .SystemAudioDeviceId);

        Assert.Equal(
            2,
            fixture.Audio
                .ResolveDefaultSystemAudioCalls);

        SessionMetadataDocument firstDocument =
            fixture.SessionStore.LoadDocument(
                firstSession.WorkingDirectory);

        SessionMetadataDocument secondDocument =
            fixture.SessionStore.LoadDocument(
                secondSession.WorkingDirectory);

        Assert.Equal(
            "render-A",
            firstDocument.Options
                .SystemAudioDeviceId);

        Assert.Equal(
            "render-B",
            secondDocument.Options
                .SystemAudioDeviceId);
    }

    private static Fixture CreateFixture(
        string suffix)
    {
        var fileSystem =
            new FakeFileSystem();

        string root =
            Path.Combine(
                Path.GetTempPath(),
                "QuietCapture",
                "RecordingStartPreparationTests",
                suffix,
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

        var sessionStore =
            new SessionStore(fileSystem);

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                indexPath);

        var persistence =
            new SessionPersistenceService(
                sessionStore,
                indexStore);

        var sessionManager =
            new SessionManager(
                fileSystem,
                new OutputPlanner(fileSystem),
                persistence);

        var audio =
            new SequencedAudioDeviceResolver();

        var preflight =
            new RecordingPreflightService(
                fileSystem,
                audio);

        var service =
            new RecordingStartPreparationService(
                preflight,
                sessionManager);

        return new Fixture(
            fileSystem,
            sessionStore,
            indexStore,
            audio,
            service,
            root,
            outputDirectory,
            new DateTimeOffset(
                2026,
                10,
                6,
                9,
                0,
                0,
                TimeSpan.Zero));
    }

    private static AppSettings CreateSettings(
        string outputDirectory,
        bool recordSystemAudio = false,
        bool recordMicrophone = false)
    {
        return new AppSettings(
            new OutputDirectoryHistory(
                outputDirectory),
            targetFrameRate: 30,
            qualityPresetId: "balanced",
            recordSystemAudio,
            recordMicrophone,
            AudioDevicePreference.Default,
            AudioDevicePreference.Default);
    }

    private static CaptureTarget CreateTarget()
    {
        return new AreaCaptureTarget(
            "DISPLAY-1",
            new PixelRect(
                20,
                30,
                1280,
                720));
    }

    private sealed record Fixture(
        FakeFileSystem FileSystem,
        SessionStore SessionStore,
        RecoveryIndexStore IndexStore,
        SequencedAudioDeviceResolver Audio,
        RecordingStartPreparationService Service,
        string Root,
        string OutputDirectory,
        DateTimeOffset CreatedAt);

    private sealed class SequencedAudioDeviceResolver
        : IAudioDeviceResolver
    {
        public Queue<string?> DefaultSystemAudioIds
            { get; } = new();

        public Queue<string?> DefaultMicrophoneIds
            { get; } = new();

        public int ResolveDefaultSystemAudioCalls
            { get; private set; }

        public int ResolveDefaultMicrophoneCalls
            { get; private set; }

        public string? ResolveDefaultSystemAudioDeviceId()
        {
            ResolveDefaultSystemAudioCalls++;

            return DefaultSystemAudioIds.Count == 0
                ? "default-render"
                : DefaultSystemAudioIds.Dequeue();
        }

        public string? ResolveDefaultMicrophoneDeviceId()
        {
            ResolveDefaultMicrophoneCalls++;

            return DefaultMicrophoneIds.Count == 0
                ? "default-capture"
                : DefaultMicrophoneIds.Dequeue();
        }

        public bool IsSystemAudioDeviceAvailable(
            string deviceId)
        {
            return true;
        }

        public bool IsMicrophoneDeviceAvailable(
            string deviceId)
        {
            return true;
        }
    }
}
