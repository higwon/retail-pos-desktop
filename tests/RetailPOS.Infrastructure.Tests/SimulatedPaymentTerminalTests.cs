using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class SimulatedPaymentTerminalTests
{
    private static readonly Guid AttemptId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task Authorization_WaitsForOperatorAndApprovesWithOperatorMetadata()
    {
        var now = new DateTimeOffset(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);
        using var terminal = new SimulatedPaymentTerminal(new StubTimeProvider(now));
        var authorization = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        var pending = terminal.PendingRequest!;

        Assert.False(authorization.IsCompleted);
        Assert.Equal(AttemptId, pending.Payload.PaymentAttemptId);
        Assert.Equal(3600m, pending.Payload.Amount);
        Assert.True(terminal.Respond(pending.RequestId, new(PaymentTerminalResponseOutcome.Approve, "APP-001", "TX-001")));

        var result = await authorization;
        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal(3600m, result.ApprovedAmount);
        Assert.Equal("APP-001", result.ApprovalCode);
        Assert.Equal("TX-001", result.TransactionReference);
        Assert.Equal(now, result.ApprovedAtUtc);
    }

    [Theory]
    [InlineData(PaymentTerminalResponseOutcome.Decline, PaymentStatus.Failed)]
    [InlineData(PaymentTerminalResponseOutcome.Cancel, PaymentStatus.Cancelled)]
    [InlineData(PaymentTerminalResponseOutcome.Timeout, PaymentStatus.Unknown)]
    [InlineData(PaymentTerminalResponseOutcome.CommunicationLoss, PaymentStatus.Unknown)]
    [InlineData(PaymentTerminalResponseOutcome.Unknown, PaymentStatus.Unknown)]
    public async Task OperatorResponse_MapsToSafePaymentStatus(PaymentTerminalResponseOutcome outcome, PaymentStatus expected)
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var authorization = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        terminal.Respond(terminal.PendingRequest!.RequestId, new(outcome));
        Assert.Equal(expected, (await authorization).Status);
    }

    [Fact]
    public async Task DuplicateOrLateApprove_IsRejected()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var authorization = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        var id = terminal.PendingRequest!.RequestId;
        Assert.True(terminal.Respond(id, new(PaymentTerminalResponseOutcome.Unknown)));
        Assert.False(terminal.Respond(id, new(PaymentTerminalResponseOutcome.Approve, "APP", "TX")));
        Assert.Equal(PaymentStatus.Unknown, (await authorization).Status);
    }

    [Fact]
    public async Task ConcurrentRequest_IsBusyAndDoesNotReplacePendingAttempt()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var first = terminal.AuthorizeAsync(new(AttemptId, 3600m), cancellation.Token);
        var second = await terminal.AuthorizeAsync(new(Guid.NewGuid(), 3600m));
        Assert.Equal(PaymentStatus.Failed, second.Status);
        Assert.Contains("busy", second.FailureMessage!, StringComparison.OrdinalIgnoreCase);
        cancellation.Cancel();
        Assert.Equal(PaymentStatus.Unknown, (await first).Status);
    }

    [Fact]
    public async Task DisconnectAndCancellationAfterDispatchBecomeUnknown()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var authorization = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        terminal.Disconnect();
        Assert.Equal(PaymentStatus.Unknown, (await authorization).Status);
        Assert.Equal(PaymentTerminalOperationalState.Disconnected, terminal.OperationalState);
    }

    [Fact]
    public async Task DisconnectBeforeApprove_RejectsApprovalAndCompletesUnknown()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var authorization = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        var requestId = terminal.PendingRequest!.RequestId;

        terminal.Disconnect();
        var approved = terminal.Respond(
            requestId,
            new(PaymentTerminalResponseOutcome.Approve, "APP-001", "TX-001"));

        Assert.False(approved);
        Assert.Equal(PaymentStatus.Unknown, (await authorization).Status);
        Assert.Equal(PaymentTerminalConnectionState.Disconnected, terminal.ConnectionState);
        Assert.Equal(PaymentTerminalOperationalState.Disconnected, terminal.OperationalState);
    }

    [Fact]
    public async Task ApproveBeforeDisconnect_PreservesApprovalAndDisconnectsTerminal()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        var authorization = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        var requestId = terminal.PendingRequest!.RequestId;

        Assert.True(terminal.Respond(
            requestId,
            new(PaymentTerminalResponseOutcome.Approve, "APP-001", "TX-001")));
        terminal.Disconnect();

        Assert.Equal(PaymentStatus.Approved, (await authorization).Status);
        Assert.Equal(PaymentTerminalConnectionState.Disconnected, terminal.ConnectionState);
        Assert.Equal(PaymentTerminalOperationalState.Disconnected, terminal.OperationalState);
    }

    [Fact]
    public void ApproveRequiresMetadata()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        _ = terminal.AuthorizeAsync(new(AttemptId, 3600m));
        Assert.Throws<ArgumentException>(() => terminal.Respond(
            terminal.PendingRequest!.RequestId,
            new(PaymentTerminalResponseOutcome.Approve)));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
