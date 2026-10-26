using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> e)
    {
        e.HasOne(x => x.Organization).WithMany(x => x.Members).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Profile).WithMany(x => x.Memberships).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.OrganizationId, x.ProfileId }).IsUnique();
    }
}
