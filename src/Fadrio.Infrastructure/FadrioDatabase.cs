using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Fadrio.Infrastructure;

public sealed class FadrioDatabase(string databasePath)
{
    private static readonly Migration[] Migrations =
    [
        new(1, "bootstrap-settings", """
            CREATE TABLE settings (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            """)
    ];

    public string DatabasePath { get; } = ValidatePath(databasePath);

    public int CurrentSchemaVersion => Migrations[^1].Version;

    public void Initialize()
    {
        SQLitePCL.Batteries_V2.Init();
        string directory = Path.GetDirectoryName(DatabasePath)!;
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA quick_check;";
            if (!string.Equals(integrity.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Fadrio database failed SQLite integrity checking.");
            }
        }

        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                checksum TEXT NOT NULL
            );
            """);

        var applied = new Dictionary<int, (string Name, string Checksum)>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT version, name, checksum FROM schema_migrations ORDER BY version;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                applied.Add(reader.GetInt32(0), (reader.GetString(1), reader.GetString(2)));
            }
        }

        if (applied.Keys.Any(version => version > CurrentSchemaVersion || version < 1))
        {
            throw new InvalidDataException("The database has a schema version this build does not support.");
        }

        foreach (Migration migration in Migrations)
        {
            string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(migration.Sql)));
            if (applied.TryGetValue(migration.Version, out (string Name, string Checksum) recorded))
            {
                if (recorded.Name != migration.Name || recorded.Checksum != checksum)
                {
                    throw new InvalidDataException($"Migration {migration.Version} does not match the recorded schema.");
                }

                continue;
            }

            if (applied.Keys.Any(version => version > migration.Version))
            {
                throw new InvalidDataException($"Migration {migration.Version} is missing from the recorded history.");
            }

            Execute(connection, transaction, migration.Sql);
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO schema_migrations (version, name, checksum) VALUES ($version, $name, $checksum);";
            insert.Parameters.AddWithValue("$version", migration.Version);
            insert.Parameters.AddWithValue("$name", migration.Name);
            insert.Parameters.AddWithValue("$checksum", checksum);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static string ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The database path must be absolute.", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed record Migration(int Version, string Name, string Sql);
}
