using System.Security.Cryptography;
using System.Text;
using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Platform.Linux;

public sealed class ApplicationResolver(
    IProcessMetadataProvider processes,
    IDesktopApplicationIndex desktopApplications,
    ISteamApplicationResolver? steamApplications = null) : IApplicationResolver
{
    private const int MinimumDesktopScore = 50;
    private const int StrongCandidateScore = 60;
    private const int SafeWinningMargin = 40;

    public async ValueTask<ApplicationIdentity> ResolveAsync(
        AudioSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProcessMetadata? process = session.ProcessId is { } processId
            ? await processes.GetAsync(processId, cancellationToken).ConfigureAwait(false)
            : null;

        if (steamApplications is not null &&
            await steamApplications.TryResolveAsync(session, process, cancellationToken).ConfigureAwait(false) is { } steamIdentity)
        {
            return steamIdentity;
        }

        var evidence = BuildPipeWireEvidence(session);
        string? executablePath = NullIfWhiteSpace(process?.ExecutablePath);
        if (executablePath is not null)
        {
            evidence.Add(new(IdentityEvidenceKind.ProcessExecutable, executablePath, "Resolved from /proc/<pid>/exe."));
        }

        string? binary = NullIfWhiteSpace(session.ProcessBinary);
        DesktopResolution? desktop = ResolveDesktopIdentity(session, executablePath, binary, evidence);
        if (desktop is not null)
        {
            return DesktopIdentity(session, process, desktop, evidence);
        }

        if (executablePath is not null)
        {
            return ExecutableIdentity(session, executablePath, evidence);
        }

        string? applicationId = NullIfWhiteSpace(session.ApplicationId);
        if (applicationId is not null)
        {
            return new ApplicationIdentity
            {
                Id = new($"pipewire:{NormalizeId(applicationId)}"),
                DisplayName = FirstNonEmpty(session.ApplicationName, binary is null ? null : Path.GetFileName(binary),
                    session.MediaName, "Unknown audio application"),
                ExecutableName = binary is null ? null : Path.GetFileName(binary),
                Icon = IconFromPipeWire(session),
                Confidence = IdentityConfidence.Medium,
                Evidence = evidence
            };
        }

        if (binary is not null)
        {
            return ExecutableIdentity(session, binary, evidence);
        }

        string displayName = FirstNonEmpty(session.ApplicationName, session.MediaName, "Unknown audio application");
        string stableEvidence = FirstNonEmpty(session.ApplicationName, session.MediaName, session.MediaRole,
            "unknown-audio-application");
        evidence.Add(new(IdentityEvidenceKind.Fallback, stableEvidence,
            "No stable executable or desktop identity was available."));
        return new ApplicationIdentity
        {
            Id = new($"unknown:{StableHash(stableEvidence)}"),
            DisplayName = displayName,
            Icon = IconFromPipeWire(session),
            Confidence = IdentityConfidence.Unknown,
            Evidence = evidence
        };
    }

    private DesktopResolution? ResolveDesktopIdentity(
        AudioSession session,
        string? executablePath,
        string? processBinary,
        List<IdentityEvidence> evidence)
    {
        var candidates = new Dictionary<string, DesktopCandidate>(StringComparer.OrdinalIgnoreCase);

        if (executablePath is not null)
        {
            AddExecutableMatches(
                desktopApplications.FindByExecutable(executablePath),
                executablePath,
                exactScore: 100,
                basenameScore: 70,
                "/proc executable");
        }

        if (processBinary is not null)
        {
            AddExecutableMatches(
                desktopApplications.FindByExecutable(processBinary),
                processBinary,
                exactScore: 80,
                basenameScore: 55,
                "PipeWire process binary");
        }

        string? applicationId = NullIfWhiteSpace(session.ApplicationId);
        if (applicationId is not null)
        {
            AddIdMatches(
                desktopApplications.FindById(applicationId),
                executablePath is null && processBinary is null ? 60 : 85,
                "PipeWire application ID");
        }

        string? iconName = NullIfWhiteSpace(session.ApplicationIconName);
        if (iconName is not null && (executablePath is not null || processBinary is not null))
        {
            AddIdMatches(desktopApplications.FindById(iconName), 70, "PipeWire icon name");
        }

        DesktopCandidate[] ranked = candidates.Values
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ranked.Length == 0 || ranked[0].Score < MinimumDesktopScore)
        {
            return null;
        }

        DesktopCandidate winner = ranked[0];
        if (ranked.Length > 1 && ranked[1].Score >= StrongCandidateScore &&
            winner.Score - ranked[1].Score < SafeWinningMargin)
        {
            string conflict = string.Join(", ", ranked
                .Where(candidate => candidate.Score >= StrongCandidateScore)
                .Select(candidate => $"{candidate.Entry.Id}={candidate.Score}"));
            evidence.Add(new(IdentityEvidenceKind.ConflictingEvidence, conflict,
                "Strong identity evidence selected different desktop applications; no desktop identity was guessed."));
            return null;
        }

        return new DesktopResolution(
            winner.Entry,
            winner.Score >= 70 ? IdentityConfidence.High : IdentityConfidence.Medium);

        void AddExecutableMatches(
            IReadOnlyList<DesktopApplicationEntry> matches,
            string executable,
            int exactScore,
            int basenameScore,
            string source)
        {
            AddAmbiguity(matches, $"{source} matched multiple XDG desktop entries");
            foreach (DesktopApplicationEntry match in Ordered(matches))
            {
                bool exact = PathsEqual(match.Executable, executable);
                AddCandidate(match, exact ? exactScore : basenameScore,
                    $"{source} {(exact ? "exactly" : "by executable name")} matched an installed XDG desktop entry.");
            }
        }

        void AddIdMatches(IReadOnlyList<DesktopApplicationEntry> matches, int score, string source)
        {
            AddAmbiguity(matches, $"{source} matched multiple XDG desktop entries");
            foreach (DesktopApplicationEntry match in Ordered(matches))
            {
                AddCandidate(match, score, $"{source} exactly matched an installed XDG desktop entry.");
            }
        }

        void AddAmbiguity(IReadOnlyList<DesktopApplicationEntry> matches, string description)
        {
            if (matches.Count > 1)
            {
                evidence.Add(new(IdentityEvidenceKind.AmbiguousDesktopEntry,
                    string.Join(',', Ordered(matches).Select(match => match.Id)), $"{description}; candidates were scored."));
            }
        }

        void AddCandidate(DesktopApplicationEntry entry, int score, string description)
        {
            if (!candidates.TryGetValue(entry.Id, out DesktopCandidate? candidate))
            {
                candidate = new(entry);
                candidates.Add(entry.Id, candidate);
            }

            candidate.Score += score;
            evidence.Add(new(IdentityEvidenceKind.DesktopEntry, entry.Id, $"{description} Score +{score}."));
        }
    }

    private static IEnumerable<DesktopApplicationEntry> Ordered(IEnumerable<DesktopApplicationEntry> entries) =>
        entries.OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase);

    private static ApplicationIdentity DesktopIdentity(
        AudioSession session,
        ProcessMetadata? process,
        DesktopResolution resolution,
        IReadOnlyList<IdentityEvidence> evidence)
    {
        DesktopApplicationEntry desktop = resolution.Entry;
        string? executablePath = NullIfWhiteSpace(process?.ExecutablePath);
        string? executableName = executablePath is null
            ? NullIfWhiteSpace(session.ProcessBinary) is { } binary ? Path.GetFileName(binary) : null
            : Path.GetFileName(executablePath);
        return new ApplicationIdentity
        {
            Id = new($"xdg:{desktop.Id}"),
            DisplayName = desktop.Name,
            DesktopFileId = desktop.Id,
            ExecutablePath = executablePath,
            ExecutableName = executableName,
            Icon = desktop.Icon is null ? IconFromPipeWire(session) : new(desktop.Icon),
            Confidence = resolution.Confidence,
            Evidence = evidence
        };
    }

    private static ApplicationIdentity ExecutableIdentity(
        AudioSession session,
        string executablePath,
        IReadOnlyList<IdentityEvidence> evidence) => new()
        {
            Id = new($"exe:{executablePath}"),
            DisplayName = FirstNonEmpty(Path.GetFileName(executablePath), session.ApplicationName, session.MediaName,
                "Unknown audio application"),
            ExecutablePath = Path.IsPathRooted(executablePath) ? executablePath : null,
            ExecutableName = Path.GetFileName(executablePath),
            Icon = IconFromPipeWire(session),
            Confidence = Path.IsPathRooted(executablePath) ? IdentityConfidence.Medium : IdentityConfidence.Low,
            Evidence = evidence
        };

    private static List<IdentityEvidence> BuildPipeWireEvidence(AudioSession session)
    {
        var evidence = new List<IdentityEvidence>();
        Add(IdentityEvidenceKind.PipeWireApplicationId, session.ApplicationId, "PipeWire application.id metadata.");
        Add(IdentityEvidenceKind.PipeWireApplicationName, session.ApplicationName, "PipeWire application.name metadata.");
        Add(IdentityEvidenceKind.PipeWireProcessBinary, session.ProcessBinary,
            "PipeWire application.process.binary metadata.");
        return evidence;

        void Add(IdentityEvidenceKind kind, string? value, string description)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                evidence.Add(new(kind, value, description));
            }
        }
    }

    private static IconReference? IconFromPipeWire(AudioSession session) =>
        NullIfWhiteSpace(session.ApplicationIconName) is { } icon ? new(icon) : null;

    private static bool PathsEqual(string? left, string right) =>
        left is not null && Path.IsPathRooted(left) && Path.IsPathRooted(right) &&
        Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.Ordinal);

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.First(candidate => !string.IsNullOrWhiteSpace(candidate))!;

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeId(string value) => value.Trim().ToLowerInvariant();

    private static string StableHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())))[..16].ToLowerInvariant();

    private sealed class DesktopCandidate(DesktopApplicationEntry entry)
    {
        public DesktopApplicationEntry Entry { get; } = entry;
        public int Score { get; set; }
    }

    private sealed record DesktopResolution(DesktopApplicationEntry Entry, IdentityConfidence Confidence);
}
