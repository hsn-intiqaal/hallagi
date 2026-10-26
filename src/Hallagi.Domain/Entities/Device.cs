namespace Hallagi.Domain.Entities;

public class Device
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = default!;            // "كاشير 1"
    public string? DeviceCode { get; set; }                 // معرّف الجهاز الفيزيائي
    public string Status { get; set; } = "Active";          // Active / Disabled
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public Branch Branch { get; set; } = default!;
}
