using System.Runtime.CompilerServices;
using Fadrio.Core;

namespace Fadrio.Application.Tests;

public sealed class MixerCommandsTests
{
    [Fact]
    public async Task VolumeAndMuteFanOutToEveryOwnedSessionOnly()
    {
        var backend = new RecordingBackend();
        var coordinator = new MixerStateCoordinator(new Resolver());
        await coordinator.ApplyAsync(new SessionAdded(1, Session("firefox-one", 10, "Firefox")), TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(new SessionAdded(1, Session("firefox-two", 11, "Firefox")), TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(new SessionAdded(1, Session("discord", 20, "Discord")), TestContext.Current.CancellationToken);
        var commands = new MixerCommands(backend, coordinator);

        await commands.SetApplicationVolumeAsync(new("app:firefox"), 0.35f, TestContext.Current.CancellationToken);
        await commands.SetApplicationMuteAsync(new("app:firefox"), true, TestContext.Current.CancellationToken);

        Assert.Equal([(10u, 0.35f), (11u, 0.35f)], backend.VolumeCalls);
        Assert.Equal([(10u, true), (11u, true)], backend.MuteCalls);
    }

    private static AudioSession Session(string id, uint nodeId, string name) => new()
    {
        Id = new(id),
        PipeWireNodeId = nodeId,
        ApplicationName = name,
        Volume = 1f,
        Active = true
    };

    private sealed class Resolver : IApplicationResolver
    {
        public ValueTask<ApplicationIdentity> ResolveAsync(AudioSession session, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ApplicationIdentity
            {
                Id = new($"app:{session.ApplicationName!.ToLowerInvariant()}"),
                DisplayName = session.ApplicationName,
                Evidence = []
            });
    }

    private sealed class RecordingBackend : IAudioBackend
    {
        public List<(uint NodeId, float Volume)> VolumeCalls { get; } = [];
        public List<(uint NodeId, bool Muted)> MuteCalls { get; } = [];

        public async IAsyncEnumerable<AudioBackendEvent> WatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }

        public ValueTask SetStreamVolumeAsync(uint nodeId, float volume, CancellationToken cancellationToken = default)
        {
            VolumeCalls.Add((nodeId, volume));
            return ValueTask.CompletedTask;
        }

        public ValueTask SetStreamMuteAsync(uint nodeId, bool muted, CancellationToken cancellationToken = default)
        {
            MuteCalls.Add((nodeId, muted));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
