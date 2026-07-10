using RetailPOS.Desktop.ViewModels;
using System.Windows;
using RetailPOS.Desktop.DeviceSimulation;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RetailPOS.Desktop.Views;

public partial class CustomerDisplayWindow : Window, ICustomerDisplayWindow
{
    private DisplayTarget? _target;

    public CustomerDisplayWindow(CustomerDisplayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += OnClosed;
        DpiChanged += OnDpiChanged;
    }

    public void ShowOn(DisplayTarget target)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Normal;
        Show();
        MoveTo(target);
    }

    public void MoveTo(DisplayTarget target)
    {
        _target = target;
        var bounds = target.WorkingBounds;
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (!SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                SetWindowPosFlags.NoActivate | SetWindowPosFlags.ShowWindow))
        {
            throw new InvalidOperationException("Customer display could not be placed on the selected monitor.");
        }
    }

    void ICustomerDisplayWindow.Activate() => Activate();

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        if (_target is not null)
        {
            MoveTo(_target);
        }
    }

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoActivate = 0x0010,
        ShowWindow = 0x0040
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        SetWindowPosFlags flags);

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        DpiChanged -= OnDpiChanged;
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
    }
}
