using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class OrganizationMember
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProfileId { get; set; }
    public UserRole Role { get; set; }
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    // Owner-adjustable fine-grained overrides, e.g. {"expense.create": true}. Null = use role default.
    public string? PermissionsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public Profile Profile { get; set; } = default!;
    public ICollection<MemberBranchAccess> BranchAccess { get; set; } = new List<MemberBranchAccess>();
}
