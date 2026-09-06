using System.Text;

namespace Fadrio.Platform.Linux;

// Steam's text KeyValues subset used by libraryfolders.vdf and app manifests.
internal sealed record SteamKeyValues(string? Value, IReadOnlyDictionary<string, SteamKeyValues> Children)
{
    public string? Text(string key) => Children.GetValueOrDefault(key)?.Value;

    public static SteamKeyValues Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        const int maximumBytes = 1024 * 1024;
        byte[] bytes = new byte[maximumBytes + 1];
        int length = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (length > maximumBytes)
        {
            throw new FormatException("Steam metadata exceeds the size limit.");
        }
        string text = Encoding.UTF8.GetString(bytes, 0, length);
        int position = 0;
        return Parse(0, false);

        SteamKeyValues Parse(int depth, bool nested)
        {
            if (depth > 16) throw new FormatException("Steam metadata exceeds the nesting limit.");
            var children = new Dictionary<string, SteamKeyValues>(StringComparer.OrdinalIgnoreCase);
            while (Token() is { } key)
            {
                if (key == "}" && nested) return new(null, children);
                if (key is "{" or "}") throw new FormatException("Unexpected Steam metadata brace.");
                string value = Token() ?? throw new FormatException("Missing Steam metadata value.");
                if (value == "}") throw new FormatException("Missing Steam metadata value.");
                SteamKeyValues entry = value == "{" ? Parse(depth + 1, true) : new(value, new Dictionary<string, SteamKeyValues>());
                if (!children.TryAdd(key, entry)) throw new FormatException("Duplicate Steam metadata key.");
            }
            if (nested) throw new FormatException("Unclosed Steam metadata object.");
            return new(null, children);
        }

        string? Token()
        {
            while (position < text.Length)
            {
                if (char.IsWhiteSpace(text[position]) || text[position] == '\uFEFF') { position++; continue; }
                if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/')
                {
                    while (position < text.Length && text[position] != '\n') position++;
                    continue;
                }
                break;
            }
            if (position == text.Length) return null;
            char first = text[position++];
            if (first is '{' or '}') return first.ToString();
            if (first != '"') throw new FormatException("Expected a quoted Steam metadata token.");
            var token = new StringBuilder();
            while (position < text.Length)
            {
                char character = text[position++];
                if (character == '"') return token.ToString();
                if (character == '\\' && position < text.Length && text[position] is '\\' or '"')
                    character = text[position++];
                token.Append(character);
            }
            throw new FormatException("Unclosed Steam metadata string.");
        }
    }
}
