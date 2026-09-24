using Fadrio.Infrastructure;

namespace Fadrio.Infrastructure.Tests;

public sealed class DesktopEntryTests
{
    private static string Fixtures => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures/desktop-files"));

    [Fact]
    public void ParsesDesktopMetadataAndRemovesFieldCodes()
    {
        var entry = DesktopEntryParser.Parse(Path.Combine(Fixtures, "firefox.desktop"));
        Assert.NotNull(entry);
        Assert.Equal("Firefox", entry.Name);
        Assert.Equal("/usr/lib/firefox/firefox", entry.Executable);
        Assert.Equal("firefox", entry.Icon);
        Assert.False(entry.NoDisplay);
    }

    [Fact]
    public void NormalizesEnvAndQuotedExecutable()
    {
        var entry = DesktopEntryParser.Parse(Path.Combine(Fixtures, "env-player.desktop"));
        Assert.Equal("/opt/Fixture Player/player", entry!.Executable);
    }

    [Fact]
    public void IndexMatchesExecutableBasename()
    {
        using var index = new XdgDesktopApplicationIndex([Fixtures], watchForChanges: false);
        Assert.Equal("Firefox", Assert.Single(index.FindByExecutable("/different/path/firefox")).Name);
    }

    [Fact]
    public void IndexMatchesCommonBinRuntimeSuffixWithoutDiscardingAmbiguity()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "firefox.desktop"),
            DesktopEntry("Firefox", "/usr/bin/firefox"));
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "hidden-firefox.desktop"),
            DesktopEntry("Hidden Firefox", "/usr/lib/firefox/firefox-bin", noDisplay: true));
        using var index = new XdgDesktopApplicationIndex([directory.Path], watchForChanges: false);

        IReadOnlyList<Fadrio.Application.DesktopApplicationEntry> matches =
            index.FindByExecutable("/usr/lib/firefox/firefox-bin");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, entry => entry.Id == "firefox");
        Assert.Contains(matches, entry => entry.Id == "hidden-firefox");
    }

    [Fact]
    public void IndexMatchesDesktopIdWithOrWithoutSuffix()
    {
        using var index = new XdgDesktopApplicationIndex([Fixtures], watchForChanges: false);
        Assert.Equal("Brave Web Browser", Assert.Single(index.FindById("brave-browser")).Name);
        Assert.Equal("Brave Web Browser", Assert.Single(index.FindById("brave-browser.desktop")).Name);
    }

    [Fact]
    public void ParsesFlatpakLauncherAndIndexesItsApplicationId()
    {
        var entry = DesktopEntryParser.Parse(Path.Combine(Fixtures, "com.spotify.Client.desktop"));

        Assert.NotNull(entry);
        Assert.Equal("/usr/bin/flatpak", entry.Executable);
        Assert.Equal("com.spotify.Client", entry.FlatpakId);
        using var index = new XdgDesktopApplicationIndex([Fixtures], watchForChanges: false);
        Assert.Equal("Spotify", Assert.Single(index.FindById("com.spotify.Client")).Name);
    }

    [Fact]
    public void ParsesAndIndexesExportedSnapMetadata()
    {
        var entry = DesktopEntryParser.Parse(Path.Combine(Fixtures, "fixture-player_fixture-player.desktop"));

        Assert.NotNull(entry);
        Assert.Equal("fixture-player", entry.SnapInstanceName);
        using var index = new XdgDesktopApplicationIndex([Fixtures], watchForChanges: false);
        Assert.Equal("Fixture Player Snap", Assert.Single(index.FindBySnapInstanceName("fixture-player")).Name);
    }

    [Fact]
    public void RefreshAtomicallyExposesCreateChangeAndDelete()
    {
        using var directory = new TemporaryDirectory();
        using var index = new XdgDesktopApplicationIndex([directory.Path], watchForChanges: false);
        long initialRevision = index.Revision;
        string desktopFile = System.IO.Path.Combine(directory.Path, "fixture.desktop");

        File.WriteAllText(desktopFile, DesktopEntry("Fixture", "/usr/bin/fixture"));
        index.Refresh();
        Assert.Equal(initialRevision + 1, index.Revision);
        Assert.Equal("Fixture", Assert.Single(index.FindById("fixture")).Name);

        File.WriteAllText(desktopFile, DesktopEntry("Updated Fixture", "/usr/bin/fixture"));
        index.Refresh();
        Assert.Equal("Updated Fixture", Assert.Single(index.FindById("fixture")).Name);

        File.Delete(desktopFile);
        index.Refresh();
        Assert.Empty(index.FindById("fixture"));
    }

    [Fact]
    public async Task WatcherDebouncesChangesAndRefreshesTheVisibleSnapshot()
    {
        using var directory = new TemporaryDirectory();
        using var index = new XdgDesktopApplicationIndex(
            [directory.Path],
            refreshDelay: TimeSpan.FromMilliseconds(200));
        long initialRevision = index.Revision;
        string desktopFile = System.IO.Path.Combine(directory.Path, "watched.desktop");

        File.WriteAllText(desktopFile, DesktopEntry("Initial", "/usr/bin/watched"));
        File.WriteAllText(desktopFile, DesktopEntry("Final", "/usr/bin/watched"));

        await WaitForRevisionAsync(index, initialRevision, TestContext.Current.CancellationToken);
        Assert.Equal(initialRevision + 1, index.Revision);
        Assert.Equal("Final", Assert.Single(index.FindById("watched")).Name);
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        Assert.Equal(initialRevision + 1, index.Revision);

        long createdRevision = index.Revision;
        File.WriteAllText(desktopFile, DesktopEntry("Changed", "/usr/bin/watched"));
        await WaitForRevisionAsync(index, createdRevision, TestContext.Current.CancellationToken);
        Assert.Equal("Changed", Assert.Single(index.FindById("watched")).Name);

        long changedRevision = index.Revision;
        File.Delete(desktopFile);
        await WaitForRevisionAsync(index, changedRevision, TestContext.Current.CancellationToken);
        Assert.Empty(index.FindById("watched"));
    }

    [Fact]
    public async Task DisposeCancelsQueuedRefreshAndRejectsManualRefresh()
    {
        using var directory = new TemporaryDirectory();
        var index = new XdgDesktopApplicationIndex(
            [directory.Path],
            refreshDelay: TimeSpan.FromMilliseconds(500));
        long revision = index.Revision;

        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "queued.desktop"),
            DesktopEntry("Queued", "/usr/bin/queued"));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        index.Dispose();

        await Task.Delay(TimeSpan.FromMilliseconds(700), TestContext.Current.CancellationToken);
        Assert.Equal(revision, index.Revision);
        Assert.Throws<ObjectDisposedException>(index.Refresh);
    }

    private static string DesktopEntry(string name, string executable, bool noDisplay = false) =>
        $"[Desktop Entry]{Environment.NewLine}Name={name}{Environment.NewLine}Exec={executable}{Environment.NewLine}" +
        $"NoDisplay={noDisplay.ToString().ToLowerInvariant()}{Environment.NewLine}";

    private static async Task WaitForRevisionAsync(
        XdgDesktopApplicationIndex index,
        long previousRevision,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (index.Revision == previousRevision)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fadrio-desktop-index-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
