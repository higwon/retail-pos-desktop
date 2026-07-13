using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.Tests;

public sealed class CashierWorkflowNavigatorTests
{
    [Fact]
    public void InitialStateIsLoginWithoutBackHistory()
    {
        var navigator = CreateNavigator();

        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
        Assert.False(navigator.CanGoBack);
        Assert.False(navigator.GoBack());
    }

    [Fact]
    public void PushAndBackRestoreTheOriginScreen()
    {
        var navigator = CreateNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);

        navigator.Navigate(CashierWorkflowScreen.ProductSearch);
        var wentBack = navigator.GoBack();

        Assert.True(wentBack);
        Assert.Equal(CashierWorkflowScreen.Register, navigator.Current);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void ReplaceKeepsTheReceiptHistoryReturnScreen()
    {
        var navigator = CreateNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        navigator.Navigate(CashierWorkflowScreen.ReceiptHistory);

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
        var navigator = CreateNavigator();
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
        var navigator = CreateNavigator();
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
        var navigator = CreateNavigator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            navigator.Navigate(CashierWorkflowScreen.ReceiptHistory));

        Assert.Contains("Login", exception.Message, StringComparison.Ordinal);
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void UnknownScreenFailsClosed()
    {
        var navigator = CreateNavigator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            navigator.Reset((CashierWorkflowScreen)999));
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
    }

    [Fact]
    public void ResetCannotBypassEntryPolicyForReceiptDetail()
    {
        var navigator = CreateNavigator();

        Assert.Throws<InvalidOperationException>(() =>
            navigator.Reset(CashierWorkflowScreen.ReceiptDetail));
        Assert.Equal(CashierWorkflowScreen.Login, navigator.Current);
    }

    [Fact]
    public void UnregisteredDestinationFailsBeforeStateChanges()
    {
        var registry = new CashierWorkflowScreenRegistry();
        registry.Register([
            CashierWorkflowScreen.Login,
            CashierWorkflowScreen.Register]);
        var navigator = new CashierWorkflowNavigator(registry);
        navigator.Reset(CashierWorkflowScreen.Register);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            navigator.Navigate(CashierWorkflowScreen.ProductSearch));

        Assert.Contains("registered", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CashierWorkflowScreen.Register, navigator.Current);
        Assert.False(navigator.CanGoBack);
    }

    [Fact]
    public void SubscriberFailureDoesNotRollBackCommittedTransition()
    {
        var navigator = CreateNavigator();
        navigator.Reset(CashierWorkflowScreen.Register);
        navigator.ScreenChanged += (_, _) => throw new InvalidOperationException("subscriber failed");

        Assert.Throws<InvalidOperationException>(() =>
            navigator.Navigate(CashierWorkflowScreen.ProductSearch));

        Assert.Equal(CashierWorkflowScreen.ProductSearch, navigator.Current);
        Assert.True(navigator.CanGoBack);
    }

    private static CashierWorkflowNavigator CreateNavigator()
    {
        var registry = new CashierWorkflowScreenRegistry();
        registry.Register(Enum.GetValues<CashierWorkflowScreen>());
        return new(registry);
    }
}
