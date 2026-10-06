using QuietCapture.Core.Models;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Preflight;
using QuietCapture.Core.Sessions;
using QuietCapture.Core.Settings;
using QuietCapture.Core.Storage;
using QuietCapture.Core.Tests.Fakes;

namespace QuietCapture.Core.Tests;

public sealed class RecordingPreflightServiceTests
{
    [Fact]
    public void MissingOutputDirectory_FailsBeforeDeviceResolution()
    {
        var fileSystem =
            new FakeFileSystem();

        var audio =
            new FakeAudioDeviceResolver();

        var intent =
            new RecordingIntent(
                new MonitorCaptureTarget(
                    "DISPLAY-1"),
                new PixelSize(
                    1920,
                    1080),
                outputDirectory: null,
                targetFrameRate: 30,
                qualityPresetId: "balanced",
                recordSystemAudio: true,
                recordMicrophone: true,
                AudioDevicePreference.Default,
                AudioDevicePreference.Default);

        RecordingPreflightResult result =
            new RecordingPreflightService(
                fileSystem,
                audio)
            .Resolve(intent);

        Assert.False(result.Succeeded);
        Assert.Equal(
            RecordingPreflightFailure
                .MissingOutputDirectory,
            result.Failure);

        Assert.Equal(
            0,
            audio.TotalCalls);
    }

    [Fact]
    public void UnwritableVolume_IsRejected()
    {
        var fileSystem =
            new FakeFileSystem
            {
                VolumeResolver = _ =>
                    new StorageVolumeInfo(
                        "VOL-A",
                        "NTFS",
                        100_000_000,
                        isWritable: false)
            };

        var audio =
            new FakeAudioDeviceResolver();

        RecordingPreflightResult result =
            new RecordingPreflightService(
                fileSystem,
                audio)
            .Resolve(
                CreateIntent());

        Assert.False(result.Succeeded);
        Assert.Equal(
            RecordingPreflightFailure
                .OutputVolumeNotWritable,
            result.Failure);

        Assert.Equal(
            0,
            audio.TotalCalls);
    }

    [Fact]
    public void Fat32Volume_IsRejected()
    {
        var fileSystem =
            new FakeFileSystem
            {
                VolumeResolver = _ =>
                    new StorageVolumeInfo(
                        "VOL-A",
                        "FAT32",
                        100_000_000,
                        isWritable: true)
            };

        RecordingPreflightResult result =
            new RecordingPreflightService(
                fileSystem,
                new FakeAudioDeviceResolver())
            .Resolve(
                CreateIntent());

        Assert.False(result.Succeeded);
        Assert.Equal(
            RecordingPreflightFailure
                .Fat32OutputNotSupported,
            result.Failure);
    }

    [Fact]
    public void DefaultSystemAudio_IsResolvedAtPreflight()
    {
        var audio =
            new FakeAudioDeviceResolver
            {
                DefaultSystemAudioDeviceId =
                    "render-current-default"
            };

        RecordingPreflightResult result =
            CreateService(audio)
                .Resolve(
                    CreateIntent(
                        recordSystemAudio: true,
                        systemPreference:
                            AudioDevicePreference.Default));

        Assert.True(result.Succeeded);

        RecordingOptions options =
            Assert.IsType<RecordingOptions>(
                result.Options);

        Assert.True(
            options.RecordSystemAudio);

        Assert.Equal(
            "render-current-default",
            options.SystemAudioDeviceId);

        Assert.Equal(
            1,
            audio.ResolveDefaultSystemAudioCalls);
    }

    [Fact]
    public void DefaultMicrophone_IsResolvedAtPreflight()
    {
        var audio =
            new FakeAudioDeviceResolver
            {
                DefaultMicrophoneDeviceId =
                    "capture-current-default"
            };

        RecordingPreflightResult result =
            CreateService(audio)
                .Resolve(
                    CreateIntent(
                        recordMicrophone: true,
                        microphonePreference:
                            AudioDevicePreference.Default));

        Assert.True(result.Succeeded);

        RecordingOptions options =
            Assert.IsType<RecordingOptions>(
                result.Options);

        Assert.True(
            options.RecordMicrophone);

        Assert.Equal(
            "capture-current-default",
            options.MicrophoneDeviceId);

        Assert.Equal(
            1,
            audio.ResolveDefaultMicrophoneCalls);
    }

    [Fact]
    public void UnavailableSpecificSystemAudioDevice_IsRejected()
    {
        var audio =
            new FakeAudioDeviceResolver
            {
                SystemAudioAvailable = false
            };

        RecordingPreflightResult result =
            CreateService(audio)
                .Resolve(
                    CreateIntent(
                        recordSystemAudio: true,
                        systemPreference:
                            AudioDevicePreference.Specific(
                                "old-render-device")));

        Assert.False(result.Succeeded);
        Assert.Equal(
            RecordingPreflightFailure
                .SpecificSystemAudioDeviceUnavailable,
            result.Failure);

        Assert.Equal(
            "old-render-device",
            audio.LastSystemAudioAvailabilityId);
    }

    [Fact]
    public void UnavailableSpecificMicrophone_IsRejected()
    {
        var audio =
            new FakeAudioDeviceResolver
            {
                MicrophoneAvailable = false
            };

        RecordingPreflightResult result =
            CreateService(audio)
                .Resolve(
                    CreateIntent(
                        recordMicrophone: true,
                        microphonePreference:
                            AudioDevicePreference.Specific(
                                "old-capture-device")));

        Assert.False(result.Succeeded);
        Assert.Equal(
            RecordingPreflightFailure
                .SpecificMicrophoneDeviceUnavailable,
            result.Failure);

        Assert.Equal(
            "old-capture-device",
            audio.LastMicrophoneAvailabilityId);
    }

    [Fact]
    public void DisabledAudio_DoesNotResolveAnyDevice()
    {
        var audio =
            new FakeAudioDeviceResolver
            {
                DefaultSystemAudioDeviceId = null,
                DefaultMicrophoneDeviceId = null,
                SystemAudioAvailable = false,
                MicrophoneAvailable = false
            };

        RecordingPreflightResult result =
            CreateService(audio)
                .Resolve(
                    CreateIntent(
                        recordSystemAudio: false,
                        recordMicrophone: false,
                        systemPreference:
                            AudioDevicePreference.Specific(
                                "stale-render"),
                        microphonePreference:
                            AudioDevicePreference.Specific(
                                "stale-capture")));

        Assert.True(result.Succeeded);

        RecordingOptions options =
            Assert.IsType<RecordingOptions>(
                result.Options);

        Assert.False(
            options.RecordSystemAudio);
        Assert.False(
            options.RecordMicrophone);

        Assert.Null(
            options.SystemAudioDeviceId);
        Assert.Null(
            options.MicrophoneDeviceId);

        Assert.Equal(
            0,
            audio.TotalCalls);
    }

    [Fact]
    public void SettingsTargetFrameRate_RemainsTargetOnly()
    {
        string outputDirectory =
            CreateOutputDirectory(
                "target-fps");

        var settings =
            new AppSettings(
                new OutputDirectoryHistory(
                    outputDirectory),
                targetFrameRate: 60,
                qualityPresetId: "high",
                recordSystemAudioByDefault: false,
                recordMicrophoneByDefault: false,
                AudioDevicePreference.Default,
                AudioDevicePreference.Default);

        RecordingIntent intent =
            RecordingIntent.FromSettings(
                new MonitorCaptureTarget(
                    "DISPLAY-1"),
                new PixelSize(
                    1920,
                    1080),
                settings);

        RecordingPreflightResult result =
            CreateService(
                new FakeAudioDeviceResolver())
            .Resolve(intent);

        Assert.True(result.Succeeded);

        RecordingOptions options =
            Assert.IsType<RecordingOptions>(
                result.Options);

        Assert.Equal(
            60,
            intent.TargetFrameRate);

        Assert.Equal(
            60,
            options.FrameRate);

        string propertyNames =
            string.Join(
                "|",
                typeof(RecordingIntent)
                    .GetProperties()
                    .Select(property =>
                        property.Name)
                    .Concat(
                        typeof(RecordingPreflightResult)
                            .GetProperties()
                            .Select(property =>
                                property.Name)))
            .ToLowerInvariant();

        Assert.DoesNotContain(
            "cfr",
            propertyNames);
        Assert.DoesNotContain(
            "fixedframerate",
            propertyNames);
        Assert.DoesNotContain(
            "fragment",
            propertyNames);
    }

    [Fact]
    public void SuccessfulPreflight_ProducesResolvedOptionsForSessionManager()
    {
        var fileSystem =
            new FakeFileSystem();

        var audio =
            new FakeAudioDeviceResolver
            {
                DefaultSystemAudioDeviceId =
                    "render-default-now",
                MicrophoneAvailable = true
            };

        string outputDirectory =
            CreateOutputDirectory(
                "session-manager");

        var settings =
            new AppSettings(
                new OutputDirectoryHistory(
                    outputDirectory),
                targetFrameRate: 30,
                qualityPresetId: "balanced",
                recordSystemAudioByDefault: true,
                recordMicrophoneByDefault: true,
                AudioDevicePreference.Default,
                AudioDevicePreference.Specific(
                    "preferred-microphone"));

        CaptureTarget target =
            new AreaCaptureTarget(
                "DISPLAY-1",
                new PixelRect(
                    100,
                    100,
                    1280,
                    720));

        RecordingIntent intent =
            RecordingIntent.FromSettings(
                target,
                new PixelSize(
                    1280,
                    720),
                settings);

        RecordingPreflightResult preflight =
            new RecordingPreflightService(
                fileSystem,
                audio)
            .Resolve(intent);

        Assert.True(preflight.Succeeded);

        RecordingOptions options =
            Assert.IsType<RecordingOptions>(
                preflight.Options);

        Assert.Equal(
            "render-default-now",
            options.SystemAudioDeviceId);

        Assert.Equal(
            "preferred-microphone",
            options.MicrophoneDeviceId);

        var indexStore =
            new RecoveryIndexStore(
                fileSystem,
                Path.Combine(
                    Path.GetTempPath(),
                    "QuietCapture",
                    "PreflightTests",
                    Guid.NewGuid().ToString("N"),
                    "recovery-index.json"));

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
                Assert.IsAssignableFrom<CaptureTarget>(
                    preflight.Target),
                options,
                Assert.IsType<string>(
                    preflight.OutputDirectory),
                new DateTimeOffset(
                    2026,
                    10,
                    6,
                    8,
                    0,
                    0,
                    TimeSpan.Zero));

        Assert.Equal(
            SessionStatus.Created,
            session.Status);

        Assert.Equal(
            options,
            session.Options);

        Assert.Equal(
            target,
            session.Target);

        Assert.True(
            fileSystem.FileExists(
                session.FinalMediaPath));

        Assert.True(
            fileSystem.FileExists(
                SessionStore.GetMetadataPath(
                    session.WorkingDirectory)));
    }

    [Fact]
    public void MissingCurrentDefaultDevice_IsRejected()
    {
        var audio =
            new FakeAudioDeviceResolver
            {
                DefaultSystemAudioDeviceId = null
            };

        RecordingPreflightResult result =
            CreateService(audio)
                .Resolve(
                    CreateIntent(
                        recordSystemAudio: true));

        Assert.False(result.Succeeded);

        Assert.Equal(
            RecordingPreflightFailure
                .DefaultSystemAudioDeviceUnavailable,
            result.Failure);
    }

    private static RecordingPreflightService CreateService(
        FakeAudioDeviceResolver audio)
    {
        return new RecordingPreflightService(
            new FakeFileSystem(),
            audio);
    }

    private static RecordingIntent CreateIntent(
        string? outputDirectory = null,
        bool recordSystemAudio = false,
        bool recordMicrophone = false,
        AudioDevicePreference? systemPreference = null,
        AudioDevicePreference? microphonePreference = null)
    {
        return new RecordingIntent(
            new MonitorCaptureTarget(
                "DISPLAY-1"),
            new PixelSize(
                1920,
                1080),
            outputDirectory ??
                CreateOutputDirectory(
                    "intent"),
            targetFrameRate: 30,
            qualityPresetId: "balanced",
            recordSystemAudio,
            recordMicrophone,
            systemPreference ??
                AudioDevicePreference.Default,
            microphonePreference ??
                AudioDevicePreference.Default);
    }

    private static string CreateOutputDirectory(
        string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "QuietCapture",
            "PreflightTests",
            suffix,
            Guid.NewGuid().ToString("N"));
    }

    private sealed class FakeAudioDeviceResolver
        : IAudioDeviceResolver
    {
        public string? DefaultSystemAudioDeviceId
            { get; set; } =
                "default-render";

        public string? DefaultMicrophoneDeviceId
            { get; set; } =
                "default-capture";

        public bool SystemAudioAvailable
            { get; set; } = true;

        public bool MicrophoneAvailable
            { get; set; } = true;

        public int ResolveDefaultSystemAudioCalls
            { get; private set; }

        public int ResolveDefaultMicrophoneCalls
            { get; private set; }

        public int SystemAudioAvailabilityCalls
            { get; private set; }

        public int MicrophoneAvailabilityCalls
            { get; private set; }

        public string? LastSystemAudioAvailabilityId
            { get; private set; }

        public string? LastMicrophoneAvailabilityId
            { get; private set; }

        public int TotalCalls =>
            ResolveDefaultSystemAudioCalls +
            ResolveDefaultMicrophoneCalls +
            SystemAudioAvailabilityCalls +
            MicrophoneAvailabilityCalls;

        public string? ResolveDefaultSystemAudioDeviceId()
        {
            ResolveDefaultSystemAudioCalls++;
            return DefaultSystemAudioDeviceId;
        }

        public string? ResolveDefaultMicrophoneDeviceId()
        {
            ResolveDefaultMicrophoneCalls++;
            return DefaultMicrophoneDeviceId;
        }

        public bool IsSystemAudioDeviceAvailable(
            string deviceId)
        {
            SystemAudioAvailabilityCalls++;
            LastSystemAudioAvailabilityId =
                deviceId;
            return SystemAudioAvailable;
        }

        public bool IsMicrophoneDeviceAvailable(
            string deviceId)
        {
            MicrophoneAvailabilityCalls++;
            LastMicrophoneAvailabilityId =
                deviceId;
            return MicrophoneAvailable;
        }
    }
}
