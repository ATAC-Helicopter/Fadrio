using Fadrio.Core;

namespace Fadrio.Application.Tests;

public sealed class MixerStateCoordinatorTests
{
    [Fact]
    public async Task GroupsSessionsByCanonicalApplicationAndHandlesRemoval()
    {
        var coordinator = new MixerStateCoordinator(new FixedResolver());
        await coordinator.ApplyAsync(new SessionAdded(1, Session("one", 10, 0.3f)), TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(new SessionAdded(1, Session("two", 11, 0.7f)), TestContext.Current.CancellationToken);

        RuntimeApplication application = Assert.Single(coordinator.Current.Applications);
        Assert.Equal(2, application.Sessions.Count);
        Assert.True(application.IsMixedVolume);

        await coordinator.ApplyAsync(new SessionRemoved(1, new("one")), TestContext.Current.CancellationToken);
        Assert.Single(Assert.Single(coordinator.Current.Applications).Sessions);
    }

    [Fact]
    public async Task IgnoresEventsFromAnOlderBackendGeneration()
    {
        var coordinator = new MixerStateCoordinator(new FixedResolver());
        await coordinator.ApplyAsync(new SessionAdded(2, Session("new", 20, 1f)), TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(new SessionAdded(1, Session("stale", 10, 1f)), TestContext.Current.CancellationToken);
        Assert.Equal("new", Assert.Single(Assert.Single(coordinator.Current.Applications).Sessions).Id.Value);
    }

    [Fact]
    public async Task DisconnectPublishesAnUnavailableEmptySnapshot()
    {
        var coordinator = new MixerStateCoordinator(new FixedResolver());
        await coordinator.ApplyAsync(new SessionAdded(1, Session("connected", 20, 1f)), TestContext.Current.CancellationToken);

        await coordinator.ApplyAsync(new BackendDisconnected(1, "fixture restart"), TestContext.Current.CancellationToken);

        Assert.Empty(coordinator.Current.Applications);
        Assert.Equal(2, coordinator.Current.Revision);
    }

    private static AudioSession Session(string id, uint nodeId, float volume) => new()
    {
        Id = new(id),
        PipeWireNodeId = nodeId,
        ApplicationName = "Firefox",
        Volume = volume,
        Active = true
    };

    private sealed class FixedResolver : IApplicationResolver
    {
        public ValueTask<ApplicationIdentity> ResolveAsync(AudioSession session, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ApplicationIdentity
            {
                Id = new("xdg:firefox"),
                DisplayName = "Firefox",
                Confidence = IdentityConfidence.High,
                Evidence = []
            });
    }
}
