using RetailPOS.Application.Authentication;

namespace RetailPOS.Desktop.Authentication;

public sealed class UnavailableLoginService : ILoginService
{
    public Task<LoginResult> SignInAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoginResult.Failure(
            "Sign-in is not configured for this environment."));
    }
}
