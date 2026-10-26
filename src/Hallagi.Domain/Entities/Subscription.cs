namespace Hallagi.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "Trial";           // Trial / Active / PastDue / Cancelled
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public DateOnly? TrialEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public SubscriptionPlan Plan { get; set; } = default!;
}
