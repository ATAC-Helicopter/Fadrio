using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Fadrio.Core;
using ApplicationId = Fadrio.Core.ApplicationId;

namespace Fadrio.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<ApplicationId, ApplicationRowViewModel> _rows = [];
    private readonly Func<ApplicationId, float, ValueTask>? _setVolume;
    private readonly Func<ApplicationId, bool, ValueTask>? _setMute;
    private string _status = UiStrings.ConnectingMessage;
    private bool _backendAvailable;
    private MixerSnapshot _latestSnapshot = MixerSnapshot.Empty;

    public MainWindowViewModel(
        Func<ApplicationId, float, ValueTask>? setVolume = null,
        Func<ApplicationId, bool, ValueTask>? setMute = null)
    {
        _setVolume = setVolume;
        _setMute = setMute;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Title => UiStrings.Title;
    public string OutputHeading => UiStrings.OutputHeading;
    public string EmptyOutputMessage => UiStrings.EmptyOutputMessage;
    public string ApplicationsHeading => UiStrings.ApplicationsHeading;
    public string EmptyApplicationsMessage => _backendAvailable
        ? UiStrings.EmptyApplicationsMessage
        : UiStrings.UnavailableApplicationsMessage;
    public ObservableCollection<ApplicationRowViewModel> Applications { get; } = [];
    public bool HasNoApplications => Applications.Count == 0;
    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public void SetBackendAvailable(bool available)
    {
        _backendAvailable = available;
        Status = available ? UiStrings.ReadyMessage : UiStrings.ReconnectingMessage;
        foreach (ApplicationRowViewModel row in Applications) row.CanControl = available;
        OnPropertyChanged(nameof(EmptyApplicationsMessage));
    }

    public void SetBackendFailed()
    {
        _backendAvailable = false;
        Status = UiStrings.BackendFailedMessage;
        foreach (ApplicationRowViewModel row in Applications) row.CanControl = false;
        OnPropertyChanged(nameof(EmptyApplicationsMessage));
    }

    public void ApplySnapshot(MixerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _latestSnapshot = snapshot;
        if (_rows.Values.Any(row => row.IsInteracting))
        {
            foreach (ApplicationRowViewModel row in Applications)
            {
                RuntimeApplication? application = snapshot.Applications.FirstOrDefault(app => app.Identity.Id == row.Id);
                if (application is null) row.CanControl = false;
                else row.Update(application, _backendAvailable);
            }
            return;
        }
        var activeIds = new HashSet<ApplicationId>();
        var ordered = new List<ApplicationRowViewModel>();
        foreach (RuntimeApplication application in snapshot.Applications
            .OrderByDescending(application => application.IsAudible)
            .ThenBy(application => application.Identity.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.Identity.Id.Value, StringComparer.Ordinal))
        {
            ApplicationId id = application.Identity.Id;
            activeIds.Add(id);
            if (!_rows.TryGetValue(id, out ApplicationRowViewModel? row))
            {
                row = new ApplicationRowViewModel(id, SendVolumeAsync, SendMuteAsync);
                row.InteractionEnded += (_, _) => ApplySnapshot(_latestSnapshot);
                _rows.Add(id, row);
            }
            row.Update(application, _backendAvailable);
            ordered.Add(row);
        }
        foreach (ApplicationId id in _rows.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _rows[id].CanControl = false;
            _rows.Remove(id);
        }
        for (int index = Applications.Count - 1; index >= 0; index--)
            if (!activeIds.Contains(Applications[index].Id)) Applications.RemoveAt(index);
        for (int index = 0; index < ordered.Count; index++)
        {
            ApplicationRowViewModel row = ordered[index];
            if (index < Applications.Count && ReferenceEquals(Applications[index], row)) continue;
            int previousIndex = Applications.IndexOf(row);
            if (previousIndex >= 0) Applications.Move(previousIndex, index);
            else Applications.Insert(index, row);
        }
        OnPropertyChanged(nameof(HasNoApplications));
    }

    private ValueTask SendVolumeAsync(ApplicationId id, float value) => _setVolume?.Invoke(id, value) ?? ValueTask.CompletedTask;
    private ValueTask SendMuteAsync(ApplicationId id, bool value) => _setMute?.Invoke(id, value) ?? ValueTask.CompletedTask;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ApplicationRowViewModel : INotifyPropertyChanged
{
    private readonly Func<ApplicationId, float, ValueTask> _setVolume;
    private readonly Func<ApplicationId, bool, ValueTask> _setMute;
    private string _displayName = string.Empty;
    private double _volume;
    private bool _isMuted;
    private bool _isMixedVolume;
    private bool _canControl;
    private bool _updating;
    private bool _isInteracting;
    private bool _changedDuringInteraction;
    private bool _preserveVolumeOnNextUpdate;
    private RuntimeApplication? _latestApplication;
    private float? _pendingVolume;
    private bool? _pendingMute;
    private bool _sendingVolume;
    private bool _sendingMute;
    private long _commandEpoch;
    private Task _volumeTask = Task.CompletedTask;
    private Task _muteTask = Task.CompletedTask;
    private string _commandError = string.Empty;

    internal ApplicationRowViewModel(ApplicationId id, Func<ApplicationId, float, ValueTask> setVolume,
        Func<ApplicationId, bool, ValueTask> setMute)
    {
        Id = id;
        _setVolume = setVolume;
        _setMute = setMute;
        ToggleMuteCommand = new RelayCommand(() => _ = ToggleMuteAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? InteractionEnded;
    public ApplicationId Id { get; }
    public ICommand ToggleMuteCommand { get; }
    public string DisplayName
    {
        get => _displayName;
        private set
        {
            if (SetField(ref _displayName, value))
            {
                OnPropertyChanged(nameof(VolumeControlLabel));
                OnPropertyChanged(nameof(MuteControlLabel));
            }
        }
    }
    public double Volume
    {
        get => _volume;
        set
        {
            double bounded = Math.Clamp(value, 0, 100);
            if (Math.Abs(_volume - bounded) < 0.01) return;
            _volume = bounded;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeLabel));
            if (!_updating && CanControl)
            {
                IsMixedVolume = false;
                _changedDuringInteraction |= IsInteracting;
                _pendingVolume = (float)(bounded / 100);
                CommandError = string.Empty;
                if (!_sendingVolume) _volumeTask = SendVolumesAsync();
            }
        }
    }
    public string VolumeLabel => IsMixedVolume ? $"{Volume:0}% mixed" : $"{Volume:0}%";
    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetField(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteLabel));
                OnPropertyChanged(nameof(MuteControlLabel));
            }
        }
    }
    public bool IsMixedVolume { get => _isMixedVolume; private set { if (SetField(ref _isMixedVolume, value)) OnPropertyChanged(nameof(VolumeLabel)); } }
    public bool CanControl
    {
        get => _canControl;
        internal set
        {
            if (SetField(ref _canControl, value) && !value)
            {
                _commandEpoch++;
                _pendingVolume = null;
                _pendingMute = null;
            }
        }
    }
    public bool IsInteracting => _isInteracting;
    public string CommandError
    {
        get => _commandError;
        private set
        {
            if (SetField(ref _commandError, value)) OnPropertyChanged(nameof(HasCommandError));
        }
    }
    public bool HasCommandError => CommandError.Length > 0;
    public string MuteLabel => IsMuted ? UiStrings.UnmuteAction : UiStrings.MuteAction;
    public string VolumeControlLabel => $"{DisplayName} volume";
    public string MuteControlLabel => $"{MuteLabel} {DisplayName}";

    internal void Update(RuntimeApplication application, bool canControl)
    {
        _latestApplication = application;
        _updating = true;
        try
        {
            DisplayName = application.Identity.DisplayName;
            if (!IsInteracting && !_sendingVolume && !_preserveVolumeOnNextUpdate)
            {
                Volume = application.EffectiveVolume * 100;
                IsMixedVolume = application.IsMixedVolume;
            }
            if (!IsInteracting) _preserveVolumeOnNextUpdate = false;
            if (!_sendingMute) IsMuted = application.IsMuted;
            CanControl = canControl;
        }
        finally { _updating = false; }
    }

    public void BeginInteraction()
    {
        if (!CanControl || _isInteracting) return;
        _isInteracting = true;
        _changedDuringInteraction = false;
    }

    public void EndInteraction()
    {
        if (!_isInteracting) return;
        _isInteracting = false;
        _preserveVolumeOnNextUpdate = _changedDuringInteraction;
        InteractionEnded?.Invoke(this, EventArgs.Empty);
    }

    public Task WaitForCommandsAsync() => Task.WhenAll(_volumeTask, _muteTask);

    private async Task SendVolumesAsync()
    {
        _sendingVolume = true;
        try
        {
            while (CanControl && _pendingVolume is float value)
            {
                _pendingVolume = null;
                long epoch = _commandEpoch;
                try { await _setVolume(Id, value); }
                catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
                {
                    if (epoch == _commandEpoch)
                    {
                        CommandError = UiStrings.CommandFailedMessage;
                        _pendingVolume = null;
                        _changedDuringInteraction = false;
                        if (!IsInteracting && _latestApplication is not null)
                        {
                            _updating = true;
                            try
                            {
                                Volume = _latestApplication.EffectiveVolume * 100;
                                IsMixedVolume = _latestApplication.IsMixedVolume;
                            }
                            finally { _updating = false; }
                        }
                    }
                }
            }
        }
        finally { _sendingVolume = false; }
    }

    private Task ToggleMuteAsync()
    {
        if (!CanControl) return Task.CompletedTask;
        IsMuted = !IsMuted;
        _pendingMute = IsMuted;
        CommandError = string.Empty;
        if (!_sendingMute) _muteTask = SendMutesAsync();
        return _muteTask;
    }

    private async Task SendMutesAsync()
    {
        _sendingMute = true;
        try
        {
            while (CanControl && _pendingMute is bool value)
            {
                _pendingMute = null;
                long epoch = _commandEpoch;
                try { await _setMute(Id, value); }
                catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
                {
                    if (epoch == _commandEpoch)
                    {
                        CommandError = UiStrings.CommandFailedMessage;
                        _pendingMute = null;
                        IsMuted = _latestApplication?.IsMuted ?? false;
                    }
                }
            }
        }
        finally { _sendingMute = false; }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}

internal static class UiStrings
{
    internal const string Title = "Fadrio";
    internal const string OutputHeading = "OUTPUT";
    internal const string EmptyOutputMessage = "Output control is coming soon";
    internal const string ApplicationsHeading = "APPLICATIONS";
    internal const string EmptyApplicationsMessage = "No applications are currently playing audio.";
    internal const string UnavailableApplicationsMessage = "Applications will appear when audio reconnects.";
    internal const string CommandFailedMessage = "Could not apply the change. Try again when audio is available.";
    internal const string ConnectingMessage = "Connecting to audio…";
    internal const string ReconnectingMessage = "Audio unavailable — reconnecting…";
    internal const string ReadyMessage = "Connected";
    internal const string BackendFailedMessage = "Audio backend could not start";
    internal const string MuteAction = "Mute";
    internal const string UnmuteAction = "Unmute";
}
