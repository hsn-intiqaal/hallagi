namespace Hallagi.Api.Domain;

// ============ Core: organizations, branches, profiles, members ============

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Currency { get; set; } = "SAR";
    public string TimeZone { get; set; } = "Asia/Riyadh";
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public OrganizationSettings? Settings { get; set; }
}

public class Branch
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string City { get; set; } = default!;
    public string? Address { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public CashDrawer? Drawer { get; set; }
}

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

public class MemberBranchAccess
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid BranchId { get; set; }
    public OrganizationMember Member { get; set; } = default!;
}
