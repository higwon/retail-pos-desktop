using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class ReceiptPrinterSimulatorViewModelTests
{
    [Fact]
    public void ApplyCommand_UpdatesNextOutcomeAndDelay()
    {
        var control = new StubControl();
        using var viewModel = new ReceiptPrinterSimulatorViewModel(control)
        {
            SelectedOutcome = new ReceiptPrintOutcomeOption(
                ReceiptPrintOutcome.PaperOut,
                "Paper out"),
            ResponseDelayMilliseconds = 1250
        };

        viewModel.ApplyCommand.Execute(null);

        Assert.Equal(ReceiptPrintOutcome.PaperOut, control.CurrentSettings.NextOutcome);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), control.CurrentSettings.ResponseDelay);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void InvalidDelay_ShowsSafeErrorWithoutChangingSettings()
    {
        var control = new StubControl();
        using var viewModel = new ReceiptPrinterSimulatorViewModel(control)
        {
            ResponseDelayMilliseconds = 60_001
        };

        viewModel.ApplyCommand.Execute(null);

        Assert.Equal(ReceiptPrinterSimulationSettings.Default, control.CurrentSettings);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public void ConnectionCommandsTrackControlState()
    {
        var control = new StubControl();
        using var viewModel = new ReceiptPrinterSimulatorViewModel(control);

        viewModel.DisconnectCommand.Execute(null);
        Assert.False(viewModel.IsConnected);
        Assert.True(viewModel.ConnectCommand.CanExecute(null));

        viewModel.ConnectCommand.Execute(null);
        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void DisposeUnsubscribesFromControlEvents()
    {
        var control = new StubControl();
        var viewModel = new ReceiptPrinterSimulatorViewModel(control);
        viewModel.Dispose();

        control.Disconnect();

        Assert.True(viewModel.IsConnected);
    }

    private sealed class StubControl : IReceiptPrinterSimulatorControl
    {
        public event EventHandler? StateChanged;

        public ReceiptPrinterSimulationSettings CurrentSettings { get; private set; } =
            ReceiptPrinterSimulationSettings.Default;
        public ReceiptPrinterConnectionState ConnectionState { get; private set; } =
            ReceiptPrinterConnectionState.Connected;
        public ReceiptPrinterOperationalState OperationalState { get; private set; } =
            ReceiptPrinterOperationalState.Ready;

        public void ConfigureNext(ReceiptPrinterSimulationSettings settings)
        {
            CurrentSettings = settings;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Connect()
        {
            ConnectionState = ReceiptPrinterConnectionState.Connected;
            OperationalState = ReceiptPrinterOperationalState.Ready;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Disconnect()
        {
            ConnectionState = ReceiptPrinterConnectionState.Disconnected;
            OperationalState = ReceiptPrinterOperationalState.Disconnected;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Reset()
        {
            CurrentSettings = ReceiptPrinterSimulationSettings.Default;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
