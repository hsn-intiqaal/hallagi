using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> e)
    {
        e.Property(x => x.FullName).HasMaxLength(150);
        e.HasIndex(x => x.OrganizationId);
        e.HasIndex(x => new { x.OrganizationId, x.Phone });
    }
}
