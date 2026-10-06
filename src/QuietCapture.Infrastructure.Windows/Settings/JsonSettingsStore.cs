using System.Text.Json;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Settings;

namespace QuietCapture.Infrastructure.Windows.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    private readonly IFileSystem _fileSystem;
    private readonly string _settingsPath;

    public JsonSettingsStore(
        IFileSystem fileSystem,
        string settingsPath)
    {
        _fileSystem =
            fileSystem ??
            throw new ArgumentNullException(
                nameof(fileSystem));

        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            throw new ArgumentException(
                "Settings path must not be empty.",
                nameof(settingsPath));
        }

        _settingsPath =
            Path.GetFullPath(settingsPath);
    }

    public string SettingsPath => _settingsPath;

    public AppSettings? Load()
    {
        if (!_fileSystem.FileExists(
                _settingsPath))
        {
            return null;
        }

        string json =
            _fileSystem.ReadAllText(
                _settingsPath);

        SettingsDocument? document;

        try
        {
            document =
                JsonSerializer.Deserialize<
                    SettingsDocument>(
                    json,
                    Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Settings JSON is invalid.",
                ex);
        }

        if (document is null)
        {
            throw new InvalidDataException(
                "Settings JSON did not contain a document.");
        }

        if (document.SchemaVersion !=
            CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported settings schema version: {document.SchemaVersion}.");
        }

        return ToDomain(document);
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory =
            Path.GetDirectoryName(
                _settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        string json =
            JsonSerializer.Serialize(
                FromDomain(settings),
                Options);

        _fileSystem.WriteAllTextAtomically(
            _settingsPath,
            json);
    }

    private static SettingsDocument FromDomain(
        AppSettings settings)
    {
        return new SettingsDocument
        {
            TargetFrameRate =
                settings.TargetFrameRate,
            QualityPresetId =
                settings.QualityPresetId,
            RecordSystemAudioByDefault =
                settings.RecordSystemAudioByDefault,
            RecordMicrophoneByDefault =
                settings.RecordMicrophoneByDefault,
            CurrentOutputDirectory =
                settings.OutputDirectories
                    .CurrentOutputDirectory,
            OutputDirectoryHistory =
                settings.OutputDirectories
                    .KnownDirectories
                    .ToList(),
            SystemAudioDevicePreference =
                FromDomain(
                    settings
                        .SystemAudioDevicePreference),
            MicrophoneDevicePreference =
                FromDomain(
                    settings
                        .MicrophoneDevicePreference)
        };
    }

    private static DevicePreferenceDocument FromDomain(
        AudioDevicePreference preference)
    {
        return new DevicePreferenceDocument
        {
            Kind =
                preference.Kind.ToString(),
            PreferredDeviceId =
                preference.PreferredDeviceId
        };
    }

    private static AppSettings ToDomain(
        SettingsDocument document)
    {
        return new AppSettings(
            new OutputDirectoryHistory(
                document.CurrentOutputDirectory,
                document.OutputDirectoryHistory),
            document.TargetFrameRate,
            document.QualityPresetId,
            document.RecordSystemAudioByDefault,
            document.RecordMicrophoneByDefault,
            ToDomain(
                document.SystemAudioDevicePreference),
            ToDomain(
                document.MicrophoneDevicePreference));
    }

    private static AudioDevicePreference ToDomain(
        DevicePreferenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!Enum.TryParse(
                document.Kind,
                ignoreCase: false,
                out AudioDevicePreferenceKind kind) ||
            !Enum.IsDefined(kind))
        {
            throw new InvalidDataException(
                "Audio-device preference kind is invalid.");
        }

        return kind switch
        {
            AudioDevicePreferenceKind.Default =>
                AudioDevicePreference.Default,

            AudioDevicePreferenceKind.Specific =>
                AudioDevicePreference.Specific(
                    document.PreferredDeviceId ??
                    throw new InvalidDataException(
                        "Specific audio-device preference is missing a device ID.")),

            _ =>
                throw new InvalidDataException(
                    "Audio-device preference kind is unsupported.")
        };
    }

    private sealed record SettingsDocument
    {
        public int SchemaVersion { get; init; } =
            CurrentSchemaVersion;

        public string? CurrentOutputDirectory
            { get; init; }

        public List<string> OutputDirectoryHistory
            { get; init; } = new();

        public int TargetFrameRate { get; init; } =
            AppSettings.DefaultTargetFrameRate;

        public string QualityPresetId { get; init; } =
            AppSettings.DefaultQualityPresetId;

        public bool RecordSystemAudioByDefault
            { get; init; }

        public bool RecordMicrophoneByDefault
            { get; init; }

        public DevicePreferenceDocument
            SystemAudioDevicePreference { get; init; } =
                DevicePreferenceDocument.Default();

        public DevicePreferenceDocument
            MicrophoneDevicePreference { get; init; } =
                DevicePreferenceDocument.Default();
    }

    private sealed record DevicePreferenceDocument
    {
        public string Kind { get; init; } =
            AudioDevicePreferenceKind.Default
                .ToString();

        public string? PreferredDeviceId
            { get; init; }

        public static DevicePreferenceDocument Default()
        {
            return new DevicePreferenceDocument();
        }
    }
}
