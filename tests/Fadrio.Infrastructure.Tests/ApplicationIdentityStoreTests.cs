using Fadrio.Application;
using Fadrio.Core;
using Fadrio.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Fadrio.Infrastructure.Tests;

public sealed class ApplicationIdentityStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fadrio-identity-store-{Guid.NewGuid():N}");

    [Fact]
    public void StableIdentityPersistsAllowlistedEvidenceAndPreservesUserOverride()
    {
        ApplicationIdentityStore store = CreateStore();
        ApplicationIdentity identity = FirefoxIdentity();
        store.SetOverride(identity.Id, "My Firefox", "custom-firefox");
        store.SaveResolvedIdentity(identity);

        StoredApplicationIdentity saved = Assert.IsType<StoredApplicationIdentity>(store.Read(identity.Id));
        Assert.Equal("xdg:firefox", saved.Key);
        Assert.Equal("Firefox", saved.DisplayName);
        Assert.Equal(IdentityConfidence.High, saved.Confidence);
        Assert.Equal("firefox", saved.DesktopFileId);
        Assert.Equal("My Firefox", saved.CustomName);
        Assert.Equal("custom-firefox", saved.CustomIcon);
        Assert.Equal("firefox", Assert.Single(saved.Evidence).Value);
        Assert.Equal("My Firefox", store.ApplyOverride(identity).DisplayName);
        Assert.Equal("custom-firefox", store.ApplyOverride(identity).Icon?.Value);

        store.SaveResolvedIdentity(identity with { DisplayName = "Firefox Browser" });
        Assert.Equal("My Firefox", store.Read(identity.Id)?.CustomName);
        Assert.Equal("Firefox Browser", store.Read(identity.Id)?.DisplayName);
    }

    [Fact]
    public void ExecutableFallbackOverrideUsesOpaqueKeyWithoutPersistingRawContext()
    {
        ApplicationIdentityStore store = CreateStore();
        var fallback = new ApplicationIdentity
        {
            Id = new("exe:/home/flavio/private/game"),
            DisplayName = "private media title",
            ExecutablePath = "/home/flavio/private/game",
            Confidence = IdentityConfidence.Medium,
            Evidence = [new(IdentityEvidenceKind.ProcessExecutable,
                "/home/flavio/private/game", "Resolved from a process.")]
        };

        store.SaveResolvedIdentity(fallback);
        Assert.Null(store.Read(fallback.Id));
        store.SetOverride(fallback.Id, "My game", null);

        StoredApplicationIdentity saved = Assert.IsType<StoredApplicationIdentity>(store.Read(fallback.Id));
        Assert.StartsWith("opaque:", saved.Key, StringComparison.Ordinal);
        Assert.Null(saved.DisplayName);
        Assert.Empty(saved.Evidence);
        Assert.Equal("My game", store.ApplyOverride(fallback).DisplayName);
        Assert.Equal("private media title", fallback.DisplayName);

        using SqliteConnection connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM applications;";
        Assert.DoesNotContain("/home/flavio", (string)command.ExecuteScalar()!, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearingOverrideRestoresResolvedPresentation()
    {
        ApplicationIdentityStore store = CreateStore();
        ApplicationIdentity identity = FirefoxIdentity();
        store.SaveResolvedIdentity(identity);
        store.SetOverride(identity.Id, "My Firefox", "my-icon");
        store.SetOverride(identity.Id, null, null);

        ApplicationIdentity restored = store.ApplyOverride(identity);
        Assert.Equal("Firefox", restored.DisplayName);
        Assert.Equal("firefox", restored.Icon?.Value);
    }

    [Fact]
    public async Task ResolverPersistsIdentityAndAppliesOverrideToLiveResult()
    {
        ApplicationIdentityStore store = CreateStore();
        ApplicationIdentity identity = FirefoxIdentity();
        store.SetOverride(identity.Id, "Browser", null);
        var resolver = new PersistentApplicationResolver(new FixtureResolver(identity), store);

        ApplicationIdentity resolved = await resolver.ResolveAsync(new AudioSession
        {
            Id = new("fixture"),
            PipeWireNodeId = 81
        }, TestContext.Current.CancellationToken);

        Assert.Equal("Browser", resolved.DisplayName);
        Assert.Equal("Firefox", store.Read(identity.Id)?.DisplayName);
        Assert.Equal("firefox", Assert.Single(store.Read(identity.Id)!.Evidence).Value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private ApplicationIdentityStore CreateStore()
    {
        var database = new FadrioDatabase(Path.Combine(_root, "fadrio.db"));
        database.Initialize();
        return new(database);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(_root, "fadrio.db")};Pooling=False");
        connection.Open();
        return connection;
    }

    private static ApplicationIdentity FirefoxIdentity() => new()
    {
        Id = new("xdg:firefox"),
        DisplayName = "Firefox",
        DesktopFileId = "firefox",
        ExecutablePath = "/usr/lib/firefox/firefox-bin",
        Icon = new("firefox"),
        Confidence = IdentityConfidence.High,
        Evidence =
        [
            new(IdentityEvidenceKind.ProcessExecutable, "/usr/lib/firefox/firefox-bin", "Process path."),
            new(IdentityEvidenceKind.DesktopEntry, "firefox", "Matched desktop launcher.")
        ]
    };

    private sealed class FixtureResolver(ApplicationIdentity identity) : IApplicationResolver
    {
        public ValueTask<ApplicationIdentity> ResolveAsync(
            AudioSession session,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(identity);
    }
}
