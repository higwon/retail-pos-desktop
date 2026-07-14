using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailPOS.Infrastructure.Persistence.Entities;

namespace RetailPOS.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);
        builder.HasIndex(product => product.Sku).IsUnique();
        builder.HasIndex(product => product.Barcode).IsUnique();
        builder.Property(product => product.Sku).HasMaxLength(64);
        builder.Property(product => product.Barcode).HasMaxLength(64);
        builder.Property(product => product.Name).HasMaxLength(200);
        builder.Property(product => product.CategoryName).HasMaxLength(100);
        builder.Property(product => product.UnitPrice).HasPrecision(18, 0);
        builder.HasIndex(product => product.UpdatedUtc);
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.LocalOrderId);
        builder.HasIndex(order => order.LocalOrderNumber).IsUnique();
        builder.HasIndex(order => order.CreatedAtUtc);
        builder.HasIndex(order => new { order.BusinessDate, order.CreatedAtUtc });
        builder.Property(order => order.LocalOrderNumber).HasMaxLength(64);
        builder.Property(order => order.SubtotalAmount).HasPrecision(18, 0);
        builder.Property(order => order.DiscountAmount).HasPrecision(18, 0);
        builder.Property(order => order.TotalAmount).HasPrecision(18, 0);
    }
}

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLineEntity>
{
    public void Configure(EntityTypeBuilder<OrderLineEntity> builder)
    {
        builder.ToTable("OrderLines");
        builder.HasKey(line => line.Id);
        builder.HasIndex(line => new { line.LocalOrderId, line.SortOrder }).IsUnique();
        builder.Property(line => line.ProductNameSnapshot).HasMaxLength(200);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 0);
        builder.Property(line => line.GrossAmount).HasPrecision(18, 0);
        builder.Property(line => line.LineDiscountAmount).HasPrecision(18, 0);
        builder.Property(line => line.LineTotalAmount).HasPrecision(18, 0);
        builder.HasOne(line => line.Order)
            .WithMany(order => order.Lines)
            .HasForeignKey(line => line.LocalOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(payment => payment.Id);
        builder.HasIndex(payment => new { payment.LocalOrderId, payment.SortOrder }).IsUnique();
        builder.Property(payment => payment.RequestedAmount).HasPrecision(18, 0);
        builder.Property(payment => payment.ApprovedAmount).HasPrecision(18, 0);
        builder.Property(payment => payment.CashTenderedAmount).HasPrecision(18, 0);
        builder.Property(payment => payment.ChangeAmount).HasPrecision(18, 0);
        builder.Property(payment => payment.ApprovalCode).HasMaxLength(100);
        builder.Property(payment => payment.TransactionReference).HasMaxLength(200);
        builder.Property(payment => payment.FailureReason).HasMaxLength(500);
        builder.HasOne(payment => payment.Order)
            .WithMany(order => order.Payments)
            .HasForeignKey(payment => payment.LocalOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PendingCheckoutConfiguration : IEntityTypeConfiguration<PendingCheckoutEntity>
{
    public void Configure(EntityTypeBuilder<PendingCheckoutEntity> builder)
    {
        builder.ToTable("PendingCheckouts");
        builder.HasKey(checkout => checkout.Id);
        builder.HasIndex(checkout => new { checkout.RecoveryStatus, checkout.CreatedAtUtc });
        builder.Property(checkout => checkout.ApprovedAmount).HasPrecision(18, 0);
        builder.Property(checkout => checkout.CashTenderedAmount).HasPrecision(18, 0);
        builder.Property(checkout => checkout.ChangeAmount).HasPrecision(18, 0);
        builder.Property(checkout => checkout.ApprovalCode).HasMaxLength(100);
        builder.Property(checkout => checkout.TransactionReference).HasMaxLength(200);
    }
}

public sealed class SyncQueueConfiguration : IEntityTypeConfiguration<SyncQueueEntity>
{
    public void Configure(EntityTypeBuilder<SyncQueueEntity> builder)
    {
        builder.ToTable("SyncQueue");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.Status, item.NextAttemptAtUtc, item.CreatedAtUtc, item.Id });
        builder.Property(item => item.ItemType).HasMaxLength(100);
        builder.Property(item => item.ReferenceKey).HasMaxLength(200);
        builder.Property(item => item.LastErrorSummary).HasMaxLength(1000);
    }
}
