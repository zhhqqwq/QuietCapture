namespace QuietCapture.Core.Settings;

public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store =
            store ??
            throw new ArgumentNullException(
                nameof(store));
    }

    public AppSettings LoadOrDefault()
    {
        try
        {
            return _store.Load() ??
                AppSettings.CreateDefault();
        }
        catch (Exception ex)
            when (IsRecoverableSettingsReadFailure(ex))
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _store.Save(settings);
    }

    public AppSettings SetCurrentOutputDirectory(
        AppSettings settings,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);

        OutputDirectoryHistory updatedDirectories =
            settings.OutputDirectories.WithCurrent(
                outputDirectory);

        AppSettings updated =
            settings.WithOutputDirectories(
                updatedDirectories);

        _store.Save(updated);

        return updated;
    }

    public IReadOnlyList<string>
        GetKnownOutputDirectories(
            AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.OutputDirectories
            .KnownDirectories;
    }

    private static bool IsRecoverableSettingsReadFailure(
        Exception ex)
    {
        return ex is
            IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            InvalidOperationException or
            ArgumentException;
    }
}
