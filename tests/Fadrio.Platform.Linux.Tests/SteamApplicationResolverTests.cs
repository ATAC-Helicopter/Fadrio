using Fadrio.Application;
using Fadrio.Core;

namespace Fadrio.Platform.Linux.Tests;

public sealed class SteamApplicationResolverTests
{
    [Theory]
    [InlineData("12345", "12345", true)]
    [InlineData("12345", "67890", false)]
    [InlineData("invalid", "12345", false)]
    [InlineData("0", "0", false)]
    public async Task CorroboratesSteamIdsAgainstDiscoveredLibrary(string appId, string compatId, bool expected)
    {
        using var fixture = new SteamFixture();
        var index = new SteamApplicationIndex([fixture.Client]);
        var resolver = new SteamApplicationResolver(index);
        var process = new ProcessMetadata(123, "/opt/proton/wine64-preloader", ["Fixture.exe"])
        {
            IdentityEnvironment = new Dictionary<string, string>
            {
                ["SteamAppId"] = appId,
                ["STEAM_COMPAT_DATA_PATH"] = Path.Combine(fixture.Library, "steamapps", "compatdata", compatId)
            }
        };
        var session = new AudioSession { Id = new("fixture"), PipeWireNodeId = 81 };
        ApplicationIdentity? identity = await resolver.TryResolveAsync(session, process, TestContext.Current.CancellationToken);
        if (expected)
        {
            Assert.NotNull(identity);
            Assert.Equal("steam:12345", identity.Id.Value);
            Assert.Equal("Fixture Game", identity.DisplayName);
            Assert.Equal(IdentityConfidence.High, identity.Confidence);
            Assert.Contains(identity.Evidence, evidence => evidence.Kind == IdentityEvidenceKind.InstallationMetadata);
        }
        else Assert.Null(identity);

        var conflicting = process with
        {
            IdentityEnvironment = new Dictionary<string, string>(process.IdentityEnvironment) { ["SteamGameId"] = "67890" }
        };
        Assert.Null(await resolver.TryResolveAsync(session, conflicting, TestContext.Current.CancellationToken));
        Assert.Null(await resolver.TryResolveAsync(session, process with { IdentityEnvironment = new Dictionary<string, string>() }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RejectsManifestIdentityMismatchAndMalformedMetadata()
    {
        using var fixture = new SteamFixture();
        Assert.Single(new SteamApplicationIndex([fixture.Client]).Find("12345"));
        string manifest = Path.Combine(fixture.Library, "steamapps", "appmanifest_12345.acf");
        File.WriteAllText(manifest, "\"AppState\" { \"appid\" \"67890\" \"name\" \"Other\" \"installdir\" \"Fixture Game\" }");
        Assert.Empty(new SteamApplicationIndex([fixture.Client]).Find("67890"));
        File.WriteAllText(manifest, "\"AppState\" { \"appid\" \"12345\"");
        Assert.Empty(new SteamApplicationIndex([fixture.Client]).Find("12345"));
    }

    private sealed class SteamFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"fadrio-steam-{Guid.NewGuid():N}");
        public string Client => Path.Combine(_root, "client");
        public string Library => Path.Combine(_root, "library");

        public SteamFixture()
        {
            Directory.CreateDirectory(Path.Combine(Client, "steamapps"));
            Directory.CreateDirectory(Path.Combine(Library, "steamapps", "common", "Fixture Game"));
            string escapedLibrary = Library.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(Client, "steamapps", "libraryfolders.vdf"),
                $"// synthetic library\n\"libraryfolders\" {{ \"1\" {{ \"path\" \"{escapedLibrary}\" }} }}");
            File.Copy(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "../../../../../tests/fixtures/steam/appmanifest_12345.acf")),
                Path.Combine(Library, "steamapps", "appmanifest_12345.acf"));
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
