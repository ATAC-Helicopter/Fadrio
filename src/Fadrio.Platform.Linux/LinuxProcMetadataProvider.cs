using Fadrio.Application;

namespace Fadrio.Platform.Linux;

public sealed class LinuxProcMetadataProvider(string procRoot = "/proc") : IProcessMetadataProvider
{
    private static readonly HashSet<string> IdentityEnvironmentKeys = new(StringComparer.Ordinal)
    {
        "SteamAppId", "SteamGameId", "STEAM_COMPAT_APP_ID",
        "STEAM_COMPAT_DATA_PATH", "STEAM_COMPAT_CLIENT_INSTALL_PATH"
    };

    public ValueTask<ProcessMetadata?> GetAsync(int processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0)
        {
            return ValueTask.FromResult<ProcessMetadata?>(null);
        }

        string directory = Path.Combine(procRoot, processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            string? executablePath = ResolveExecutable(Path.Combine(directory, "exe"));
            IReadOnlyList<string> arguments = ReadNullSeparated(Path.Combine(directory, "cmdline"));
            return ValueTask.FromResult<ProcessMetadata?>(new(processId, executablePath, arguments)
            {
                IdentityEnvironment = ReadIdentityEnvironment(directory, executablePath, arguments)
            });
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or
                                          UnauthorizedAccessException or IOException)
        {
            // A stream's process can legitimately exit between the PipeWire event and inspection.
            return ValueTask.FromResult<ProcessMetadata?>(null);
        }
    }

    private static string? ResolveExecutable(string path)
    {
        FileSystemInfo? target = File.ResolveLinkTarget(path, returnFinalTarget: true);
        return target?.FullName;
    }

    private static IReadOnlyDictionary<string, string> ReadIdentityEnvironment(
        string directory, string? executablePath, IReadOnlyList<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string binary = Path.GetFileName(executablePath ?? string.Empty);
        bool relevant = binary.Contains("wine", StringComparison.OrdinalIgnoreCase) ||
            binary.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            arguments.Any(argument => argument.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (!relevant)
        {
            return values;
        }

        try
        {
            // Process environments may contain secrets. Bound the read and retain only
            // the identity keys, without exposing unrelated values to application code.
            using FileStream stream = File.OpenRead(Path.Combine(directory, "environ"));
            const int maximumBytes = 1024 * 1024;
            byte[] buffer = new byte[maximumBytes + 1];
            int length = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (length > maximumBytes)
            {
                return values;
            }
            string environment = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
            foreach (string entry in environment.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = entry.IndexOf('=');
                if (separator > 0 && IdentityEnvironmentKeys.Contains(entry[..separator]))
                {
                    values[entry[..separator]] = entry[(separator + 1)..];
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Optional evidence can disappear or be inaccessible independently of exe.
        }
        return values;
    }

    private static IReadOnlyList<string> ReadNullSeparated(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return System.Text.Encoding.UTF8.GetString(bytes)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
