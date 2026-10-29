using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Service
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    /// <summary>Expected chair time — used to build appointment slots.</summary>
    public int DurationMinutes { get; set; } = 30;
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<BranchServicePrice> BranchPrices { get; set; } = new List<BranchServicePrice>();
}
