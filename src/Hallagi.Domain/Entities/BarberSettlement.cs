namespace Hallagi.Domain.Entities;

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
