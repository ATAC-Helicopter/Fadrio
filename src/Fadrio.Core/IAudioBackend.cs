namespace Fadrio.Core;

public interface IAudioBackend : IAsyncDisposable
{
    IAsyncEnumerable<AudioBackendEvent> WatchAsync(CancellationToken cancellationToken = default);
    ValueTask SetStreamVolumeAsync(uint nodeId, float volume, CancellationToken cancellationToken = default);
    ValueTask SetStreamMuteAsync(uint nodeId, bool muted, CancellationToken cancellationToken = default);
}
