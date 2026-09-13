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
    public void IndexIsBuiltOnceAndMatchesExecutableBasename()
    {
        var index = new XdgDesktopApplicationIndex([Fixtures]);
        Assert.Equal("Firefox", Assert.Single(index.FindByExecutable("/different/path/firefox")).Name);
    }

    [Fact]
    public void IndexMatchesDesktopIdWithOrWithoutSuffix()
    {
        var index = new XdgDesktopApplicationIndex([Fixtures]);
        Assert.Equal("Brave Web Browser", Assert.Single(index.FindById("brave-browser")).Name);
        Assert.Equal("Brave Web Browser", Assert.Single(index.FindById("brave-browser.desktop")).Name);
    }
}
