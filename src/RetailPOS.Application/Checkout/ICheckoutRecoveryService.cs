using RetailPOS.Application.Persistence;

namespace RetailPOS.Application.Checkout;

public interface ICheckoutRecoveryService
{
    Task<IReadOnlyList<CheckoutRecoveryRecord>> GetRecoverableAsync(
        CancellationToken cancellationToken = default);

    Task<CheckoutRecoveryCompletionResult> CompleteAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default);

    Task MarkManagerReviewRequiredAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default);

    Task ResolveManagerReviewAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default);
}

public sealed record CheckoutRecoveryRecord(
    Guid PendingCheckoutId,
    Guid StoreId,
    Guid TerminalId,
    Guid CashierId,
    DateTimeOffset CreatedAtUtc,
    PendingCheckoutStatus RecoveryStatus,
    decimal ApprovedAmount,
    string PaymentMethod,
    string? ApprovalCode,
    string? TransactionReference,
    DateTimeOffset? PaymentApprovedAtUtc,
    Guid? OrderId,
    IReadOnlyList<CheckoutRecoveryLine> Lines,
    decimal CartSubtotal,
    decimal DiscountAmount,
    decimal CartTotal,
    bool IsSnapshotReadable,
    bool CanCompleteOrder,
    bool CanResolveReview,
    string? WarningMessage);

public sealed record CheckoutRecoveryLine(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record CheckoutRecoveryCompletionResult(
    bool Succeeded,
    Guid? LocalOrderId,
    bool AlreadyCompleted,
    string Message);

public sealed class CheckoutRecoveryService(
    IPendingCheckoutRepository pendingCheckoutRepository,
    IOrderCompletionService orderCompletionService,
    ICheckoutClock clock) : ICheckoutRecoveryService
{
    public async Task<IReadOnlyList<CheckoutRecoveryRecord>> GetRecoverableAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await pendingCheckoutRepository.GetUnresolvedAsync(cancellationToken);
        var normalized = new List<PendingCheckoutRecord>(records.Count);
        foreach (var record in records)
        {
            if (record.RecoveryStatus == PendingCheckoutStatus.AwaitingPayment)
            {
                var interrupted = record with
                {
                    RecoveryStatus = PendingCheckoutStatus.ManagerReviewRequired,
                    PaymentStatus = RetailPOS.Domain.Payments.PaymentStatus.Unknown,
                    LastUpdatedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow))
                };
                await pendingCheckoutRepository.SaveAsync(interrupted, CancellationToken.None);
                normalized.Add(interrupted);
            }
            else
            {
                normalized.Add(record);
            }
        }

        return normalized
            .Where(record => record.RecoveryStatus is
                PendingCheckoutStatus.ApprovedButOrderNotCreated or
                PendingCheckoutStatus.ManagerReviewRequired)
            .Select(ToRecoveryRecord)
            .ToList();
    }

    public async Task<CheckoutRecoveryCompletionResult> CompleteAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await orderCompletionService.CompleteAsync(pendingCheckoutId, cancellationToken);
            return new CheckoutRecoveryCompletionResult(
                true,
                result.LocalOrderId,
                result.AlreadyCompleted,
                result.AlreadyCompleted
                    ? "Order recovery was already completed."
                    : "Order recovery completed.");
        }
        catch (Exception exception) when (IsRecoverableUserBoundaryException(exception))
        {
            return new CheckoutRecoveryCompletionResult(
                false,
                null,
                false,
                "Recovery could not complete automatically. Request manager review before returning to checkout.");
        }
    }

    public Task MarkManagerReviewRequiredAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default) =>
        pendingCheckoutRepository.MarkManagerReviewRequiredAsync(
            pendingCheckoutId,
            EnsureUtc(clock.UtcNow, nameof(clock.UtcNow)),
            cancellationToken);

    public async Task ResolveManagerReviewAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default)
    {
        var record = await pendingCheckoutRepository.GetByIdAsync(
            pendingCheckoutId,
            cancellationToken) ?? throw new KeyNotFoundException(
                $"Pending checkout '{pendingCheckoutId}' was not found.");

        if (record.RecoveryStatus != PendingCheckoutStatus.ManagerReviewRequired ||
            record.PaymentStatus != RetailPOS.Domain.Payments.PaymentStatus.Unknown)
        {
            throw new InvalidOperationException(
                "Only an unknown payment in manager review can be resolved.");
        }

        await pendingCheckoutRepository.SaveAsync(record with
        {
            RecoveryStatus = PendingCheckoutStatus.ReviewResolved,
            LastUpdatedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow))
        }, CancellationToken.None);
    }

    private static CheckoutRecoveryRecord ToRecoveryRecord(PendingCheckoutRecord record)
    {
        var snapshotResult = RestoreCartSnapshot(record.CartSnapshotJson);
        var snapshot = snapshotResult.Snapshot;
        var canCompleteOrder =
            record.RecoveryStatus == PendingCheckoutStatus.ApprovedButOrderNotCreated &&
            snapshotResult.IsReadable &&
            HasCompleteApprovedPaymentMetadata(record);
        var canResolveReview =
            record.RecoveryStatus == PendingCheckoutStatus.ManagerReviewRequired &&
            record.PaymentStatus == RetailPOS.Domain.Payments.PaymentStatus.Unknown;
        var approvedAmount = record.PaymentStatus == RetailPOS.Domain.Payments.PaymentStatus.Approved
            ? record.ApprovedAmount ?? (snapshotResult.IsReadable ? snapshot.Total : 0m)
            : 0m;
        var warningMessage = record.PaymentStatus == RetailPOS.Domain.Payments.PaymentStatus.Unknown
            ? "Payment approval status is unknown. Manager review is required before retry."
            : canCompleteOrder
                ? null
                : "Checkout data needs manager review.";

        return new CheckoutRecoveryRecord(
            record.Id,
            record.StoreId,
            record.TerminalId,
            record.CashierId,
            record.CreatedAtUtc,
            record.RecoveryStatus,
            approvedAmount,
            record.PaymentStatus.ToString(),
            record.ApprovalCode,
            record.TransactionReference,
            record.PaymentApprovedAtUtc,
            record.OrderId,
            snapshotResult.IsReadable
                ? snapshot.Lines.Select(line => new CheckoutRecoveryLine(
                    line.ProductName,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal)).ToList()
                : [],
            snapshotResult.IsReadable ? snapshot.Subtotal : 0m,
            snapshotResult.IsReadable ? snapshot.DiscountAmount : 0m,
            snapshotResult.IsReadable ? snapshot.Total : 0m,
            snapshotResult.IsReadable,
            canCompleteOrder,
            canResolveReview,
            warningMessage);
    }

    private static RestoredCartSnapshot RestoreCartSnapshot(string cartSnapshotJson)
    {
        try
        {
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<CartSnapshotPayload>(
                cartSnapshotJson,
                JsonOptions.Default);

            if (snapshot is null || !IsValidSnapshot(snapshot))
            {
                return RestoredCartSnapshot.ManagerReview;
            }

            return new RestoredCartSnapshot(snapshot, IsReadable: true);
        }
        catch (System.Text.Json.JsonException)
        {
            return RestoredCartSnapshot.ManagerReview;
        }
    }

    private static bool IsValidSnapshot(CartSnapshotPayload snapshot)
    {
        if (snapshot.Lines is null ||
            snapshot.Lines.Count == 0 ||
            snapshot.Subtotal < 0m ||
            snapshot.DiscountAmount < 0m ||
            snapshot.Total < 0m ||
            snapshot.DiscountAmount > snapshot.Subtotal ||
            snapshot.Total != snapshot.Subtotal - snapshot.DiscountAmount)
        {
            return false;
        }

        var lineTotal = 0m;
        foreach (var line in snapshot.Lines)
        {
            if (line.ProductId == Guid.Empty ||
                string.IsNullOrWhiteSpace(line.ProductName) ||
                line.UnitPrice < 0m ||
                line.Quantity <= 0 ||
                line.LineTotal < 0m ||
                line.LineTotal != line.UnitPrice * line.Quantity)
            {
                return false;
            }

            lineTotal += line.LineTotal;
        }

        return lineTotal == snapshot.Subtotal;
    }

    private static bool HasCompleteApprovedPaymentMetadata(PendingCheckoutRecord record) =>
        record.PaymentStatus == RetailPOS.Domain.Payments.PaymentStatus.Approved &&
        record.ApprovedAmount is > 0m &&
        record.PaymentApprovedAtUtc is not null &&
        record.OrderId is not null;

    private sealed record RestoredCartSnapshot(CartSnapshotPayload Snapshot, bool IsReadable)
    {
        public static RestoredCartSnapshot ManagerReview { get; } = new(CartSnapshotPayload.Empty, IsReadable: false);
    }

    private static bool IsRecoverableUserBoundaryException(Exception exception) =>
        exception is KeyNotFoundException or InvalidOperationException or System.Text.Json.JsonException;

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC.", parameterName);
        }

        return value;
    }

    private sealed record CartSnapshotPayload(
        IReadOnlyList<CartLineSnapshotPayload> Lines,
        decimal Subtotal,
        string? DiscountType,
        decimal? DiscountValue,
        decimal DiscountAmount,
        decimal Total)
    {
        public static CartSnapshotPayload Empty { get; } = new([], 0m, null, null, 0m, 0m);
    }

    private sealed record CartLineSnapshotPayload(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);

    private static class JsonOptions
    {
        public static readonly System.Text.Json.JsonSerializerOptions Default = new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
