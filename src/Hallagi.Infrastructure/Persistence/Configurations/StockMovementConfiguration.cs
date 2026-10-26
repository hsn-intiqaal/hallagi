using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> e)
    {
        e.Property(x => x.UnitCost).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Note).HasMaxLength(300);
        e.HasIndex(x => new { x.ProductId, x.BranchId, x.CreatedAt });
    }
}
