using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class SettlementItemConfiguration : IEntityTypeConfiguration<SettlementItem>
{
    public void Configure(EntityTypeBuilder<SettlementItem> e)
    {
        e.Property(x => x.Amount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Settlement).WithMany(x => x.Items).HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.Component).HasMaxLength(60);
    }
}
