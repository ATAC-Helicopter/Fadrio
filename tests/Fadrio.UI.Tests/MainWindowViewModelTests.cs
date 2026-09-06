using Fadrio.UI.ViewModels;

namespace Fadrio.UI.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ProductionShellStartsWithHonestEmptyStates()
    {
        var viewModel = new MainWindowViewModel();
        Assert.Equal("No output available", viewModel.EmptyOutputMessage);
        Assert.Equal("No applications are currently playing audio.", viewModel.EmptyApplicationsMessage);
    }
}
