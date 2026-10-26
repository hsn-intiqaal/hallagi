namespace Hallagi.Api.Domain;

// ============ Core: organizations, branches, profiles, members ============

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Currency { get; set; } = "SAR";
    public string TimeZone { get; set; } = "Asia/Riyadh";
    public string? LogoUrl { get; set; }
    /// <summary>Brand primary color as #RRGGBB — drives the tenant UI theme.</summary>
    public string PrimaryColor { get; set; } = "#2563EB";
    /// <summary>Brand secondary color as #RRGGBB — topbar / headings.</summary>
    public string SecondaryColor { get; set; } = "#173653";
    /// <summary>Wide banner image (data URL) shown behind the organization header.</summary>
    public string? CoverImageUrl { get; set; }
    /// <summary>URL-safe public identifier for the online booking page (/book/{slug}).</summary>
    public string? Slug { get; set; }
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

/// <summary>Long-lived refresh token (only its SHA-256 hash is stored). Rotated on every use.</summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public Guid? MemberId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}
