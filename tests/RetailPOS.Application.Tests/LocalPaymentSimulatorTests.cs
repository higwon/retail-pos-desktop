using RetailPOS.Application.Payments;
using RetailPOS.Domain.Payments;

namespace RetailPOS.Application.Tests;

public sealed class LocalPaymentSimulatorTests
{
    private static readonly DateTimeOffset ApprovedAtUtc =
        new(2026, 7, 7, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public async Task SimulateAsync_ApprovesPositiveWholeKrwAmount()
    {
        var simulator = new LocalPaymentSimulator(() => ApprovedAtUtc);

        var result = await simulator.SimulateAsync(new PaymentSimulationRequest(
            PaymentMethod.Card,
            9_000m));

        Assert.True(result.IsApproved);
        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal(PaymentMethod.Card, result.Method);
        Assert.Equal(9_000m, result.ApprovedAmount);
        Assert.Equal("APP-CARD-000000009000", result.ApprovalCode);
        Assert.Equal("SIM-CARD-20260707010203-000000009000", result.TransactionReference);
        Assert.Equal(TimeSpan.Zero, result.ApprovedAtUtc?.Offset);
    }

    [Fact]
    public async Task SimulateAsync_ReturnsDeterministicFailure()
    {
        var simulator = new LocalPaymentSimulator(() => ApprovedAtUtc);

        var result = await simulator.SimulateAsync(new PaymentSimulationRequest(
            PaymentMethod.Cash,
            5_000m,
            PaymentSimulationMode.Fail));

        Assert.False(result.IsApproved);
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal(PaymentMethod.Cash, result.Method);
        Assert.Null(result.ApprovalCode);
        Assert.Null(result.TransactionReference);
        Assert.Equal("Payment was declined by the local simulator.", result.FailureMessage);
    }

    [Theory]
    [InlineData(PaymentSimulationMode.Timeout, PaymentStatus.Failed, "Payment timed out. Keep the cart and try again.")]
    [InlineData(PaymentSimulationMode.Cancel, PaymentStatus.Cancelled, "Payment was cancelled. Cart was not changed.")]
    [InlineData(PaymentSimulationMode.CommunicationError, PaymentStatus.Failed, "Payment terminal communication failed. Try again or ask a manager to review checkout status.")]
    public async Task SimulateAsync_ReturnsDistinctNonApprovedOutcomes(
        PaymentSimulationMode mode,
        PaymentStatus expectedStatus,
        string expectedMessage)
    {
        var simulator = new LocalPaymentSimulator(() => ApprovedAtUtc);

        var result = await simulator.SimulateAsync(new PaymentSimulationRequest(
            PaymentMethod.Card,
            5_000m,
            mode));

        Assert.False(result.IsApproved);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(PaymentMethod.Card, result.Method);
        Assert.Null(result.ApprovedAmount);
        Assert.Null(result.ApprovalCode);
        Assert.Null(result.TransactionReference);
        Assert.Null(result.ApprovedAtUtc);
        Assert.Equal(expectedMessage, result.FailureMessage);
    }

    [Fact]
    public async Task SimulateAsync_RejectsUnsupportedMode()
    {
        var simulator = new LocalPaymentSimulator(() => ApprovedAtUtc);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            simulator.SimulateAsync(new PaymentSimulationRequest(
                PaymentMethod.Card,
                5_000m,
                (PaymentSimulationMode)999)));

        Assert.Equal("Mode", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000.5)]
    public async Task SimulateAsync_RejectsInvalidAmount(decimal amount)
    {
        var simulator = new LocalPaymentSimulator(() => ApprovedAtUtc);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            simulator.SimulateAsync(new PaymentSimulationRequest(PaymentMethod.Card, amount)));
    }

    [Fact]
    public async Task SimulateAsync_RequiresUtcApprovalTimestamp()
    {
        var simulator = new LocalPaymentSimulator(
            () => new DateTimeOffset(2026, 7, 7, 10, 2, 3, TimeSpan.FromHours(9)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            simulator.SimulateAsync(new PaymentSimulationRequest(PaymentMethod.Card, 9_000m)));
    }
}
