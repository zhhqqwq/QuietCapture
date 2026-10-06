namespace QuietCapture.Core.Settings;

public sealed record OutputDirectoryHistory
{
    private readonly IReadOnlyList<string>
        _knownDirectories;

    public OutputDirectoryHistory(
        string? currentOutputDirectory,
        IEnumerable<string>? history = null)
    {
        string? current =
            NormalizeOptional(
                currentOutputDirectory);

        var known =
            new List<string>();

        if (current is not null)
        {
            known.Add(current);
        }

        if (history is not null)
        {
            foreach (string path in history)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string normalized =
                    Normalize(path);

                if (!known.Contains(
                        normalized,
                        StringComparer.OrdinalIgnoreCase))
                {
                    known.Add(normalized);
                }
            }
        }

        CurrentOutputDirectory = current;
        _knownDirectories = known;
    }

    public string? CurrentOutputDirectory { get; }

    public IReadOnlyList<string> KnownDirectories =>
        _knownDirectories;

    public static OutputDirectoryHistory Empty { get; } =
        new(
            currentOutputDirectory: null);

    public OutputDirectoryHistory WithCurrent(
        string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                outputDirectory))
        {
            throw new ArgumentException(
                "Output directory must not be empty.",
                nameof(outputDirectory));
        }

        return new OutputDirectoryHistory(
            outputDirectory,
            _knownDirectories);
    }

    private static string? NormalizeOptional(
        string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Normalize(path);
    }

    private static string Normalize(string path)
    {
        string fullPath =
            Path.GetFullPath(path);

        string? root =
            Path.GetPathRoot(fullPath);

        if (!string.IsNullOrWhiteSpace(root) &&
            string.Equals(
                fullPath,
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }
}
