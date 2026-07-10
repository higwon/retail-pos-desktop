using RetailPOS.Application.Devices;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class SimulatedBarcodeScannerTests
{
    [Fact]
    public async Task EmitAsync_WhenConnectedRaisesTrimmedBarcode()
    {
        using var scanner = new SimulatedBarcodeScanner();
        BarcodeScannedEventArgs? received = null;
        scanner.BarcodeScanned += (_, args) => received = args;

        var emitted = await scanner.EmitAsync("  8801000000011  ");

        Assert.True(emitted);
        Assert.Equal("8801000000011", received?.Barcode);
    }

    [Fact]
    public async Task EmitAsync_WhenDisconnectedDoesNotRaiseEvent()
    {
        using var scanner = new SimulatedBarcodeScanner();
        var count = 0;
        scanner.BarcodeScanned += (_, _) => count++;
        scanner.Disconnect();

        var emitted = await scanner.EmitAsync("8801000000011");

        Assert.False(emitted);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DisconnectReconnectAndRepeatedBackgroundScansAreDeterministic()
    {
        using var scanner = new SimulatedBarcodeScanner
        {
            EmitOnBackgroundThread = true
        };
        var barcodes = new List<string>();
        scanner.BarcodeScanned += (_, args) =>
        {
            lock (barcodes)
            {
                barcodes.Add(args.Barcode);
            }
        };

        await scanner.EmitAsync("first");
        scanner.Disconnect();
        Assert.False(await scanner.EmitAsync("ignored"));
        scanner.Connect();
        await scanner.EmitAsync("second");

        Assert.Equal(["first", "second"], barcodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmitAsync_EmptyBarcodeIsRejected(string barcode)
    {
        using var scanner = new SimulatedBarcodeScanner();

        await Assert.ThrowsAsync<ArgumentException>(() => scanner.EmitAsync(barcode));
    }
}
