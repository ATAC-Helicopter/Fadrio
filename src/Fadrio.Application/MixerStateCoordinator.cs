using Fadrio.Core;

namespace Fadrio.Application;

public sealed class MixerStateCoordinator(IApplicationResolver resolver)
{
    private readonly Dictionary<AudioSessionId, AudioSession> _sessions = [];
    private readonly Dictionary<AudioSessionId, ApplicationIdentity> _identities = [];
    private long _generation = -1;
    private long _revision;

    public MixerSnapshot Current { get; private set; } = MixerSnapshot.Empty;
    public event EventHandler<MixerSnapshot>? SnapshotChanged;

    public async ValueTask ApplyAsync(AudioBackendEvent backendEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backendEvent);
        if (backendEvent.Generation < _generation)
        {
            return;
        }

        if (backendEvent.Generation > _generation)
        {
            _generation = backendEvent.Generation;
            _sessions.Clear();
            _identities.Clear();
        }

        switch (backendEvent)
        {
            case SessionAdded added:
                await AddOrUpdateAsync(added.Session, cancellationToken).ConfigureAwait(false);
                break;
            case SessionChanged changed:
                await AddOrUpdateAsync(changed.Session, cancellationToken).ConfigureAwait(false);
                break;
            case SessionRemoved removed:
                _sessions.Remove(removed.SessionId);
                _identities.Remove(removed.SessionId);
                break;
            case BackendDisconnected:
                _sessions.Clear();
                _identities.Clear();
                break;
            case BackendReady:
                break;
        }

        Publish();
    }

    public async Task RunAsync(IAsyncEnumerable<AudioBackendEvent> events, CancellationToken cancellationToken = default)
    {
        await foreach (AudioBackendEvent backendEvent in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            await ApplyAsync(backendEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask AddOrUpdateAsync(AudioSession session, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(session.Id, out AudioSession? previous) &&
            !IdentityInputsChanged(previous, session) && _identities.ContainsKey(session.Id))
        {
            _sessions[session.Id] = session;
            return;
        }

        _sessions[session.Id] = session;
        _identities[session.Id] = await resolver.ResolveAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private static bool IdentityInputsChanged(AudioSession previous, AudioSession current) =>
        previous.ProcessId != current.ProcessId ||
        previous.ApplicationName != current.ApplicationName ||
        previous.ApplicationId != current.ApplicationId ||
        previous.ApplicationIconName != current.ApplicationIconName ||
        previous.ProcessBinary != current.ProcessBinary ||
        previous.MediaName != current.MediaName ||
        previous.MediaRole != current.MediaRole;

    private void Publish()
    {
        RuntimeApplication[] applications = _sessions
            .Join(_identities, session => session.Key, identity => identity.Key,
                (session, identity) => (Session: session.Value, Identity: identity.Value))
            .GroupBy(item => item.Identity.Id)
            .Select(group => new RuntimeApplication(group.First().Identity, group.Select(item => item.Session)))
            .OrderBy(application => application.Identity.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Current = new MixerSnapshot(applications, ++_revision);
        SnapshotChanged?.Invoke(this, Current);
    }
}
