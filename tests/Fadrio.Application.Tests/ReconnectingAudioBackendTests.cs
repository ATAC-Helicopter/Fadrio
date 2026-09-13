using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Fadrio.Core;

namespace Fadrio.Application.Tests;

public sealed class ReconnectingAudioBackendTests
{
    [Fact]
    public async Task ReconnectsWithANewGenerationAfterDisconnect()
    {
        int created = 0;
        await using var backend = new ReconnectingAudioBackend(
            () => new FixtureBackend(++created), TimeSpan.Zero, TimeSpan.Zero);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var events = new List<AudioBackendEvent>();

        await foreach (AudioBackendEvent backendEvent in backend.WatchAsync(timeout.Token))
        {
            events.Add(backendEvent);
            if (events.OfType<SessionAdded>().Count() == 2)
            {
                break;
            }
        }

        Assert.Equal([1L, 2L], events.OfType<BackendReady>().Select(item => item.Generation));
        Assert.Contains(events, item => item is BackendDisconnected { Generation: 1 });
        Assert.Equal(
            [new AudioSessionId("backend:1:81"), new AudioSessionId("backend:2:81")],
            events.OfType<SessionAdded>().Select(item => item.Session.Id));
        Assert.Equal(2, created);
    }

    [Fact]
    public async Task CommandsUseTheRebuiltBackendAfterReconnect()
    {
        var created = new List<FixtureBackend>();
        await using var backend = new ReconnectingAudioBackend(
            () =>
            {
                var fixture = new FixtureBackend(created.Count + 1);
                created.Add(fixture);
                return fixture;
            }, TimeSpan.Zero, TimeSpan.Zero);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(2));
        var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (AudioBackendEvent backendEvent in backend.WatchAsync(cancellation.Token))
                {
                    if (backendEvent is BackendReady { Generation: 2 })
                    {
                        secondReady.TrySetResult();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
        }, TestContext.Current.CancellationToken);

        await secondReady.Task.WaitAsync(cancellation.Token);
        await backend.SetStreamVolumeAsync(81, 0.45f, cancellation.Token);
        await backend.SetStreamMuteAsync(81, true, cancellation.Token);

        FixtureBackend rebuilt = Assert.IsType<FixtureBackend>(created[1]);
        Assert.Equal(["volume:81:0.45", "mute:81:true"], rebuilt.Commands);
        Assert.Empty(created[0].Commands);

        await cancellation.CancelAsync();
        await consumer;
    }

    private sealed class FixtureBackend(int instance) : IAudioBackend
    {
        public ConcurrentQueue<string> Commands { get; } = [];

        public async IAsyncEnumerable<AudioBackendEvent> WatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new BackendReady(99);
            yield return new SessionAdded(99, new AudioSession
            {
                Id = new("pipewire:99:81"),
                PipeWireNodeId = 81,
                Volume = 1f,
                Active = true
            });
            if (instance == 1)
            {
                yield return new BackendDisconnected(99, "fixture disconnect");
            }
            else
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }

        public ValueTask SetStreamVolumeAsync(uint nodeId, float volume, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue($"volume:{nodeId}:{volume.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            return ValueTask.CompletedTask;
        }

        public ValueTask SetStreamMuteAsync(uint nodeId, bool muted, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue($"mute:{nodeId}:{muted.ToString().ToLowerInvariant()}");
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
