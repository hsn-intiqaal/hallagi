using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class CashHandoverConfiguration : IEntityTypeConfiguration<CashHandover>
{
    public void Configure(EntityTypeBuilder<CashHandover> e)
    {
        e.Property(x => x.Amount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.BranchId, x.HandoverDate });
        e.Property(x => x.Status).HasMaxLength(20);
    }
}
