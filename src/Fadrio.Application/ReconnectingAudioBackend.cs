using System.Runtime.CompilerServices;
using Fadrio.Core;

namespace Fadrio.Application;

public sealed class ReconnectingAudioBackend(
    Func<IAudioBackend> backendFactory,
    TimeSpan? initialBackoff = null,
    TimeSpan? maximumBackoff = null) : IAudioBackend
{
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly TimeSpan _initialBackoff = initialBackoff ?? TimeSpan.FromMilliseconds(250);
    private readonly TimeSpan _maximumBackoff = maximumBackoff ?? TimeSpan.FromSeconds(30);
    private IAudioBackend? _current;
    private bool _disposed;

    public async IAsyncEnumerable<AudioBackendEvent> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _disposeCancellation.Token);
        CancellationToken token = linkedCancellation.Token;
        long generation = 0;
        TimeSpan backoff = _initialBackoff;

        while (!token.IsCancellationRequested)
        {
            IAudioBackend? backend = null;
            bool becameReady = false;
            string? failure = null;
            generation++;
            try
            {
                backend = backendFactory();
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                failure = exception.Message;
            }

            if (backend is not null)
            {
                Volatile.Write(ref _current, backend);
                try
                {
                    await foreach (AudioBackendEvent backendEvent in ReadBackendEventsAsync(backend, token).ConfigureAwait(false))
                    {
                        AudioBackendEvent remapped = WithGeneration(backendEvent, generation);
                        becameReady |= remapped is BackendReady;
                        yield return remapped;
                        if (remapped is BackendDisconnected)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    if (ReferenceEquals(Volatile.Read(ref _current), backend))
                    {
                        Volatile.Write(ref _current, null);
                    }
                    await backend.DisposeAsync().ConfigureAwait(false);
                }
            }

            if (failure is not null)
            {
                yield return new BackendDisconnected(generation, failure);
            }

            if (becameReady)
            {
                backoff = _initialBackoff;
            }
            await Task.Delay(backoff, token).ConfigureAwait(false);
            backoff = TimeSpan.FromMilliseconds(Math.Min(
                Math.Max(1, backoff.TotalMilliseconds * 2),
                _maximumBackoff.TotalMilliseconds));
        }
    }

    private static async IAsyncEnumerable<AudioBackendEvent> ReadBackendEventsAsync(
        IAudioBackend backend,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using IAsyncEnumerator<AudioBackendEvent> enumerator =
            backend.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            AudioBackendEvent? backendEvent = null;
            string? failure = null;
            bool hasEvent = false;
            bool cancelled = false;
            try
            {
                hasEvent = await enumerator.MoveNextAsync().ConfigureAwait(false);
                if (hasEvent)
                {
                    backendEvent = enumerator.Current;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                failure = exception.Message;
            }

            if (cancelled)
            {
                yield break;
            }
            if (failure is not null)
            {
                yield return new BackendDisconnected(0, failure);
                yield break;
            }
            if (!hasEvent)
            {
                yield break;
            }
            yield return backendEvent!;
        }
    }

    public ValueTask SetStreamVolumeAsync(uint nodeId, float volume, CancellationToken cancellationToken = default) =>
        CurrentBackend().SetStreamVolumeAsync(nodeId, volume, cancellationToken);

    public ValueTask SetStreamMuteAsync(uint nodeId, bool muted, CancellationToken cancellationToken = default) =>
        CurrentBackend().SetStreamMuteAsync(nodeId, muted, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await _disposeCancellation.CancelAsync().ConfigureAwait(false);
        IAudioBackend? backend = Volatile.Read(ref _current);
        if (backend is not null)
        {
            await backend.DisposeAsync().ConfigureAwait(false);
        }
        _disposeCancellation.Dispose();
    }

    private IAudioBackend CurrentBackend() =>
        Volatile.Read(ref _current) ?? throw new InvalidOperationException("The audio backend is reconnecting.");

    private static AudioBackendEvent WithGeneration(AudioBackendEvent backendEvent, long generation) => backendEvent switch
    {
        BackendReady => new BackendReady(generation),
        BackendDisconnected disconnected => new BackendDisconnected(generation, disconnected.Reason),
        SessionAdded added => new SessionAdded(generation, WithGeneration(added.Session, generation)),
        SessionChanged changed => new SessionChanged(generation, WithGeneration(changed.Session, generation)),
        SessionRemoved removed => new SessionRemoved(generation, WithGeneration(removed.SessionId, generation)),
        _ => throw new ArgumentOutOfRangeException(nameof(backendEvent), backendEvent, "Unknown backend event.")
    };

    private static AudioSession WithGeneration(AudioSession session, long generation) =>
        session with { Id = WithGeneration(session.Id, generation) };

    private static AudioSessionId WithGeneration(AudioSessionId sessionId, long generation)
    {
        string value = sessionId.Value;
        int finalSeparator = value.LastIndexOf(':');
        string stableSuffix = finalSeparator >= 0 ? value[(finalSeparator + 1)..] : value;
        return new($"backend:{generation}:{stableSuffix}");
    }
}
