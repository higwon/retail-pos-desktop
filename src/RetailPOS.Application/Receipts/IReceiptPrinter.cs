namespace RetailPOS.Application.Receipts;

public interface IReceiptPrinter
{
    Task<ReceiptPrintResult> PrintAsync(
        ReceiptPreview receipt,
        CancellationToken cancellationToken = default);
}

public enum ReceiptPrintOutcome
{
    Printed,
    PaperOut,
    CoverOpen,
    Disconnected,
    Timeout,
    Cancelled,
    Busy,
    Failed
}

public sealed record ReceiptPrintResult(
    ReceiptPrintOutcome Outcome,
    DateTimeOffset? PrintedAtUtc,
    string Message)
{
    public bool Succeeded => Outcome == ReceiptPrintOutcome.Printed;
}
