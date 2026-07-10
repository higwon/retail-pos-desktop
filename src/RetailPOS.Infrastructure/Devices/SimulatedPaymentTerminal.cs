using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Infrastructure.Devices;

public enum PaymentTerminalSimulationScenario
{
    Approve,
    Decline,
    Cancel,
    Timeout,
    CommunicationError
}

public sealed record PaymentTerminalSimulationSettings(
    PaymentTerminalSimulationScenario Scenario,
    TimeSpan ResponseDelay)
{
    public static PaymentTerminalSimulationSettings Default { get; } =
        new(PaymentTerminalSimulationScenario.Approve, TimeSpan.Zero);
}

public interface IPaymentTerminalSimulatorControl
{
    PaymentTerminalSimulationSettings Current { get; }
    void ConfigureNext(PaymentTerminalSimulationSettings settings);
}

public sealed class SimulatedPaymentTerminal(TimeProvider timeProvider)
    : IPaymentTerminal, IPaymentTerminalSimulatorControl
{
    private readonly object _sync = new();
    private PaymentTerminalSimulationSettings _next = PaymentTerminalSimulationSettings.Default;

    public PaymentTerminalSimulationSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _next;
            }
        }
    }

    public void ConfigureNext(PaymentTerminalSimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ResponseDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Payment terminal response delay cannot be negative.");
        }

        lock (_sync)
        {
            _next = settings;
        }
    }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var settings = TakeNextSettings();
        if (settings.ResponseDelay > TimeSpan.Zero)
        {
            await Task.Delay(settings.ResponseDelay, timeProvider, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return settings.Scenario switch
        {
            PaymentTerminalSimulationScenario.Approve => Approved(request),
            PaymentTerminalSimulationScenario.Decline => NonApproved(
                request,
                PaymentStatus.Failed,
                "Card payment was declined."),
            PaymentTerminalSimulationScenario.Cancel => NonApproved(
                request,
                PaymentStatus.Cancelled,
                "Card payment was cancelled by the terminal."),
            PaymentTerminalSimulationScenario.Timeout => NonApproved(
                request,
                PaymentStatus.Unknown,
                "Card payment timed out. Approval status is unknown and requires review."),
            PaymentTerminalSimulationScenario.CommunicationError => NonApproved(
                request,
                PaymentStatus.Unknown,
                "Card terminal communication was lost. Approval status is unknown and requires review."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.Scenario,
                "Unsupported payment terminal simulation scenario.")
        };
    }

    private PaymentTerminalSimulationSettings TakeNextSettings()
    {
        lock (_sync)
        {
            var settings = _next;
            _next = PaymentTerminalSimulationSettings.Default;
            return settings;
        }
    }

    private PaymentAuthorizationResult Approved(PaymentAuthorizationRequest request)
    {
        var approvedAtUtc = timeProvider.GetUtcNow();
        var amountCode = decimal.ToInt64(request.Amount).ToString("000000000000");
        return new PaymentAuthorizationResult(
            PaymentStatus.Approved,
            request.Amount,
            request.Amount,
            $"APP-CARD-{amountCode}",
            $"SIM-CARD-{request.PaymentAttemptId:N}",
            approvedAtUtc,
            null);
    }

    private static PaymentAuthorizationResult NonApproved(
        PaymentAuthorizationRequest request,
        PaymentStatus status,
        string message) =>
        new(status, request.Amount, null, null, null, null, message);

    private static void Validate(PaymentAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PaymentAttemptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Payment attempt identity is required.",
                nameof(request));
        }

        if (request.Amount <= 0m || decimal.Truncate(request.Amount) != request.Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Payment amount must be a positive whole-KRW value.");
        }
    }
}
