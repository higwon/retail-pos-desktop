using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Persistence;

public interface IReceiptHistoryRepository
{
    Task<ReceiptHistoryPageRecord> SearchAsync(
        DateOnly businessDate,
        string? searchText,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record ReceiptHistoryPageRecord(
    IReadOnlyList<ReceiptHistorySummaryRecord> Items,
    bool HasMore);

public sealed record ReceiptHistorySummaryRecord(
    Guid LocalOrderId,
    string LocalOrderNumber,
    DateTimeOffset CompletedAtUtc,
    PaymentMethod PaymentMethod,
    decimal TotalAmount,
    Guid CashierId,
    Guid TerminalId);
