using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using RetailPOS.Desktop.ViewModels;

namespace RetailPOS.Desktop.Tests;

public sealed partial class CashierViewBindingContractTests
{
    [Theory]
    [InlineData("CheckoutRecoveryView.xaml", typeof(CheckoutRecoveryViewModel), typeof(CheckoutRecoveryItemViewModel), typeof(CheckoutRecoveryLineViewModel))]
    [InlineData("DashboardView.xaml", typeof(DashboardViewModel), typeof(DashboardRecentOrderViewModel))]
    [InlineData("StatusView.xaml", typeof(StatusViewModel), typeof(DeviceStatusItemViewModel), typeof(SyncQueueItemViewModel))]
    public void BindingPaths_ResolveAgainstDeclaredViewContexts(
        string xamlFile,
        Type rootContext,
        params Type[] itemContexts)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "src", "RetailPOS.Desktop", "Views", xamlFile));
        var contexts = new[] { rootContext }.Concat(itemContexts).ToArray();
        var bindingPaths = BindingPathRegex().Matches(source)
            .Select(match => match.Groups[1].Value)
            .Where(path => path is not "Converter" and not "RelativeSource")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(bindingPaths);
        foreach (var path in bindingPaths)
        {
            Assert.True(contexts.Any(context => Resolves(context, path)),
                $"Binding '{path}' in {xamlFile} does not resolve on any declared DataContext.");
        }
    }

    private static bool Resolves(Type context, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            var property = context.GetProperty(segment,
                BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return false;
            }

            context = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        return true;
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [GeneratedRegex(@"\{Binding\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.CultureInvariant)]
    private static partial Regex BindingPathRegex();
}
