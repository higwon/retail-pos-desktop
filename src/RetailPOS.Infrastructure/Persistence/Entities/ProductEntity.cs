namespace RetailPOS.Infrastructure.Persistence.Entities;

public sealed class ProductEntity
{
    public Guid Id { get; set; }
    public required string Sku { get; set; }
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public required string CategoryName { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}
