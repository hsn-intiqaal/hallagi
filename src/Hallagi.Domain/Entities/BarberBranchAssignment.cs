namespace Hallagi.Domain.Entities;

public class BarberBranchAssignment
{
    public Guid Id { get; set; }
    public Guid BarberId { get; set; }
    public Guid BranchId { get; set; }
    public Barber Barber { get; set; } = default!;
}
