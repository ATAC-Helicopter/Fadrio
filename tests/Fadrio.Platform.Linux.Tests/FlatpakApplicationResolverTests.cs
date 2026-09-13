using Fadrio.Application;
using Fadrio.Core;
using Fadrio.Infrastructure;

namespace Fadrio.Platform.Linux.Tests;

public sealed class FlatpakApplicationResolverTests
{
    private static string DesktopFixtures => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures/desktop-files"));

    [Fact]
    public async Task TrustedSandboxIdProducesExactCanonicalIdentityFromHostDesktopEntry()
    {
        var resolver = new FlatpakApplicationResolver(new XdgDesktopApplicationIndex([DesktopFixtures]));
        var process = new ProcessMetadata(42, "/app/extra/bin/spotify", ["spotify"])
        {
            FlatpakId = "com.spotify.Client"
        };

        ApplicationIdentity? identity = await resolver.TryResolveAsync(
            Session(), process, TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Equal("flatpak:com.spotify.Client", identity.Id.Value);
        Assert.Equal("com.spotify.Client", identity.FlatpakId);
        Assert.Equal("com.spotify.Client", identity.DesktopFileId);
        Assert.Equal("Spotify", identity.DisplayName);
        Assert.Equal("com.spotify.Client", identity.Icon?.Value);
        Assert.Equal(IdentityConfidence.Exact, identity.Confidence);
        Assert.Contains(identity.Evidence, item => item.Kind == IdentityEvidenceKind.FlatpakApplicationId);
    }

    [Fact]
    public async Task TrustedSandboxIdStillWinsWithoutDesktopMetadata()
    {
        var resolver = new FlatpakApplicationResolver(new EmptyDesktopIndex());
        var process = new ProcessMetadata(42, "/app/bin/player", []) { FlatpakId = "org.example.Player" };

        ApplicationIdentity? identity = await resolver.TryResolveAsync(
            Session(), process, TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Equal("flatpak:org.example.Player", identity.Id.Value);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
        Assert.Equal("Fixture Player", identity.DisplayName);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("../org.example.Player")]
    [InlineData("org.example.Player:evil")]
    [InlineData("org.example-site.Player")]
    [InlineData("Org.example.Player")]
    [InlineData("org.example.2Player")]
    public async Task InvalidSandboxIdIsRejected(string id)
    {
        var resolver = new FlatpakApplicationResolver(new EmptyDesktopIndex());
        var process = new ProcessMetadata(42, "/app/bin/player", []) { FlatpakId = id };

        Assert.Null(await resolver.TryResolveAsync(
            Session(), process, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplicationResolverChecksFlatpakBeforeSteamAndDesktop()
    {
        var desktopIndex = new XdgDesktopApplicationIndex([DesktopFixtures]);
        var process = new ProcessMetadata(42, "/app/extra/bin/spotify", [])
        {
            FlatpakId = "com.spotify.Client"
        };
        var resolver = new ApplicationResolver(
            new FixedProcessProvider(process), desktopIndex, new UnexpectedSteamResolver(),
            new FlatpakApplicationResolver(desktopIndex));

        ApplicationIdentity identity = await resolver.ResolveAsync(
            Session(), TestContext.Current.CancellationToken);

        Assert.Equal("flatpak:com.spotify.Client", identity.Id.Value);
    }

    [Fact]
    public async Task InvalidFlatpakEvidenceFallsBackToOrdinaryExecutableIdentity()
    {
        var desktopIndex = new EmptyDesktopIndex();
        var process = new ProcessMetadata(42, "/app/bin/player", []) { FlatpakId = "not-an-app-id" };
        var resolver = new ApplicationResolver(
            new FixedProcessProvider(process), desktopIndex, flatpakApplications: new FlatpakApplicationResolver(desktopIndex));

        ApplicationIdentity identity = await resolver.ResolveAsync(
            Session() with { ApplicationId = null }, TestContext.Current.CancellationToken);

        Assert.Equal("exe:/app/bin/player", identity.Id.Value);
        Assert.Null(identity.FlatpakId);
    }

    private static AudioSession Session() => new()
    {
        Id = new("flatpak-fixture"),
        PipeWireNodeId = 71,
        ProcessId = 42,
        ApplicationId = "com.spotify.Client",
        ApplicationName = "Fixture Player",
        ProcessBinary = "spotify",
        Volume = 0.5f,
        Active = true
    };

    private sealed class EmptyDesktopIndex : IDesktopApplicationIndex
    {
        public IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath) => [];
        public IReadOnlyList<DesktopApplicationEntry> FindById(string desktopFileId) => [];
    }

    private sealed class FixedProcessProvider(ProcessMetadata process) : IProcessMetadataProvider
    {
        public ValueTask<ProcessMetadata?> GetAsync(int processId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProcessMetadata?>(process);
    }

    private sealed class UnexpectedSteamResolver : ISteamApplicationResolver
    {
        public ValueTask<ApplicationIdentity?> TryResolveAsync(
            AudioSession session,
            ProcessMetadata? process,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Steam resolution must not run for trusted Flatpak evidence.");
    }
}
