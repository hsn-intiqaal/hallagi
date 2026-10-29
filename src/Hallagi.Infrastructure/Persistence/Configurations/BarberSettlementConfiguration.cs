using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BarberSettlementConfiguration : IEntityTypeConfiguration<BarberSettlement>
{
    public void Configure(EntityTypeBuilder<BarberSettlement> e)
    {
        e.Property(x => x.SalaryEarned).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.CommissionEarned).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Bonus).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Allowances).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Reimbursements).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.AdvancesRepaid).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.ChairRent).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Deductions).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.PreviousBalance).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.AlreadyPaid).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.NetPayable).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.PayrollPeriod).WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Barber).WithMany().HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.OrganizationId, x.PayrollPeriodId, x.BarberId }).IsUnique();
        e.Property(x => x.Status).HasMaxLength(20);
    }
}
