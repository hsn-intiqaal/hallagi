using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> e)
    {
        e.HasIndex(x => new { x.OrganizationId, x.StartDate, x.EndDate }).IsUnique();
        e.Property(x => x.Name).HasMaxLength(100);
        e.Property(x => x.Status).HasMaxLength(20);
    }
}
