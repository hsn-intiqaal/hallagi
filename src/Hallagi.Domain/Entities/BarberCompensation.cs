using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class BarberCompensation
{
    public Guid Id { get; set; }
    public Guid BarberId { get; set; }
    public CompensationType Type { get; set; }
    public decimal? MonthlySalary { get; set; }
    public decimal? CommissionRate { get; set; } // 0.30 = 30%
    public decimal? ChairRentAmount { get; set; }
    public string? Notes { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public Barber Barber { get; set; } = default!;
}
