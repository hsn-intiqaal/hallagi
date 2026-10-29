using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Branch
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    /// <summary>Daily opening hours (organization local time) — drives booking availability.</summary>
    public TimeOnly OpensAt { get; set; } = new(9, 0);
    public TimeOnly ClosesAt { get; set; } = new(23, 0);
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public CashDrawer? Drawer { get; set; }
}
