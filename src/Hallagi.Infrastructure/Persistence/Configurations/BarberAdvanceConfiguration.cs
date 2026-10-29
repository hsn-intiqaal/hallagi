using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BarberAdvanceConfiguration : IEntityTypeConfiguration<BarberAdvance>
{
    public void Configure(EntityTypeBuilder<BarberAdvance> e)
    {
        e.Property(x => x.OriginalAmount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.RepaidAmount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Barber).WithMany().HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.BarberId, x.Status });
        e.Property(x => x.Status).HasMaxLength(20);
    }
}
