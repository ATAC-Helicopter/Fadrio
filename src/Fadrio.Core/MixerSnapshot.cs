namespace Fadrio.Core;

public sealed record MixerSnapshot
{
    public static MixerSnapshot Empty { get; } = new([], 0);

    public MixerSnapshot(IEnumerable<RuntimeApplication> applications, long revision)
    {
        Applications = applications?.ToArray()
            ?? throw new ArgumentNullException(nameof(applications));
        Revision = revision;
    }

    public IReadOnlyList<RuntimeApplication> Applications { get; }
    public long Revision { get; }
}
