using RetailPOS.Domain.Products;

namespace RetailPOS.Application.Products;

public static class ProductCatalogCategories
{
    public const string All = "All categories";

    public static IReadOnlyList<string> From(IEnumerable<Product> products) =>
        [All, .. products
            .Select(product => product.CategoryName.Trim())
            .Where(category => category.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)];
}
