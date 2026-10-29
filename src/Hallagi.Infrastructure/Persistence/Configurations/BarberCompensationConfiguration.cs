using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BarberCompensationConfiguration : IEntityTypeConfiguration<BarberCompensation>
{
    public void Configure(EntityTypeBuilder<BarberCompensation> e)
    {
        e.Property(x => x.MonthlySalary).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.CommissionRate).HasColumnType(ColumnTypes.Rate);
        e.Property(x => x.ChairRentAmount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Barber).WithMany(x => x.CompensationSettings).HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.BarberId);
    }
}
