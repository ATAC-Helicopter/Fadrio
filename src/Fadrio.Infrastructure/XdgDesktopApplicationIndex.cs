using Fadrio.Application;

namespace Fadrio.Infrastructure;

public sealed class XdgDesktopApplicationIndex : IDesktopApplicationIndex, IDesktopApplicationIndexRevision, IDisposable
{
    private readonly string[] _directories;
    private readonly FileSystemWatcher[] _watchers;
    private readonly Timer? _refreshTimer;
    private readonly object _refreshLock = new();
    private DesktopApplicationEntry[] _entries = [];
    private long _revision;
    private bool _disposed;

    public XdgDesktopApplicationIndex(
        IEnumerable<string>? applicationDirectories = null,
        bool watchForChanges = true,
        TimeSpan? refreshDelay = null)
    {
        _directories = (applicationDirectories ?? GetStandardApplicationDirectories())
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Refresh();
        if (!watchForChanges)
        {
            _watchers = [];
            return;
        }

        _refreshTimer = new Timer(_ => RefreshSafely(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        TimeSpan delay = refreshDelay ?? TimeSpan.FromMilliseconds(250);
        _watchers = _directories.Where(Directory.Exists).Select(directory => CreateWatcher(directory, delay)).ToArray();
    }

    public long Revision => Interlocked.Read(ref _revision);

    public void Refresh()
    {
        lock (_refreshLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DesktopApplicationEntry[] entries = _directories
                .Where(Directory.Exists)
                .SelectMany(EnumerateDesktopFiles)
                .Select(TryParse)
                .Where(entry => entry is not null && !entry.Hidden)
                .Cast<DesktopApplicationEntry>()
                .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            Interlocked.Exchange(ref _entries, entries);
            Interlocked.Increment(ref _revision);
        }
    }

    public IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        string name = Path.GetFileName(executablePath);
        DesktopApplicationEntry[] entries = Volatile.Read(ref _entries);
        DesktopApplicationEntry[] matches = entries.Where(entry => entry.Executable is not null &&
            (PathEquals(entry.Executable, executablePath) ||
             ExecutableNamesMatch(Path.GetFileName(entry.Executable), name)))
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
        DesktopApplicationEntry[] entries = Volatile.Read(ref _entries);
        return entries
            .Where(entry => entry.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                entry.FlatpakId?.Equals(normalized, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
    }

    public IReadOnlyList<DesktopApplicationEntry> FindBySnapInstanceName(string snapInstanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapInstanceName);
        DesktopApplicationEntry[] entries = Volatile.Read(ref _entries);
        return entries
            .Where(entry => entry.SnapInstanceName?.Equals(snapInstanceName, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
    }

    public void Dispose()
    {
        lock (_refreshLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (FileSystemWatcher watcher in _watchers)
            {
                watcher.Dispose();
            }
            _refreshTimer?.Dispose();
        }
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

    private static IEnumerable<string> EnumerateDesktopFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.desktop", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private FileSystemWatcher CreateWatcher(string directory, TimeSpan delay)
    {
        var watcher = new FileSystemWatcher(directory, "*.desktop")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        FileSystemEventHandler changed = (_, _) => ScheduleRefresh(delay);
        RenamedEventHandler renamed = (_, _) => ScheduleRefresh(delay);
        watcher.Created += changed;
        watcher.Changed += changed;
        watcher.Deleted += changed;
        watcher.Renamed += renamed;
        watcher.Error += (_, _) => ScheduleRefresh(delay);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void ScheduleRefresh(TimeSpan delay)
    {
        lock (_refreshLock)
        {
            if (!_disposed)
            {
                _refreshTimer?.Change(delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void RefreshSafely()
    {
        try
        {
            Refresh();
        }
        catch (ObjectDisposedException)
        {
            // A queued debounce callback can race disposal.
        }
    }

    private static bool PathEquals(string left, string right) =>
        Path.IsPathRooted(left) && Path.IsPathRooted(right) &&
        Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.Ordinal);

    private static bool ExecutableNamesMatch(string left, string right) =>
        NormalizeExecutableName(left).Equals(NormalizeExecutableName(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeExecutableName(string value) =>
        value.EndsWith("-bin", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
}
