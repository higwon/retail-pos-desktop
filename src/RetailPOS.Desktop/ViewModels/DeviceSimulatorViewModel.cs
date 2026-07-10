using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Receipts;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.ViewModels;

public sealed class DeviceSimulatorViewModel(
    ReceiptPrinterSimulatorViewModel receiptPrinter) : IDisposable
{
    public string EnvironmentName => "Development";
    public ReceiptPrinterSimulatorViewModel ReceiptPrinter { get; } = receiptPrinter;

    public void Dispose() => ReceiptPrinter.Dispose();
}

public sealed partial class ReceiptPrinterSimulatorViewModel : ObservableObject, IDisposable
{
    private readonly IReceiptPrinterSimulatorControl _control;
    private bool _disposed;

    public ReceiptPrinterSimulatorViewModel(IReceiptPrinterSimulatorControl control)
    {
        _control = control;
        Outcomes = Enum.GetValues<ReceiptPrintOutcome>()
            .Select(outcome => new ReceiptPrintOutcomeOption(outcome, Label(outcome)))
            .ToArray();
        ConnectCommand = new RelayCommand(Connect, () => !IsConnected);
        DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
        ApplyCommand = new RelayCommand(Apply, () => !IsPrinting);
        ResetCommand = new RelayCommand(Reset, () => !IsPrinting);
        _control.StateChanged += OnControlStateChanged;
        Refresh();
    }

    public IReadOnlyList<ReceiptPrintOutcomeOption> Outcomes { get; }
    public IRelayCommand ConnectCommand { get; }
    public IRelayCommand DisconnectCommand { get; }
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand ResetCommand { get; }

    [ObservableProperty]
    private ReceiptPrintOutcomeOption? _selectedOutcome;

    [ObservableProperty]
    private int _responseDelayMilliseconds;

    [ObservableProperty]
    private string _connectionState = string.Empty;

    [ObservableProperty]
    private string _operationalState = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    private void Connect()
    {
        _control.Connect();
        StatusMessage = "Receipt printer connected.";
        ErrorMessage = null;
    }

    private void Disconnect()
    {
        _control.Disconnect();
        StatusMessage = "Receipt printer disconnected.";
        ErrorMessage = null;
    }

    private void Apply()
    {
        if (SelectedOutcome is null ||
            ResponseDelayMilliseconds < 0 ||
            ResponseDelayMilliseconds > 60_000)
        {
            StatusMessage = null;
            ErrorMessage = "Choose an outcome and enter a delay from 0 to 60000 ms.";
            return;
        }

        _control.ConfigureNext(new ReceiptPrinterSimulationSettings(
            SelectedOutcome.Outcome,
            TimeSpan.FromMilliseconds(ResponseDelayMilliseconds)));
        ErrorMessage = null;
        StatusMessage = "Printer simulation settings applied.";
    }

    private void Reset()
    {
        _control.Reset();
        ErrorMessage = null;
        StatusMessage = "Printer simulation reset to defaults.";
    }

    private void OnControlStateChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(Refresh);
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        var settings = _control.CurrentSettings;
        ConnectionState = _control.ConnectionState.ToString();
        OperationalState = _control.OperationalState.ToString();
        IsConnected = _control.ConnectionState == ReceiptPrinterConnectionState.Connected;
        IsPrinting = _control.OperationalState == ReceiptPrinterOperationalState.Printing;
        SelectedOutcome = Outcomes.Single(option => option.Outcome == settings.NextOutcome);
        ResponseDelayMilliseconds = checked((int)settings.ResponseDelay.TotalMilliseconds);
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }

    private static string Label(ReceiptPrintOutcome outcome) => outcome switch
    {
        ReceiptPrintOutcome.Printed => "Printed",
        ReceiptPrintOutcome.PaperOut => "Paper out",
        ReceiptPrintOutcome.CoverOpen => "Cover open",
        ReceiptPrintOutcome.Disconnected => "Disconnected",
        ReceiptPrintOutcome.Timeout => "Timeout",
        ReceiptPrintOutcome.Cancelled => "Cancelled",
        ReceiptPrintOutcome.Busy => "Busy",
        ReceiptPrintOutcome.Failed => "Unexpected failure",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _control.StateChanged -= OnControlStateChanged;
    }
}

public sealed record ReceiptPrintOutcomeOption(
    ReceiptPrintOutcome Outcome,
    string Label);
