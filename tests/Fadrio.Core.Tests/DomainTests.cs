using Fadrio.Core;

namespace Fadrio.Core.Tests;

public sealed class DomainTests
{
    [Fact]
    public void ApplicationId_UsesValueEquality()
    {
        Assert.Equal(new ApplicationId("xdg:firefox"), new ApplicationId("xdg:firefox"));
        Assert.NotEqual(new ApplicationId("xdg:firefox"), new ApplicationId("xdg:discord"));
    }

    [Fact]
    public void RuntimeApplication_AggregatesMultipleSessionsAndDetectsMixedVolume()
    {
        RuntimeApplication application = new(Identity("xdg:firefox"),
        [
            Session("one", 0.4f, muted: false),
            Session("two", 0.6f, muted: true)
        ]);

        Assert.Equal(2, application.Sessions.Count);
        Assert.True(application.IsMixedVolume);
        Assert.Equal(0.5f, application.EffectiveVolume, 3);
        Assert.False(application.IsMuted);
    }

    [Fact]
    public void MixerSnapshot_CopiesInputCollection()
    {
        var source = new List<RuntimeApplication> { new(Identity("xdg:firefox"), []) };
        var snapshot = new MixerSnapshot(source, 1);
        source.Clear();
        Assert.Single(snapshot.Applications);
    }

    private static ApplicationIdentity Identity(string id) => new()
    {
        Id = new(id),
        DisplayName = id,
        Evidence = []
    };

    private static AudioSession Session(string id, float volume, bool muted) => new()
    {
        Id = new(id),
        PipeWireNodeId = 1,
        Volume = volume,
        Muted = muted,
        Active = true
    };
}
