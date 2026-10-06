using QuietCapture.Core.Models;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Settings;
using QuietCapture.Core.Storage;

namespace QuietCapture.Core.Preflight;

public sealed class RecordingPreflightService
{
    private readonly IFileSystem _fileSystem;
    private readonly IAudioDeviceResolver
        _audioDeviceResolver;

    public RecordingPreflightService(
        IFileSystem fileSystem,
        IAudioDeviceResolver audioDeviceResolver)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));

        _audioDeviceResolver =
            audioDeviceResolver ??
            throw new ArgumentNullException(
                nameof(audioDeviceResolver));
    }

    public RecordingPreflightResult Resolve(
        RecordingIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (string.IsNullOrWhiteSpace(
                intent.OutputDirectory))
        {
            return RecordingPreflightResult.Failed(
                RecordingPreflightFailure
                    .MissingOutputDirectory,
                "An output directory must be selected before recording.");
        }

        string outputDirectory =
            intent.OutputDirectory!;

        StorageVolumeInfo outputVolume;

        try
        {
            outputVolume =
                _fileSystem.GetStorageVolumeInfo(
                    outputDirectory);
        }
        catch (Exception ex)
            when (IsStorageResolutionException(ex))
        {
            return RecordingPreflightResult.Failed(
                RecordingPreflightFailure
                    .OutputVolumeUnavailable,
                ex.Message);
        }

        if (!outputVolume.IsWritable)
        {
            return RecordingPreflightResult.Failed(
                RecordingPreflightFailure
                    .OutputVolumeNotWritable,
                "The output volume is not writable.");
        }

        if (outputVolume.IsFat32)
        {
            return RecordingPreflightResult.Failed(
                RecordingPreflightFailure
                    .Fat32OutputNotSupported,
                "FAT32 is not supported for recording output.");
        }

        DeviceResolution systemAudio =
            ResolveSystemAudio(intent);

        if (!systemAudio.Succeeded)
        {
            return RecordingPreflightResult.Failed(
                systemAudio.Failure!.Value,
                systemAudio.Error!);
        }

        DeviceResolution microphone =
            ResolveMicrophone(intent);

        if (!microphone.Succeeded)
        {
            return RecordingPreflightResult.Failed(
                microphone.Failure!.Value,
                microphone.Error!);
        }

        var options =
            new RecordingOptions(
                intent.OutputSize,
                intent.TargetFrameRate,
                new VideoQualityPreset(
                    intent.QualityPresetId),
                intent.RecordSystemAudio,
                intent.RecordMicrophone,
                systemAudio.DeviceId,
                microphone.DeviceId);

        return RecordingPreflightResult.Success(
            intent.Target,
            outputDirectory,
            options);
    }

    private DeviceResolution ResolveSystemAudio(
        RecordingIntent intent)
    {
        if (!intent.RecordSystemAudio)
        {
            return DeviceResolution.Success(
                deviceId: null);
        }

        AudioDevicePreference preference =
            intent.SystemAudioDevicePreference;

        if (preference.UsesDefaultDevice)
        {
            string? deviceId;

            try
            {
                deviceId =
                    _audioDeviceResolver
                        .ResolveDefaultSystemAudioDeviceId();
            }
            catch (Exception ex)
                when (IsDeviceResolutionException(ex))
            {
                return DeviceResolution.Failed(
                    RecordingPreflightFailure
                        .DefaultSystemAudioDeviceUnavailable,
                    ex.Message);
            }

            return string.IsNullOrWhiteSpace(
                    deviceId)
                ? DeviceResolution.Failed(
                    RecordingPreflightFailure
                        .DefaultSystemAudioDeviceUnavailable,
                    "The current default system-audio device could not be resolved.")
                : DeviceResolution.Success(deviceId);
        }

        string preferredId =
            preference.PreferredDeviceId!;

        try
        {
            if (!_audioDeviceResolver
                    .IsSystemAudioDeviceAvailable(
                        preferredId))
            {
                return DeviceResolution.Failed(
                    RecordingPreflightFailure
                        .SpecificSystemAudioDeviceUnavailable,
                    "The preferred system-audio device is unavailable.");
            }
        }
        catch (Exception ex)
            when (IsDeviceResolutionException(ex))
        {
            return DeviceResolution.Failed(
                RecordingPreflightFailure
                    .SpecificSystemAudioDeviceUnavailable,
                ex.Message);
        }

        return DeviceResolution.Success(
            preferredId);
    }

    private DeviceResolution ResolveMicrophone(
        RecordingIntent intent)
    {
        if (!intent.RecordMicrophone)
        {
            return DeviceResolution.Success(
                deviceId: null);
        }

        AudioDevicePreference preference =
            intent.MicrophoneDevicePreference;

        if (preference.UsesDefaultDevice)
        {
            string? deviceId;

            try
            {
                deviceId =
                    _audioDeviceResolver
                        .ResolveDefaultMicrophoneDeviceId();
            }
            catch (Exception ex)
                when (IsDeviceResolutionException(ex))
            {
                return DeviceResolution.Failed(
                    RecordingPreflightFailure
                        .DefaultMicrophoneDeviceUnavailable,
                    ex.Message);
            }

            return string.IsNullOrWhiteSpace(
                    deviceId)
                ? DeviceResolution.Failed(
                    RecordingPreflightFailure
                        .DefaultMicrophoneDeviceUnavailable,
                    "The current default microphone device could not be resolved.")
                : DeviceResolution.Success(deviceId);
        }

        string preferredId =
            preference.PreferredDeviceId!;

        try
        {
            if (!_audioDeviceResolver
                    .IsMicrophoneDeviceAvailable(
                        preferredId))
            {
                return DeviceResolution.Failed(
                    RecordingPreflightFailure
                        .SpecificMicrophoneDeviceUnavailable,
                    "The preferred microphone device is unavailable.");
            }
        }
        catch (Exception ex)
            when (IsDeviceResolutionException(ex))
        {
            return DeviceResolution.Failed(
                RecordingPreflightFailure
                    .SpecificMicrophoneDeviceUnavailable,
                ex.Message);
        }

        return DeviceResolution.Success(
            preferredId);
    }

    private static bool IsStorageResolutionException(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            ArgumentException;
    }

    private static bool IsDeviceResolutionException(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException;
    }

    private sealed record DeviceResolution(
        bool Succeeded,
        string? DeviceId,
        RecordingPreflightFailure? Failure,
        string? Error)
    {
        public static DeviceResolution Success(
            string? deviceId)
        {
            return new DeviceResolution(
                true,
                deviceId,
                null,
                null);
        }

        public static DeviceResolution Failed(
            RecordingPreflightFailure failure,
            string error)
        {
            return new DeviceResolution(
                false,
                null,
                failure,
                error);
        }
    }
}
