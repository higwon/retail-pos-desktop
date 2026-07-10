using RetailPOS.Desktop.ViewModels;
using RetailPOS.Infrastructure.Devices;

namespace RetailPOS.Desktop.Tests;

public sealed class CardTerminalSimulatorViewModelTests
{
    [Fact]
    public void ApplyAndConnectionCommandsUpdateTerminalWithoutCashierDependency()
    {
        using var terminal = new SimulatedPaymentTerminal(TimeProvider.System);
        using var viewModel = new CardTerminalSimulatorViewModel(terminal)
        {
            SelectedScenario = PaymentTerminalSimulationScenario.Timeout,
            ResponseDelayMilliseconds = 1250
        };

        viewModel.ApplyCommand.Execute(null);
        Assert.Equal(PaymentTerminalSimulationScenario.Timeout, terminal.Current.Scenario);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), terminal.Current.ResponseDelay);

        viewModel.DisconnectCommand.Execute(null);
        Assert.False(viewModel.IsConnected);
        viewModel.ConnectCommand.Execute(null);
        Assert.True(viewModel.IsConnected);
    }
}
