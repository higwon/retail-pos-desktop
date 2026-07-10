using RetailPOS.Desktop.ViewModels;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class BarcodeScannerSimulatorViewModelTests
{
    [Fact]
    public async Task EmitCommandRequiresConnectedScannerAndNonEmptyBarcode()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var viewModel = new BarcodeScannerSimulatorViewModel(scanner);

        Assert.False(viewModel.EmitScanCommand.CanExecute(null));
        viewModel.Barcode = "8801000000011";
        Assert.True(viewModel.EmitScanCommand.CanExecute(null));

        viewModel.DisconnectCommand.Execute(null);
        Assert.False(viewModel.EmitScanCommand.CanExecute(null));

        viewModel.ConnectCommand.Execute(null);
        await viewModel.EmitScanCommand.ExecuteAsync(null);
        Assert.Contains("8801000000011", viewModel.StatusMessage);
    }

    [Fact]
    public void BackgroundToggleAndConnectionCommandsUpdateControl()
    {
        using var scanner = new SimulatedBarcodeScanner();
        using var viewModel = new BarcodeScannerSimulatorViewModel(scanner);

        viewModel.EmitOnBackgroundThread = true;
        viewModel.DisconnectCommand.Execute(null);

        Assert.True(scanner.EmitOnBackgroundThread);
        Assert.Equal(BarcodeScannerConnectionState.Disconnected, scanner.ConnectionState);
        Assert.False(viewModel.IsConnected);
    }

    [Fact]
    public void DisposeUnsubscribesFromControlEvents()
    {
        using var scanner = new SimulatedBarcodeScanner();
        var viewModel = new BarcodeScannerSimulatorViewModel(scanner);
        viewModel.Dispose();

        scanner.Disconnect();

        Assert.True(viewModel.IsConnected);
    }
}
