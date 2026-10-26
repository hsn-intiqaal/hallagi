using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class ProductStockConfiguration : IEntityTypeConfiguration<ProductStock>
{
    public void Configure(EntityTypeBuilder<ProductStock> e)
    {
        e.HasOne(x => x.Product).WithMany(x => x.Stock).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.ProductId, x.BranchId }).IsUnique();
    }
}
