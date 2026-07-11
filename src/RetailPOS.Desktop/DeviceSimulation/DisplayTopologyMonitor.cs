using Microsoft.Win32;

namespace RetailPOS.Desktop.DeviceSimulation;

public interface IDisplayTopologyMonitor
{
    event EventHandler? Changed;
}

public sealed class WindowsDisplayTopologyMonitor : IDisplayTopologyMonitor, IDisposable
{
    private bool _disposed;

    public WindowsDisplayTopologyMonitor() =>
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

    public event EventHandler? Changed;

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
