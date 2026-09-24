using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fadrio.Infrastructure;
using Fadrio.UI.ViewModels;

namespace Fadrio.UI;

public sealed partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var database = new FadrioDatabase(FadrioDataPaths.Resolve().DatabasePath);
            MixerUiSession? session = null;
            var viewModel = new MainWindowViewModel(
                (id, volume) => session!.SetVolumeAsync(id, volume),
                (id, muted) => session!.SetMuteAsync(id, muted));
            session = new MixerUiSession(viewModel, database);
            var window = new MainWindow
            {
                DataContext = viewModel
            };
            window.Opened += (_, _) => session.Start();
            window.Closed += async (_, _) => await session.DisposeAsync();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
