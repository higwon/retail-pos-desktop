using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Infrastructure.Tests;

public sealed class SimulatedPaymentTerminalTests
{
    private static readonly Guid AttemptId =
        Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task AuthorizeAsync_DefaultScenarioApprovesRequest()
    {
        var now = new DateTimeOffset(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);
        var terminal = new SimulatedPaymentTerminal(new StubTimeProvider(now));

        var result = await terminal.AuthorizeAsync(new PaymentAuthorizationRequest(AttemptId, 3600m));

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal(3600m, result.ApprovedAmount);
        Assert.Equal(now, result.ApprovedAtUtc);
        Assert.Contains(AttemptId.ToString("N"), result.TransactionReference);
    }

    [Theory]
    [InlineData(PaymentTerminalSimulationScenario.Decline, PaymentStatus.Failed)]
    [InlineData(PaymentTerminalSimulationScenario.Cancel, PaymentStatus.Cancelled)]
    [InlineData(PaymentTerminalSimulationScenario.Timeout, PaymentStatus.Unknown)]
    [InlineData(PaymentTerminalSimulationScenario.CommunicationError, PaymentStatus.Unknown)]
    public async Task AuthorizeAsync_MapsConfiguredScenario(
        PaymentTerminalSimulationScenario scenario,
        PaymentStatus expectedStatus)
    {
        var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        terminal.ConfigureNext(new PaymentTerminalSimulationSettings(scenario, TimeSpan.Zero));

        var result = await terminal.AuthorizeAsync(new PaymentAuthorizationRequest(AttemptId, 3600m));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(PaymentTerminalSimulationSettings.Default, terminal.Current);
    }

    [Fact]
    public async Task AuthorizeAsync_CancellationDuringDelayIsPropagated()
    {
        var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        terminal.ConfigureNext(new PaymentTerminalSimulationSettings(
            PaymentTerminalSimulationScenario.Approve,
            TimeSpan.FromMinutes(1)));
        using var cancellation = new CancellationTokenSource();

        var authorization = terminal.AuthorizeAsync(
            new PaymentAuthorizationRequest(AttemptId, 3600m),
            cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authorization);
    }

    [Fact]
    public async Task AuthorizeAsync_UnknownScenarioIsNeverApproved()
    {
        var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        terminal.ConfigureNext(new PaymentTerminalSimulationSettings(
            (PaymentTerminalSimulationScenario)999,
            TimeSpan.Zero));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            terminal.AuthorizeAsync(new PaymentAuthorizationRequest(AttemptId, 3600m)));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
