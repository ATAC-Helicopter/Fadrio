using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Fadrio.UI.ViewModels;

namespace Fadrio.UI.Controls;

// Presentation-only gesture adapter. Audio commands remain in the view model.
public sealed class ApplicationVolumeSlider : Slider
{
    private readonly HashSet<Key> _pressedKeys = [];
    private ApplicationRowViewModel? _interactionRow;
    private bool _pointerActive;

    public ApplicationVolumeSlider()
    {
        AddHandler(PointerPressedEvent, (_, args) =>
        {
            if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Focus();
            _pointerActive = true;
            Begin();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, _) => EndPointer(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => EndPointer(), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, args) =>
        {
            if (!IsVolumeKey(args.Key)) return;
            _pressedKeys.Add(args.Key);
            Begin();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, args) =>
        {
            _pressedKeys.Remove(args.Key);
            EndIfIdle();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        LostFocus += (_, _) =>
        {
            _pressedKeys.Clear();
            EndIfIdle();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _pointerActive = false;
            _pressedKeys.Clear();
            EndIfIdle();
        };
    }

    protected override Type StyleKeyOverride => typeof(Slider);

    private void Begin()
    {
        if (_interactionRow is not null || !IsEnabled) return;
        _interactionRow = DataContext as ApplicationRowViewModel;
        _interactionRow?.BeginInteraction();
    }

    private void EndPointer()
    {
        _pointerActive = false;
        EndIfIdle();
    }

    private void EndIfIdle()
    {
        if (_pointerActive || _pressedKeys.Count > 0) return;
        ApplicationRowViewModel? row = _interactionRow;
        _interactionRow = null;
        row?.EndInteraction();
    }

    private static bool IsVolumeKey(Key key) => key is
        Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
}
