using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> e)
    {
        e.Property(x => x.CustomerName).HasMaxLength(150);
        e.Property(x => x.CustomerPhone).HasMaxLength(30);
        e.Property(x => x.Notes).HasMaxLength(500);
        e.Property(x => x.Reference).HasMaxLength(12);
        e.HasIndex(x => new { x.BranchId, x.StartAt });
        e.HasIndex(x => new { x.BarberId, x.StartAt });
        e.HasIndex(x => new { x.OrganizationId, x.Reference }).IsUnique();
    }
}
