using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Platform.Linux;

public sealed class FlatpakApplicationResolver(IDesktopApplicationIndex desktopApplications)
    : IFlatpakApplicationResolver
{
    public ValueTask<ApplicationIdentity?> TryResolveAsync(
        AudioSession session,
        ProcessMetadata? process,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        string? flatpakId = NormalizeFlatpakId(process?.FlatpakId);
        if (flatpakId is null)
        {
            return ValueTask.FromResult<ApplicationIdentity?>(null);
        }

        DesktopApplicationEntry? desktop = desktopApplications.FindById(flatpakId)
            .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var evidence = new List<IdentityEvidence>
        {
            new(IdentityEvidenceKind.FlatpakApplicationId, flatpakId,
                "Read from the process sandbox .flatpak-info metadata.")
        };
        if (desktop is not null)
        {
            evidence.Add(new(IdentityEvidenceKind.DesktopEntry, desktop.Id,
                "Flatpak application ID matched an installed host desktop entry."));
        }

        string? executablePath = string.IsNullOrWhiteSpace(process?.ExecutablePath) ? null : process.ExecutablePath;
        string? executableName = executablePath is null
            ? string.IsNullOrWhiteSpace(session.ProcessBinary) ? null : Path.GetFileName(session.ProcessBinary)
            : Path.GetFileName(executablePath);
        return ValueTask.FromResult<ApplicationIdentity?>(new ApplicationIdentity
        {
            Id = new($"flatpak:{flatpakId}"),
            DisplayName = desktop?.Name ?? FirstNonEmpty(session.ApplicationName, flatpakId),
            DesktopFileId = desktop?.Id,
            ExecutablePath = executablePath,
            ExecutableName = executableName,
            FlatpakId = flatpakId,
            Icon = !string.IsNullOrWhiteSpace(desktop?.Icon)
                ? new(desktop.Icon)
                : !string.IsNullOrWhiteSpace(session.ApplicationIconName) ? new(session.ApplicationIconName) : null,
            Confidence = desktop is null ? IdentityConfidence.High : IdentityConfidence.Exact,
            Evidence = evidence
        });
    }

    private static string? NormalizeFlatpakId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string id = value.Trim();
        string[] segments = id.Split('.');
        if (id.Length > 255 || segments.Length < 3 ||
            segments.Any(segment => segment.Length == 0 || char.IsAsciiDigit(segment[0])) ||
            segments[..^1].Any(segment => segment.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '_') || char.IsAsciiLetterUpper(character))) ||
            segments[^1].Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
        {
            return null;
        }

        return id;
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.First(candidate => !string.IsNullOrWhiteSpace(candidate))!;
}
