using RetailPOS.Desktop.Views;
using System.Drawing;
using System.Windows.Forms;

namespace RetailPOS.Desktop.DeviceSimulation;

public sealed record DisplayTarget(string Id, string Name, Rectangle Bounds, bool IsPrimary);

public interface IDisplayTargetProvider { IReadOnlyList<DisplayTarget> GetTargets(); }

public sealed class WindowsDisplayTargetProvider : IDisplayTargetProvider
{
    public IReadOnlyList<DisplayTarget> GetTargets() => Screen.AllScreens
        .Select(screen => new DisplayTarget(screen.DeviceName, screen.DeviceName, screen.Bounds, screen.Primary))
        .ToArray();
}

public interface ICustomerDisplayWindow
{
    bool IsVisible { get; }
    event EventHandler? Closed;
    void ShowOn(DisplayTarget target);
    void Activate();
    void Close();
}

public sealed class CustomerDisplayHost(
    IDisplayTargetProvider targetProvider,
    Func<ICustomerDisplayWindow> windowFactory) : IDisposable
{
    private ICustomerDisplayWindow? _window;
    private string? _selectedTargetId;
    private bool _disposed;
    public event EventHandler? StateChanged;
    public IReadOnlyList<DisplayTarget> Targets { get; private set; } = [];
    public bool IsOpen => _window?.IsVisible == true;
    public string? SelectedTargetId => _selectedTargetId;
    public string StatusMessage { get; private set; } = "Select a secondary display.";

    public void RefreshTargets()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Targets = targetProvider.GetTargets();
        if (_selectedTargetId is not null && Targets.All(target => target.Id != _selectedTargetId))
        {
            Close(); _selectedTargetId = null;
            StatusMessage = "Selected display disconnected. Select an available display to reopen.";
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Open(string targetId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); RefreshTargets();
        var target = Targets.SingleOrDefault(item => item.Id == targetId);
        if (target is null) { StatusMessage = "Selected display is not available."; StateChanged?.Invoke(this, EventArgs.Empty); return; }
        if (target.IsPrimary) { StatusMessage = "Choose a secondary display. The cashier display cannot be used."; StateChanged?.Invoke(this, EventArgs.Empty); return; }
        _selectedTargetId = target.Id;
        if (_window is { IsVisible: true }) { _window.Activate(); StatusMessage = $"Customer display open on {target.Name}."; StateChanged?.Invoke(this, EventArgs.Empty); return; }
        _window = windowFactory(); _window.Closed += OnClosed; _window.ShowOn(target);
        StatusMessage = $"Customer display open on {target.Name}."; StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Close()
    {
        if (_window is null) return;
        var window = _window; _window = null; window.Closed -= OnClosed; window.Close();
        StatusMessage = "Customer display closed."; StateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnClosed(object? sender, EventArgs e) { if (sender is ICustomerDisplayWindow w) w.Closed -= OnClosed; _window = null; StatusMessage = "Customer display closed."; StateChanged?.Invoke(this, EventArgs.Empty); }
    public void Dispose() { if (_disposed) return; Close(); _disposed = true; }
}
