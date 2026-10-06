using System.IO;
using QuietCapture.Core.Ports;
using QuietCapture.Core.Settings;
using QuietCapture.Infrastructure.Windows.Settings;

namespace QuietCapture.App;

internal sealed class SettingsStartupComposition
{
    private const string ProductDataDirectoryName =
        "ScreenRecorder";
    private const string SettingsFileName =
        "settings.json";

    private readonly SettingsService _settingsService;

    private SettingsStartupComposition(
        SettingsService settingsService,
        string settingsPath)
    {
        _settingsService = settingsService;
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public static SettingsStartupComposition CreateDefault(
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        string appData =
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .ApplicationData);

        string settingsPath =
            Path.Combine(
                appData,
                ProductDataDirectoryName,
                SettingsFileName);

        return new SettingsStartupComposition(
            new SettingsService(
                new JsonSettingsStore(
                    fileSystem,
                    settingsPath)),
            settingsPath);
    }

    public AppSettings LoadOrDefault()
    {
        return _settingsService.LoadOrDefault();
    }

    public IReadOnlyList<string> GetKnownOutputDirectories(
        AppSettings settings)
    {
        return _settingsService
            .GetKnownOutputDirectories(
                settings);
    }
}
