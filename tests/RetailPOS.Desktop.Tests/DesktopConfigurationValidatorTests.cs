using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RetailPOS.Desktop.Authentication;
using RetailPOS.Desktop.Configuration;

namespace RetailPOS.Desktop.Tests;

public sealed class DesktopConfigurationValidatorTests
{
    [Fact]
    public void ProductionProfile_AcceptsSafeDefaults()
    {
        var result = DesktopConfigurationValidator.Validate(Configuration());

        Assert.Equal(RetailPosProfile.Production, result.Profile);
        Assert.False(result.EnableDemoLogin);
        Assert.Equal("https", result.ApiBaseAddress.Scheme);
    }

    [Theory]
    [InlineData("Runtime:EnableDemoLogin", "true", "Demo login")]
    [InlineData("DeviceSimulation:Enabled", "true", "Device simulation")]
    [InlineData("ApiSync:BaseAddress", "http://localhost:5000/", "HTTPS")]
    [InlineData("SyncScheduler:BatchSize", "0", "BatchSize")]
    [InlineData("ApiConnectivity:IntervalSeconds", "1", "IntervalSeconds")]
    public void InvalidCriticalConfiguration_FailsWithSafeActionableMessage(
        string key,
        string value,
        string expected)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            DesktopConfigurationValidator.Validate(Configuration((key, value))));

        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DemoProfile_AllowsLocalApiSimulatorAndDemoLogin()
    {
        var result = DesktopConfigurationValidator.Validate(Configuration(
            ("Runtime:Profile", "Demo"),
            ("Runtime:EnableDemoLogin", "true"),
            ("DeviceSimulation:Enabled", "true"),
            ("ApiSync:BaseAddress", "http://localhost:5000/")));

        Assert.Equal(RetailPosProfile.Demo, result.Profile);
        Assert.True(result.EnableDemoLogin);
    }

    [Fact]
    public async Task ProductionLoginBoundary_ReturnsUserSafeUnavailableMessage()
    {
        var result = await new UnavailableLoginService().SignInAsync(new("E0001", "secret"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.FailureMessage, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Runtime:Profile"] = "Production",
            ["Runtime:EnableDemoLogin"] = "false",
            ["ApiSync:BaseAddress"] = "https://localhost:5001/",
            ["DeviceSimulation:Enabled"] = "false",
            ["SyncScheduler:InitialDelaySeconds"] = "5",
            ["SyncScheduler:IntervalSeconds"] = "60",
            ["SyncScheduler:BatchSize"] = "10",
            ["ApiConnectivity:InitialDelaySeconds"] = "2",
            ["ApiConnectivity:IntervalSeconds"] = "30",
            ["Serilog:Desktop:RetainedFileCountLimit"] = "14"
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
