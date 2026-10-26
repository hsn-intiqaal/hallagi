using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Barber
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public DateOnly? StartDate { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    // Optional login account linked to this barber profile (enables the "my barber" role).
    public Guid? LinkedProfileId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<BarberBranchAssignment> BranchAssignments { get; set; } = new List<BarberBranchAssignment>();
    public ICollection<BarberCompensation> CompensationSettings { get; set; } = new List<BarberCompensation>();
}
