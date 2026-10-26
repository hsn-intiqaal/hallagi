using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class MemberBranchAccessConfiguration : IEntityTypeConfiguration<MemberBranchAccess>
{
    public void Configure(EntityTypeBuilder<MemberBranchAccess> e)
    {
        e.HasOne(x => x.Member).WithMany(x => x.BranchAccess).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.MemberId, x.BranchId }).IsUnique();
    }
}
