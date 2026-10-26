using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Coupon
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;            // stored upper-case
    public string? Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }                      // percent (0-100) or SAR amount
    public decimal? MinSubtotal { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
