using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BranchServicePriceConfiguration : IEntityTypeConfiguration<BranchServicePrice>
{
    public void Configure(EntityTypeBuilder<BranchServicePrice> e)
    {
        e.Property(x => x.Price).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Service).WithMany(x => x.BranchPrices).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.ServiceId, x.BranchId }).IsUnique();
    }
}
