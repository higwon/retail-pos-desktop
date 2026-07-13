using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Tests;

public sealed class CashierWorkflowNavigatorTests
{
    [Fact]
    public void InitialStateIsLoginWithoutBackHistory()
    {
        var navigator = new CashierWorkflowNavigator();

        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
        Assert.False(navigator.CanGoBack);
        Assert.False(navigator.GoBack());
    }

    [Fact]
    public void PushAndBackRestoreTheOriginScreen()
    {
        var navigator = new CashierWorkflowNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);

        navigator.Navigate(CashierWorkflowScreen.ProductSearch);
        var wentBack = navigator.GoBack();

        Assert.True(wentBack);
        Assert.Equal(CashierWorkflowScreen.Register, navigator.Current);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void ReplaceKeepsThePrePaymentReturnScreen()
    {
        var navigator = new CashierWorkflowNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        navigator.Navigate(CashierWorkflowScreen.CardPayment);

        navigator.Navigate(
            CashierWorkflowScreen.ReceiptDetail,
            CashierWorkflowNavigationKind.Replace);

        Assert.Equal(CashierWorkflowScreen.ReceiptDetail, navigator.Current);
        Assert.True(navigator.GoBack());
        Assert.Equal(CashierWorkflowScreen.Register, navigator.Current);
    }

    [Fact]
    public void ResetClearsBackHistoryAndPublishesTypedChange()
    {
        var navigator = new CashierWorkflowNavigator();
        CashierWorkflowChangedEventArgs? published = null;
        navigator.ScreenChanged += (_, change) => published = change;
        navigator.Reset(CashierWorkflowScreen.Register);
        navigator.Navigate(CashierWorkflowScreen.ProductSearch);

        navigator.Reset(CashierWorkflowScreen.Status);

        Assert.False(navigator.CanGoBack);
        Assert.Equal(CashierWorkflowScreen.ProductSearch, published?.Previous);
        Assert.Equal(CashierWorkflowScreen.Status, published?.Current);
        Assert.Equal(CashierWorkflowNavigationKind.Reset, published?.Kind);
    }

    [Fact]
    public void DuplicateNavigationDoesNotPublishOrCreateHistory()
    {
        var navigator = new CashierWorkflowNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        var changes = 0;
        navigator.ScreenChanged += (_, _) => changes++;

        navigator.Navigate(CashierWorkflowScreen.Register);

        Assert.Equal(0, changes);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void InvalidTransitionFailsWithoutChangingState()
    {
        var navigator = new CashierWorkflowNavigator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            navigator.Navigate(CashierWorkflowScreen.CardPayment));

        Assert.Contains("Login", exception.Message, StringComparison.Ordinal);
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void UnknownScreenFailsClosed()
    {
        var navigator = new CashierWorkflowNavigator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            navigator.Reset((CashierWorkflowScreen)999));
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
    }

    [Fact]
    public void ResetCannotBypassEntryPolicyForPaymentScreens()
    {
        var navigator = new CashierWorkflowNavigator();

        Assert.Throws<InvalidOperationException>(() =>
            navigator.Reset(CashierWorkflowScreen.CardPayment));
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
    }
}
