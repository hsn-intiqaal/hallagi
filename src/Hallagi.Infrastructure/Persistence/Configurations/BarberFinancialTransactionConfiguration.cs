using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BarberFinancialTransactionConfiguration : IEntityTypeConfiguration<BarberFinancialTransaction>
{
    public void Configure(EntityTypeBuilder<BarberFinancialTransaction> e)
    {
        e.Property(x => x.Amount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Barber).WithMany().HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.BarberId, x.TxDate });
        e.HasIndex(x => x.SourceRef);
        e.Property(x => x.Status).HasMaxLength(20);
        e.Property(x => x.SourceType).HasMaxLength(40);
    }
}
