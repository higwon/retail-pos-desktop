using RetailPOS.Application.Receipts;

namespace RetailPOS.Infrastructure.Devices;

public enum ReceiptPrintSimulationMode
{
    Succeed,
    Fail
}

public sealed class LocalReceiptPrinter(
    TimeProvider timeProvider,
    ReceiptPrintSimulationMode mode = ReceiptPrintSimulationMode.Succeed) : IReceiptPrinter
{
    public Task<ReceiptPrintResult> PrintAsync(
        ReceiptPreview receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();

        var result = mode switch
        {
            ReceiptPrintSimulationMode.Succeed => new ReceiptPrintResult(
                true,
                timeProvider.GetUtcNow(),
                "Receipt printed successfully."),
            ReceiptPrintSimulationMode.Fail => new ReceiptPrintResult(
                false,
                null,
                "Receipt could not be printed. Try again."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported receipt print simulation mode.")
        };

        return Task.FromResult(result);
    }
}
