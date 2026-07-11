using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Receipts;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.ViewModels;

public sealed class DeviceSimulatorViewModel(
    ReceiptPrinterSimulatorViewModel receiptPrinter,
    BarcodeScannerSimulatorViewModel barcodeScanner,
    CardTerminalSimulatorViewModel cardTerminal,
    CustomerDisplayHostViewModel customerDisplay) : IDisposable
{
    public string EnvironmentName => "Development";
    public ReceiptPrinterSimulatorViewModel ReceiptPrinter { get; } = receiptPrinter;
    public BarcodeScannerSimulatorViewModel BarcodeScanner { get; } = barcodeScanner;
    public CardTerminalSimulatorViewModel CardTerminal { get; } = cardTerminal;
    public CustomerDisplayHostViewModel CustomerDisplay { get; } = customerDisplay;

    public void Dispose()
    {
        ReceiptPrinter.Dispose();
        BarcodeScanner.Dispose();
        CardTerminal.Dispose();
        CustomerDisplay.Dispose();
    }
}

public sealed partial class CustomerDisplayHostViewModel : ObservableObject, IDisposable
{
    private readonly RetailPOS.Desktop.DeviceSimulation.CustomerDisplayHost _host;
    private bool _disposed;
    public CustomerDisplayHostViewModel(RetailPOS.Desktop.DeviceSimulation.CustomerDisplayHost host)
    {
        _host = host; OpenCommand = new RelayCommand(Open, CanOpen); CloseCommand = new RelayCommand(host.Close, () => IsOpen);
        host.StateChanged += OnChanged; host.RefreshTargets(); Refresh();
    }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand CloseCommand { get; }
    [ObservableProperty] private IReadOnlyList<RetailPOS.Desktop.DeviceSimulation.DisplayTarget> _targets = [];
    [ObservableProperty] private RetailPOS.Desktop.DeviceSimulation.DisplayTarget? _selectedTarget;
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _statusMessage = "";
    partial void OnSelectedTargetChanged(RetailPOS.Desktop.DeviceSimulation.DisplayTarget? value) => OpenCommand.NotifyCanExecuteChanged();
    private bool CanOpen() => SelectedTarget is not null &&
        (!IsOpen || SelectedTarget.Id != _host.SelectedTargetId);
    private void Open() { if (SelectedTarget is not null) _host.Open(SelectedTarget.Id); }
    private void OnChanged(object? s, EventArgs e) => Refresh();
    private void Refresh() { Targets = _host.Targets.Where(x => !x.IsPrimary).ToArray(); SelectedTarget = Targets.FirstOrDefault(x => x.Id == _host.SelectedTargetId) ?? SelectedTarget ?? Targets.FirstOrDefault(); IsOpen = _host.IsOpen; StatusMessage = _host.StatusMessage; OpenCommand.NotifyCanExecuteChanged(); CloseCommand.NotifyCanExecuteChanged(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _host.StateChanged -= OnChanged; }
}

public sealed partial class CardTerminalSimulatorViewModel : ObservableObject, IDisposable
{
    private readonly IPaymentTerminalSimulatorControl _control;
    private bool _disposed;
    public CardTerminalSimulatorViewModel(IPaymentTerminalSimulatorControl control)
    {
        _control = control;
        Scenarios = Enum.GetValues<PaymentTerminalSimulationScenario>();
        ConnectCommand = new RelayCommand(control.Connect, () => !IsConnected);
        DisconnectCommand = new RelayCommand(control.Disconnect, () => IsConnected);
        ApplyCommand = new RelayCommand(Apply, () => !IsBusy);
        ResetCommand = new RelayCommand(control.Reset, () => !IsBusy);
        control.StateChanged += OnChanged; Refresh();
    }
    public IReadOnlyList<PaymentTerminalSimulationScenario> Scenarios { get; }
    public IRelayCommand ConnectCommand { get; }
    public IRelayCommand DisconnectCommand { get; }
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand ResetCommand { get; }
    [ObservableProperty] private PaymentTerminalSimulationScenario _selectedScenario;
    [ObservableProperty] private int _responseDelayMilliseconds;
    [ObservableProperty] private string _connectionState = "";
    [ObservableProperty] private string _operationalState = "";
    [ObservableProperty] private string _lastOutcome = "None";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    private void Apply()
    {
        if (ResponseDelayMilliseconds is < 0 or > 60000) { ErrorMessage = "Enter a delay from 0 to 60000 ms."; StatusMessage = null; return; }
        _control.ConfigureNext(new(SelectedScenario, TimeSpan.FromMilliseconds(ResponseDelayMilliseconds)));
        ErrorMessage = null; StatusMessage = "Card terminal settings applied.";
    }
    private void OnChanged(object? s, EventArgs e) { var d = System.Windows.Application.Current?.Dispatcher; if (d is not null && !d.CheckAccess()) { _ = d.BeginInvoke(Refresh); return; } Refresh(); }
    private void Refresh()
    {
        var current = _control.Current; SelectedScenario = current.Scenario; ResponseDelayMilliseconds = (int)current.ResponseDelay.TotalMilliseconds;
        ConnectionState = _control.ConnectionState.ToString(); OperationalState = _control.OperationalState.ToString(); LastOutcome = _control.LastOutcome?.ToString() ?? "None";
        IsConnected = _control.ConnectionState == PaymentTerminalConnectionState.Connected; IsBusy = _control.OperationalState is PaymentTerminalOperationalState.WaitingForCard or PaymentTerminalOperationalState.Processing;
        ConnectCommand.NotifyCanExecuteChanged(); DisconnectCommand.NotifyCanExecuteChanged(); ApplyCommand.NotifyCanExecuteChanged(); ResetCommand.NotifyCanExecuteChanged();
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _control.StateChanged -= OnChanged; }
}

public sealed partial class BarcodeScannerSimulatorViewModel : ObservableObject, IDisposable
{
    private readonly IBarcodeScannerSimulatorControl _control;
    private readonly AsyncRelayCommand _emitScanCommand;
    private bool _disposed;

    public BarcodeScannerSimulatorViewModel(IBarcodeScannerSimulatorControl control)
    {
        _control = control;
        ConnectCommand = new RelayCommand(Connect, () => !IsConnected);
        DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
        _emitScanCommand = new AsyncRelayCommand(EmitScanAsync, CanEmitScan);
        _control.StateChanged += OnControlStateChanged;
        Refresh();
    }

    public IRelayCommand ConnectCommand { get; }
    public IRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand EmitScanCommand => _emitScanCommand;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EmitScanCommand))]
    private string _barcode = string.Empty;

    [ObservableProperty]
    private bool _emitOnBackgroundThread;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EmitScanCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionState = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    partial void OnEmitOnBackgroundThreadChanged(bool value) =>
        _control.EmitOnBackgroundThread = value;

    private bool CanEmitScan() => IsConnected && !string.IsNullOrWhiteSpace(Barcode);

    private async Task EmitScanAsync(CancellationToken cancellationToken)
    {
        try
        {
            var barcode = Barcode.Trim();
            if (!await _control.EmitAsync(barcode, cancellationToken))
            {
                StatusMessage = null;
                ErrorMessage = "Barcode scanner is disconnected.";
                return;
            }

            ErrorMessage = null;
            StatusMessage = $"Scan emitted: {barcode}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ArgumentException)
        {
            StatusMessage = null;
            ErrorMessage = "Enter a barcode before emitting a scan.";
        }
    }

    private void Connect()
    {
        _control.Connect();
        ErrorMessage = null;
        StatusMessage = "Barcode scanner connected.";
    }

    private void Disconnect()
    {
        _control.Disconnect();
        ErrorMessage = null;
        StatusMessage = "Barcode scanner disconnected.";
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
        ConnectionState = _control.ConnectionState.ToString();
        IsConnected = _control.ConnectionState == BarcodeScannerConnectionState.Connected;
        EmitOnBackgroundThread = _control.EmitOnBackgroundThread;
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        _emitScanCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _control.StateChanged -= OnControlStateChanged;
        _emitScanCommand.Cancel();
    }
}

public sealed partial class ReceiptPrinterSimulatorViewModel : ObservableObject, IDisposable
{
    private readonly IReceiptPrinterSimulatorControl _control;
    private bool _disposed;

    public ReceiptPrinterSimulatorViewModel(IReceiptPrinterSimulatorControl control)
    {
        _control = control;
        Outcomes = Enum.GetValues<ReceiptPrintOutcome>()
            .Where(outcome => outcome != ReceiptPrintOutcome.Busy)
            .Select(outcome => new ReceiptPrintOutcomeOption(outcome, Label(outcome)))
            .ToArray();
        ConnectCommand = new RelayCommand(Connect, () => !IsConnected);
        DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
        RespondCommand = new RelayCommand(Respond, () => PendingRequestId is not null && SelectedOutcome is not null);
        ResetCommand = new RelayCommand(Reset, () => !IsPrinting);
        _control.StateChanged += OnControlStateChanged;
        Refresh();
    }

    public IReadOnlyList<ReceiptPrintOutcomeOption> Outcomes { get; }
    public IRelayCommand ConnectCommand { get; }
    public IRelayCommand DisconnectCommand { get; }
    public IRelayCommand RespondCommand { get; }
    public IRelayCommand ResetCommand { get; }

    [ObservableProperty]
    private ReceiptPrintOutcomeOption? _selectedOutcome;

    [ObservableProperty]
    private Guid? _pendingRequestId;

    [ObservableProperty]
    private string _pendingRequestTitle = "No pending print request";

    [ObservableProperty]
    private string _pendingRequestMetadata = "Print from POS to inspect receipt details here.";

    [ObservableProperty]
    private string _printableText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ReceiptPrintHistoryViewModel> _recentRequests = [];

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

    private void Respond()
    {
        if (PendingRequestId is null || SelectedOutcome is null)
        {
            return;
        }

        var label = SelectedOutcome.Label;
        if (_control.Respond(PendingRequestId.Value, SelectedOutcome.Outcome))
        {
            ErrorMessage = null;
            StatusMessage = $"{label} response sent to POS.";
        }
        else
        {
            StatusMessage = null;
            ErrorMessage = "This request is no longer pending. Refresh and try the current request.";
        }
    }

    private void Reset()
    {
        _control.Reset();
        ErrorMessage = null;
        StatusMessage = "Printer state reset.";
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
        ConnectionState = _control.ConnectionState.ToString();
        OperationalState = _control.OperationalState.ToString();
        IsConnected = _control.ConnectionState == ReceiptPrinterConnectionState.Connected;
        IsPrinting = _control.OperationalState == ReceiptPrinterOperationalState.Printing;
        SelectedOutcome ??= Outcomes[0];

        var pending = _control.PendingRequest;
        PendingRequestId = pending?.RequestId;
        PendingRequestTitle = pending is null
            ? "No pending print request"
            : $"Receipt {pending.BusinessIdentity}";
        PendingRequestMetadata = pending is null
            ? "Print from POS to inspect receipt details here."
            : $"{pending.Payload.Receipt.StoreName} · {pending.Payload.Receipt.RegisterName} · " +
              $"{pending.Payload.Receipt.CashierName} · requested {pending.ReceivedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        PrintableText = pending?.Payload.PrintableText ?? string.Empty;
        RecentRequests = _control.RecentRequests
            .Select(request => new ReceiptPrintHistoryViewModel(
                request.BusinessIdentity,
                request.State == DeviceRequestState.Completed
                    ? request.Result.ToString()
                    : request.State.ToString(),
                request.CompletedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-"))
            .ToArray();
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        RespondCommand.NotifyCanExecuteChanged();
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

public sealed record ReceiptPrintHistoryViewModel(
    string ReceiptIdentity,
    string Outcome,
    string CompletedAtText);
