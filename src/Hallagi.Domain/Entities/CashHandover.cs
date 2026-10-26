namespace Hallagi.Domain.Entities;

/// <summary>Physical cash handover between people (barber -> owner, cashier -> owner, etc.).</summary>
public class CashHandover
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public Guid? BarberId { get; set; }
    public Guid? ShiftId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly HandoverDate { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Confirmed";       // Pending / Confirmed / Rejected
    public Guid? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Branch Branch { get; set; } = default!;
}
