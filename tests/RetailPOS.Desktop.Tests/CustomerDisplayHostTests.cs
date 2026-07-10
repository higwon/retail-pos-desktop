using RetailPOS.Desktop.DeviceSimulation;
using RetailPOS.Desktop.ViewModels;
using System.Drawing;

namespace RetailPOS.Desktop.Tests;

public sealed class CustomerDisplayHostTests
{
    [Fact]
    public void RepeatedOpenReusesWindowAndPrimaryIsRejected()
    {
        var provider = new StubProvider(); var factory = new WindowFactory(); using var host = new CustomerDisplayHost(provider, factory.Create);
        host.Open("primary"); Assert.Equal(0, factory.Count);
        host.Open("secondary"); host.Open("secondary");
        Assert.Equal(1, factory.Count); Assert.Equal(1, factory.Window.Activations); Assert.True(host.IsOpen);
    }

    [Fact]
    public void SelectedMonitorDisconnectClosesWindowAndRequiresSelection()
    {
        var provider = new StubProvider(); var factory = new WindowFactory(); using var host = new CustomerDisplayHost(provider, factory.Create);
        host.Open("secondary"); provider.IncludeSecondary = false; host.RefreshTargets();
        Assert.False(host.IsOpen); Assert.Null(host.SelectedTargetId); Assert.Contains("disconnected", host.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenOnDifferentTargetMovesExistingWindowAndKeepsSelectionAligned()
    {
        var provider = new StubProvider { IncludeThird = true }; var factory = new WindowFactory(); using var host = new CustomerDisplayHost(provider, factory.Create);
        host.Open("secondary"); host.Open("third");
        Assert.Equal(1, factory.Count); Assert.Equal("third", factory.Window.Target?.Id); Assert.Equal("third", host.SelectedTargetId);
    }

    [Fact]
    public void ViewModelAllowsMovingOpenWindowToDifferentSelectedTarget()
    {
        var provider = new StubProvider { IncludeThird = true }; var factory = new WindowFactory(); using var host = new CustomerDisplayHost(provider, factory.Create); using var viewModel = new CustomerDisplayHostViewModel(host);
        viewModel.SelectedTarget = viewModel.Targets.Single(target => target.Id == "secondary");
        viewModel.OpenCommand.Execute(null);
        Assert.False(viewModel.OpenCommand.CanExecute(null));

        viewModel.SelectedTarget = viewModel.Targets.Single(target => target.Id == "third");
        Assert.True(viewModel.OpenCommand.CanExecute(null));
        viewModel.OpenCommand.Execute(null);

        Assert.Equal(1, factory.Count); Assert.Equal("third", factory.Window.Target?.Id); Assert.Equal("third", host.SelectedTargetId);
    }

    private sealed class StubProvider : IDisplayTargetProvider
    {
        public bool IncludeSecondary { get; set; } = true;
        public bool IncludeThird { get; set; }
        public IReadOnlyList<DisplayTarget> GetTargets()
        {
            var targets = new List<DisplayTarget> { new("primary", "Primary", new(0,0,100,90), true) };
            if (IncludeSecondary) targets.Add(new("secondary", "Secondary", new(100,0,100,90), false));
            if (IncludeThird) targets.Add(new("third", "Third", new(200,0,100,90), false));
            return targets;
        }
    }
    private sealed class WindowFactory
    {
        public int Count { get; private set; } public FakeWindow Window { get; } = new();
        public ICustomerDisplayWindow Create() { Count++; return Window; }
    }
    private sealed class FakeWindow : ICustomerDisplayWindow
    {
        public bool IsVisible { get; private set; } public int Activations { get; private set; } public DisplayTarget? Target { get; private set; } public event EventHandler? Closed;
        public void ShowOn(DisplayTarget target) { IsVisible = true; Target = target; } public void MoveTo(DisplayTarget target) => Target = target; public void Activate() => Activations++;
        public void Close() { IsVisible = false; Closed?.Invoke(this, EventArgs.Empty); }
    }
}
