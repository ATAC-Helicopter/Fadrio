using Fadrio.Core;

namespace Fadrio.Application;

public interface IApplicationResolver
{
    ValueTask<ApplicationIdentity> ResolveAsync(AudioSession session, CancellationToken cancellationToken = default);
}

public sealed record ProcessMetadata(int ProcessId, string? ExecutablePath, IReadOnlyList<string> Arguments)
{
    public string? FlatpakId { get; init; }
    public string? SnapName { get; init; }
    public string? SnapInstanceName { get; init; }
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
    string SourcePath,
    string? FlatpakId = null,
    string? SnapInstanceName = null);

public interface IDesktopApplicationIndex
{
    IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath);
    IReadOnlyList<DesktopApplicationEntry> FindById(string desktopFileId);
    IReadOnlyList<DesktopApplicationEntry> FindBySnapInstanceName(string snapInstanceName) =>
        FindById(snapInstanceName);
}

public interface IDesktopApplicationIndexRevision
{
    long Revision { get; }
    void Refresh();
}

public interface IApplicationIdentityRevision
{
    long Revision { get; }
}

public sealed record ResolvedIcon(string Path, string MediaType, int Width, int Height);

public interface ILocalIconResolver
{
    ValueTask<ResolvedIcon?> ResolveAsync(
        IconReference reference,
        CancellationToken cancellationToken = default);
}

public interface ISteamApplicationResolver
{
    ValueTask<ApplicationIdentity?> TryResolveAsync(
        AudioSession session,
        ProcessMetadata? process,
        CancellationToken cancellationToken = default);
}

public interface IFlatpakApplicationResolver
{
    ValueTask<ApplicationIdentity?> TryResolveAsync(
        AudioSession session,
        ProcessMetadata? process,
        CancellationToken cancellationToken = default);
}

public interface ISnapApplicationResolver
{
    ValueTask<ApplicationIdentity?> TryResolveAsync(
        AudioSession session,
        ProcessMetadata? process,
        CancellationToken cancellationToken = default);
}
