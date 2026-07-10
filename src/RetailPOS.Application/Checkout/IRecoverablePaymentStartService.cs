using RetailPOS.Application.Payments;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetailPOS.Application.Checkout;

public interface IRecoverablePaymentStartService
{
    Task<RecoverablePaymentStartResult> StartAsync(
        CartSnapshot cart,
        PaymentMethod method,
        PaymentSimulationMode mode = PaymentSimulationMode.Approve,
        CancellationToken cancellationToken = default);
}

public sealed class RecoverablePaymentStartService(
    IPendingCheckoutRepository pendingCheckoutRepository,
    IPaymentSimulator paymentSimulator,
    ICheckoutContextProvider checkoutContextProvider,
    ICheckoutClock clock,
    ICheckoutIdGenerator idGenerator) : IRecoverablePaymentStartService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<RecoverablePaymentStartResult> StartAsync(
        CartSnapshot cart,
        PaymentMethod method,
        PaymentSimulationMode mode = PaymentSimulationMode.Approve,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);
        if (cart.IsEmpty || cart.Total <= 0)
        {
            throw new InvalidOperationException("Payment requires a positive checkout total.");
        }

        var context = checkoutContextProvider.GetCurrent();
        var checkoutId = idGenerator.NewId();
        var createdAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        var paymentRequest = new PaymentRequestSnapshot(method, cart.Total, createdAtUtc);
        var awaitingPayment = new PendingCheckoutRecord(
            checkoutId,
            context.StoreId,
            context.TerminalId,
            context.CashierId,
            createdAtUtc,
            PendingCheckoutStatus.AwaitingPayment,
            Serialize(CartSnapshotPayload.From(cart)),
            Serialize(paymentRequest),
            PaymentStatus.Pending,
            null,
            null,
            null,
            null,
            null,
            null,
            createdAtUtc);

        await pendingCheckoutRepository.SaveAsync(awaitingPayment, cancellationToken);

        var simulation = await paymentSimulator.SimulateAsync(
            new PaymentSimulationRequest(method, cart.Total, mode),
            cancellationToken);

        var updated = simulation.IsApproved
            ? ApprovedRecord(awaitingPayment, simulation)
            : NonApprovedRecord(awaitingPayment, simulation);

        await pendingCheckoutRepository.SaveAsync(updated, cancellationToken);

        return new RecoverablePaymentStartResult(
            updated.Id,
            updated.OrderId,
            updated.RecoveryStatus,
            updated.PaymentStatus,
            simulation.Method,
            simulation.RequestedAmount,
            updated.ApprovedAmount,
            updated.ApprovalCode,
            updated.TransactionReference,
            updated.PaymentApprovedAtUtc,
            simulation.FailureMessage);
    }

    private PendingCheckoutRecord ApprovedRecord(
        PendingCheckoutRecord awaitingPayment,
        PaymentSimulationResult simulation)
    {
        if (simulation.ApprovedAmount is null || simulation.ApprovedAtUtc is null)
        {
            throw new InvalidOperationException("Approved payment simulation must include approval details.");
        }

        var approvedAtUtc = EnsureUtc(simulation.ApprovedAtUtc.Value, nameof(simulation.ApprovedAtUtc));
        var orderId = idGenerator.NewId();
        var paymentSnapshot = new PaymentResultSnapshot(
            simulation.Method,
            simulation.RequestedAmount,
            PaymentStatus.Approved,
            simulation.ApprovedAmount,
            simulation.ApprovalCode,
            simulation.TransactionReference,
            approvedAtUtc,
            null);

        return awaitingPayment with
        {
            RecoveryStatus = PendingCheckoutStatus.ApprovedButOrderNotCreated,
            PaymentSnapshotJson = Serialize(paymentSnapshot),
            PaymentStatus = PaymentStatus.Approved,
            ApprovalCode = simulation.ApprovalCode,
            ApprovedAmount = simulation.ApprovedAmount,
            TransactionReference = simulation.TransactionReference,
            PaymentApprovedAtUtc = approvedAtUtc,
            OrderId = orderId,
            LastUpdatedAtUtc = approvedAtUtc
        };
    }

    private PendingCheckoutRecord NonApprovedRecord(
        PendingCheckoutRecord awaitingPayment,
        PaymentSimulationResult simulation)
    {
        var failedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        var paymentSnapshot = new PaymentResultSnapshot(
            simulation.Method,
            simulation.RequestedAmount,
            simulation.Status,
            null,
            null,
            null,
            null,
            simulation.FailureMessage);

        return awaitingPayment with
        {
            RecoveryStatus = PendingCheckoutStatus.PaymentFailed,
            PaymentSnapshotJson = Serialize(paymentSnapshot),
            PaymentStatus = simulation.Status,
            ApprovalCode = null,
            ApprovedAmount = null,
            TransactionReference = null,
            PaymentApprovedAtUtc = null,
            OrderId = null,
            LastUpdatedAtUtc = failedAtUtc
        };
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

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
        public static CartSnapshotPayload From(CartSnapshot snapshot) => new(
            snapshot.Lines.Select(line => new CartLineSnapshotPayload(
                line.ProductId,
                line.ProductName,
                line.UnitPrice,
                line.Quantity,
                line.LineTotal)).ToArray(),
            snapshot.Subtotal,
            snapshot.DiscountType?.ToString(),
            snapshot.DiscountValue,
            snapshot.DiscountAmount,
            snapshot.Total);
    }

    private sealed record CartLineSnapshotPayload(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);

    private sealed record PaymentRequestSnapshot(
        PaymentMethod Method,
        decimal RequestedAmount,
        DateTimeOffset RequestedAtUtc);

    private sealed record PaymentResultSnapshot(
        PaymentMethod Method,
        decimal RequestedAmount,
        PaymentStatus Status,
        decimal? ApprovedAmount,
        string? ApprovalCode,
        string? TransactionReference,
        DateTimeOffset? ApprovedAtUtc,
        string? FailureMessage);
}

public sealed record RecoverablePaymentStartResult(
    Guid PendingCheckoutId,
    Guid? OrderId,
    PendingCheckoutStatus RecoveryStatus,
    PaymentStatus PaymentStatus,
    PaymentMethod Method,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string? ApprovalCode,
    string? TransactionReference,
    DateTimeOffset? ApprovedAtUtc,
    string? FailureMessage)
{
    public bool IsApproved => PaymentStatus == PaymentStatus.Approved;
}
