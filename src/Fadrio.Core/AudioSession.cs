namespace Fadrio.Core;

public sealed record AudioSession
{
    public required AudioSessionId Id { get; init; }
    public required uint PipeWireNodeId { get; init; }
    public int? ProcessId { get; init; }
    public string? ApplicationName { get; init; }
    public string? ApplicationId { get; init; }
    public string? ApplicationIconName { get; init; }
    public string? ProcessBinary { get; init; }
    public string? MediaName { get; init; }
    public string? MediaRole { get; init; }
    public float Volume { get; init; } = 1f;
    public bool Muted { get; init; }
    public bool Active { get; init; }
    public DeviceId? OutputDevice { get; init; }
}
