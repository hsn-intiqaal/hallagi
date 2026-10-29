using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> e)
    {
        e.HasIndex(x => x.Name);
        e.Property(x => x.Name).HasMaxLength(150);
        e.Property(x => x.Slug).HasMaxLength(60);
        e.HasIndex(x => x.Slug).IsUnique();
    }
}
