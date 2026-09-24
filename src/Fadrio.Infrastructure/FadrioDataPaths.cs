namespace Fadrio.Infrastructure;

public sealed record FadrioDataPaths(
    string ConfigDirectory,
    string DataDirectory,
    string CacheDirectory,
    string StateDirectory)
{
    public string DatabasePath => Path.Combine(DataDirectory, "fadrio.db");

    public static FadrioDataPaths Resolve() => Resolve(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetEnvironmentVariable);

    public static FadrioDataPaths Resolve(string userHome, Func<string, string?> getEnvironmentVariable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userHome);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        if (!Path.IsPathFullyQualified(userHome))
        {
            throw new ArgumentException("The user home path must be absolute.", nameof(userHome));
        }

        string home = Path.GetFullPath(userHome);
        return new(
            AppDirectory("XDG_CONFIG_HOME", ".config"),
            AppDirectory("XDG_DATA_HOME", ".local/share"),
            AppDirectory("XDG_CACHE_HOME", ".cache"),
            AppDirectory("XDG_STATE_HOME", ".local/state"));

        string AppDirectory(string variable, string fallback)
        {
            string? configured = getEnvironmentVariable(variable);
            string baseDirectory = !string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured)
                ? configured
                : Path.Combine(home, fallback);
            return Path.Combine(Path.GetFullPath(baseDirectory), "fadrio");
        }
    }
}
