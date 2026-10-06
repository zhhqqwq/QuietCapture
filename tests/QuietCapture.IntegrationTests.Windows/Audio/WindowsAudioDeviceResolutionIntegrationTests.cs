using QuietCapture.Core.Models;
using QuietCapture.Core.Preflight;
using QuietCapture.Core.Settings;
using QuietCapture.Infrastructure.Windows.Audio;
using QuietCapture.Infrastructure.Windows.Storage;

namespace QuietCapture.IntegrationTests.Windows.Audio;

public sealed class WindowsAudioDeviceResolutionIntegrationTests
{
    [Fact]
    public void EnumerateRenderEndpoints_ReturnsActiveRenderSnapshots()
    {
        var enumerator =
            new AudioEndpointEnumerator();

        IReadOnlyList<AudioEndpointSnapshot> endpoints =
            enumerator.EnumerateRenderEndpoints();

        Assert.All(
            endpoints,
            endpoint =>
            {
                Assert.Equal(
                    AudioEndpointFlow.Render,
                    endpoint.Flow);
                Assert.True(endpoint.IsActive);
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        endpoint.Id));
            });

        Assert.Equal(
            endpoints.Count,
            endpoints
                .Select(endpoint => endpoint.Id)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count());
    }

    [Fact]
    public void EnumerateCaptureEndpoints_ReturnsActiveCaptureSnapshots()
    {
        var enumerator =
            new AudioEndpointEnumerator();

        IReadOnlyList<AudioEndpointSnapshot> endpoints =
            enumerator.EnumerateCaptureEndpoints();

        Assert.All(
            endpoints,
            endpoint =>
            {
                Assert.Equal(
                    AudioEndpointFlow.Capture,
                    endpoint.Flow);
                Assert.True(endpoint.IsActive);
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        endpoint.Id));
            });

        Assert.Equal(
            endpoints.Count,
            endpoints
                .Select(endpoint => endpoint.Id)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count());
    }

    [Fact]
    public void ResolveCurrentDefaultRender_MatchesEnumerator()
    {
        var enumerator =
            new AudioEndpointEnumerator();

        AudioEndpointSnapshot? expected =
            enumerator.GetDefaultRenderEndpoint();

        var resolver =
            new WindowsAudioDeviceResolver(
                enumerator);

        string? actual =
            resolver.ResolveDefaultSystemAudioDeviceId();

        Assert.Equal(
            expected?.Id,
            actual,
            ignoreCase: true);
    }

    [Fact]
    public void ResolveCurrentDefaultCapture_MatchesEnumerator()
    {
        var enumerator =
            new AudioEndpointEnumerator();

        AudioEndpointSnapshot? expected =
            enumerator.GetDefaultCaptureEndpoint();

        var resolver =
            new WindowsAudioDeviceResolver(
                enumerator);

        string? actual =
            resolver.ResolveDefaultMicrophoneDeviceId();

        Assert.Equal(
            expected?.Id,
            actual,
            ignoreCase: true);
    }

    [Fact]
    public void SpecificDeviceAvailability_MatchesActiveEndpointSets()
    {
        var enumerator =
            new AudioEndpointEnumerator();

        var resolver =
            new WindowsAudioDeviceResolver(
                enumerator);

        IReadOnlyList<AudioEndpointSnapshot> render =
            enumerator.EnumerateRenderEndpoints();

        IReadOnlyList<AudioEndpointSnapshot> capture =
            enumerator.EnumerateCaptureEndpoints();

        Assert.All(
            render,
            endpoint =>
                Assert.True(
                    resolver.IsSystemAudioDeviceAvailable(
                        endpoint.Id)));

        Assert.All(
            capture,
            endpoint =>
                Assert.True(
                    resolver.IsMicrophoneDeviceAvailable(
                        endpoint.Id)));
    }

    [Fact]
    public void MissingOrStaleDeviceId_IsUnavailable()
    {
        var resolver =
            new WindowsAudioDeviceResolver();

        string staleId =
            $"QuietCapture-Missing-{Guid.NewGuid():N}";

        Assert.False(
            resolver.IsSystemAudioDeviceAvailable(
                staleId));

        Assert.False(
            resolver.IsMicrophoneDeviceAvailable(
                staleId));
    }

    [Fact]
    public void Preflight_ResolvesConcreteIdsWhenDefaultsExist()
    {
        using var scope =
            TestDirectoryScope.Create();

        var enumerator =
            new AudioEndpointEnumerator();

        AudioEndpointSnapshot? render =
            enumerator.GetDefaultRenderEndpoint();

        AudioEndpointSnapshot? capture =
            enumerator.GetDefaultCaptureEndpoint();

        var resolver =
            new WindowsAudioDeviceResolver(
                enumerator);

        bool enableRender =
            render is not null;

        bool enableCapture =
            capture is not null;

        if (!enableRender &&
            !enableCapture)
        {
            enableRender = true;
        }

        var intent =
            new RecordingIntent(
                new MonitorCaptureTarget(
                    "DISPLAY-1"),
                new PixelSize(
                    1280,
                    720),
                scope.Path,
                targetFrameRate: 30,
                qualityPresetId: "balanced",
                recordSystemAudio: enableRender,
                recordMicrophone: enableCapture,
                AudioDevicePreference.Default,
                AudioDevicePreference.Default);

        RecordingPreflightResult result =
            new RecordingPreflightService(
                new WindowsFileSystem(),
                resolver)
            .Resolve(intent);

        if (render is null &&
            capture is null)
        {
            Assert.False(result.Succeeded);
            Assert.Equal(
                RecordingPreflightFailure
                    .DefaultSystemAudioDeviceUnavailable,
                result.Failure);
            return;
        }

        Assert.True(result.Succeeded);

        RecordingOptions options =
            Assert.IsType<RecordingOptions>(
                result.Options);

        if (render is not null)
        {
            Assert.Equal(
                render.Id,
                options.SystemAudioDeviceId,
                ignoreCase: true);
        }
        else
        {
            Assert.Null(
                options.SystemAudioDeviceId);
        }

        if (capture is not null)
        {
            Assert.Equal(
                capture.Id,
                options.MicrophoneDeviceId,
                ignoreCase: true);
        }
        else
        {
            Assert.Null(
                options.MicrophoneDeviceId);
        }
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
                    "AudioResolutionIntegration",
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
