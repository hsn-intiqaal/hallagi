namespace Hallagi.Api.Domain;

// ============ Commerce: retail products, stock, coupons, loyalty ============

public class Product
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProductStock> Stock { get; set; } = new List<ProductStock>();
}

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

public class Coupon
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;            // stored upper-case
    public string? Description { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }                      // percent (0-100) or SAR amount
    public decimal? MinSubtotal { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class LoyaltyTransaction
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    public LoyaltyTxType Type { get; set; }
    public int Points { get; set; }                         // signed
    public int BalanceAfter { get; set; }
    public Guid? SaleId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
