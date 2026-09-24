using Avalonia;
using Fadrio.Infrastructure;

namespace Fadrio.UI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        new FadrioDatabase(FadrioDataPaths.Resolve().DatabasePath).Initialize();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
