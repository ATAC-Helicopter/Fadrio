using Avalonia.Threading;
using Fadrio.Application;
using Fadrio.Core;
using Fadrio.Infrastructure;
using Fadrio.NativeInterop;
using Fadrio.Platform.Linux;
using Fadrio.UI.ViewModels;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.UI;

internal sealed class MixerUiSession : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ReconnectingAudioBackend _backend = new(() => new NativeAudioBackend());
    private readonly XdgDesktopApplicationIndex _desktopIndex = new();
    private readonly MixerStateCoordinator _coordinator;
    private readonly MixerCommands _commands;
    private readonly MainWindowViewModel _viewModel;
    private Task? _runTask;

    public MixerUiSession(MainWindowViewModel viewModel, FadrioDatabase database)
    {
        _viewModel = viewModel;
        IApplicationResolver resolver = new ApplicationResolver(new LinuxProcMetadataProvider(), _desktopIndex,
            new SteamApplicationResolver(new SteamApplicationIndex()),
            new FlatpakApplicationResolver(_desktopIndex), new SnapApplicationResolver(_desktopIndex));
        resolver = new PersistentApplicationResolver(resolver, new ApplicationIdentityStore(database));
        _coordinator = new MixerStateCoordinator(resolver);
        _commands = new MixerCommands(_backend, _coordinator);
    }

    public void Start() => _runTask ??= Task.Run(RunAsync);

    public ValueTask SetVolumeAsync(ApplicationId id, float volume) =>
        _commands.SetApplicationVolumeAsync(id, volume, _shutdown.Token);

    public ValueTask SetMuteAsync(ApplicationId id, bool muted) =>
        _commands.SetApplicationMuteAsync(id, muted, _shutdown.Token);

    private async Task RunAsync()
    {
        try
        {
            await foreach (AudioBackendEvent backendEvent in _backend.WatchAsync(_shutdown.Token))
            {
                await _coordinator.ApplyAsync(backendEvent, _shutdown.Token);
                MixerSnapshot snapshot = _coordinator.Current;
                bool? available = backendEvent switch
                {
                    BackendReady => true,
                    BackendDisconnected => false,
                    _ => null
                };
                Dispatcher.UIThread.Post(() =>
                {
                    if (available.HasValue) _viewModel.SetBackendAvailable(available.Value);
                    _viewModel.ApplySnapshot(snapshot);
                });
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or DllNotFoundException)
        {
            Dispatcher.UIThread.Post(_viewModel.SetBackendFailed);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        if (_runTask is not null) await _runTask;
        await _backend.DisposeAsync();
        _desktopIndex.Dispose();
        _shutdown.Dispose();
    }
}
