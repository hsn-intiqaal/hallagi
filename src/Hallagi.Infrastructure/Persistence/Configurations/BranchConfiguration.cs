using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> e)
    {
        e.HasOne(x => x.Organization).WithMany(x => x.Branches).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.OrganizationId, x.Name });
        e.Property(x => x.Name).HasMaxLength(150);
        e.Property(x => x.City).HasMaxLength(100);
    }
}
