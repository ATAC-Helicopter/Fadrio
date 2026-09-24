using System.Runtime.CompilerServices;
using Fadrio.Application;
using Fadrio.Core;
using Fadrio.Infrastructure;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.Platform.Linux.Tests;

public sealed class FirefoxIdentityQualificationTests
{
    private static string DesktopFixtures => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures/desktop-files"));

    [Fact]
    public async Task MultipleStreamsGroupAndRemainStableAcrossRecreation()
    {
        var processMetadata = new Dictionary<int, ProcessMetadata>
        {
            [4101] = new(4101, "/usr/lib/firefox/firefox", ["firefox", "-contentproc"]),
            [4102] = new(4102, "/usr/lib/firefox/firefox", ["firefox", "-contentproc"]),
            [4103] = new(4103, "/usr/lib/firefox/firefox", ["firefox", "-contentproc"]),
        };
        var resolver = new ApplicationResolver(
            new FixtureProcessProvider(processMetadata),
            new XdgDesktopApplicationIndex([DesktopFixtures]));
        var coordinator = new MixerStateCoordinator(resolver);

        await coordinator.ApplyAsync(
            new SessionAdded(1, FirefoxSession("music", 71, 4101, "Music")),
            TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(
            new SessionAdded(1, FirefoxSession("video", 72, 4102, "Video")),
            TestContext.Current.CancellationToken);

        RuntimeApplication firefox = Assert.Single(coordinator.Current.Applications);
        Assert.Equal(new ApplicationId("xdg:firefox"), firefox.Identity.Id);
        Assert.Equal("Firefox", firefox.Identity.DisplayName);
        Assert.Equal("firefox", firefox.Identity.Icon?.Value);
        Assert.Equal(IdentityConfidence.High, firefox.Identity.Confidence);
        Assert.Equal(2, firefox.Sessions.Count);
        Assert.Contains(firefox.Identity.Evidence, evidence =>
            evidence.Kind == IdentityEvidenceKind.ProcessExecutable);
        Assert.Contains(firefox.Identity.Evidence, evidence =>
            evidence.Kind == IdentityEvidenceKind.DesktopEntry);

        await using var backend = new RecordingBackend();
        var commands = new MixerCommands(backend, coordinator);
        await commands.SetApplicationVolumeAsync(
            firefox.Identity.Id, 0.42f, TestContext.Current.CancellationToken);
        await commands.SetApplicationMuteAsync(
            firefox.Identity.Id, true, TestContext.Current.CancellationToken);

        Assert.Equal([(71u, 0.42f), (72u, 0.42f)], backend.VolumeCalls);
        Assert.Equal([(71u, true), (72u, true)], backend.MuteCalls);

        await coordinator.ApplyAsync(
            new SessionRemoved(1, new("video")), TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(
            new SessionAdded(1, FirefoxSession("video-recreated", 73, 4103, "Video")),
            TestContext.Current.CancellationToken);

        RuntimeApplication recreated = Assert.Single(coordinator.Current.Applications);
        Assert.Equal(new ApplicationId("xdg:firefox"), recreated.Identity.Id);
        Assert.Equal([71u, 73u], recreated.Sessions.Select(session => session.PipeWireNodeId).Order());
    }

    [Fact]
    public async Task VisibleFirefoxLauncherWinsOverExactHiddenHelperForBinRuntime()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteDesktop("firefox.desktop", "Firefox", "/usr/bin/firefox", "firefox", noDisplay: false);
        directory.WriteDesktop(
            "userapp-Firefox-NTRKV3.desktop",
            "Firefox",
            "/usr/lib/firefox/firefox-bin",
            icon: null,
            noDisplay: true);
        using var index = new XdgDesktopApplicationIndex([directory.Path], watchForChanges: false);
        var resolver = new ApplicationResolver(
            new FixtureProcessProvider(new Dictionary<int, ProcessMetadata>
            {
                [4201] = new(4201, "/usr/lib/firefox/firefox-bin", ["firefox-bin"]),
                [4202] = new(4202, "/usr/lib/firefox/firefox-bin", ["firefox-bin"])
            }),
            index);
        var coordinator = new MixerStateCoordinator(resolver);

        await coordinator.ApplyAsync(
            new SessionAdded(1, LiveFirefoxSession("one", 81, 4201)),
            TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(
            new SessionAdded(1, LiveFirefoxSession("two", 82, 4202)),
            TestContext.Current.CancellationToken);

        RuntimeApplication firefox = Assert.Single(coordinator.Current.Applications);
        Assert.Equal(new ApplicationId("xdg:firefox"), firefox.Identity.Id);
        Assert.Equal("firefox", firefox.Identity.Icon?.Value);
        Assert.Equal(IdentityConfidence.High, firefox.Identity.Confidence);
        Assert.Equal(2, firefox.Sessions.Count);
    }

    [Fact]
    public async Task MultipleVisibleFirefoxCandidatesFallBackInsteadOfGuessing()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteDesktop("firefox.desktop", "Firefox", "/usr/bin/firefox", "firefox", noDisplay: false);
        directory.WriteDesktop("firefox-alt.desktop", "Alternate Firefox", "/opt/firefox", "firefox-alt", noDisplay: false);
        using var index = new XdgDesktopApplicationIndex([directory.Path], watchForChanges: false);
        var resolver = new ApplicationResolver(
            new FixtureProcessProvider(new Dictionary<int, ProcessMetadata>
            {
                [4301] = new(4301, "/usr/lib/firefox/firefox-bin", ["firefox-bin"])
            }),
            index);

        ApplicationIdentity identity = await resolver.ResolveAsync(
            LiveFirefoxSession("ambiguous", 91, 4301),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ApplicationId("exe:/usr/lib/firefox/firefox-bin"), identity.Id);
        Assert.Equal(IdentityConfidence.Medium, identity.Confidence);
        Assert.Contains(identity.Evidence, evidence => evidence.Kind == IdentityEvidenceKind.ConflictingEvidence);
    }

    [Fact]
    public async Task MissingDesktopMetadataFallsBackToStableExecutableIdentity()
    {
        using var directory = new TemporaryDirectory();
        using var index = new XdgDesktopApplicationIndex([directory.Path], watchForChanges: false);
        var resolver = new ApplicationResolver(
            new FixtureProcessProvider(new Dictionary<int, ProcessMetadata>
            {
                [4401] = new(4401, "/usr/lib/firefox/firefox-bin", ["firefox-bin"])
            }),
            index);

        ApplicationIdentity identity = await resolver.ResolveAsync(
            LiveFirefoxSession("fallback", 101, 4401),
            TestContext.Current.CancellationToken);

        Assert.Equal(new ApplicationId("exe:/usr/lib/firefox/firefox-bin"), identity.Id);
        Assert.Equal("firefox-bin", identity.DisplayName);
        Assert.Equal(IdentityConfidence.Medium, identity.Confidence);
    }

    private static AudioSession FirefoxSession(string id, uint nodeId, int processId, string mediaName) => new()
    {
        Id = new(id),
        PipeWireNodeId = nodeId,
        ProcessId = processId,
        ApplicationId = "org.mozilla.firefox",
        ApplicationName = "Firefox",
        ApplicationIconName = "firefox",
        ProcessBinary = "firefox",
        MediaName = mediaName,
        MediaRole = "Music",
        Volume = 1f,
        Active = true,
    };

    private static AudioSession LiveFirefoxSession(string id, uint nodeId, int processId) => new()
    {
        Id = new(id),
        PipeWireNodeId = nodeId,
        ProcessId = processId,
        ApplicationName = "Firefox",
        ProcessBinary = "firefox-bin",
        MediaName = "AudioStream",
        Volume = 1f,
        Active = true,
    };

    private sealed class FixtureProcessProvider(IReadOnlyDictionary<int, ProcessMetadata> processes)
        : IProcessMetadataProvider
    {
        public ValueTask<ProcessMetadata?> GetAsync(
            int processId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(processes.GetValueOrDefault(processId));
    }

    private sealed class RecordingBackend : IAudioBackend
    {
        public List<(uint NodeId, float Volume)> VolumeCalls { get; } = [];
        public List<(uint NodeId, bool Muted)> MuteCalls { get; } = [];

        public async IAsyncEnumerable<AudioBackendEvent> WatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }

        public ValueTask SetStreamVolumeAsync(
            uint nodeId,
            float volume,
            CancellationToken cancellationToken = default)
        {
            VolumeCalls.Add((nodeId, volume));
            return ValueTask.CompletedTask;
        }

        public ValueTask SetStreamMuteAsync(
            uint nodeId,
            bool muted,
            CancellationToken cancellationToken = default)
        {
            MuteCalls.Add((nodeId, muted));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fadrio-firefox-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void WriteDesktop(
            string fileName,
            string name,
            string executable,
            string? icon,
            bool noDisplay)
        {
            string iconLine = icon is null ? string.Empty : $"Icon={icon}{Environment.NewLine}";
            File.WriteAllText(
                System.IO.Path.Combine(Path, fileName),
                $"[Desktop Entry]{Environment.NewLine}Name={name}{Environment.NewLine}" +
                $"Exec={executable}{Environment.NewLine}{iconLine}" +
                $"NoDisplay={noDisplay.ToString().ToLowerInvariant()}{Environment.NewLine}");
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
