namespace Hallagi.Domain.Entities;

public class PayrollPeriod
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;            // "أكتوبر 2026"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "Open";            // Open / Closed
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
