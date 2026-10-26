using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> e)
    {
        e.Property(x => x.Amount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Shift).WithMany(x => x.Movements).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.ShiftId);
    }
}
