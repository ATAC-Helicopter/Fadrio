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
}
