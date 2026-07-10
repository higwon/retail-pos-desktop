namespace RetailPOS.Application.Devices;

public interface IBarcodeScanner
{
    event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
}

public sealed class BarcodeScannedEventArgs(string barcode) : EventArgs
{
    public string Barcode { get; } = string.IsNullOrWhiteSpace(barcode)
        ? throw new ArgumentException("A scanned barcode is required.", nameof(barcode))
        : barcode.Trim();
}
