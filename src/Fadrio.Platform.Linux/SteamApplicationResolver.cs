using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Platform.Linux;

public sealed class SteamApplicationResolver(SteamApplicationIndex applications) : ISteamApplicationResolver
{
    public ValueTask<ApplicationIdentity?> TryResolveAsync(AudioSession session, ProcessMetadata? process,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (process is null) return ValueTask.FromResult<ApplicationIdentity?>(null);
        IReadOnlyDictionary<string, string> environment = process.IdentityEnvironment;
        string[] ids = new[] { "SteamAppId", "SteamGameId", "STEAM_COMPAT_APP_ID" }
            .Where(environment.ContainsKey).Select(key => environment[key]).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length != 1 || !SteamApplicationIndex.ValidAppId(ids[0])) return ValueTask.FromResult<ApplicationIdentity?>(null);
        string id = ids[0];
        if (!environment.TryGetValue("STEAM_COMPAT_DATA_PATH", out string? compat) ||
            !Path.IsPathFullyQualified(compat) || compat.Contains('\0')) return ValueTask.FromResult<ApplicationIdentity?>(null);
        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(compat));
        SteamApplicationEntry[] candidates = applications.Find(id).Where(entry =>
            normalized == Path.Combine(entry.LibraryPath, "steamapps", "compatdata", id)).ToArray();
        if (candidates.Length != 1) return ValueTask.FromResult<ApplicationIdentity?>(null);
        SteamApplicationEntry game = candidates[0];
        return ValueTask.FromResult<ApplicationIdentity?>(new ApplicationIdentity
        {
            Id = new($"steam:{id}"),
            DisplayName = game.Name,
            SteamAppId = id,
            ExecutablePath = process.ExecutablePath,
            ExecutableName = Path.GetFileName(process.ExecutablePath),
            Confidence = IdentityConfidence.High,
            Evidence = [
                new(IdentityEvidenceKind.ProcessExecutable, process.ExecutablePath ?? "unknown", "Runtime executable from /proc."),
                new(IdentityEvidenceKind.ProcessEnvironment, id, "Selected Steam application environment keys agree."),
                new(IdentityEvidenceKind.InstallationMetadata, id, "Installed Steam manifest and compatibility directory corroborate the application.")
            ]
        });
    }
}
