using Fadrio.Application;

namespace Fadrio.Infrastructure;

public sealed class XdgDesktopApplicationIndex : IDesktopApplicationIndex
{
    private readonly IReadOnlyList<DesktopApplicationEntry> _entries;

    public XdgDesktopApplicationIndex(IEnumerable<string>? applicationDirectories = null)
    {
        IEnumerable<string> directories = applicationDirectories ?? GetStandardApplicationDirectories();
        _entries = directories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.desktop", SearchOption.AllDirectories))
            .Select(TryParse)
            .Where(entry => entry is not null && !entry.Hidden)
            .Cast<DesktopApplicationEntry>()
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        string name = Path.GetFileName(executablePath);
        DesktopApplicationEntry[] matches = _entries.Where(entry => entry.Executable is not null &&
            (PathEquals(entry.Executable, executablePath) ||
             Path.GetFileName(entry.Executable).Equals(name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        DesktopApplicationEntry[] visible = matches.Where(entry => !entry.NoDisplay).ToArray();
        // A hidden URL handler can share its application's executable and icon.
        // Collapse only this corroborated helper shape; retain other ambiguity.
        if (visible.Length == 1 && matches.All(entry => entry == visible[0] ||
            (entry.NoDisplay && PathEquals(entry.Executable!, visible[0].Executable!) &&
             entry.Icon is not null && entry.Icon == visible[0].Icon)))
        {
            return visible;
        }
        return matches;
    }

    public IReadOnlyList<DesktopApplicationEntry> FindById(string desktopFileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desktopFileId);
        string normalized = desktopFileId.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase)
            ? desktopFileId[..^8]
            : desktopFileId;
        return _entries
            .Where(entry => entry.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public static IEnumerable<string> GetStandardApplicationDirectories()
    {
        string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        yield return Path.Combine(dataHome, "applications");

        string dataDirectories = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share";
        foreach (string directory in dataDirectories.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(directory, "applications");
        }
    }

    private static DesktopApplicationEntry? TryParse(string path)
    {
        try
        {
            return DesktopEntryParser.Parse(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool PathEquals(string left, string right) =>
        Path.IsPathRooted(left) && Path.IsPathRooted(right) &&
        Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.Ordinal);
}
