using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class CashMovement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ShiftId { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; } // always positive; direction determined by Type
    public string? Description { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? ExpenseId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Shift Shift { get; set; } = default!;
}
