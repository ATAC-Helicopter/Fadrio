using Fadrio.Core;

namespace Fadrio.NativeInterop;

public enum NativeEventType
{
    Ready = 1,
    Disconnected = 2,
    SessionAdded = 3,
    SessionChanged = 4,
    SessionRemoved = 5
}

public sealed record NativeEventData(
    NativeEventType Type,
    long Generation,
    uint NodeId = 0,
    int ProcessId = -1,
    float Volume = 1f,
    bool Muted = false,
    bool Active = false,
    string? ApplicationName = null,
    string? ApplicationId = null,
    string? ApplicationIconName = null,
    string? ProcessBinary = null,
    string? MediaName = null,
    string? MediaRole = null,
    string? Message = null);

public static class NativeEventTranslator
{
    public static AudioBackendEvent Translate(NativeEventData nativeEvent)
    {
        ArgumentNullException.ThrowIfNull(nativeEvent);
        return nativeEvent.Type switch
        {
            NativeEventType.Ready => new BackendReady(nativeEvent.Generation),
            NativeEventType.Disconnected => new BackendDisconnected(nativeEvent.Generation, nativeEvent.Message),
            NativeEventType.SessionAdded => new SessionAdded(nativeEvent.Generation, CreateSession(nativeEvent)),
            NativeEventType.SessionChanged => new SessionChanged(nativeEvent.Generation, CreateSession(nativeEvent)),
            NativeEventType.SessionRemoved => new SessionRemoved(nativeEvent.Generation, SessionId(nativeEvent)),
            _ => throw new ArgumentOutOfRangeException(nameof(nativeEvent), nativeEvent.Type, "Unknown native event type.")
        };
    }

    private static AudioSession CreateSession(NativeEventData data) => new()
    {
        Id = SessionId(data),
        PipeWireNodeId = data.NodeId,
        ProcessId = data.ProcessId > 0 ? data.ProcessId : null,
        ApplicationName = data.ApplicationName,
        ApplicationId = data.ApplicationId,
        ApplicationIconName = data.ApplicationIconName,
        ProcessBinary = data.ProcessBinary,
        MediaName = data.MediaName,
        MediaRole = data.MediaRole,
        Volume = Math.Clamp(data.Volume, 0f, 1f),
        Muted = data.Muted,
        Active = data.Active
    };

    private static AudioSessionId SessionId(NativeEventData data) => new($"pipewire:{data.Generation}:{data.NodeId}");
}
