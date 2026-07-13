namespace RetailPOS.Desktop.Workflow;

public enum CashierWorkflowScreen
{
    Login,
    Register,
    ProductSearch,
    CardPayment,
    CashPayment,
    ReceiptHistory,
    ReceiptDetail,
    Recovery,
    Dashboard,
    Status
}

public enum CashierWorkflowNavigationKind
{
    Push,
    Replace,
    Back,
    Reset
}

public sealed class CashierWorkflowChangedEventArgs(
    CashierWorkflowScreen previous,
    CashierWorkflowScreen current,
    CashierWorkflowNavigationKind kind) : EventArgs
{
    public CashierWorkflowScreen Previous { get; } = previous;
    public CashierWorkflowScreen Current { get; } = current;
    public CashierWorkflowNavigationKind Kind { get; } = kind;
}

public sealed class CashierWorkflowNavigator
{
    private readonly object _sync = new();
    private readonly Stack<CashierWorkflowScreen> _backStack = new();
    private CashierWorkflowScreen _current = CashierWorkflowScreen.Login;

    public event EventHandler<CashierWorkflowChangedEventArgs>? ScreenChanged;

    public CashierWorkflowScreen Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public bool CanGoBack
    {
        get
        {
            lock (_sync)
            {
                return _backStack.Count > 0;
            }
        }
    }

    public bool CanNavigateTo(CashierWorkflowScreen destination)
    {
        EnsureDefined(destination);
        lock (_sync)
        {
            return destination == _current || IsAllowed(_current, destination);
        }
    }

    public void Navigate(
        CashierWorkflowScreen destination,
        CashierWorkflowNavigationKind kind = CashierWorkflowNavigationKind.Push)
    {
        EnsureDefined(destination);
        if (kind is not CashierWorkflowNavigationKind.Push and
            not CashierWorkflowNavigationKind.Replace)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Navigate supports only push or replace transitions.");
        }

        CashierWorkflowChangedEventArgs change;
        lock (_sync)
        {
            if (destination == _current)
            {
                return;
            }

            if (!IsAllowed(_current, destination))
            {
                throw new InvalidOperationException(
                    $"Cashier workflow cannot navigate from {_current} to {destination}.");
            }

            var previous = _current;
            if (kind == CashierWorkflowNavigationKind.Push)
            {
                _backStack.Push(previous);
            }

            _current = destination;
            change = new(previous, destination, kind);
        }

        ScreenChanged?.Invoke(this, change);
    }

    public bool GoBack()
    {
        CashierWorkflowChangedEventArgs change;
        lock (_sync)
        {
            if (_backStack.Count == 0)
            {
                return false;
            }

            var previous = _current;
            _current = _backStack.Pop();
            change = new(
                previous,
                _current,
                CashierWorkflowNavigationKind.Back);
        }

        ScreenChanged?.Invoke(this, change);
        return true;
    }

    public void Reset(CashierWorkflowScreen destination)
    {
        EnsureDefined(destination);
        if (!IsResetTarget(destination))
        {
            throw new InvalidOperationException(
                $"Cashier workflow cannot reset directly to {destination}.");
        }

        CashierWorkflowChangedEventArgs change;
        lock (_sync)
        {
            if (destination == _current && _backStack.Count == 0)
            {
                return;
            }

            var previous = _current;
            _backStack.Clear();
            _current = destination;
            change = new(
                previous,
                destination,
                CashierWorkflowNavigationKind.Reset);
        }

        ScreenChanged?.Invoke(this, change);
    }

    private static bool IsAllowed(
        CashierWorkflowScreen current,
        CashierWorkflowScreen destination) => current switch
    {
        CashierWorkflowScreen.Login => destination == CashierWorkflowScreen.Register,
        CashierWorkflowScreen.Register => destination is
            CashierWorkflowScreen.ProductSearch or
            CashierWorkflowScreen.CardPayment or
            CashierWorkflowScreen.CashPayment or
            CashierWorkflowScreen.ReceiptHistory or
            CashierWorkflowScreen.Recovery or
            CashierWorkflowScreen.Dashboard or
            CashierWorkflowScreen.Status,
        CashierWorkflowScreen.ProductSearch => destination == CashierWorkflowScreen.Register,
        CashierWorkflowScreen.CardPayment => destination is
            CashierWorkflowScreen.Register or
            CashierWorkflowScreen.ReceiptDetail or
            CashierWorkflowScreen.Recovery,
        CashierWorkflowScreen.CashPayment => destination is
            CashierWorkflowScreen.Register or
            CashierWorkflowScreen.ReceiptDetail,
        CashierWorkflowScreen.ReceiptHistory => destination is
            CashierWorkflowScreen.Register or
            CashierWorkflowScreen.ReceiptDetail,
        CashierWorkflowScreen.ReceiptDetail => destination is
            CashierWorkflowScreen.Register or
            CashierWorkflowScreen.ReceiptHistory,
        CashierWorkflowScreen.Recovery => destination is
            CashierWorkflowScreen.Register or
            CashierWorkflowScreen.ReceiptDetail,
        CashierWorkflowScreen.Dashboard or CashierWorkflowScreen.Status =>
            destination == CashierWorkflowScreen.Register,
        _ => throw new ArgumentOutOfRangeException(
            nameof(current),
            current,
            "Unsupported cashier workflow screen.")
    };

    private static bool IsResetTarget(CashierWorkflowScreen screen) => screen is
        CashierWorkflowScreen.Login or
        CashierWorkflowScreen.Register or
        CashierWorkflowScreen.ReceiptHistory or
        CashierWorkflowScreen.Recovery or
        CashierWorkflowScreen.Dashboard or
        CashierWorkflowScreen.Status;

    private static void EnsureDefined(CashierWorkflowScreen screen)
    {
        if (!Enum.IsDefined(screen))
        {
            throw new ArgumentOutOfRangeException(
                nameof(screen),
                screen,
                "Unsupported cashier workflow screen.");
        }
    }
}
