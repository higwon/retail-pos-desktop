using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Tests;

public sealed class WorkflowWindowHostTests
{
    [Fact]
    public void RepeatedShowReusesVisibleWindowAndCloseAllowsNewInstance()
    {
        var factory = new FakeFactory(); using var host = new WorkflowWindowHost<FakeWindow>(factory.Create);
        host.ShowOrActivate(); host.ShowOrActivate();
        Assert.Equal(1, factory.Count); Assert.Equal(1, factory.Last.Activations);
        factory.Last.Close(); host.ShowOrActivate();
        Assert.Equal(2, factory.Count);
    }

    [Fact]
    public void DisposeClosesOwnedWindow()
    {
        var factory = new FakeFactory(); var host = new WorkflowWindowHost<FakeWindow>(factory.Create);
        var closedEvents = 0; host.WindowClosed += (_, _) => closedEvents++;
        host.ShowOrActivate(); host.Dispose();
        Assert.False(factory.Last.IsVisible);
        Assert.Equal(0, closedEvents);
    }

    private sealed class FakeFactory
    {
        public int Count { get; private set; } public FakeWindow Last { get; private set; } = null!;
        public FakeWindow Create() { Count++; return Last = new(); }
    }
    private sealed class FakeWindow : IWorkflowWindow
    {
        public bool IsVisible { get; private set; } public int Activations { get; private set; } public event EventHandler? Closed;
        public void Show() => IsVisible = true; public void Activate() => Activations++;
        public void Close() { if (!IsVisible) return; IsVisible = false; Closed?.Invoke(this, EventArgs.Empty); }
    }
}
