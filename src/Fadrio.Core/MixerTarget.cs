namespace Fadrio.Core;

public abstract record MixerTarget;
public sealed record ApplicationTarget(ApplicationId Id) : MixerTarget;
public sealed record MasterTarget(DeviceId? Device) : MixerTarget;
public sealed record DynamicRoleTarget(DynamicRole Role) : MixerTarget;

public enum DynamicRole
{
    CurrentGame,
    Communications,
    Music,
    Browser,
    SystemSounds,
    Master
}
