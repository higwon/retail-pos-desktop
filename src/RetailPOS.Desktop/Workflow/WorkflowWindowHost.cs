namespace RetailPOS.Desktop.Workflow;

public interface IWorkflowWindow
{
    bool IsVisible { get; }
    event EventHandler? Closed;
    void Show();
    void Activate();
    void Close();
}

public interface IWorkflowWindowCloser
{
    void Close();
}

public sealed class WorkflowWindowHost<TWindow>(Func<TWindow> windowFactory)
    : IWorkflowWindowCloser, IDisposable
    where TWindow : class, IWorkflowWindow
{
    private TWindow? _window;
    private bool _disposed;
    public event EventHandler? WindowClosed;
    public bool IsOpen => _window?.IsVisible == true;

    public void ShowOrActivate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_window is { IsVisible: true } open)
        {
            open.Activate();
            return;
        }

        _window = windowFactory();
        _window.Closed += OnClosed;
        _window.Show();
    }

    public void Close()
    {
        Close(notify: true);
    }

    private void Close(bool notify)
    {
        if (_window is null) return;
        var window = _window;
        _window = null;
        window.Closed -= OnClosed;
        window.Close();
        if (notify) WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (sender is TWindow window) window.Closed -= OnClosed;
        _window = null;
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Close(notify: false);
        _disposed = true;
    }
}
