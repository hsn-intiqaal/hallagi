using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.Property(x => x.Price).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Cost).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Name).HasMaxLength(150);
        e.Property(x => x.Sku).HasMaxLength(60);
        e.Property(x => x.Barcode).HasMaxLength(60);
        e.HasIndex(x => new { x.OrganizationId, x.Name });
        e.HasIndex(x => new { x.OrganizationId, x.Barcode });
    }
}
