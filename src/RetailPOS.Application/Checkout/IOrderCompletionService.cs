using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Orders;
using RetailPOS.Domain.Payments;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetailPOS.Application.Checkout;

public interface IOrderCompletionService
{
    Task<OrderCompletionResult> CompleteAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default);
}

public sealed class OrderCompletionService(
    IPendingCheckoutRepository pendingCheckoutRepository,
    IOrderRepository orderRepository,
    ISyncQueueRepository syncQueueRepository,
    ILocalTransaction localTransaction,
    ICheckoutClock clock,
    ICheckoutIdGenerator idGenerator) : IOrderCompletionService
{
    private const int OrderUploadSchemaVersion = 1;
    private const string SyncItemType = "Order";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<OrderCompletionResult> CompleteAsync(
        Guid pendingCheckoutId,
        CancellationToken cancellationToken = default)
    {
        var checkout = await pendingCheckoutRepository.GetByIdAsync(pendingCheckoutId, cancellationToken)
            ?? throw new KeyNotFoundException($"Pending checkout '{pendingCheckoutId}' was not found.");

        if (checkout.RecoveryStatus == PendingCheckoutStatus.Completed && checkout.OrderId is { } completedOrderId)
        {
            return new OrderCompletionResult(completedOrderId, AlreadyCompleted: true);
        }

        ValidateApprovedCheckout(checkout);
        var orderId = checkout.OrderId!.Value;
        var referenceKey = IdempotencyKey(checkout.StoreId, checkout.TerminalId, orderId);

        if (await orderRepository.ExistsAsync(orderId, cancellationToken))
        {
            await EnsureAlreadyCreatedOrderIsResolvedAsync(checkout, referenceKey, cancellationToken);
            return new OrderCompletionResult(orderId, AlreadyCompleted: true);
        }

        var order = ToOrder(checkout);
        var completedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        var queueItem = new SyncQueueRecord(
            idGenerator.NewId(),
            SyncItemType,
            order.LocalOrderId,
            Serialize(OrderUploadPayload.From(order, referenceKey)),
            referenceKey,
            SyncQueueStatus.Pending,
            0,
            completedAtUtc,
            null,
            completedAtUtc,
            completedAtUtc);

        await localTransaction.ExecuteAsync(async token =>
        {
            await orderRepository.SaveAsync(order, token);
            await pendingCheckoutRepository.MarkCompletedAsync(checkout.Id, order.LocalOrderId, completedAtUtc, token);
            if (!await syncQueueRepository.ExistsByReferenceKeyAsync(referenceKey, token))
            {
                await syncQueueRepository.EnqueueAsync(queueItem, token);
            }
        }, cancellationToken);

        return new OrderCompletionResult(order.LocalOrderId, AlreadyCompleted: false);
    }

    private async Task EnsureAlreadyCreatedOrderIsResolvedAsync(
        PendingCheckoutRecord checkout,
        string referenceKey,
        CancellationToken cancellationToken)
    {
        if (checkout.RecoveryStatus == PendingCheckoutStatus.Completed)
        {
            return;
        }

        var orderId = checkout.OrderId!.Value;
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException("Existing recovery order could not be restored.");
        var completedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        var queueItem = new SyncQueueRecord(
            idGenerator.NewId(),
            SyncItemType,
            order.LocalOrderId,
            Serialize(OrderUploadPayload.From(order, referenceKey)),
            referenceKey,
            SyncQueueStatus.Pending,
            0,
            completedAtUtc,
            null,
            completedAtUtc,
            completedAtUtc);

        await localTransaction.ExecuteAsync(async token =>
        {
            await pendingCheckoutRepository.MarkCompletedAsync(checkout.Id, order.LocalOrderId, completedAtUtc, token);
            if (!await syncQueueRepository.ExistsByReferenceKeyAsync(referenceKey, token))
            {
                await syncQueueRepository.EnqueueAsync(queueItem, token);
            }
        }, cancellationToken);
    }

    private static void ValidateApprovedCheckout(PendingCheckoutRecord checkout)
    {
        if (checkout.RecoveryStatus != PendingCheckoutStatus.ApprovedButOrderNotCreated ||
            checkout.PaymentStatus != PaymentStatus.Approved ||
            checkout.OrderId is null ||
            checkout.ApprovedAmount is null ||
            checkout.PaymentApprovedAtUtc is null)
        {
            throw new InvalidOperationException("Only approved pending checkouts can be completed.");
        }
    }

    private static Order ToOrder(PendingCheckoutRecord checkout)
    {
        var cart = Deserialize<CartSnapshotPayload>(checkout.CartSnapshotJson);
        var paymentSnapshot = Deserialize<PaymentSnapshotPayload>(checkout.PaymentSnapshotJson);
        var lineDiscounts = AllocateDiscounts(cart);
        var lines = cart.Lines.Select((line, index) => new OrderLine(
            line.ProductId,
            line.ProductName,
            line.UnitPrice,
            line.Quantity,
            lineDiscounts[index]));

        var payment = new Payment(
            Guid.NewGuid(),
            paymentSnapshot.Method,
            paymentSnapshot.RequestedAmount,
            checkout.CreatedAtUtc);
        payment.Approve(
            checkout.ApprovedAmount!.Value,
            checkout.PaymentApprovedAtUtc!.Value,
            checkout.ApprovalCode,
            checkout.TransactionReference);

        return new Order(
            checkout.OrderId!.Value,
            LocalOrderNumber(checkout.OrderId.Value, checkout.PaymentApprovedAtUtc.Value),
            checkout.StoreId,
            checkout.TerminalId,
            checkout.CashierId,
            BusinessDate(checkout.CreatedAtUtc),
            checkout.PaymentApprovedAtUtc.Value,
            lines,
            [payment]);
    }

    private static decimal[] AllocateDiscounts(CartSnapshotPayload cart)
    {
        var discounts = new decimal[cart.Lines.Count];
        if (cart.DiscountAmount == 0m || cart.Lines.Count == 0)
        {
            return discounts;
        }

        var remaining = cart.DiscountAmount;
        for (var index = 0; index < cart.Lines.Count; index++)
        {
            if (index == cart.Lines.Count - 1)
            {
                discounts[index] = remaining;
                break;
            }

            var lineGross = cart.Lines[index].LineTotal;
            var discount = decimal.Round(
                cart.DiscountAmount * lineGross / cart.Subtotal,
                0,
                MidpointRounding.AwayFromZero);
            discount = decimal.Min(discount, remaining);
            discounts[index] = discount;
            remaining -= discount;
        }

        return discounts;
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("Pending checkout snapshot could not be restored.");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string LocalOrderNumber(Guid orderId, DateTimeOffset approvedAtUtc) =>
        $"LOCAL-{approvedAtUtc:yyyyMMdd}-{orderId.ToString("N")[..12].ToUpperInvariant()}";

    private static DateOnly BusinessDate(DateTimeOffset createdAtUtc) =>
        DateOnly.FromDateTime(createdAtUtc.ToLocalTime().Date);

    private static string IdempotencyKey(Guid storeId, Guid terminalId, Guid localOrderId) =>
        $"{storeId:N}:{terminalId:N}:{localOrderId:N}";

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
        decimal Total);

    private sealed record CartLineSnapshotPayload(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);

    private sealed record PaymentSnapshotPayload(
        PaymentMethod Method,
        decimal RequestedAmount,
        PaymentStatus Status,
        decimal? ApprovedAmount,
        string? ApprovalCode,
        string? TransactionReference,
        DateTimeOffset? ApprovedAtUtc,
        string? FailureMessage);

    private sealed record OrderUploadPayload(
        int SchemaVersion,
        Guid StoreId,
        Guid TerminalId,
        Guid LocalOrderId,
        string IdempotencyKey,
        string LocalOrderNumber,
        DateOnly BusinessDate,
        Guid CashierId,
        decimal SubtotalAmount,
        decimal DiscountAmount,
        decimal TotalAmount,
        DateTimeOffset CreatedAt,
        IReadOnlyList<OrderUploadLinePayload> Lines,
        IReadOnlyList<OrderUploadPaymentPayload> Payments)
    {
        public static OrderUploadPayload From(Order order, string idempotencyKey) => new(
            OrderUploadSchemaVersion,
            order.StoreId,
            order.TerminalId,
            order.LocalOrderId,
            idempotencyKey,
            order.LocalOrderNumber,
            order.BusinessDate,
            order.CashierId,
            order.SubtotalAmount,
            order.DiscountAmount,
            order.TotalAmount,
            order.CreatedAtUtc,
            order.Lines.Select(OrderUploadLinePayload.From).ToArray(),
            order.Payments.Select(OrderUploadPaymentPayload.From).ToArray());
    }

    private sealed record OrderUploadLinePayload(
        Guid ProductId,
        string ProductNameSnapshot,
        decimal UnitPrice,
        int Quantity,
        decimal LineDiscountAmount,
        decimal LineTotalAmount)
    {
        public static OrderUploadLinePayload From(OrderLine line) => new(
            line.ProductId,
            line.ProductNameSnapshot,
            line.UnitPrice,
            line.Quantity,
            line.LineDiscountAmount,
            line.LineTotalAmount);
    }

    private sealed record OrderUploadPaymentPayload(
        string PaymentMethod,
        decimal ApprovedAmount,
        string ApprovalCode,
        string? TransactionReference,
        DateTimeOffset ApprovedAtUtc)
    {
        public static OrderUploadPaymentPayload From(Payment payment)
        {
            if (payment.Status != PaymentStatus.Approved ||
                payment.ApprovedAmount is null ||
                payment.ApprovedAtUtc is null)
            {
                throw new InvalidOperationException("Only approved payments can be added to the order upload payload.");
            }

            return new OrderUploadPaymentPayload(
                payment.Method.ToString(),
                payment.ApprovedAmount.Value,
                RequiredText(payment.ApprovalCode, nameof(payment.ApprovalCode)),
                payment.TransactionReference,
                payment.ApprovedAtUtc.Value);
        }
    }

    private static string RequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{parameterName} is required for the order upload payload.");
        }

        return value;
    }
}

public sealed record OrderCompletionResult(Guid LocalOrderId, bool AlreadyCompleted);
