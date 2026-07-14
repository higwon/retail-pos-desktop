namespace RetailPOS.Api.Orders;

public sealed record OrderUploadRequest(
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
    IReadOnlyList<OrderUploadLineRequest> Lines,
    IReadOnlyList<OrderUploadPaymentRequest> Payments)
{
    public const int CurrentSchemaVersion = 1;

    public OrderUploadValidationResult Validate()
    {
        var errors = new Dictionary<string, string[]>();

        RequireSchemaVersion(errors);
        RequireIdentity(errors, StoreId, nameof(StoreId));
        RequireIdentity(errors, TerminalId, nameof(TerminalId));
        RequireIdentity(errors, LocalOrderId, nameof(LocalOrderId));
        RequireIdentity(errors, CashierId, nameof(CashierId));
        RequireText(errors, IdempotencyKey, nameof(IdempotencyKey));
        RequireText(errors, LocalOrderNumber, nameof(LocalOrderNumber));

        if (BusinessDate == default)
        {
            errors[nameof(BusinessDate)] = ["businessDate is required."];
        }

        if (CreatedAt == default)
        {
            errors[nameof(CreatedAt)] = ["createdAt is required."];
        }
        else if (CreatedAt.Offset != TimeSpan.Zero)
        {
            errors[nameof(CreatedAt)] = ["createdAt must be a UTC timestamp."];
        }

        ValidateMoney(errors, SubtotalAmount, nameof(SubtotalAmount));
        ValidateMoney(errors, DiscountAmount, nameof(DiscountAmount));
        ValidateMoney(errors, TotalAmount, nameof(TotalAmount));
        if (DiscountAmount > SubtotalAmount)
        {
            errors[nameof(DiscountAmount)] = ["discountAmount cannot exceed subtotalAmount."];
        }

        if (TotalAmount != SubtotalAmount - DiscountAmount)
        {
            errors[nameof(TotalAmount)] = ["totalAmount must equal subtotalAmount minus discountAmount."];
        }

        ValidateLines(errors);
        ValidatePayments(errors);

        return errors.Count == 0
            ? OrderUploadValidationResult.Success()
            : OrderUploadValidationResult.Failure(errors);
    }

    private void RequireSchemaVersion(IDictionary<string, string[]> errors)
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            errors[nameof(SchemaVersion)] = [$"schemaVersion must be {CurrentSchemaVersion}."];
        }
    }

    private void ValidateLines(IDictionary<string, string[]> errors)
    {
        if (Lines is null || Lines.Count == 0)
        {
            errors[nameof(Lines)] = ["at least one order line is required."];
            return;
        }

        var lineTotal = 0m;
        for (var index = 0; index < Lines.Count; index++)
        {
            var line = Lines[index];
            var prefix = $"{nameof(Lines)}[{index}]";
            RequireIdentity(errors, line.ProductId, $"{prefix}.{nameof(line.ProductId)}");
            RequireText(errors, line.ProductNameSnapshot, $"{prefix}.{nameof(line.ProductNameSnapshot)}");
            ValidateMoney(errors, line.UnitPrice, $"{prefix}.{nameof(line.UnitPrice)}");
            ValidateMoney(errors, line.LineDiscountAmount, $"{prefix}.{nameof(line.LineDiscountAmount)}");
            ValidateMoney(errors, line.LineTotalAmount, $"{prefix}.{nameof(line.LineTotalAmount)}");

            if (line.Quantity <= 0)
            {
                errors[$"{prefix}.{nameof(line.Quantity)}"] = ["quantity must be greater than zero."];
            }

            var grossAmount = line.UnitPrice * line.Quantity;
            if (line.LineDiscountAmount > grossAmount)
            {
                errors[$"{prefix}.{nameof(line.LineDiscountAmount)}"] =
                    ["lineDiscountAmount cannot exceed line gross amount."];
            }

            if (line.LineTotalAmount != grossAmount - line.LineDiscountAmount)
            {
                errors[$"{prefix}.{nameof(line.LineTotalAmount)}"] =
                    ["lineTotalAmount must equal unitPrice times quantity minus lineDiscountAmount."];
            }

            lineTotal += line.LineTotalAmount;
        }

        if (lineTotal != TotalAmount)
        {
            errors[nameof(Lines)] = ["order line totals must equal totalAmount."];
        }
    }

    private void ValidatePayments(IDictionary<string, string[]> errors)
    {
        if (Payments is null || Payments.Count == 0)
        {
            errors[nameof(Payments)] = ["at least one approved payment is required."];
            return;
        }

        var paymentTotal = 0m;
        for (var index = 0; index < Payments.Count; index++)
        {
            var payment = Payments[index];
            var prefix = $"{nameof(Payments)}[{index}]";
            RequireText(errors, payment.PaymentMethod, $"{prefix}.{nameof(payment.PaymentMethod)}");
            ValidateMoney(errors, payment.ApprovedAmount, $"{prefix}.{nameof(payment.ApprovedAmount)}");
            RequireText(errors, payment.ApprovalCode, $"{prefix}.{nameof(payment.ApprovalCode)}");
            ValidateCashMetadata(errors, payment, prefix);

            if (payment.ApprovedAtUtc == default)
            {
                errors[$"{prefix}.{nameof(payment.ApprovedAtUtc)}"] =
                    ["approvedAtUtc is required."];
            }
            else if (payment.ApprovedAtUtc.Offset != TimeSpan.Zero)
            {
                errors[$"{prefix}.{nameof(payment.ApprovedAtUtc)}"] =
                    ["approvedAtUtc must be a UTC timestamp."];
            }

            paymentTotal += payment.ApprovedAmount;
        }

        if (paymentTotal != TotalAmount)
        {
            errors[nameof(Payments)] = ["approved payment total must equal totalAmount."];
        }
    }

    private static void ValidateCashMetadata(
        IDictionary<string, string[]> errors,
        OrderUploadPaymentRequest payment,
        string prefix)
    {
        var isCash = string.Equals(payment.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase);
        if (!isCash)
        {
            if (payment.CashTenderedAmount is not null || payment.ChangeAmount is not null)
            {
                errors[$"{prefix}.{nameof(payment.CashTenderedAmount)}"] =
                    ["cash tender metadata is only valid for cash payments."];
            }

            return;
        }

        // Both null accepts queued payloads created before schema v1 gained optional cash metadata.
        if (payment.CashTenderedAmount is null && payment.ChangeAmount is null)
        {
            return;
        }

        if (payment.CashTenderedAmount is null || payment.ChangeAmount is null)
        {
            errors[$"{prefix}.{nameof(payment.CashTenderedAmount)}"] =
                ["cashTenderedAmount and changeAmount must be provided together."];
            return;
        }

        ValidateMoney(
            errors,
            payment.CashTenderedAmount.Value,
            $"{prefix}.{nameof(payment.CashTenderedAmount)}");
        ValidateMoney(
            errors,
            payment.ChangeAmount.Value,
            $"{prefix}.{nameof(payment.ChangeAmount)}");
        if (payment.CashTenderedAmount < payment.ApprovedAmount ||
            payment.ChangeAmount != payment.CashTenderedAmount - payment.ApprovedAmount)
        {
            errors[$"{prefix}.{nameof(payment.ChangeAmount)}"] =
                ["changeAmount must equal cashTenderedAmount minus approvedAmount."];
        }
    }

    private static void RequireIdentity(
        IDictionary<string, string[]> errors,
        Guid value,
        string key)
    {
        if (value == Guid.Empty)
        {
            errors[key] = [$"{ToJsonName(key)} is required."];
        }
    }

    private static void RequireText(
        IDictionary<string, string[]> errors,
        string? value,
        string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[key] = [$"{ToJsonName(key)} is required."];
        }
    }

    private static void ValidateMoney(
        IDictionary<string, string[]> errors,
        decimal value,
        string key)
    {
        if (value < 0m || value != decimal.Truncate(value))
        {
            errors[key] = [$"{ToJsonName(key)} must be a whole non-negative amount."];
        }
    }

    private static string ToJsonName(string name) =>
        char.ToLowerInvariant(name[0]) + name[1..];
}

public sealed record OrderUploadLineRequest(
    Guid ProductId,
    string ProductNameSnapshot,
    decimal UnitPrice,
    int Quantity,
    decimal LineDiscountAmount,
    decimal LineTotalAmount);

public sealed record OrderUploadPaymentRequest(
    string PaymentMethod,
    decimal ApprovedAmount,
    string ApprovalCode,
    string? TransactionReference,
    DateTimeOffset ApprovedAtUtc,
    decimal? CashTenderedAmount = null,
    decimal? ChangeAmount = null);

public sealed class OrderUploadValidationResult
{
    private OrderUploadValidationResult(IDictionary<string, string[]> errors)
    {
        Errors = errors;
    }

    public bool Succeeded => Errors.Count == 0;
    public IDictionary<string, string[]> Errors { get; }

    public static OrderUploadValidationResult Success() => new(new Dictionary<string, string[]>());
    public static OrderUploadValidationResult Failure(IDictionary<string, string[]> errors) => new(errors);
}
