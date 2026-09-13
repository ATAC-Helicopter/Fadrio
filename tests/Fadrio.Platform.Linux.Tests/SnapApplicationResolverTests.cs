using Fadrio.Application;
using Fadrio.Core;
using Fadrio.Infrastructure;

namespace Fadrio.Platform.Linux.Tests;

public sealed class SnapApplicationResolverTests
{
    private static string DesktopFixtures => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures/desktop-files"));

    [Fact]
    public async Task CorroboratedSnapEnvironmentProducesCanonicalIdentity()
    {
        var resolver = new SnapApplicationResolver(new XdgDesktopApplicationIndex([DesktopFixtures]));

        ApplicationIdentity? identity = await resolver.TryResolveAsync(
            Session(), Process(), TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Equal("snap:fixture-player", identity.Id.Value);
        Assert.Equal("fixture-player", identity.SnapId);
        Assert.Equal("Fixture Player Snap", identity.DisplayName);
        Assert.Equal("fixture-player_fixture-player", identity.DesktopFileId);
        Assert.Equal(IdentityConfidence.Exact, identity.Confidence);
        Assert.Contains(identity.Evidence, item => item.Kind == IdentityEvidenceKind.SnapPackageName);
    }

    [Fact]
    public async Task MissingOrContradictorySnapRootFallsBack()
    {
        var snap = new SnapApplicationResolver(new EmptyDesktopIndex());
        ProcessMetadata process = Process() with
        {
            IdentityEnvironment = new Dictionary<string, string> { ["SNAP"] = "/snap/other/current" }
        };
        var resolver = new ApplicationResolver(
            new FixedProcessProvider(process), new EmptyDesktopIndex(), snapApplications: snap);

        Assert.Null(await snap.TryResolveAsync(Session(), process, TestContext.Current.CancellationToken));
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(), TestContext.Current.CancellationToken);
        Assert.Equal("exe:/snap/fixture-player/current/bin/player", identity.Id.Value);
    }

    [Fact]
    public async Task NamedInstanceIsStableWithoutRequiringSnapInstallation()
    {
        ProcessMetadata process = Process() with { SnapInstanceName = "fixture-player_work" };
        var resolver = new SnapApplicationResolver(new EmptyDesktopIndex());

        ApplicationIdentity? identity = await resolver.TryResolveAsync(
            Session(), process, TestContext.Current.CancellationToken);

        Assert.Equal("snap:fixture-player_work", identity!.Id.Value);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
    }

    private static ProcessMetadata Process() => new(42, "/snap/fixture-player/current/bin/player", [])
    {
        SnapName = "fixture-player",
        SnapInstanceName = "fixture-player",
        IdentityEnvironment = new Dictionary<string, string> { ["SNAP"] = "/snap/fixture-player/current" }
    };

    private static AudioSession Session() => new()
    {
        Id = new("snap-fixture"),
        PipeWireNodeId = 72,
        ProcessId = 42,
        ApplicationName = "Fixture",
        ProcessBinary = "player",
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
}
