using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Infrastructure.Devices;

public enum PaymentTerminalResponseOutcome { Approve, Decline, Cancel, Timeout, CommunicationLoss, Unknown }
public enum PaymentTerminalConnectionState { Disconnected, Connected }
public enum PaymentTerminalOperationalState { Disconnected, Idle, Processing, Approved, Declined, Cancelled, Unknown, Faulted }

public sealed record PaymentTerminalRequestPayload(Guid PaymentAttemptId, decimal Amount);
public sealed record PaymentTerminalResponse(
    PaymentTerminalResponseOutcome Outcome,
    string? ApprovalCode = null,
    string? TransactionReference = null);

public interface IPaymentTerminalSimulatorControl
{
    event EventHandler? StateChanged;
    PaymentTerminalConnectionState ConnectionState { get; }
    PaymentTerminalOperationalState OperationalState { get; }
    PaymentStatus? LastOutcome { get; }
    DeviceRequest<PaymentTerminalRequestPayload, PaymentTerminalResponse>? PendingRequest { get; }
    IReadOnlyList<DeviceRequest<PaymentTerminalRequestPayload, PaymentTerminalResponse>> RecentRequests { get; }
    bool Respond(Guid requestId, PaymentTerminalResponse response);
    void Connect();
    void Disconnect();
    void Reset();
}

public sealed class SimulatedPaymentTerminal : IPaymentTerminal, IPaymentTerminalSimulatorControl, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);
    private readonly object _sync = new();
    private readonly object _operationSync = new();
    private readonly TimeProvider _timeProvider;
    private readonly DeviceRequestQueue<PaymentTerminalRequestPayload, PaymentTerminalResponse> _requests;
    private PaymentTerminalConnectionState _connection = PaymentTerminalConnectionState.Connected;
    private PaymentTerminalOperationalState _state = PaymentTerminalOperationalState.Idle;
    private PaymentStatus? _lastOutcome;
    private bool _disposed;

    public SimulatedPaymentTerminal(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _requests = new("Card terminal", timeProvider);
        _requests.Changed += OnRequestsChanged;
    }

    public event EventHandler? StateChanged;
    public PaymentTerminalConnectionState ConnectionState { get { lock (_sync) return _connection; } }
    public PaymentTerminalOperationalState OperationalState { get { lock (_sync) return _state; } }
    public PaymentStatus? LastOutcome { get { lock (_sync) return _lastOutcome; } }
    public DeviceRequest<PaymentTerminalRequestPayload, PaymentTerminalResponse>? PendingRequest => _requests.Pending;
    public IReadOnlyList<DeviceRequest<PaymentTerminalRequestPayload, PaymentTerminalResponse>> RecentRequests => _requests.Recent;

    public bool Respond(Guid requestId, PaymentTerminalResponse response)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(response);
        if (!Enum.IsDefined(response.Outcome)) throw new ArgumentOutOfRangeException(nameof(response));
        if (response.Outcome == PaymentTerminalResponseOutcome.Approve &&
            (string.IsNullOrWhiteSpace(response.ApprovalCode) || string.IsNullOrWhiteSpace(response.TransactionReference)))
        {
            throw new ArgumentException("Approval code and transaction reference are required for approval.", nameof(response));
        }

        lock (_operationSync)
        {
            if (ConnectionState == PaymentTerminalConnectionState.Disconnected)
            {
                return false;
            }

            return _requests.TryComplete(requestId, response);
        }
    }

    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            _connection = PaymentTerminalConnectionState.Connected;
            _state = _requests.Pending is null ? PaymentTerminalOperationalState.Idle : PaymentTerminalOperationalState.Processing;
        }
        RaiseChanged();
    }

    public void Disconnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_operationSync)
        {
            var pending = _requests.Pending;
            lock (_sync)
            {
                _connection = PaymentTerminalConnectionState.Disconnected;
                _state = PaymentTerminalOperationalState.Disconnected;
            }

            if (pending is not null)
            {
                _requests.TryDisconnect(pending.RequestId);
            }
        }

        RaiseChanged();
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_requests.Pending is not null) throw new InvalidOperationException("Terminal cannot reset during authorization.");
        lock (_sync)
        {
            _lastOutcome = null;
            _state = _connection == PaymentTerminalConnectionState.Connected
                ? PaymentTerminalOperationalState.Idle
                : PaymentTerminalOperationalState.Disconnected;
        }
        RaiseChanged();
    }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Validate(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (ConnectionState == PaymentTerminalConnectionState.Disconnected)
            return NonApproved(request, PaymentStatus.Failed, "Card terminal is disconnected.");

        Task<DeviceRequest<PaymentTerminalRequestPayload, PaymentTerminalResponse>> completion;
        try
        {
            completion = _requests.BeginAsync(
                request.PaymentAttemptId.ToString("N"),
                new(request.PaymentAttemptId, request.Amount),
                RequestTimeout,
                cancellationToken);
            var pending = _requests.Pending;
            if (ConnectionState == PaymentTerminalConnectionState.Disconnected && pending is not null)
                _requests.TryDisconnect(pending.RequestId);
        }
        catch (DeviceRequestBusyException)
        {
            return NonApproved(request, PaymentStatus.Failed, "Card terminal is busy.");
        }

        var completed = await completion;
        if (completed.State != DeviceRequestState.Completed)
        {
            return Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Unknown,
                "Card payment was interrupted after dispatch. Approval status is unknown and requires review.");
        }

        var response = completed.Result!;
        return response.Outcome switch
        {
            PaymentTerminalResponseOutcome.Approve => CompleteApproved(request, response),
            PaymentTerminalResponseOutcome.Decline => Complete(request, PaymentStatus.Failed, PaymentTerminalOperationalState.Declined, "Card payment was declined."),
            PaymentTerminalResponseOutcome.Cancel => Complete(request, PaymentStatus.Cancelled, PaymentTerminalOperationalState.Cancelled, "Card payment was cancelled by the terminal."),
            PaymentTerminalResponseOutcome.Timeout => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Unknown, "Card payment timed out. Approval status is unknown and requires review."),
            PaymentTerminalResponseOutcome.CommunicationLoss => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Faulted, "Card terminal communication was lost. Approval status is unknown and requires review."),
            _ => Complete(request, PaymentStatus.Unknown, PaymentTerminalOperationalState.Unknown, "Card payment outcome is unknown and requires review.")
        };
    }

    private PaymentAuthorizationResult CompleteApproved(PaymentAuthorizationRequest request, PaymentTerminalResponse response)
    {
        SetOutcome(PaymentStatus.Approved, PaymentTerminalOperationalState.Approved);
        return new(PaymentStatus.Approved, request.Amount, request.Amount,
            response.ApprovalCode!.Trim(), response.TransactionReference!.Trim(), _timeProvider.GetUtcNow(), null);
    }

    private PaymentAuthorizationResult Complete(PaymentAuthorizationRequest request, PaymentStatus status, PaymentTerminalOperationalState state, string message)
    {
        SetOutcome(status, state);
        return NonApproved(request, status, message);
    }

    private void OnRequestsChanged(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (_connection == PaymentTerminalConnectionState.Connected && _requests.Pending is not null)
                _state = PaymentTerminalOperationalState.Processing;
        }
        RaiseChanged();
    }

    private void SetOutcome(PaymentStatus status, PaymentTerminalOperationalState state)
    {
        lock (_sync) { _lastOutcome = status; _state = _connection == PaymentTerminalConnectionState.Connected ? state : PaymentTerminalOperationalState.Disconnected; }
        RaiseChanged();
    }

    private static PaymentAuthorizationResult NonApproved(PaymentAuthorizationRequest request, PaymentStatus status, string message) =>
        new(status, request.Amount, null, null, null, null, message);
    private static void Validate(PaymentAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PaymentAttemptId == Guid.Empty) throw new ArgumentException("Payment attempt identity is required.", nameof(request));
        if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount) throw new ArgumentOutOfRangeException(nameof(request));
    }
    private void RaiseChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _requests.Changed -= OnRequestsChanged;
        _requests.Dispose();
    }
}
