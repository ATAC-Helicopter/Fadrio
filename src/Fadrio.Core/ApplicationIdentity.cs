namespace Fadrio.Core;

public enum IdentityConfidence
{
    Unknown,
    Low,
    Medium,
    High,
    Exact
}

public enum IdentityEvidenceKind
{
    PipeWireApplicationId,
    PipeWireApplicationName,
    PipeWireProcessBinary,
    ProcessExecutable,
    DesktopEntry,
    AmbiguousDesktopEntry,
    ConflictingEvidence,
    Fallback,
    ProcessEnvironment,
    InstallationMetadata
}

public sealed record IdentityEvidence(
    IdentityEvidenceKind Kind,
    string Value,
    string Description);

public sealed record IconReference(string Value);

public sealed record ApplicationIdentity
{
    public required ApplicationId Id { get; init; }
    public required string DisplayName { get; init; }
    public string? DesktopFileId { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ExecutableName { get; init; }
    public string? FlatpakId { get; init; }
    public string? SnapId { get; init; }
    public string? SteamAppId { get; init; }
    public string? WineExecutable { get; init; }
    public IconReference? Icon { get; init; }
    public IdentityConfidence Confidence { get; init; }
    public IReadOnlyList<IdentityEvidence> Evidence { get; init; } = [];
}
