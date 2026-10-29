namespace Hallagi.Domain.Entities;

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
