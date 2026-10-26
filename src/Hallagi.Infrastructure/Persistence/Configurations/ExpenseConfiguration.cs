using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> e)
    {
        e.Property(x => x.Amount).HasColumnType(ColumnTypes.Money);
        e.HasIndex(x => new { x.BranchId, x.ExpenseDate });
        e.Property(x => x.Description).HasMaxLength(500);
    }
}
