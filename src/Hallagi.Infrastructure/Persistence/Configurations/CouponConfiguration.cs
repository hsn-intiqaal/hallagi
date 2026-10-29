using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> e)
    {
        e.Property(x => x.Value).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.MinSubtotal).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.MaxDiscount).HasColumnType(ColumnTypes.Money);
        e.Property(x => x.Code).HasMaxLength(40);
        e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}
