using RetailPOS.Application.Receipts;

namespace RetailPOS.Desktop.ViewModels;

public sealed class ReceiptPreviewState
{
    public ReceiptPreview? Current { get; private set; }

    public bool HasReceipt => Current is not null;

    public void Set(ReceiptPreview receipt)
    {
        Current = receipt ?? throw new ArgumentNullException(nameof(receipt));
    }

    public ReceiptPreview? GetCurrent() => Current;

    public void Clear() => Current = null;
}
