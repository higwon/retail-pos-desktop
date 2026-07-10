using Microsoft.Extensions.Options;
using RetailPOS.Desktop.Views;

namespace RetailPOS.Desktop.DeviceSimulation;

public sealed class DeviceSimulatorWindowHost(
    Func<DeviceSimulatorWindow> windowFactory,
    IOptions<DeviceSimulationOptions> options) : IDisposable
{
    private DeviceSimulatorWindow? _window;
    private bool _disposed;

    public bool IsEnabled => options.Value.Enabled;

    public void ShowOrActivate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsEnabled)
        {
            return;
        }

        if (_window is { IsVisible: true } existing)
        {
            if (existing.WindowState == System.Windows.WindowState.Minimized)
            {
                existing.WindowState = System.Windows.WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        var window = windowFactory();
        window.Owner = System.Windows.Application.Current?.MainWindow;
        window.Closed += OnWindowClosed;
        _window = window;
        window.Show();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is DeviceSimulatorWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _window = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_window is { } window)
        {
            window.Closed -= OnWindowClosed;
            _window = null;
            window.Close();
        }
    }
}
