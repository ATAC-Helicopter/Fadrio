using Fadrio.Application;
using Fadrio.Core;
using Fadrio.Infrastructure;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.Platform.Linux.Tests;

public sealed class ApplicationResolverTests
{
    private static string DesktopFixtures => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures/desktop-files"));

    [Fact]
    public async Task ResolvesNormalNativeExecutableToDesktopEntry()
    {
        var resolver = Resolver(new ProcessMetadata(42, "/usr/lib/firefox/firefox", ["firefox"]));
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(processId: 42), TestContext.Current.CancellationToken);
        Assert.Equal(new ApplicationId("xdg:firefox"), identity.Id);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
    }

    [Fact]
    public async Task MissingProcProcessFallsBackToPipeWireIdentity()
    {
        var resolver = Resolver(process: null);
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(processId: 999, applicationId: "org.example.Player"), TestContext.Current.CancellationToken);
        Assert.Equal(new ApplicationId("pipewire:org.example.player"), identity.Id);
        Assert.DoesNotContain("999", identity.Id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessDisappearingDuringLookupIsNormal()
    {
        var resolver = new ApplicationResolver(new VanishingProcessProvider(), new EmptyDesktopIndex());
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(processId: 15, applicationId: null), TestContext.Current.CancellationToken);
        Assert.StartsWith("unknown:", identity.Id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguousDesktopEntryUsesExecutableIdentity()
    {
        var resolver = new ApplicationResolver(
            new FixedProcessProvider(new(7, "/usr/bin/player", [])),
            new AmbiguousDesktopIndex());
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(processId: 7), TestContext.Current.CancellationToken);
        Assert.Equal(new ApplicationId("exe:/usr/bin/player"), identity.Id);
        Assert.Contains(identity.Evidence, evidence => evidence.Kind == IdentityEvidenceKind.AmbiguousDesktopEntry);
        IdentityEvidence conflict = Assert.Single(identity.Evidence,
            evidence => evidence.Kind == IdentityEvidenceKind.ConflictingEvidence);
        Assert.Equal("one=100, two=100", conflict.Value);
    }

    [Fact]
    public async Task UnmatchedExecutableHasStableExecutableIdentity()
    {
        var resolver = new ApplicationResolver(
            new FixedProcessProvider(new(8, "/opt/example/audio-player", [])),
            new EmptyDesktopIndex());
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(processId: 8), TestContext.Current.CancellationToken);
        Assert.Equal(new ApplicationId("exe:/opt/example/audio-player"), identity.Id);
        Assert.Equal("audio-player", identity.DisplayName);
    }

    [Fact]
    public async Task ExactDesktopIdHintCorroboratesWrapperExecutable()
    {
        var resolver = new ApplicationResolver(
            new FixedProcessProvider(new(9, "/opt/brave.com/brave/brave", ["brave", "--type=utility"])),
            new XdgDesktopApplicationIndex([DesktopFixtures]));
        AudioSession session = Session(processId: 9, applicationId: null) with
        {
            ApplicationName = "Brave",
            ApplicationIconName = "brave-browser",
            ProcessBinary = "brave"
        };

        ApplicationIdentity identity = await resolver.ResolveAsync(session, TestContext.Current.CancellationToken);

        Assert.Equal(new ApplicationId("xdg:brave-browser"), identity.Id);
        Assert.Equal("Brave Web Browser", identity.DisplayName);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
        Assert.Contains(identity.Evidence, evidence =>
            evidence.Kind == IdentityEvidenceKind.DesktopEntry && evidence.Value == "brave-browser");
    }

    [Fact]
    public async Task AgreeingExecutableAndApplicationIdStrengthenDesktopIdentity()
    {
        var resolver = Resolver(new ProcessMetadata(10, "/opt/discord/discord", ["discord"]));
        AudioSession session = Session(processId: 10, applicationId: "conflict-discord") with
        {
            ApplicationName = "Discord",
            ProcessBinary = "discord"
        };

        ApplicationIdentity identity = await resolver.ResolveAsync(session, TestContext.Current.CancellationToken);

        Assert.Equal(new ApplicationId("xdg:conflict-discord"), identity.Id);
        Assert.Equal("Discord", identity.DisplayName);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
        Assert.True(identity.Evidence.Count(item =>
            item.Kind == IdentityEvidenceKind.DesktopEntry && item.Value == "conflict-discord") >= 2);
    }

    [Fact]
    public async Task ConflictingStrongEvidenceFallsBackWithoutGuessing()
    {
        var resolver = Resolver(new ProcessMetadata(11, "/opt/slack/slack", ["slack"]));
        AudioSession session = Session(processId: 11, applicationId: "conflict-discord") with
        {
            ApplicationName = "Discord",
            ProcessBinary = null
        };

        ApplicationIdentity identity = await resolver.ResolveAsync(session, TestContext.Current.CancellationToken);

        Assert.Equal(new ApplicationId("exe:/opt/slack/slack"), identity.Id);
        Assert.Equal(IdentityConfidence.Medium, identity.Confidence);
        IdentityEvidence conflict = Assert.Single(identity.Evidence,
            item => item.Kind == IdentityEvidenceKind.ConflictingEvidence);
        Assert.Equal("conflict-slack=100, conflict-discord=85", conflict.Value);
    }

    [Fact]
    public async Task UnknownIdentityHashDoesNotDependOnPid()
    {
        var resolver = Resolver(process: null);
        ApplicationIdentity first = await resolver.ResolveAsync(Session(processId: 1, applicationId: null), TestContext.Current.CancellationToken);
        ApplicationIdentity second = await resolver.ResolveAsync(Session(processId: 2, applicationId: null), TestContext.Current.CancellationToken);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task ElectronAudioHelperResolvesToInstalledApplicationDespiteGenericMetadata()
    {
        var resolver = Resolver(new ProcessMetadata(42, "/usr/share/code/code", ["--type=utility"]));
        ApplicationIdentity identity = await resolver.ResolveAsync(Session(42, null) with
        {
            ApplicationName = "Chromium",
            ProcessBinary = "code",
            ApplicationIconName = "chromium-browser"
        }, TestContext.Current.CancellationToken);
        Assert.Equal("xdg:code", identity.Id.Value);
        Assert.Equal("Visual Studio Code", identity.DisplayName);
        Assert.Equal("vscode", identity.Icon?.Value);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
        Assert.Contains(identity.Evidence, item => item.Kind == IdentityEvidenceKind.ProcessExecutable);
        Assert.Contains(identity.Evidence, item => item.Kind == IdentityEvidenceKind.DesktopEntry);
        var coordinator = new MixerStateCoordinator(resolver);
        await coordinator.ApplyAsync(new SessionAdded(1, Session(42, null) with { Id = new("electron-one") }),
            TestContext.Current.CancellationToken);
        await coordinator.ApplyAsync(new SessionAdded(1, Session(43, null) with { Id = new("electron-two"), PipeWireNodeId = 82 }),
            TestContext.Current.CancellationToken);
        RuntimeApplication application = Assert.Single(coordinator.Current.Applications);
        Assert.Equal("xdg:code", application.Identity.Id.Value);
        Assert.Equal(2, application.Sessions.Count);
    }

    private static ApplicationResolver Resolver(ProcessMetadata? process) => new(
        new FixedProcessProvider(process), new XdgDesktopApplicationIndex([DesktopFixtures]));

    private static AudioSession Session(int? processId, string? applicationId = "firefox") => new()
    {
        Id = new("fixture"),
        PipeWireNodeId = 81,
        ProcessId = processId,
        ApplicationId = applicationId,
        ApplicationName = "Firefox",
        MediaName = "Fixture audio",
        Volume = 0.5f,
        Active = true
    };

    private sealed class FixedProcessProvider(ProcessMetadata? process) : IProcessMetadataProvider
    {
        public ValueTask<ProcessMetadata?> GetAsync(int processId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(process);
    }

    private sealed class VanishingProcessProvider : IProcessMetadataProvider
    {
        public ValueTask<ProcessMetadata?> GetAsync(int processId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProcessMetadata?>(null);
    }

    private sealed class EmptyDesktopIndex : IDesktopApplicationIndex
    {
        public IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath) => [];
        public IReadOnlyList<DesktopApplicationEntry> FindById(string desktopFileId) => [];
    }

    private sealed class AmbiguousDesktopIndex : IDesktopApplicationIndex
    {
        public IReadOnlyList<DesktopApplicationEntry> FindByExecutable(string executablePath) =>
        [
            new("one", "One", executablePath, null, null, false, false, "one.desktop"),
            new("two", "Two", executablePath, null, null, false, false, "two.desktop")
        ];

        public IReadOnlyList<DesktopApplicationEntry> FindById(string desktopFileId) => [];
    }
}
