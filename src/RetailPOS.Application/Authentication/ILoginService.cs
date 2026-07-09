namespace RetailPOS.Application.Authentication;

public sealed record LoginRequest(string EmployeeCode, string Password);

public sealed record LoginResult(
    bool IsSuccess,
    CashierSession? Session,
    string? FailureMessage)
{
    public static LoginResult Success(CashierSession session) => new(true, session, null);

    public static LoginResult Failure(string message) => new(false, null, message);
}

public interface ILoginService
{
    Task<LoginResult> SignInAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
