using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Platform.Linux;

public sealed class SnapApplicationResolver(IDesktopApplicationIndex desktopApplications)
    : ISnapApplicationResolver
{
    public ValueTask<ApplicationIdentity?> TryResolveAsync(
        AudioSession session,
        ProcessMetadata? process,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        string? snapName = NormalizeName(process?.SnapName);
        string? instanceName = NormalizeInstanceName(process?.SnapInstanceName, snapName);
        if (snapName is null || instanceName is null || process is null ||
            !HasCorroboratedSnapRoot(process, snapName))
        {
            return ValueTask.FromResult<ApplicationIdentity?>(null);
        }

        DesktopApplicationEntry? desktop = desktopApplications.FindBySnapInstanceName(instanceName)
            .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var evidence = new List<IdentityEvidence>
        {
            new(IdentityEvidenceKind.SnapPackageName, instanceName,
                "Snap package environment agreed with its mounted package root.")
        };
        if (desktop is not null)
        {
            evidence.Add(new(IdentityEvidenceKind.DesktopEntry, desktop.Id,
                "Snap instance matched an exported host desktop entry."));
        }

        string? executablePath = string.IsNullOrWhiteSpace(process.ExecutablePath) ? null : process.ExecutablePath;
        return ValueTask.FromResult<ApplicationIdentity?>(new ApplicationIdentity
        {
            Id = new($"snap:{instanceName}"),
            DisplayName = desktop?.Name ?? FirstNonEmpty(session.ApplicationName, instanceName),
            DesktopFileId = desktop?.Id,
            ExecutablePath = executablePath,
            ExecutableName = executablePath is null
                ? string.IsNullOrWhiteSpace(session.ProcessBinary) ? null : Path.GetFileName(session.ProcessBinary)
                : Path.GetFileName(executablePath),
            SnapId = instanceName,
            Icon = !string.IsNullOrWhiteSpace(desktop?.Icon)
                ? new(desktop.Icon)
                : !string.IsNullOrWhiteSpace(session.ApplicationIconName) ? new(session.ApplicationIconName) : null,
            Confidence = desktop is null ? IdentityConfidence.High : IdentityConfidence.Exact,
            Evidence = evidence
        });
    }

    private static bool HasCorroboratedSnapRoot(ProcessMetadata process, string snapName) =>
        process.IdentityEnvironment.TryGetValue("SNAP", out string? root) && Path.IsPathRooted(root) &&
        Path.GetFullPath(root).StartsWith($"/snap/{snapName}/", StringComparison.Ordinal);

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string name = value.Trim();
        return name.Length <= 40 && char.IsAsciiLetterOrDigit(name[0]) && char.IsAsciiLetterOrDigit(name[^1]) &&
            name.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-')
            ? name
            : null;
    }

    private static string? NormalizeInstanceName(string? value, string? snapName)
    {
        string instance = string.IsNullOrWhiteSpace(value) ? snapName ?? string.Empty : value.Trim();
        string[] parts = instance.Split('_');
        if (parts.Length is < 1 or > 2 || NormalizeName(parts[0]) != snapName ||
            parts.Length == 2 && (parts[1].Length is 0 or > 10 ||
                parts[1].Any(character => !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character)))))
        {
            return null;
        }

        return instance;
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.First(candidate => !string.IsNullOrWhiteSpace(candidate))!;
}
