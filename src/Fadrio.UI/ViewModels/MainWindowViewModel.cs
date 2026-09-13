namespace Fadrio.UI.ViewModels;

public sealed class MainWindowViewModel
{
    public string Title => UiStrings.Title;
    public string OutputHeading => UiStrings.OutputHeading;
    public string EmptyOutputMessage => UiStrings.EmptyOutputMessage;
    public string ApplicationsHeading => UiStrings.ApplicationsHeading;
    public string EmptyApplicationsMessage => UiStrings.EmptyApplicationsMessage;
}

internal static class UiStrings
{
    internal const string Title = "Fadrio";
    internal const string OutputHeading = "OUTPUT";
    internal const string EmptyOutputMessage = "No output available";
    internal const string ApplicationsHeading = "APPLICATIONS";
    internal const string EmptyApplicationsMessage = "No applications are currently playing audio.";
}
