using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Receipts;

public interface IReceiptHistoryQuery
{
    Task<ReceiptHistoryPage> SearchAsync(
        ReceiptHistoryRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceiptPreview?> GetDetailAsync(
        Guid localOrderId,
        CancellationToken cancellationToken = default);
}

public sealed record ReceiptHistoryRequest(
    DateOnly BusinessDate,
    string? SearchText = null,
    int Offset = 0,
    int Limit = 50);

public sealed record ReceiptHistoryPage(
    IReadOnlyList<ReceiptHistorySummary> Items,
    bool HasMore);

public sealed record ReceiptHistorySummary(
    Guid LocalOrderId,
    string OrderNumber,
    DateTimeOffset CompletedAtUtc,
    PaymentMethod PaymentMethod,
    decimal TotalAmount,
    string CashierName);

public sealed class ReceiptHistoryQuery(
    IReceiptHistoryRepository repository,
    IReceiptService receiptService,
    IReceiptContextProvider receiptContextProvider) : IReceiptHistoryQuery
{
    public const int MaximumPageSize = 100;

    public async Task<ReceiptHistoryPage> SearchAsync(
        ReceiptHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        if (request.Limit is <= 0 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Receipt history page size must be between 1 and {MaximumPageSize}.");
        }

        var page = await repository.SearchAsync(
            request.BusinessDate,
            NormalizeSearch(request.SearchText),
            request.Offset,
            request.Limit,
            cancellationToken);
        var summaries = page.Items.Select(item =>
        {
            var context = receiptContextProvider.GetCurrent(item.CashierId, item.TerminalId);
            return new ReceiptHistorySummary(
                item.LocalOrderId,
                item.LocalOrderNumber,
                item.CompletedAtUtc,
                item.PaymentMethod,
                item.TotalAmount,
                context.CashierName);
        }).ToArray();

        return new ReceiptHistoryPage(summaries, page.HasMore);
    }

    public async Task<ReceiptPreview?> GetDetailAsync(
        Guid localOrderId,
        CancellationToken cancellationToken = default)
    {
        if (localOrderId == Guid.Empty)
        {
            throw new ArgumentException("Order identity is required.", nameof(localOrderId));
        }

        try
        {
            return await receiptService.GenerateAsync(localOrderId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string? NormalizeSearch(string? searchText) =>
        string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
}
