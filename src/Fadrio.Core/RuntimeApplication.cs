namespace Fadrio.Core;

public sealed record RuntimeApplication
{
    public RuntimeApplication(ApplicationIdentity identity, IEnumerable<AudioSession> sessions)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Sessions = sessions?.OrderBy(session => session.Id.Value, StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(sessions));

        IsRunning = Sessions.Count > 0;
        IsAudible = Sessions.Any(session => session.Active && !session.Muted && session.Volume > 0f);
        IsMuted = Sessions.Count > 0 && Sessions.All(session => session.Muted);
        IsMixedVolume = Sessions.Select(session => session.Volume).Distinct().Skip(1).Any();
        EffectiveVolume = Sessions.Count == 0 ? 0f : Sessions.Average(session => session.Volume);
    }

    public ApplicationIdentity Identity { get; }
    public IReadOnlyList<AudioSession> Sessions { get; }
    public bool IsRunning { get; }
    public bool IsAudible { get; }
    public float EffectiveVolume { get; }
    public bool IsMuted { get; }
    public bool IsMixedVolume { get; }
}
