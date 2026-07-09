using RetailPOS.Application.Authentication;

namespace RetailPOS.Application.Tests;

public sealed class DemoLoginServiceTests
{
    [Fact]
    public async Task SignInAsync_WithValidDemoAccount_EstablishesCurrentSession()
    {
        var sessionContext = new CurrentSessionContext();
        var service = new DemoLoginService(sessionContext);

        var result = await service.SignInAsync(new LoginRequest(" E0001 ", "1234"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);
        Assert.True(sessionContext.IsSignedIn);
        Assert.Same(result.Session, sessionContext.Current);
        Assert.Equal("E0001", result.Session.EmployeeCode);
        Assert.Equal("Cashier A", result.Session.CashierName);
        Assert.Equal(result.Session.CashierId, sessionContext.GetCurrent().CashierId);
    }

    [Fact]
    public async Task SignInAsync_WithInvalidPassword_ReturnsSafeFailure()
    {
        var sessionContext = new CurrentSessionContext();
        var service = new DemoLoginService(sessionContext);

        var result = await service.SignInAsync(new LoginRequest("E0001", "wrong"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.False(sessionContext.IsSignedIn);
        Assert.Equal("Employee code or password is incorrect.", result.FailureMessage);
    }

    [Theory]
    [InlineData("", "1234")]
    [InlineData("E0001", "")]
    public async Task SignInAsync_WithMissingInput_ReturnsValidationFailure(
        string employeeCode,
        string password)
    {
        var service = new DemoLoginService(new CurrentSessionContext());

        var result = await service.SignInAsync(new LoginRequest(employeeCode, password));

        Assert.False(result.IsSuccess);
        Assert.Equal("Enter employee code and password.", result.FailureMessage);
    }
}
