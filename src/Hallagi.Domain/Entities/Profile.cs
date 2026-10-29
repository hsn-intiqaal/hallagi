using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Profile
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = default!;
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    /// <summary>Platform owner (الحساب الأعلى) — not a member of any organization.</summary>
    public bool IsSuperAdmin { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<OrganizationMember> Memberships { get; set; } = new List<OrganizationMember>();
}
