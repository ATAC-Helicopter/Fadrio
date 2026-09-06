namespace Fadrio.Core;

public abstract record AudioBackendEvent(long Generation);
public sealed record BackendReady(long Generation) : AudioBackendEvent(Generation);
public sealed record BackendDisconnected(long Generation, string? Reason) : AudioBackendEvent(Generation);
public sealed record SessionAdded(long Generation, AudioSession Session) : AudioBackendEvent(Generation);
public sealed record SessionChanged(long Generation, AudioSession Session) : AudioBackendEvent(Generation);
public sealed record SessionRemoved(long Generation, AudioSessionId SessionId) : AudioBackendEvent(Generation);
