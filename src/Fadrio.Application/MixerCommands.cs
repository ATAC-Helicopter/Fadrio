using Fadrio.Core;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.Application;

public sealed class MixerCommands(IAudioBackend backend, MixerStateCoordinator coordinator)
{
    public async ValueTask SetApplicationVolumeAsync(
        ApplicationId applicationId,
        float volume,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(volume, 0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 1f);
        RuntimeApplication application = Find(applicationId);
        foreach (AudioSession session in application.Sessions)
        {
            await backend.SetStreamVolumeAsync(session.PipeWireNodeId, volume, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask SetApplicationMuteAsync(
        ApplicationId applicationId,
        bool muted,
        CancellationToken cancellationToken = default)
    {
        RuntimeApplication application = Find(applicationId);
        foreach (AudioSession session in application.Sessions)
        {
            await backend.SetStreamMuteAsync(session.PipeWireNodeId, muted, cancellationToken).ConfigureAwait(false);
        }
    }

    private RuntimeApplication Find(ApplicationId id) =>
        coordinator.Current.Applications.Single(application => application.Identity.Id == id);
}
