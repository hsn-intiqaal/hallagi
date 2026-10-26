namespace Hallagi.Domain.Entities;

/// <summary>Advance paid to a barber; balance is derived from repayments.</summary>
public class BarberAdvance
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BarberId { get; set; }
    public Guid? BranchId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal RepaidAmount { get; set; }
    public DateOnly AdvanceDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Approved";        // Pending / Approved / Settled / Cancelled
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }

    public Barber Barber { get; set; } = default!;
}
