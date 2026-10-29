using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> e)
    {
        e.Property(x => x.OpeningCash).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.CountedCash).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.ExpectedCash).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Difference).HasColumnType(ColumnTypes.Money);
        e.HasIndex(x => new { x.BranchId, x.Status });
    }
}
