using RetailPOS.Application.Payments;
using RetailPOS.Desktop.ViewModels;
using RetailPOS.Domain.Payments;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class CardTerminalSimulatorViewModelTests
{
    private static readonly Guid AttemptId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Fact]
    public async Task PendingRequest_IsDisplayedWithDeterministicApprovalMetadata()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        using var viewModel = new CardTerminalSimulatorViewModel(terminal);
        var authorization = terminal.AuthorizeAsync(new PaymentAuthorizationRequest(AttemptId, 2500m));

        Assert.Equal(AttemptId.ToString("N"), viewModel.PaymentAttemptId);
        Assert.Equal("2,500 KRW", viewModel.RequestedAmount);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ApprovalCode));
        Assert.Contains(AttemptId.ToString("N"), viewModel.TransactionReference);
        Assert.True(viewModel.RespondCommand.CanExecute(null));

        viewModel.RespondCommand.Execute(null);
        Assert.Equal(PaymentStatus.Approved, (await authorization).Status);
        Assert.Single(viewModel.RecentRequests);
    }

    [Fact]
    public void ConnectionCommandsUpdateTerminal()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        using var viewModel = new CardTerminalSimulatorViewModel(terminal);
        viewModel.DisconnectCommand.Execute(null);
        Assert.False(viewModel.IsConnected);
        viewModel.ConnectCommand.Execute(null);
        Assert.True(viewModel.IsConnected);
    }
}
