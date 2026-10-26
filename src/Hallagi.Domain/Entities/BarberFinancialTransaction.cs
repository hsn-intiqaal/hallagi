using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

/// <summary>Immutable ledger entry. Corrections are made with Reversal + a new entry, never edits.</summary>
public class BarberFinancialTransaction
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BarberId { get; set; }
    public Guid? BranchId { get; set; }
    public BarberTxType Type { get; set; }
    public BarberTxDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public DateOnly TxDate { get; set; }
    public string? Description { get; set; }
    public string? SourceType { get; set; }                 // "Sale", "Settlement", "Manual", "Handover"...
    public Guid? SourceId { get; set; }
    public string? SourceRef { get; set; }                  // idempotency/lookup key
    public string Status { get; set; } = "Approved";        // Pending / Approved / Reversed
    public Guid? ReversedById { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }

    public Barber Barber { get; set; } = default!;
}
