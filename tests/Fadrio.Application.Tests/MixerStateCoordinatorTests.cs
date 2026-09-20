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

    [Fact]
    public async Task ReResolvesExistingSessionsOnceWhenIdentityRevisionChanges()
    {
        var resolver = new RevisableResolver();
        var coordinator = new MixerStateCoordinator(resolver);
        await coordinator.ApplyAsync(new SessionAdded(1, Session("one", 10, 0.3f)), TestContext.Current.CancellationToken);

        resolver.Advance("xdg:renamed", "Renamed");
        await coordinator.ApplyAsync(new SessionChanged(1, Session("one", 10, 0.5f)), TestContext.Current.CancellationToken);

        Assert.Equal(2, resolver.ResolveCount);
        Assert.Equal("xdg:renamed", Assert.Single(coordinator.Current.Applications).Identity.Id.Value);

        await coordinator.ApplyAsync(new SessionChanged(1, Session("one", 10, 0.7f)), TestContext.Current.CancellationToken);
        Assert.Equal(2, resolver.ResolveCount);
    }

    [Fact]
    public async Task NewGenerationUsesCurrentIdentityRevisionWithoutRedundantRefresh()
    {
        var resolver = new RevisableResolver();
        var coordinator = new MixerStateCoordinator(resolver);
        resolver.Advance("xdg:current", "Current");

        await coordinator.ApplyAsync(new SessionAdded(1, Session("one", 10, 0.3f)), TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(new SessionChanged(1, Session("one", 10, 0.5f)), TestContext.Current.CancellationToken);

        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal("xdg:current", Assert.Single(coordinator.Current.Applications).Identity.Id.Value);
    }

    [Fact]
    public async Task FailedIdentityRefreshIsRetriedWithoutPublishingPartialResults()
    {
        var resolver = new RevisableResolver();
        var coordinator = new MixerStateCoordinator(resolver);
        await coordinator.ApplyAsync(new SessionAdded(1, Session("one", 10, 0.3f)), TestContext.Current.CancellationToken);
        resolver.Advance("xdg:recovered", "Recovered");
        resolver.FailNextResolution = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ApplyAsync(
            new SessionChanged(1, Session("one", 10, 0.5f)),
            TestContext.Current.CancellationToken).AsTask());
        Assert.Equal("xdg:initial", Assert.Single(coordinator.Current.Applications).Identity.Id.Value);

        await coordinator.ApplyAsync(new SessionChanged(1, Session("one", 10, 0.5f)), TestContext.Current.CancellationToken);
        Assert.Equal("xdg:recovered", Assert.Single(coordinator.Current.Applications).Identity.Id.Value);
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

    private sealed class RevisableResolver : IApplicationResolver, IApplicationIdentityRevision
    {
        private string _id = "xdg:initial";
        private string _name = "Initial";

        public long Revision { get; private set; }
        public int ResolveCount { get; private set; }
        public bool FailNextResolution { get; set; }

        public void Advance(string id, string name)
        {
            _id = id;
            _name = name;
            Revision++;
        }

        public ValueTask<ApplicationIdentity> ResolveAsync(
            AudioSession session,
            CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            if (FailNextResolution)
            {
                FailNextResolution = false;
                throw new InvalidOperationException("Fixture resolution failure.");
            }

            return ValueTask.FromResult(new ApplicationIdentity
            {
                Id = new(_id),
                DisplayName = _name,
                Confidence = IdentityConfidence.High,
                Evidence = []
            });
        }
    }
}
