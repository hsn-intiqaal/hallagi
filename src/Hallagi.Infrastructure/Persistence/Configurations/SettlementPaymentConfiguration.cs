using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class SettlementPaymentConfiguration : IEntityTypeConfiguration<SettlementPayment>
{
    public void Configure(EntityTypeBuilder<SettlementPayment> e)
    {
        e.Property(x => x.Amount).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Settlement).WithMany(x => x.Payments).HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.Method).HasMaxLength(20);
    }
}
