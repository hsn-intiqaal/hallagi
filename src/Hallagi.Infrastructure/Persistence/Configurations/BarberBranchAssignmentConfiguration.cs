using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class BarberBranchAssignmentConfiguration : IEntityTypeConfiguration<BarberBranchAssignment>
{
    public void Configure(EntityTypeBuilder<BarberBranchAssignment> e)
    {
        e.HasOne(x => x.Barber).WithMany(x => x.BranchAssignments).HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.BarberId, x.BranchId }).IsUnique();
    }
}
