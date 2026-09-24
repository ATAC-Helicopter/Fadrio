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
    public string EmptyApplicationsMessage => UiStrings.EmptyApplicationsMessage;
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
    }

    public void SetBackendFailed()
    {
        _backendAvailable = false;
        Status = UiStrings.BackendFailedMessage;
        foreach (ApplicationRowViewModel row in Applications) row.CanControl = false;
    }

    public void ApplySnapshot(MixerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var activeIds = new HashSet<ApplicationId>();
        var ordered = new List<ApplicationRowViewModel>();
        foreach (RuntimeApplication application in snapshot.Applications)
        {
            ApplicationId id = application.Identity.Id;
            activeIds.Add(id);
            if (!_rows.TryGetValue(id, out ApplicationRowViewModel? row))
            {
                row = new ApplicationRowViewModel(id, SendVolumeAsync, SendMuteAsync);
                _rows.Add(id, row);
            }
            row.Update(application, _backendAvailable);
            ordered.Add(row);
        }
        foreach (ApplicationId id in _rows.Keys.Where(id => !activeIds.Contains(id)).ToArray()) _rows.Remove(id);
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

    internal ApplicationRowViewModel(ApplicationId id, Func<ApplicationId, float, ValueTask> setVolume,
        Func<ApplicationId, bool, ValueTask> setMute)
    {
        Id = id;
        _setVolume = setVolume;
        _setMute = setMute;
        ToggleMuteCommand = new RelayCommand(() => _ = ToggleMuteAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
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
            if (!_updating && CanControl) _ = SetVolumeAsync((float)(bounded / 100));
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
    public bool CanControl { get => _canControl; internal set => SetField(ref _canControl, value); }
    public string MuteLabel => IsMuted ? UiStrings.UnmuteAction : UiStrings.MuteAction;
    public string VolumeControlLabel => $"{DisplayName} volume";
    public string MuteControlLabel => $"{MuteLabel} {DisplayName}";

    internal void Update(RuntimeApplication application, bool canControl)
    {
        _updating = true;
        try
        {
            DisplayName = application.Identity.DisplayName;
            Volume = application.EffectiveVolume * 100;
            IsMixedVolume = application.IsMixedVolume;
            IsMuted = application.IsMuted;
            CanControl = canControl;
        }
        finally { _updating = false; }
    }

    private async Task SetVolumeAsync(float value)
    {
        try { await _setVolume(Id, value); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            // A stream can disappear, or the backend can stop, during a drag.
        }
    }

    private async Task ToggleMuteAsync()
    {
        if (!CanControl) return;
        try { await _setMute(Id, !IsMuted); }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            // A disappearing stream is normal.
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
    private void OnPropertyChanged(string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
    internal const string ConnectingMessage = "Connecting to audio…";
    internal const string ReconnectingMessage = "Audio unavailable — reconnecting…";
    internal const string ReadyMessage = "Connected";
    internal const string BackendFailedMessage = "Audio backend could not start";
    internal const string MuteAction = "Mute";
    internal const string UnmuteAction = "Unmute";
}
