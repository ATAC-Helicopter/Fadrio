using Fadrio.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Fadrio.Infrastructure.Tests;

public sealed class FadrioPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fadrio-persistence-{Guid.NewGuid():N}");

    [Fact]
    public void XdgPathsUseAbsoluteOverridesAndHomeFallbacks()
    {
        string home = Path.Combine(_root, "home");
        var variables = new Dictionary<string, string>
        {
            ["XDG_CONFIG_HOME"] = Path.Combine(_root, "config"),
            ["XDG_DATA_HOME"] = "relative-data",
            ["XDG_CACHE_HOME"] = Path.Combine(_root, "cache"),
            ["XDG_STATE_HOME"] = ""
        };

        FadrioDataPaths paths = FadrioDataPaths.Resolve(home, name => variables.GetValueOrDefault(name));

        Assert.Equal(Path.Combine(_root, "config", "fadrio"), paths.ConfigDirectory);
        Assert.Equal(Path.Combine(home, ".local", "share", "fadrio"), paths.DataDirectory);
        Assert.Equal(Path.Combine(_root, "cache", "fadrio"), paths.CacheDirectory);
        Assert.Equal(Path.Combine(home, ".local", "state", "fadrio"), paths.StateDirectory);
        Assert.Equal(Path.Combine(paths.DataDirectory, "fadrio.db"), paths.DatabasePath);
    }

    [Fact]
    public void FreshDatabaseBootstrapsOnceAndKeepsExistingSettings()
    {
        var database = new FadrioDatabase(Path.Combine(_root, "data", "fadrio.db"));
        database.Initialize();

        using (SqliteConnection connection = Open(database.DatabasePath))
        {
            Assert.Equal(2L, ScalarLong(connection, "SELECT COUNT(*) FROM schema_migrations;"));
            Assert.Equal(2L, ScalarLong(connection, "SELECT MAX(version) FROM schema_migrations;"));
            Execute(connection, "INSERT INTO settings (key, value) VALUES ('theme', 'dark');");
        }

        database.Initialize();

        using SqliteConnection reopened = Open(database.DatabasePath);
        Assert.Equal(2L, ScalarLong(reopened, "SELECT COUNT(*) FROM schema_migrations;"));
        using var command = reopened.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = 'theme';";
        Assert.Equal("dark", command.ExecuteScalar());
    }

    [Fact]
    public void FutureSchemaIsRejectedWithoutModifyingItsHistory()
    {
        var database = new FadrioDatabase(Path.Combine(_root, "future", "fadrio.db"));
        database.Initialize();
        using (SqliteConnection connection = Open(database.DatabasePath))
        {
            Execute(connection, "INSERT INTO schema_migrations (version, name, checksum) VALUES (99, 'future', 'fixture');");
        }

        Assert.Throws<InvalidDataException>(database.Initialize);

        using SqliteConnection reopened = Open(database.DatabasePath);
        Assert.Equal(3L, ScalarLong(reopened, "SELECT COUNT(*) FROM schema_migrations;"));
    }

    [Fact]
    public void ChangedMigrationChecksumIsRejectedWithoutModifyingSettings()
    {
        var database = new FadrioDatabase(Path.Combine(_root, "checksum", "fadrio.db"));
        database.Initialize();
        using (SqliteConnection connection = Open(database.DatabasePath))
        {
            Execute(connection, "INSERT INTO settings (key, value) VALUES ('theme', 'dark');");
            Execute(connection, "UPDATE schema_migrations SET checksum = 'changed' WHERE version = 1;");
        }

        Assert.Throws<InvalidDataException>(database.Initialize);

        using SqliteConnection reopened = Open(database.DatabasePath);
        using var command = reopened.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = 'theme';";
        Assert.Equal("dark", command.ExecuteScalar());
    }

    [Fact]
    public void UpgradingFromBootstrapSchemaPreservesExistingSettings()
    {
        var database = new FadrioDatabase(Path.Combine(_root, "upgrade", "fadrio.db"));
        database.Initialize();
        using (SqliteConnection connection = Open(database.DatabasePath))
        {
            Execute(connection, "INSERT INTO settings (key, value) VALUES ('theme', 'dark');");
            Execute(connection, "DROP TABLE application_evidence; DROP TABLE applications;");
            Execute(connection, "DELETE FROM schema_migrations WHERE version = 2;");
        }

        database.Initialize();

        using SqliteConnection upgraded = Open(database.DatabasePath);
        Assert.Equal(2L, ScalarLong(upgraded, "SELECT COUNT(*) FROM schema_migrations;"));
        using var command = upgraded.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = 'theme';";
        Assert.Equal("dark", command.ExecuteScalar());
    }

    [Fact]
    public void NewDataDirectoryIsPrivateToTheCurrentUser()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var database = new FadrioDatabase(Path.Combine(_root, "private", "fadrio.db"));
        database.Initialize();

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(Path.GetDirectoryName(database.DatabasePath)!));
    }

    [Fact]
    public void FailedInitialMigrationRollsBackItsHistory()
    {
        string path = Path.Combine(_root, "conflict", "fadrio.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (SqliteConnection connection = Open(path))
        {
            Execute(connection, "CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);");
        }

        var database = new FadrioDatabase(path);
        Assert.Throws<SqliteException>(database.Initialize);

        using SqliteConnection reopened = Open(path);
        Assert.Equal(0L, ScalarLong(reopened,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations';"));
        Assert.Equal(1L, ScalarLong(reopened,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'settings';"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static SqliteConnection Open(string path)
    {
        SQLitePCL.Batteries_V2.Init();
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
