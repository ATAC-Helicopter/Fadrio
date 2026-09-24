using System.Security.Cryptography;
using System.Text;
using Fadrio.Core;
using Microsoft.Data.Sqlite;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.Infrastructure;

public sealed record StoredApplicationIdentity(
    string Key,
    string? DisplayName,
    IdentityConfidence? Confidence,
    string? DesktopFileId,
    string? FlatpakId,
    string? SnapId,
    string? SteamAppId,
    string? CustomName,
    string? CustomIcon,
    IReadOnlyList<IdentityEvidence> Evidence);

public sealed class ApplicationIdentityStore(FadrioDatabase database)
{
    public void SaveResolvedIdentity(ApplicationIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        string key = PersistenceKey(identity.Id);
        if (key.StartsWith("opaque:", StringComparison.Ordinal))
        {
            // Fallback names and resolver evidence may contain private media or process context.
            return;
        }

        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO applications
                    (id, display_name, confidence, desktop_file_id, flatpak_id, snap_id, steam_app_id)
                VALUES
                    ($id, $display_name, $confidence, $desktop_file_id, $flatpak_id, $snap_id, $steam_app_id)
                ON CONFLICT(id) DO UPDATE SET
                    display_name = excluded.display_name,
                    confidence = excluded.confidence,
                    desktop_file_id = excluded.desktop_file_id,
                    flatpak_id = excluded.flatpak_id,
                    snap_id = excluded.snap_id,
                    steam_app_id = excluded.steam_app_id;
                """;
            command.Parameters.AddWithValue("$id", key);
            command.Parameters.AddWithValue("$display_name", identity.DisplayName);
            command.Parameters.AddWithValue("$confidence", (int)identity.Confidence);
            command.Parameters.AddWithValue("$desktop_file_id", DbValue(SafeIdentifier(identity.DesktopFileId)));
            command.Parameters.AddWithValue("$flatpak_id", DbValue(SafeIdentifier(identity.FlatpakId)));
            command.Parameters.AddWithValue("$snap_id", DbValue(SafeIdentifier(identity.SnapId)));
            command.Parameters.AddWithValue("$steam_app_id", DbValue(SafeIdentifier(identity.SteamAppId)));
            command.ExecuteNonQuery();
        }

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM application_evidence WHERE application_id = $id;";
            delete.Parameters.AddWithValue("$id", key);
            delete.ExecuteNonQuery();
        }

        foreach ((IdentityEvidenceKind kind, string value) in identity.Evidence
            .Select(item => (item.Kind, Value: SafeEvidenceValue(item)))
            .Where(item => item.Value is not null)
            .Select(item => (item.Kind, item.Value!))
            .Distinct())
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO application_evidence (application_id, kind, value)
                VALUES ($id, $kind, $value);
                """;
            insert.Parameters.AddWithValue("$id", key);
            insert.Parameters.AddWithValue("$kind", (int)kind);
            insert.Parameters.AddWithValue("$value", value);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void SetOverride(ApplicationId id, string? customName, string? customIcon)
    {
        string? name = TrimChoice(customName);
        string? icon = TrimChoice(customIcon);
        using SqliteConnection connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO applications (id, custom_name, custom_icon)
            VALUES ($id, $name, $icon)
            ON CONFLICT(id) DO UPDATE SET
                custom_name = excluded.custom_name,
                custom_icon = excluded.custom_icon;
            """;
        command.Parameters.AddWithValue("$id", PersistenceKey(id));
        command.Parameters.AddWithValue("$name", DbValue(name));
        command.Parameters.AddWithValue("$icon", DbValue(icon));
        command.ExecuteNonQuery();
    }

    public StoredApplicationIdentity? Read(ApplicationId id)
    {
        using SqliteConnection connection = Open();
        string key = PersistenceKey(id);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT display_name, confidence, desktop_file_id, flatpak_id, snap_id,
                steam_app_id, custom_name, custom_icon
            FROM applications WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", key);
        string? displayName;
        IdentityConfidence? confidence;
        string? desktopFileId;
        string? flatpakId;
        string? snapId;
        string? steamAppId;
        string? customName;
        string? customIcon;
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            displayName = OptionalString(reader, 0);
            confidence = reader.IsDBNull(1) ? null : (IdentityConfidence)reader.GetInt32(1);
            desktopFileId = OptionalString(reader, 2);
            flatpakId = OptionalString(reader, 3);
            snapId = OptionalString(reader, 4);
            steamAppId = OptionalString(reader, 5);
            customName = OptionalString(reader, 6);
            customIcon = OptionalString(reader, 7);
        }

        return new StoredApplicationIdentity(
            key,
            displayName,
            confidence,
            desktopFileId,
            flatpakId,
            snapId,
            steamAppId,
            customName,
            customIcon,
            ReadEvidence(connection, key));
    }

    public ApplicationIdentity ApplyOverride(ApplicationIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        StoredApplicationIdentity? stored = Read(identity.Id);
        return stored is null ? identity : identity with
        {
            DisplayName = stored.CustomName ?? identity.DisplayName,
            Icon = stored.CustomIcon is null ? identity.Icon : new(stored.CustomIcon)
        };
    }

    private SqliteConnection Open()
    {
        SQLitePCL.Batteries_V2.Init();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = database.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
            ForeignKeys = true
        }.ToString());
        connection.Open();
        return connection;
    }

    private static IReadOnlyList<IdentityEvidence> ReadEvidence(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kind, value FROM application_evidence
            WHERE application_id = $id ORDER BY kind, value;
            """;
        command.Parameters.AddWithValue("$id", key);
        using SqliteDataReader reader = command.ExecuteReader();
        var evidence = new List<IdentityEvidence>();
        while (reader.Read())
        {
            evidence.Add(new((IdentityEvidenceKind)reader.GetInt32(0), reader.GetString(1),
                "Persisted stable identity evidence."));
        }

        return evidence;
    }

    private static string PersistenceKey(ApplicationId id)
    {
        string value = id.Value;
        int separator = value.IndexOf(':');
        if (separator > 0 && separator < value.Length - 1 &&
            value[..separator] is ("xdg" or "flatpak" or "snap" or "steam") &&
            SafeIdentifier(value[(separator + 1)..]) is not null)
        {
            return value;
        }

        return "opaque:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string? SafeEvidenceValue(IdentityEvidence evidence) => evidence.Kind switch
    {
        IdentityEvidenceKind.DesktopEntry or IdentityEvidenceKind.FlatpakApplicationId or
            IdentityEvidenceKind.SnapPackageName => SafeIdentifier(evidence.Value),
        _ => null
    };

    private static string? SafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        char.IsAsciiLetterOrDigit(value[0]) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
            ? value
            : null;

    private static string? TrimChoice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > 512 || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException("Override values must be at most 512 characters and contain no control characters.",
                nameof(value));
        }

        return trimmed;
    }

    private static object DbValue(string? value) => value is null ? DBNull.Value : value;

    private static string? OptionalString(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);
}
