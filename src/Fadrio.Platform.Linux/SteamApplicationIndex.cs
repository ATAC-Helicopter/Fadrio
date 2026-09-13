namespace Fadrio.Platform.Linux;

public sealed record SteamApplicationEntry(string AppId, string Name, string InstallDirectory, string LibraryPath);

public sealed class SteamApplicationIndex
{
    private readonly IReadOnlyDictionary<string, SteamApplicationEntry[]> _applications;

    public SteamApplicationIndex(IEnumerable<string>? clientDirectories = null)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        clientDirectories ??= [Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam")];
        var libraries = new HashSet<string>(StringComparer.Ordinal);
        foreach (string client in clientDirectories)
        {
            AddLibrary(client);
            try
            {
                SteamKeyValues metadata = SteamKeyValues.Read(Path.Combine(client, "steamapps", "libraryfolders.vdf"));
                if (metadata.Children.TryGetValue("libraryfolders", out SteamKeyValues? folders))
                {
                    foreach (SteamKeyValues folder in folders.Children.Values)
                        if (folder.Text("path") is { } path) AddLibrary(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException) { }
        }
        var applications = new List<SteamApplicationEntry>();
        foreach (string library in libraries)
        {
            try
            {
                foreach (string manifest in Directory.GetFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf"))
                {
                    try
                    {
                        SteamKeyValues metadata = SteamKeyValues.Read(manifest);
                        if (!metadata.Children.TryGetValue("AppState", out SteamKeyValues? state)) continue;
                        string? id = state.Text("appid"), name = state.Text("name"), install = state.Text("installdir");
                        if (!ValidAppId(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(install) ||
                            install is "." or ".." || install.IndexOfAny(['/', '\\']) >= 0 ||
                            Path.GetFileName(manifest) != $"appmanifest_{id}.acf") continue;
                        string directory = Path.Combine(library, "steamapps", "common", install);
                        if (Directory.Exists(directory)) applications.Add(new(id!, name, directory, library));
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException) { }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        _applications = applications.GroupBy(entry => entry.AppId).ToDictionary(group => group.Key, group => group.ToArray());

        void AddLibrary(string path)
        {
            try
            {
                if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) return;
                libraries.Add(new DirectoryInfo(path).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    public IReadOnlyList<SteamApplicationEntry> Find(string appId) => _applications.GetValueOrDefault(appId) ?? [];

    internal static bool ValidAppId(string? value) => value is { Length: > 0 and <= 10 } &&
        value[0] != '0' && value.All(char.IsAsciiDigit) && uint.TryParse(value, out _);
}
