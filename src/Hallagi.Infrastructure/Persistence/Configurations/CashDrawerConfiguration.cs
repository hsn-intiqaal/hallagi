using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class CashDrawerConfiguration : IEntityTypeConfiguration<CashDrawer>
{
    public void Configure(EntityTypeBuilder<CashDrawer> e)
    {
        e.HasIndex(x => x.BranchId).IsUnique();
    }
}
