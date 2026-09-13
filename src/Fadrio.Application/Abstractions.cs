using Fadrio.Core;

namespace Fadrio.Application;

public interface IApplicationResolver
{
    ValueTask<ApplicationIdentity> ResolveAsync(AudioSession session, CancellationToken cancellationToken = default);
}

public sealed record ProcessMetadata(int ProcessId, string? ExecutablePath, IReadOnlyList<string> Arguments)
{
    public IReadOnlyDictionary<string, string> IdentityEnvironment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public interface IProcessMetadataProvider
{
    ValueTask<ProcessMetadata?> GetAsync(int processId, CancellationToken cancellationToken = default);
}

public sealed record DesktopApplicationEntry(
    string Id,
    string Name,
    string? Executable,
    string? Icon,
    string? StartupWmClass,
    bool NoDisplay,
    bool Hidden,
    string SourcePath);

public interface IDesktopApplicationIndex
{
    IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath);
    IReadOnlyList<DesktopApplicationEntry> FindById(string desktopFileId);
}

public interface ISteamApplicationResolver
{
    ValueTask<ApplicationIdentity?> TryResolveAsync(
        AudioSession session,
        ProcessMetadata? process,
        CancellationToken cancellationToken = default);
}
