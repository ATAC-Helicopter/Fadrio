namespace Fadrio.Platform.Linux.Tests;

public sealed class LinuxProcMetadataProviderTests
{
    [Fact]
    public async Task ReadsSyntheticProcFixture()
    {
        string root = Path.Combine(Path.GetTempPath(), $"fadrio-proc-{Guid.NewGuid():N}");
        string processDirectory = Path.Combine(root, "123");
        Directory.CreateDirectory(processDirectory);
        try
        {
            string executable = Path.Combine(root, "test-player");
            await File.WriteAllTextAsync(executable, "fixture", TestContext.Current.CancellationToken);
            File.CreateSymbolicLink(Path.Combine(processDirectory, "exe"), executable);
            await File.WriteAllBytesAsync(Path.Combine(processDirectory, "cmdline"),
                "test-player\0--play\0"u8.ToArray(), TestContext.Current.CancellationToken);
            var provider = new LinuxProcMetadataProvider(root);
            Fadrio.Application.ProcessMetadata? result = await provider.GetAsync(123, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Equal(executable, result.ExecutablePath);
            Assert.Equal(["test-player", "--play"], result.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MissingProcessReturnsNull()
    {
        var provider = new LinuxProcMetadataProvider("/definitely/not/a/proc/filesystem");
        Assert.Null(await provider.GetAsync(123, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("wine64-preloader", true)]
    [InlineData("Game.exe", true)]
    [InlineData("brave", false)]
    public async Task RetainsOnlyRelevantIdentityEnvironment(string binary, bool relevant)
    {
        string root = Path.Combine(Path.GetTempPath(), $"fadrio-proc-{Guid.NewGuid():N}");
        string directory = Path.Combine(root, "123");
        Directory.CreateDirectory(directory);
        try
        {
            string executable = Path.Combine(root, binary);
            await File.WriteAllTextAsync(executable, "fixture", TestContext.Current.CancellationToken);
            File.CreateSymbolicLink(Path.Combine(directory, "exe"), executable);
            await File.WriteAllTextAsync(Path.Combine(directory, "cmdline"), binary + "\0",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "environ"),
                "SteamAppId=12345\0STEAM_COMPAT_DATA_PATH=/fixtures/compatdata/12345\0SECRET_TOKEN=must-not-escape\0",
                TestContext.Current.CancellationToken);
            var provider = new LinuxProcMetadataProvider(root);
            var result = await provider.GetAsync(123, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.DoesNotContain("SECRET_TOKEN", result.IdentityEnvironment.Keys);
            Assert.Equal(relevant ? 2 : 0, result.IdentityEnvironment.Count);
            if (relevant)
            {
                Assert.Equal("12345", result.IdentityEnvironment["SteamAppId"]);
            }
            File.Delete(Path.Combine(directory, "environ"));
            var withoutEnvironment = await provider.GetAsync(123, TestContext.Current.CancellationToken);
            Assert.NotNull(withoutEnvironment);
            Assert.Equal(executable, withoutEnvironment.ExecutablePath);
            Assert.Empty(withoutEnvironment.IdentityEnvironment);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
