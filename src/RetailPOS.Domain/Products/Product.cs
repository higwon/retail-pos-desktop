using RetailPOS.Domain.Common;

namespace RetailPOS.Domain.Products;

public sealed class Product
{
    public Product(Guid id, string sku, string barcode, string name, string categoryName,
        decimal unitPrice, bool isActive = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product identity is required.", nameof(id));
        }

        Id = id;
        Sku = DomainGuard.Required(sku, nameof(sku));
        Barcode = DomainGuard.Required(barcode, nameof(barcode));
        Name = DomainGuard.Required(name, nameof(name));
        CategoryName = DomainGuard.Required(categoryName, nameof(categoryName));
        UnitPrice = DomainGuard.Money(unitPrice, nameof(unitPrice));
        IsActive = isActive;
    }

    public Guid Id { get; }
    public string Sku { get; }
    public string Barcode { get; }
    public string Name { get; }
    public string CategoryName { get; }
    public decimal UnitPrice { get; }
    public bool IsActive { get; }
}
