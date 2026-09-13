using System.Text;
using Fadrio.Application;

namespace Fadrio.Infrastructure;

public static class DesktopEntryParser
{
    private static readonly HashSet<char> FieldCodes = ['u', 'U', 'f', 'F', 'i', 'c', 'k'];

    public static DesktopApplicationEntry? Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using StreamReader reader = File.OpenText(path);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inDesktopEntry = false;

        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                inDesktopEntry = trimmed.Equals("[Desktop Entry]", StringComparison.Ordinal);
                continue;
            }

            if (!inDesktopEntry || trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            int separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = trimmed[..separator];
            if (!key.Contains('[', StringComparison.Ordinal))
            {
                values[key] = trimmed[(separator + 1)..];
            }
        }

        if (!values.TryGetValue("Name", out string? name) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string id = Path.GetFileNameWithoutExtension(path);
        values.TryGetValue("Exec", out string? exec);
        values.TryGetValue("Icon", out string? icon);
        values.TryGetValue("StartupWMClass", out string? startupWmClass);
        return new DesktopApplicationEntry(
            id,
            name,
            NormalizeExecutable(exec),
            icon,
            startupWmClass,
            ReadBoolean(values, "NoDisplay"),
            ReadBoolean(values, "Hidden"),
            path);
    }

    public static string? NormalizeExecutable(string? exec)
    {
        if (string.IsNullOrWhiteSpace(exec))
        {
            return null;
        }

        IReadOnlyList<string> tokens = Tokenize(exec);
        int index = 0;
        if (tokens.Count > 0 && Path.GetFileName(tokens[0]).Equals("env", StringComparison.Ordinal))
        {
            index++;
            while (index < tokens.Count && tokens[index].Contains('=', StringComparison.Ordinal) &&
                   !tokens[index].StartsWith("-", StringComparison.Ordinal))
            {
                index++;
            }
        }

        while (index < tokens.Count && (tokens[index].Length == 2 && tokens[index][0] == '%' && FieldCodes.Contains(tokens[index][1])))
        {
            index++;
        }

        return index < tokens.Count ? tokens[index] : null;
    }

    private static IReadOnlyList<string> Tokenize(string value)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char quote = '\0';
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else if (character == '\\' && index + 1 < value.Length)
                {
                    current.Append(value[++index]);
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (char.IsWhiteSpace(character))
            {
                AddToken(tokens, current);
            }
            else
            {
                current.Append(character);
            }
        }

        AddToken(tokens, current);
        return tokens;
    }

    private static void AddToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        string token = current.ToString();
        if (!(token.Length == 2 && token[0] == '%' && FieldCodes.Contains(token[1])))
        {
            tokens.Add(token);
        }

        current.Clear();
    }

    private static bool ReadBoolean(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
}
