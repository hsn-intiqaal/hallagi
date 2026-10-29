namespace Hallagi.Domain.Entities;

/// <summary>On-hand quantity of a product in one branch.</summary>
public class ProductStock
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public Product Product { get; set; } = default!;
}
