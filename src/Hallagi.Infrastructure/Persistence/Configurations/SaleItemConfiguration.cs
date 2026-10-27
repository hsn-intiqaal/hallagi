using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> e)
    {
        e.Property(x => x.UnitPrice).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.LineTotal).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.UnitCost).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Sale).WithMany(x => x.Items).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.SaleId);
    }
}
