using System.Text.Json;
using System.Text.Json.Serialization;
using RetailPOS.Application.Payments;
using RetailPOS.Application.Persistence;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Checkout;

public interface IRecoverablePaymentStartService
{
    Task<RecoverablePaymentStartResult> StartAsync(
        CartSnapshot cart,
        PaymentMethod method,
        CancellationToken cancellationToken = default);

    Task<RecoverablePaymentStartResult> StartCashAsync(
        CartSnapshot cart,
        decimal tenderedAmount,
        CancellationToken cancellationToken = default) =>
        StartAsync(cart, PaymentMethod.Cash, cancellationToken);
}

public sealed class RecoverablePaymentStartService(
    IPendingCheckoutRepository pendingCheckoutRepository,
    IPaymentTerminal paymentTerminal,
    ICashPaymentProcessor cashPaymentProcessor,
    ICheckoutContextProvider checkoutContextProvider,
    ICheckoutClock clock,
    ICheckoutIdGenerator idGenerator) : IRecoverablePaymentStartService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _startGate = new(1, 1);

    public async Task<RecoverablePaymentStartResult> StartAsync(
        CartSnapshot cart,
        PaymentMethod method,
        CancellationToken cancellationToken = default)
    {
        if (!await _startGate.WaitAsync(0))
        {
            throw new InvalidOperationException("A payment attempt is already in progress.");
        }

        try
        {
            return await StartCoreAsync(
                cart,
                method,
                method == PaymentMethod.Cash ? cart.Total : null,
                cancellationToken);
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task<RecoverablePaymentStartResult> StartCashAsync(
        CartSnapshot cart,
        decimal tenderedAmount,
        CancellationToken cancellationToken = default)
    {
        if (!await _startGate.WaitAsync(0))
        {
            throw new InvalidOperationException("A payment attempt is already in progress.");
        }

        try
        {
            return await StartCoreAsync(cart, PaymentMethod.Cash, tenderedAmount, cancellationToken);
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task<RecoverablePaymentStartResult> StartCoreAsync(
        CartSnapshot cart,
        PaymentMethod method,
        decimal? cashTenderedAmount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cart);
        if (cart.IsEmpty || cart.Total <= 0m)
        {
            throw new InvalidOperationException("Payment requires a positive checkout total.");
        }

        if (method is not PaymentMethod.Card and not PaymentMethod.Cash)
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported payment method.");
        }

        if (method == PaymentMethod.Card && cashTenderedAmount is not null)
        {
            throw new ArgumentException("Card payments cannot include cash tender metadata.");
        }

        if (method == PaymentMethod.Cash)
        {
            ValidateCashTender(cart.Total, cashTenderedAmount);
        }

        var context = checkoutContextProvider.GetCurrent();
        await EnsureNoActivePaymentAsync(context, CancellationToken.None);

        var checkoutId = idGenerator.NewId();
        var createdAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        var paymentRequest = new PaymentRequestSnapshot(
            checkoutId,
            method,
            cart.Total,
            createdAtUtc,
            cashTenderedAmount,
            cashTenderedAmount is null ? null : cashTenderedAmount - cart.Total);
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
            createdAtUtc,
            cashTenderedAmount,
            cashTenderedAmount is null ? null : cashTenderedAmount - cart.Total);

        // Once created, payment-attempt state must remain durable even if the caller cancels.
        await pendingCheckoutRepository.SaveAsync(awaitingPayment, CancellationToken.None);

        PaymentAuthorizationResult authorization;
        if (cancellationToken.IsCancellationRequested)
        {
            authorization = Cancelled(
                cart.Total,
                "Payment was cancelled before it was sent to the payment device.");
        }
        else
        {
            authorization = await AuthorizeAsync(
                checkoutId,
                cart.Total,
                method,
                cashTenderedAmount,
                cancellationToken);
        }

        authorization = Normalize(authorization, cart.Total, method);
        var updated = authorization.Status switch
        {
            PaymentStatus.Approved => ApprovedRecord(awaitingPayment, method, authorization),
            PaymentStatus.Failed or PaymentStatus.Cancelled =>
                NonApprovedRecord(awaitingPayment, method, authorization),
            PaymentStatus.Unknown => UnknownRecord(awaitingPayment, method, authorization),
            _ => UnknownRecord(
                awaitingPayment,
                method,
                Unknown(cart.Total, "Payment outcome could not be confirmed and requires review."))
        };

        await pendingCheckoutRepository.SaveAsync(updated, CancellationToken.None);
        return ToResult(updated, method, authorization);
    }

    private async Task<PaymentAuthorizationResult> AuthorizeAsync(
        Guid paymentAttemptId,
        decimal amount,
        PaymentMethod method,
        decimal? cashTenderedAmount,
        CancellationToken cancellationToken)
    {
        try
        {
            return method switch
            {
                PaymentMethod.Card => await paymentTerminal.AuthorizeAsync(
                    new PaymentAuthorizationRequest(paymentAttemptId, amount),
                    cancellationToken),
                PaymentMethod.Cash => await cashPaymentProcessor.AcceptAsync(
                    new CashPaymentRequest(
                        paymentAttemptId,
                        amount,
                        cashTenderedAmount ?? amount),
                    cancellationToken),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method,
                    "Unsupported payment method.")
            };
        }
        catch (OperationCanceledException) when (method == PaymentMethod.Card)
        {
            return Unknown(
                amount,
                "Card payment was interrupted after it was sent. Approval status is unknown and requires review.");
        }
        catch (OperationCanceledException)
        {
            return Cancelled(amount, "Cash payment was cancelled before acceptance.");
        }
        catch (Exception) when (method == PaymentMethod.Card)
        {
            return Unknown(
                amount,
                "Card terminal response could not be confirmed. Approval status is unknown and requires review.");
        }
        catch (Exception)
        {
            return Failed(amount, "Cash payment could not be recorded. Cart was not changed.");
        }
    }

    private async Task EnsureNoActivePaymentAsync(
        CheckoutContext context,
        CancellationToken cancellationToken)
    {
        var unresolved = await pendingCheckoutRepository.GetUnresolvedAsync(cancellationToken);
        var hasActivePayment = unresolved.Any(record =>
            record.StoreId == context.StoreId &&
            record.TerminalId == context.TerminalId &&
            record.RecoveryStatus is
                PendingCheckoutStatus.AwaitingPayment or
                PendingCheckoutStatus.ApprovedButOrderNotCreated or
                PendingCheckoutStatus.ManagerReviewRequired);

        if (hasActivePayment)
        {
            throw new InvalidOperationException(
                "This terminal has an unresolved payment that must be completed or reviewed before retry.");
        }
    }

    private PendingCheckoutRecord ApprovedRecord(
        PendingCheckoutRecord awaitingPayment,
        PaymentMethod method,
        PaymentAuthorizationResult authorization)
    {
        var approvedAtUtc = EnsureUtc(authorization.ApprovedAtUtc!.Value, nameof(authorization.ApprovedAtUtc));
        var orderId = idGenerator.NewId();
        return awaitingPayment with
        {
            RecoveryStatus = PendingCheckoutStatus.ApprovedButOrderNotCreated,
            PaymentSnapshotJson = Serialize(PaymentResultSnapshot.From(method, authorization)),
            PaymentStatus = PaymentStatus.Approved,
            ApprovalCode = authorization.ApprovalCode,
            ApprovedAmount = authorization.ApprovedAmount,
            CashTenderedAmount = authorization.CashTenderedAmount,
            ChangeAmount = authorization.ChangeAmount,
            TransactionReference = authorization.TransactionReference,
            PaymentApprovedAtUtc = approvedAtUtc,
            OrderId = orderId,
            LastUpdatedAtUtc = approvedAtUtc
        };
    }

    private PendingCheckoutRecord NonApprovedRecord(
        PendingCheckoutRecord awaitingPayment,
        PaymentMethod method,
        PaymentAuthorizationResult authorization)
    {
        var updatedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        return awaitingPayment with
        {
            RecoveryStatus = PendingCheckoutStatus.PaymentFailed,
            PaymentSnapshotJson = Serialize(PaymentResultSnapshot.From(method, authorization)),
            PaymentStatus = authorization.Status,
            ApprovalCode = null,
            ApprovedAmount = null,
            CashTenderedAmount = null,
            ChangeAmount = null,
            TransactionReference = null,
            PaymentApprovedAtUtc = null,
            OrderId = null,
            LastUpdatedAtUtc = updatedAtUtc
        };
    }

    private PendingCheckoutRecord UnknownRecord(
        PendingCheckoutRecord awaitingPayment,
        PaymentMethod method,
        PaymentAuthorizationResult authorization)
    {
        var updatedAtUtc = EnsureUtc(clock.UtcNow, nameof(clock.UtcNow));
        return awaitingPayment with
        {
            RecoveryStatus = PendingCheckoutStatus.ManagerReviewRequired,
            PaymentSnapshotJson = Serialize(PaymentResultSnapshot.From(method, authorization)),
            PaymentStatus = PaymentStatus.Unknown,
            ApprovalCode = null,
            ApprovedAmount = null,
            CashTenderedAmount = null,
            ChangeAmount = null,
            TransactionReference = null,
            PaymentApprovedAtUtc = null,
            OrderId = null,
            LastUpdatedAtUtc = updatedAtUtc
        };
    }

    private static PaymentAuthorizationResult Normalize(
        PaymentAuthorizationResult result,
        decimal requestedAmount,
        PaymentMethod method)
    {
        if (result.RequestedAmount != requestedAmount)
        {
            return Unknown(requestedAmount, "Payment amount did not match the request and requires review.");
        }

        if (result.Status == PaymentStatus.Approved &&
            (result.ApprovedAmount != requestedAmount ||
             result.ApprovedAtUtc is null ||
             string.IsNullOrWhiteSpace(result.ApprovalCode)))
        {
            return Unknown(requestedAmount, "Payment approval details were incomplete and require review.");
        }

        if (method == PaymentMethod.Card &&
            (result.CashTenderedAmount is not null || result.ChangeAmount is not null))
        {
            return Unknown(requestedAmount, "Card payment returned invalid cash tender metadata and requires review.");
        }

        if (method == PaymentMethod.Cash && result.Status == PaymentStatus.Approved &&
            (result.CashTenderedAmount is null ||
             result.ChangeAmount is null ||
             result.CashTenderedAmount < requestedAmount ||
             decimal.Truncate(result.CashTenderedAmount.Value) != result.CashTenderedAmount ||
             decimal.Truncate(result.ChangeAmount.Value) != result.ChangeAmount ||
             result.ChangeAmount != result.CashTenderedAmount - requestedAmount))
        {
            return Unknown(requestedAmount, "Cash tender details were incomplete and require review.");
        }

        return result.Status is
            PaymentStatus.Approved or
            PaymentStatus.Failed or
            PaymentStatus.Cancelled or
            PaymentStatus.Unknown
            ? result
            : Unknown(requestedAmount, "Payment outcome could not be confirmed and requires review.");
    }

    private static RecoverablePaymentStartResult ToResult(
        PendingCheckoutRecord record,
        PaymentMethod method,
        PaymentAuthorizationResult authorization) =>
        new(
            record.Id,
            record.OrderId,
            record.RecoveryStatus,
            record.PaymentStatus,
            method,
            authorization.RequestedAmount,
            record.ApprovedAmount,
            record.ApprovalCode,
            record.TransactionReference,
            record.PaymentApprovedAtUtc,
            authorization.FailureMessage,
            record.CashTenderedAmount,
            record.ChangeAmount);

    private static PaymentAuthorizationResult Failed(decimal amount, string message) =>
        new(PaymentStatus.Failed, amount, null, null, null, null, message);

    private static PaymentAuthorizationResult Cancelled(decimal amount, string message) =>
        new(PaymentStatus.Cancelled, amount, null, null, null, null, message);

    private static PaymentAuthorizationResult Unknown(decimal amount, string message) =>
        new(PaymentStatus.Unknown, amount, null, null, null, null, message);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static void ValidateCashTender(decimal amountDue, decimal? cashTenderedAmount)
    {
        if (cashTenderedAmount is null ||
            cashTenderedAmount < amountDue ||
            decimal.Truncate(cashTenderedAmount.Value) != cashTenderedAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cashTenderedAmount),
                "Cash tendered amount must be a whole-KRW value at least equal to the amount due.");
        }
    }

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
        Guid PaymentAttemptId,
        PaymentMethod Method,
        decimal RequestedAmount,
        DateTimeOffset RequestedAtUtc,
        decimal? CashTenderedAmount,
        decimal? ChangeAmount);

    private sealed record PaymentResultSnapshot(
        PaymentMethod Method,
        decimal RequestedAmount,
        PaymentStatus Status,
        decimal? ApprovedAmount,
        string? ApprovalCode,
        string? TransactionReference,
        DateTimeOffset? ApprovedAtUtc,
        string? FailureMessage,
        decimal? CashTenderedAmount,
        decimal? ChangeAmount)
    {
        public static PaymentResultSnapshot From(
            PaymentMethod method,
            PaymentAuthorizationResult result) =>
            new(
                method,
                result.RequestedAmount,
                result.Status,
                result.ApprovedAmount,
                result.ApprovalCode,
                result.TransactionReference,
                result.ApprovedAtUtc,
                result.FailureMessage,
                result.CashTenderedAmount,
                result.ChangeAmount);
    }
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
    string? FailureMessage,
    decimal? CashTenderedAmount = null,
    decimal? ChangeAmount = null)
{
    public bool IsApproved => PaymentStatus == PaymentStatus.Approved;
    public bool IsUnknown => PaymentStatus == PaymentStatus.Unknown;
}
