using Fadrio.Core;
using Fadrio.UI.ViewModels;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.UI.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void EmptyShellDoesNotPretendToHaveAnOutputDevice()
    {
        var viewModel = new MainWindowViewModel();
        Assert.Equal("Output control is coming soon", viewModel.EmptyOutputMessage);
        Assert.Equal("No applications are currently playing audio.", viewModel.EmptyApplicationsMessage);
        Assert.True(viewModel.HasNoApplications);
    }

    [Fact]
    public void SnapshotsReuseRowsAndNeverEmitCommandsForBackendRefresh()
    {
        int volumeCommands = 0;
        var viewModel = new MainWindowViewModel((_, _) => { volumeCommands++; return ValueTask.CompletedTask; });
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.3f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);

        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Browser", 0.7f)));

        Assert.Same(row, Assert.Single(viewModel.Applications));
        Assert.Equal("Browser", row.DisplayName);
        Assert.Equal("Browser volume", row.VolumeControlLabel);
        Assert.Equal("Mute Browser", row.MuteControlLabel);
        Assert.Equal(70, row.Volume, 1);
        Assert.Equal(0, volumeCommands);
    }

    [Fact]
    public void SliderAndMuteCommandTargetStableApplicationId()
    {
        ApplicationId? volumeId = null;
        float sentVolume = -1;
        ApplicationId? muteId = null;
        bool? sentMute = null;
        var viewModel = new MainWindowViewModel(
            (id, volume) => { volumeId = id; sentVolume = volume; return ValueTask.CompletedTask; },
            (id, muted) => { muteId = id; sentMute = muted; return ValueTask.CompletedTask; });
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);

        row.Volume = 25;
        row.ToggleMuteCommand.Execute(null);

        Assert.Equal(new ApplicationId("xdg:firefox"), volumeId);
        Assert.Equal(0.25f, sentVolume);
        Assert.Equal(new ApplicationId("xdg:firefox"), muteId);
        Assert.True(sentMute);
    }

    [Fact]
    public void DisconnectDisablesControlsAndClearsRemovedApplications()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.3f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);

        viewModel.SetBackendAvailable(false);
        viewModel.ApplySnapshot(MixerSnapshot.Empty);

        Assert.False(row.CanControl);
        Assert.True(viewModel.HasNoApplications);
        Assert.Empty(viewModel.Applications);
        Assert.Contains("reconnecting", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotOrderChangesWithoutReplacingExistingRows()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.ApplySnapshot(Snapshot(
            App("xdg:firefox", "Firefox", 0.3f),
            App("xdg:music", "Music", 0.6f)));
        ApplicationRowViewModel firefox = viewModel.Applications[0];
        ApplicationRowViewModel music = viewModel.Applications[1];

        viewModel.ApplySnapshot(Snapshot(
            App("xdg:music", "Music", 0.6f),
            App("xdg:firefox", "Firefox", 0.3f)));

        Assert.Same(music, viewModel.Applications[0]);
        Assert.Same(firefox, viewModel.Applications[1]);
    }

    [Fact]
    public void MixedSessionsShowAnHonestCombinedValue()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.2f, 0.8f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);

        Assert.True(row.IsMixedVolume);
        Assert.Equal("50% mixed", row.VolumeLabel);
    }

    private static MixerSnapshot Snapshot(params RuntimeApplication[] apps) => new(apps, 1);

    private static RuntimeApplication App(string id, string name, params float[] volumes) => new(
        new ApplicationIdentity { Id = new(id), DisplayName = name },
        volumes.Select((volume, index) => new AudioSession
        {
            Id = new($"session:{index}"),
            PipeWireNodeId = (uint)index + 1,
            Volume = volume
        }));
}
