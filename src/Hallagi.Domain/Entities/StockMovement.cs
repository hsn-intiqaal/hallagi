using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

/// <summary>Immutable stock journal: every quantity change is recorded with its reason.</summary>
public class StockMovement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }                       // signed: + in, − out
    public int BalanceAfter { get; set; }
    public decimal? UnitCost { get; set; }
    public Guid? SaleId { get; set; }
    public string? Note { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
