using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> e)
    {
        e.HasKey(x => x.OrganizationId);
        e.Property(x => x.VatRate).HasColumnType(ColumnTypes.Rate);
        e.Property(x => x.MaxManualDiscountPercent).HasColumnType(ColumnTypes.Rate);
        e.Property(x => x.LoyaltyPointsPerSar).HasColumnType(ColumnTypes.Rate);
        e.Property(x => x.LoyaltyPointValue).HasColumnType(ColumnTypes.Rate);
        e.Property(x => x.VatNumber).HasMaxLength(15);
        e.Property(x => x.CommercialRegistration).HasMaxLength(20);
        e.Property(x => x.LegalName).HasMaxLength(200);
        e.HasOne(x => x.Organization).WithOne(x => x.Settings!).HasForeignKey<OrganizationSettings>(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}
