using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace RetailPOS.Desktop.Configuration;

public enum RetailPosProfile { Development, Demo, Production }

public sealed class RuntimeOptions
{
    public const string SectionName = "Runtime";
    public string Profile { get; set; } = nameof(RetailPosProfile.Production);
    public bool EnableDemoLogin { get; set; }
}

public sealed class ApiSyncOptions
{
    public const string SectionName = "ApiSync";
    public string BaseAddress { get; set; } = string.Empty;
}

public sealed record ValidatedDesktopConfiguration(
    RetailPosProfile Profile,
    bool EnableDemoLogin,
    Uri ApiBaseAddress);

public static class DesktopConfigurationValidator
{
    public static ValidatedDesktopConfiguration Validate(IConfiguration configuration)
    {
        var failures = new List<string>();
        var runtime = configuration.GetSection(RuntimeOptions.SectionName).Get<RuntimeOptions>() ?? new();
        var api = configuration.GetSection(ApiSyncOptions.SectionName).Get<ApiSyncOptions>() ?? new();
        var simulationEnabled = configuration.GetValue("DeviceSimulation:Enabled", false);

        if (!Enum.TryParse<RetailPosProfile>(runtime.Profile, true, out var profile))
        {
            failures.Add("Runtime profile must be Development, Demo, or Production.");
        }

        if (!Uri.TryCreate(api.BaseAddress, UriKind.Absolute, out var apiBaseAddress) ||
            apiBaseAddress.Scheme is not ("http" or "https"))
        {
            failures.Add("API base address must be an absolute HTTP or HTTPS URI.");
        }

        ValidateRange(configuration, "SyncScheduler:InitialDelaySeconds", 0, 3600, failures);
        ValidateRange(configuration, "SyncScheduler:IntervalSeconds", 5, 86400, failures);
        ValidateRange(configuration, "SyncScheduler:BatchSize", 1, 1000, failures);
        ValidateRange(configuration, "ApiConnectivity:InitialDelaySeconds", 0, 3600, failures);
        ValidateRange(configuration, "ApiConnectivity:IntervalSeconds", 5, 86400, failures);
        ValidateRange(configuration, "Serilog:Desktop:RetainedFileCountLimit", 1, 365, failures);

        if (profile == RetailPosProfile.Production)
        {
            if (runtime.EnableDemoLogin) failures.Add("Demo login must be disabled in Production profile.");
            if (simulationEnabled) failures.Add("Device simulation must be disabled in Production profile.");
            if (apiBaseAddress?.Scheme != Uri.UriSchemeHttps)
                failures.Add("Production profile requires an HTTPS API base address.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(
                "DesktopConfiguration",
                typeof(ValidatedDesktopConfiguration),
                failures);
        }

        return new(profile, runtime.EnableDemoLogin, apiBaseAddress!);
    }

    private static void ValidateRange(
        IConfiguration configuration,
        string key,
        int minimum,
        int maximum,
        ICollection<string> failures)
    {
        var value = configuration.GetValue<int?>(key);
        if (value is null || value < minimum || value > maximum)
            failures.Add($"{key} must be between {minimum} and {maximum}.");
    }
}
