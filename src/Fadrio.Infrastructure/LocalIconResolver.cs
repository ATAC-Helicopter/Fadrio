using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Infrastructure;

public sealed record LocalIconResolverOptions
{
    public IReadOnlyList<string>? IconDirectories { get; init; }
    public string? CacheDirectory { get; init; }
    public long MaxSourceBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxDimension { get; init; } = 4096;
    public int MaxCacheEntries { get; init; } = 256;
    public long MaxCacheBytes { get; init; } = 64 * 1024 * 1024;
    public int MaxSearchEntries { get; init; } = 50_000;
}

public sealed class LocalIconResolver : ILocalIconResolver
{
    private const int PngHeaderLength = 24;
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];
    private readonly string[] _trustedDirectories;
    private readonly string _cacheDirectory;
    private readonly LocalIconResolverOptions _options;

    public LocalIconResolver(LocalIconResolverOptions? options = null)
    {
        _options = options ?? new LocalIconResolverOptions();
        ValidateOptions(_options);
        _trustedDirectories = (_options.IconDirectories ?? GetStandardIconDirectories())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeDirectory)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _cacheDirectory = Path.GetFullPath(_options.CacheDirectory ?? GetStandardCacheDirectory());
    }

    public async ValueTask<ResolvedIcon?> ResolveAsync(
        IconReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (string.IsNullOrWhiteSpace(reference.Value))
        {
            return null;
        }

        string value = reference.Value.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !uri.IsFile)
        {
            return null;
        }

        string? sourcePath = Path.IsPathRooted(value)
            ? ValidateCandidate(value)
            : FindNamedIcon(value, cancellationToken);
        if (sourcePath is null)
        {
            return null;
        }

        try
        {
            return await CacheAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IEnumerable<string> GetStandardIconDirectories()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
            Path.Combine(userProfile, ".local", "share");
        yield return Path.Combine(dataHome, "icons");
        yield return Path.Combine(userProfile, ".icons");

        string dataDirectories = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share";
        foreach (string directory in dataDirectories.Split(
                     ':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(directory, "icons");
            yield return Path.Combine(directory, "pixmaps");
        }
    }

    public static string GetStandardCacheDirectory()
    {
        string cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        return Path.Combine(cacheHome, "fadrio", "icons");
    }

    private string? FindNamedIcon(string value, CancellationToken cancellationToken)
    {
        if (value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            value is "." or "..")
        {
            return null;
        }

        string extension = Path.GetExtension(value);
        if (extension.Length > 0 && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string fileName = extension.Length == 0 ? $"{value}.png" : value;
        int visited = 0;
        foreach (string trustedDirectory in _trustedDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(trustedDirectory))
            {
                continue;
            }

            string directPath = Path.Combine(trustedDirectory, fileName);
            string? direct = ValidateCandidate(directPath);
            if (direct is not null)
            {
                return direct;
            }

            var pending = new Queue<string>();
            pending.Enqueue(trustedDirectory);
            while (pending.Count > 0 && visited < _options.MaxSearchEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pending.Dequeue();
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(directory)
                                 .Order(StringComparer.Ordinal))
                    {
                        if (++visited > _options.MaxSearchEntries)
                        {
                            break;
                        }

                        FileAttributes attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            pending.Enqueue(entry);
                        }
                        else if (Path.GetFileName(entry).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            string? candidate = ValidateCandidate(entry);
                            if (candidate is not null)
                            {
                                return candidate;
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // Installed icon trees can change while they are indexed.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip unreadable theme directories.
                }
            }
        }

        return null;
    }

    private string? ValidateCandidate(string path)
    {
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            string resolvedPath = ResolveExistingPath(path);
            return _trustedDirectories.Any(directory => IsWithinDirectory(resolvedPath, directory))
                ? resolvedPath
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async ValueTask<ResolvedIcon?> CacheAsync(string sourcePath, CancellationToken cancellationToken)
    {
        FileInfo sourceInfo = new(sourcePath);
        if (sourceInfo.Length < PngHeaderLength || sourceInfo.Length > _options.MaxSourceBytes)
        {
            return null;
        }

        await using FileStream source = new(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] header = new byte[PngHeaderLength];
        if (await source.ReadAtLeastAsync(header, PngHeaderLength, throwOnEndOfStream: false, cancellationToken)
                .ConfigureAwait(false) != PngHeaderLength ||
            !header.AsSpan(0, 8).SequenceEqual(PngSignature) ||
            BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4)) != 13 ||
            !header.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        uint width = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4));
        uint height = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4));
        if (width is 0 || height is 0 || width > _options.MaxDimension || height > _options.MaxDimension)
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        string signature = $"{sourcePath}\n{sourceInfo.Length}\n{sourceInfo.LastWriteTimeUtc.Ticks}";
        string key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        string destination = Path.Combine(_cacheDirectory, $"{key}.png");
        if (!File.Exists(destination))
        {
            string temporary = Path.Combine(_cacheDirectory, $".{key}.{Guid.NewGuid():N}.tmp");
            try
            {
                source.Position = 0;
                await using (FileStream output = new(
                                 temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    File.Move(temporary, destination);
                }
                catch (IOException) when (File.Exists(destination))
                {
                    File.Delete(temporary);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        File.SetLastWriteTimeUtc(destination, DateTime.UtcNow);
        PruneCache(destination);
        return new ResolvedIcon(destination, "image/png", checked((int)width), checked((int)height));
    }

    private void PruneCache(string currentPath)
    {
        FileInfo[] entries;
        try
        {
            entries = new DirectoryInfo(_cacheDirectory)
                .EnumerateFiles("*.png", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.FullName.Equals(currentPath, StringComparison.Ordinal))
                .ThenByDescending(file => file.LastWriteTimeUtc)
                .ToArray();
        }
        catch (IOException)
        {
            return;
        }

        long retainedBytes = 0;
        for (int index = 0; index < entries.Length; index++)
        {
            FileInfo entry = entries[index];
            bool retain = index < _options.MaxCacheEntries &&
                retainedBytes <= _options.MaxCacheBytes - entry.Length;
            if (retain)
            {
                retainedBytes += entry.Length;
                continue;
            }

            try
            {
                entry.Delete();
            }
            catch (IOException)
            {
                // A concurrent resolver may still be reading the entry.
            }
            catch (UnauthorizedAccessException)
            {
                // Cache cleanup is best-effort.
            }
        }
    }

    private static string ResolveExistingPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? throw new IOException("Icon path has no filesystem root.");
        string current = root;
        foreach (string segment in Path.GetRelativePath(root, fullPath).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                continue;
            }

            FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ??
                throw new IOException("Unable to resolve icon path link.");
        }

        return Path.GetFullPath(current);
    }

    private static bool IsWithinDirectory(string path, string directory)
    {
        string relative = Path.GetRelativePath(directory, path);
        return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    private static string NormalizeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            return fullPath;
        }

        return ResolveExistingPath(fullPath);
    }

    private static void ValidateOptions(LocalIconResolverOptions options)
    {
        if (options.MaxSourceBytes < PngHeaderLength || options.MaxDimension <= 0 ||
            options.MaxCacheEntries <= 0 || options.MaxCacheBytes < options.MaxSourceBytes ||
            options.MaxSearchEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Icon resolver limits must be positive and internally consistent.");
        }
    }
}
