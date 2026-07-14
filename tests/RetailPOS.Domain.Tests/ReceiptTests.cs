using RetailPOS.Domain.Payments;
using RetailPOS.Domain.Receipts;

namespace RetailPOS.Domain.Tests;

public sealed class ReceiptTests
{
    [Fact]
    public void Constructor_CreatesConsistentReceiptSummary()
    {
        var receipt = new Receipt("Retail Store", "Seoul", "ORDER-001", "Cashier A", "Register 01",
            DateTimeOffset.UtcNow,
            [new ReceiptLine("Shampoo", 5_000m, 2, 1_000m)],
            [new ReceiptPaymentSummary(PaymentMethod.Card, 9_000m, "APPROVED-001")]);

        Assert.Equal(10_000m, receipt.SubtotalAmount);
        Assert.Equal(1_000m, receipt.DiscountAmount);
        Assert.Equal(9_000m, receipt.TotalAmount);
        Assert.Equal(9_000m, receipt.PaymentTotal);
    }

    [Fact]
    public void Constructor_RejectsPaymentMismatch() =>
        Assert.Throws<ArgumentException>(() => new Receipt("Retail Store", "Seoul", "ORDER-001",
            "Cashier A", "Register 01", DateTimeOffset.UtcNow,
            [new ReceiptLine("Product", 10_000m, 1)],
            [new ReceiptPaymentSummary(PaymentMethod.Cash, 9_000m)]));

    [Fact]
    public void CashPaymentSummary_CapturesTenderAndChange()
    {
        var payment = new ReceiptPaymentSummary(
            PaymentMethod.Cash,
            9_000m,
            cashTenderedAmount: 10_000m,
            changeAmount: 1_000m);

        Assert.Equal(10_000m, payment.CashTenderedAmount);
        Assert.Equal(1_000m, payment.ChangeAmount);
    }
}
