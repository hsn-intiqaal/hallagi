using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
    public ExpenseSource Source { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Active;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    public Guid? VoidedBy { get; set; }
}
