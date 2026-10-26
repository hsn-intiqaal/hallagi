using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class AppointmentItemConfiguration : IEntityTypeConfiguration<AppointmentItem>
{
    public void Configure(EntityTypeBuilder<AppointmentItem> e)
    {
        e.Property(x => x.Price).HasColumnType(ColumnTypes.Money);
        e.HasOne(x => x.Appointment).WithMany(x => x.Items).HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
