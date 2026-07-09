using RetailPOS.Application.Authentication;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Tests;

public sealed class LoginViewModelTests
{
    [Fact]
    public async Task SignInCommand_WithValidInput_RaisesSignedInAndClearsPassword()
    {
        var session = Session();
        var loginService = new StubLoginService(LoginResult.Success(session));
        var viewModel = new LoginViewModel(loginService)
        {
            EmployeeCode = "E0001",
            Password = "1234"
        };
        var signedInCount = 0;
        viewModel.SignedIn += (_, _) => signedInCount++;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal(1, signedInCount);
        Assert.Equal("E0001", loginService.Request?.EmployeeCode);
        Assert.Equal("1234", loginService.Request?.Password);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Theory]
    [InlineData("", "1234")]
    [InlineData("E0001", "")]
    public async Task SignInCommand_WithMissingInput_DoesNotCallLoginService(
        string employeeCode,
        string password)
    {
        var loginService = new StubLoginService(LoginResult.Success(Session()));
        var viewModel = new LoginViewModel(loginService)
        {
            EmployeeCode = employeeCode,
            Password = password
        };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Null(loginService.Request);
        Assert.Equal("Enter employee code and password.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SignInCommand_WithFailedLogin_ShowsSafeMessageWithoutNavigation()
    {
        var loginService = new StubLoginService(
            LoginResult.Failure("Employee code or password is incorrect."));
        var viewModel = new LoginViewModel(loginService)
        {
            EmployeeCode = "E0001",
            Password = "wrong"
        };
        var signedInCount = 0;
        viewModel.SignedIn += (_, _) => signedInCount++;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Equal(0, signedInCount);
        Assert.Equal("Employee code or password is incorrect.", viewModel.ErrorMessage);
        Assert.Equal("wrong", viewModel.Password);
    }

    private static CashierSession Session() => new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000001"),
        "E0001",
        "Cashier A",
        DateTimeOffset.UtcNow);

    private sealed class StubLoginService(LoginResult result) : ILoginService
    {
        public LoginRequest? Request { get; private set; }

        public Task<LoginResult> SignInAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
