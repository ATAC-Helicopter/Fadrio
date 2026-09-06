using Avalonia;
using Avalonia.Styling;
using AvaloniaApplication = Avalonia.Application;

namespace Fadrio.UI;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public static class ThemePreferenceService
{
    public static void Apply(AvaloniaApplication application, ThemePreference preference)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
