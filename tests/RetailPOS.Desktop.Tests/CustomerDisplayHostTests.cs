using RetailPOS.Desktop.DeviceSimulation;
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

    private sealed class StubProvider : IDisplayTargetProvider
    {
        public bool IncludeSecondary { get; set; } = true;
        public IReadOnlyList<DisplayTarget> GetTargets() => IncludeSecondary
            ? [new("primary", "Primary", new(0,0,100,100), true), new("secondary", "Secondary", new(100,0,100,100), false)]
            : [new("primary", "Primary", new(0,0,100,100), true)];
    }
    private sealed class WindowFactory
    {
        public int Count { get; private set; } public FakeWindow Window { get; } = new();
        public ICustomerDisplayWindow Create() { Count++; return Window; }
    }
    private sealed class FakeWindow : ICustomerDisplayWindow
    {
        public bool IsVisible { get; private set; } public int Activations { get; private set; } public event EventHandler? Closed;
        public void ShowOn(DisplayTarget target) => IsVisible = true; public void Activate() => Activations++;
        public void Close() { IsVisible = false; Closed?.Invoke(this, EventArgs.Empty); }
    }
}
