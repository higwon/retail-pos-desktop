using RetailPOS.Application.Receipts;

namespace RetailPOS.Desktop.ViewModels;

public sealed class ReceiptLineViewModel(ReceiptPreviewLine line)
{
    public string ProductName { get; } = line.ProductName;
    public string QuantityText => $"x {line.Quantity:N0}";
    public string UnitPriceText => $"{line.UnitPrice:N0} KRW";
    public string DiscountAmountText => line.DiscountAmount > 0
        ? $"-{line.DiscountAmount:N0} KRW"
        : "-";
    public string TotalAmountText => $"{line.TotalAmount:N0} KRW";
}

public sealed class ReceiptPaymentViewModel(ReceiptPreviewPayment payment)
{
    public string MethodText => payment.Method.ToString();
    public string AmountText => $"{payment.Amount:N0} KRW";
    public string ApprovalCodeText => payment.ApprovalCode ?? "-";
    public bool HasCashTenderDetails => payment.CashTenderedAmount is not null;
    public string CashTenderedText => $"Tendered {payment.CashTenderedAmount:N0} KRW";
    public string ChangeText => $"Change {payment.ChangeAmount:N0} KRW";
}
