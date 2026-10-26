using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> e)
    {
        e.Property(x => x.BasePrice).HasColumnType(ColumnTypes.Money);
        e.HasIndex(x => new { x.OrganizationId, x.Name });
        e.Property(x => x.Name).HasMaxLength(150);
    }
}
