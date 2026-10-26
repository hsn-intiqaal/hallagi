using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> e)
    {
        e.Property(x => x.TotalAmount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.GrossAmount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.DiscountAmount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.SubtotalAmount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.VatAmount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.CommissionBase).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.VatRate).HasColumnType(ColumnTypes.Rate);
        e.Property(x => x.CouponCode).HasMaxLength(40);
        e.Property(x => x.InvoiceSellerName).HasMaxLength(200);
        e.Property(x => x.InvoiceVatNumber).HasMaxLength(15);
        e.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
        e.HasIndex(x => new { x.OrganizationId, x.ReceiptNo }).IsUnique();
        e.HasIndex(x => new { x.BranchId, x.CreatedAt });
        e.HasIndex(x => new { x.BarberId, x.CreatedAt });
    }
}
