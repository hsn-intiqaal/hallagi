namespace Hallagi.Api.Domain;

// ============ Barber financial ledger ============

public enum BarberTxType
{
    CommissionEarned,
    SalaryAccrued,
    Bonus,
    ChairRentDue,
    ChairRentPaid,
    AdvanceGiven,
    AdvanceRepaid,
    ExpensePaidByBarber,
    ExpensePaidByShop,
    CashFloatAllocated,
    CashHandover,
    ReimbursementDue,
    ApprovedAllowance,
    ApprovedAdjustment,
    SettlementPayment,
    Reversal
}

public enum BarberTxDirection { Debit, Credit } // Debit = على الحلاق (مدين), Credit = للحلاق (دائن)

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

// ============ Settlements (payroll) ============

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

public class BarberSettlement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public Guid BarberId { get; set; }
    public Guid? BranchId { get; set; }
    public string AgreementSnapshot { get; set; } = "{}";   // JSON snapshot of the agreement used
    public decimal SalaryEarned { get; set; }
    public decimal CommissionEarned { get; set; }
    public decimal Bonus { get; set; }
    public decimal Allowances { get; set; }
    public decimal Reimbursements { get; set; }
    public decimal AdvancesRepaid { get; set; }
    public decimal ChairRent { get; set; }
    public decimal Deductions { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal AlreadyPaid { get; set; }
    public decimal NetPayable { get; set; }
    public string Status { get; set; } = "Draft";           // Draft / Approved / Paid / Cancelled
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Note { get; set; }

    public PayrollPeriod PayrollPeriod { get; set; } = default!;
    public Barber Barber { get; set; } = default!;
    public ICollection<SettlementItem> Items { get; set; } = new List<SettlementItem>();
    public ICollection<SettlementPayment> Payments { get; set; } = new List<SettlementPayment>();
}

public class SettlementItem
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public string Component { get; set; } = default!;       // "راتب", "عمولة", "سلفة", ...
    public string? SourceRef { get; set; }                  // traceability to the source records
    public decimal Amount { get; set; }
    public string? Explanation { get; set; }
    public BarberSettlement Settlement { get; set; } = default!;
}

public class SettlementPayment
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Method { get; set; }                     // Cash / Bank
    public string? Note { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public BarberSettlement Settlement { get; set; } = default!;
}
