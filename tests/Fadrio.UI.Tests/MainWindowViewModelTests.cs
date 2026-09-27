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
        Assert.Contains("reconnects", viewModel.EmptyApplicationsMessage, StringComparison.Ordinal);
        viewModel.SetBackendAvailable(true);
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
            App("xdg:music", "A Music", 0.6f),
            App("xdg:firefox", "Firefox", 0.3f)));

        Assert.Same(music, viewModel.Applications[0]);
        Assert.Same(firefox, viewModel.Applications[1]);
    }

    [Fact]
    public void AudibleRowsSortFirstWithDeterministicAlphabeticalFallback()
    {
        var viewModel = new MainWindowViewModel();
        RuntimeApplication audible = App("xdg:z", "Zebra", 0.5f);
        audible = new RuntimeApplication(audible.Identity, audible.Sessions.Select(session => session with { Active = true }));
        viewModel.ApplySnapshot(Snapshot(App("xdg:b", "Beta", 0.5f), audible, App("xdg:a", "Alpha", 0.5f)));

        Assert.Equal(["Zebra", "Alpha", "Beta"], viewModel.Applications.Select(row => row.DisplayName));
    }

    [Fact]
    public void DragFreezesOrderAndVolumeUntilReleaseWithoutLosingNewRows()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:b", "Beta", 0.5f), App("xdg:c", "Charlie", 0.5f)));
        ApplicationRowViewModel row = viewModel.Applications[0];
        row.BeginInteraction();
        row.Volume = 25;

        viewModel.ApplySnapshot(Snapshot(App("xdg:b", "Zulu", 0.9f), App("xdg:a", "Alpha", 0.4f)));

        Assert.Same(row, viewModel.Applications[0]);
        Assert.Equal(25, row.Volume);
        Assert.Equal(2, viewModel.Applications.Count);
        row.EndInteraction();
        Assert.Equal(["Alpha", "Zulu"], viewModel.Applications.Select(candidate => candidate.DisplayName));
        Assert.Equal(25, row.Volume);
        viewModel.ApplySnapshot(Snapshot(App("xdg:b", "Zulu", 0.3f)));
        Assert.Equal(30, row.Volume, 1);
    }

    [Fact]
    public void RemovedRowDisablesImmediatelyButStaysUnderPointerUntilRelease()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.BeginInteraction();

        viewModel.ApplySnapshot(MixerSnapshot.Empty);

        Assert.False(row.CanControl);
        Assert.Same(row, Assert.Single(viewModel.Applications));
        row.EndInteraction();
        Assert.Empty(viewModel.Applications);
    }

    [Fact]
    public async Task VolumeCommandsAreSerializedAndKeepOnlyNewestQueuedValue()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new List<float>();
        var viewModel = new MainWindowViewModel(async (_, volume) =>
        {
            sent.Add(volume);
            if (sent.Count == 1) await gate.Task;
        });
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.Volume = 10;
        row.Volume = 20;
        row.Volume = 30;
        Assert.Single(sent);

        gate.SetResult();
        await row.WaitForCommandsAsync();

        Assert.Equal([0.1f, 0.3f], sent);
    }

    [Fact]
    public async Task DisconnectDiscardsQueuedVolumeRatherThanReplayingItAfterReconnect()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new List<float>();
        var viewModel = new MainWindowViewModel(async (_, volume) => { sent.Add(volume); await gate.Task; });
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.Volume = 10;
        row.Volume = 20;
        viewModel.SetBackendAvailable(false);
        viewModel.SetBackendAvailable(true);
        gate.SetResult();
        await row.WaitForCommandsAsync();

        Assert.Equal([0.1f], sent);
    }

    [Fact]
    public async Task RapidMuteClicksToggleUserIntentRatherThanStaleBackendState()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new List<bool>();
        var viewModel = new MainWindowViewModel(setMute: async (_, muted) =>
        {
            sent.Add(muted);
            if (sent.Count == 1) await gate.Task;
        });
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.ToggleMuteCommand.Execute(null);
        row.ToggleMuteCommand.Execute(null);
        Assert.False(row.IsMuted);
        gate.SetResult();
        await row.WaitForCommandsAsync();

        Assert.Equal([true, false], sent);
    }

    [Fact]
    public async Task CommandFailureIsVisibleAndMuteRevertsToObservedState()
    {
        var viewModel = new MainWindowViewModel(setMute: (_, _) => ValueTask.FromException(new IOException("fixture")));
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.ToggleMuteCommand.Execute(null);
        await row.WaitForCommandsAsync();

        Assert.True(row.HasCommandError);
        Assert.False(row.IsMuted);
        Assert.DoesNotContain("fixture", row.CommandError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedVolumeCommandRestoresObservedValueWithoutAnotherCommand()
    {
        int attempts = 0;
        var viewModel = new MainWindowViewModel((_, _) =>
        {
            attempts++;
            return ValueTask.FromException(new IOException("fixture"));
        });
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.Volume = 25;
        await row.WaitForCommandsAsync();

        Assert.Equal(50, row.Volume);
        Assert.Equal(1, attempts);
        Assert.True(row.HasCommandError);
    }

    [Fact]
    public void RowChangesNotifyTheSpecificBoundProperties()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        var notifications = new List<string?>();
        row.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        row.Volume = 25;
        row.ToggleMuteCommand.Execute(null);

        Assert.Contains(nameof(row.Volume), notifications);
        Assert.Contains(nameof(row.VolumeLabel), notifications);
        Assert.Contains(nameof(row.IsMuted), notifications);
        Assert.Contains(nameof(row.MuteLabel), notifications);
        Assert.DoesNotContain(null, notifications);
    }

    [Fact]
    public async Task FailureDuringDragRestoresObservedVolumeOnRelease()
    {
        var viewModel = new MainWindowViewModel((_, _) => ValueTask.FromException(new IOException("fixture")));
        viewModel.SetBackendAvailable(true);
        viewModel.ApplySnapshot(Snapshot(App("xdg:firefox", "Firefox", 0.5f)));
        ApplicationRowViewModel row = Assert.Single(viewModel.Applications);
        row.BeginInteraction();
        row.Volume = 25;
        await row.WaitForCommandsAsync();
        Assert.Equal(25, row.Volume);

        row.EndInteraction();

        Assert.Equal(50, row.Volume);
        Assert.True(row.HasCommandError);
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
