using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Infrastructure.Devices;

public enum PaymentTerminalSimulationScenario { Approve, Decline, Cancel, Timeout, CommunicationError, Unknown }
public enum PaymentTerminalConnectionState { Disconnected, Connected }
public enum PaymentTerminalOperationalState { Disconnected, Idle, WaitingForCard, Processing, Approved, Declined, Cancelled, Unknown, Faulted }

public sealed record PaymentTerminalSimulationSettings(PaymentTerminalSimulationScenario Scenario, TimeSpan ResponseDelay)
{
    public static PaymentTerminalSimulationSettings Default { get; } = new(PaymentTerminalSimulationScenario.Approve, TimeSpan.Zero);
}

public interface IPaymentTerminalSimulatorControl
{
    event EventHandler? StateChanged;
    PaymentTerminalSimulationSettings Current { get; }
    PaymentTerminalConnectionState ConnectionState { get; }
    PaymentTerminalOperationalState OperationalState { get; }
    PaymentStatus? LastOutcome { get; }
    void ConfigureNext(PaymentTerminalSimulationSettings settings);
    void Connect();
    void Disconnect();
    void Reset();
}

public sealed class SimulatedPaymentTerminal(TimeProvider timeProvider) : IPaymentTerminal, IPaymentTerminalSimulatorControl, IDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PaymentTerminalSimulationSettings _next = PaymentTerminalSimulationSettings.Default;
    private PaymentTerminalConnectionState _connection = PaymentTerminalConnectionState.Connected;
    private PaymentTerminalOperationalState _state = PaymentTerminalOperationalState.Idle;
    private PaymentStatus? _lastOutcome;
    private CancellationTokenSource? _activeCancellation;
    private bool _disposed;

    public event EventHandler? StateChanged;
    public PaymentTerminalSimulationSettings Current { get { lock (_sync) return _next; } }
    public PaymentTerminalConnectionState ConnectionState { get { lock (_sync) return _connection; } }
    public PaymentTerminalOperationalState OperationalState { get { lock (_sync) return _state; } }
    public PaymentStatus? LastOutcome { get { lock (_sync) return _lastOutcome; } }

    public void ConfigureNext(PaymentTerminalSimulationSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Scenario)) throw new ArgumentOutOfRangeException(nameof(settings), "Unsupported payment terminal scenario.");
        if (settings.ResponseDelay < TimeSpan.Zero || settings.ResponseDelay > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(settings), "Response delay must be between zero and one minute.");
        lock (_sync) _next = settings; RaiseChanged();
    }

    public void Connect() { ObjectDisposedException.ThrowIf(_disposed, this); lock (_sync) { _connection = PaymentTerminalConnectionState.Connected; _state = PaymentTerminalOperationalState.Idle; } RaiseChanged(); }
    public void Disconnect() { ObjectDisposedException.ThrowIf(_disposed, this); CancellationTokenSource? active; lock (_sync) { _connection = PaymentTerminalConnectionState.Disconnected; _state = PaymentTerminalOperationalState.Disconnected; active = _activeCancellation; } active?.Cancel(); RaiseChanged(); }
    public void Reset() { ObjectDisposedException.ThrowIf(_disposed, this); lock (_sync) { if (_state is PaymentTerminalOperationalState.WaitingForCard or PaymentTerminalOperationalState.Processing) throw new InvalidOperationException("Terminal cannot reset during authorization."); _next = PaymentTerminalSimulationSettings.Default; _lastOutcome = null; _state = _connection == PaymentTerminalConnectionState.Connected ? PaymentTerminalOperationalState.Idle : PaymentTerminalOperationalState.Disconnected; } RaiseChanged(); }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); Validate(request); cancellationToken.ThrowIfCancellationRequested();
        if (!_gate.Wait(0)) return NonApproved(request, PaymentStatus.Failed, "Card terminal is busy.");
        try
        {
            PaymentTerminalSimulationSettings settings;
            CancellationTokenSource operationCancellation;
            lock (_sync)
            {
                if (_connection == PaymentTerminalConnectionState.Disconnected) return NonApproved(request, PaymentStatus.Failed, "Card terminal is disconnected.");
                settings = _next; _next = PaymentTerminalSimulationSettings.Default; _state = PaymentTerminalOperationalState.WaitingForCard;
                operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeCancellation = operationCancellation;
            }
            RaiseChanged(); SetState(PaymentTerminalOperationalState.Processing);
            try { if (settings.ResponseDelay > TimeSpan.Zero) await Task.Delay(settings.ResponseDelay, timeProvider, operationCancellation.Token); operationCancellation.Token.ThrowIfCancellationRequested(); }
            catch (OperationCanceledException) { return Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Unknown, "Card payment was interrupted after dispatch. Approval status is unknown and requires review."); }

            return settings.Scenario switch
            {
                PaymentTerminalSimulationScenario.Approve => CompleteApproved(request),
                PaymentTerminalSimulationScenario.Decline => Complete(request, PaymentStatus.Failed, PaymentTerminalOperationalState.Declined, "Card payment was declined."),
                PaymentTerminalSimulationScenario.Cancel => Complete(request, PaymentStatus.Cancelled, PaymentTerminalOperationalState.Cancelled, "Card payment was cancelled by the terminal."),
                PaymentTerminalSimulationScenario.Timeout => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Unknown, "Card payment timed out. Approval status is unknown and requires review."),
                PaymentTerminalSimulationScenario.CommunicationError => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Faulted, "Card terminal communication was lost. Approval status is unknown and requires review."),
                PaymentTerminalSimulationScenario.Unknown => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Unknown, "Card payment outcome is unknown and requires review."),
                _ => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Faulted, "Unsupported terminal outcome requires review.")
            };
        }
        finally { CancellationTokenSource? active; lock (_sync) { active = _activeCancellation; _activeCancellation = null; _state = _connection == PaymentTerminalConnectionState.Connected ? PaymentTerminalOperationalState.Idle : PaymentTerminalOperationalState.Disconnected; } active?.Dispose(); _gate.Release(); RaiseChanged(); }
    }

    private PaymentAuthorizationResult CompleteApproved(PaymentAuthorizationRequest request)
    {
        var at = timeProvider.GetUtcNow(); SetOutcome(PaymentStatus.Approved, PaymentTerminalOperationalState.Approved);
        return new(PaymentStatus.Approved, request.Amount, request.Amount, $"APP-CARD-{decimal.ToInt64(request.Amount):000000000000}", $"SIM-CARD-{request.PaymentAttemptId:N}", at, null);
    }
    private PaymentAuthorizationResult Complete(PaymentAuthorizationRequest request, PaymentStatus status, PaymentTerminalOperationalState state, string message) { SetOutcome(status, state); return NonApproved(request, status, message); }
    private void SetState(PaymentTerminalOperationalState state) { lock (_sync) _state = state; RaiseChanged(); }
    private void SetOutcome(PaymentStatus status, PaymentTerminalOperationalState state) { lock (_sync) { _lastOutcome = status; _state = state; } RaiseChanged(); }
    private static PaymentAuthorizationResult NonApproved(PaymentAuthorizationRequest r, PaymentStatus s, string m) => new(s, r.Amount, null, null, null, null, m);
    private static void Validate(PaymentAuthorizationRequest r) { ArgumentNullException.ThrowIfNull(r); if (r.PaymentAttemptId == Guid.Empty) throw new ArgumentException("Payment attempt identity is required.", nameof(r)); if (r.Amount <= 0 || decimal.Truncate(r.Amount) != r.Amount) throw new ArgumentOutOfRangeException(nameof(r)); }
    private void RaiseChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    public void Dispose() { _disposed = true; lock (_sync) _activeCancellation?.Cancel(); }
}
