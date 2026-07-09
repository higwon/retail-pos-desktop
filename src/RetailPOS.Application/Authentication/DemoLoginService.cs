namespace RetailPOS.Application.Authentication;

public sealed class DemoLoginService : ILoginService
{
    private static readonly Guid DemoStoreId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoTerminalId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DemoAccount[] Accounts =
    [
        new(
            "E0001",
            "1234",
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            "Cashier A"),
        new(
            "M0001",
            "1234",
            Guid.Parse("30000000-0000-0000-0000-000000000002"),
            "Manager A")
    ];

    private readonly ICurrentSessionContext _sessionContext;
    private readonly TimeProvider _timeProvider;

    public DemoLoginService(ICurrentSessionContext sessionContext, TimeProvider? timeProvider = null)
    {
        _sessionContext = sessionContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<LoginResult> SignInAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var employeeCode = request.EmployeeCode.Trim();
        if (string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrEmpty(request.Password))
        {
            return Task.FromResult(LoginResult.Failure("Enter employee code and password."));
        }

        var account = Accounts.FirstOrDefault(account =>
            string.Equals(account.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase) &&
            account.Password == request.Password);

        if (account is null)
        {
            return Task.FromResult(LoginResult.Failure("Employee code or password is incorrect."));
        }

        var session = new CashierSession(
            DemoStoreId,
            DemoTerminalId,
            account.CashierId,
            account.EmployeeCode,
            account.CashierName,
            _timeProvider.GetUtcNow());
        _sessionContext.SignIn(session);
        return Task.FromResult(LoginResult.Success(session));
    }

    private sealed record DemoAccount(
        string EmployeeCode,
        string Password,
        Guid CashierId,
        string CashierName);
}
