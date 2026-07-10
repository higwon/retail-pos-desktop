namespace RetailPOS.Application.Receipts;

public interface IReceiptPrinter
{
    Task<ReceiptPrintResult> PrintAsync(
        ReceiptPreview receipt,
        CancellationToken cancellationToken = default);
}

public sealed record ReceiptPrintResult(
    bool Succeeded,
    DateTimeOffset? PrintedAtUtc,
    string Message);
