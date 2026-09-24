using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Infrastructure;

public sealed class PersistentApplicationResolver(
    IApplicationResolver inner,
    ApplicationIdentityStore store) : IApplicationResolver, IApplicationIdentityRevision
{
    public long Revision => inner is IApplicationIdentityRevision revision ? revision.Revision : 0;

    public async ValueTask<ApplicationIdentity> ResolveAsync(
        AudioSession session,
        CancellationToken cancellationToken = default)
    {
        ApplicationIdentity identity = await inner.ResolveAsync(session, cancellationToken).ConfigureAwait(false);
        store.SaveResolvedIdentity(identity);
        return store.ApplyOverride(identity);
    }
}
