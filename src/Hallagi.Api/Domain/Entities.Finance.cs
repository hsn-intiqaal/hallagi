namespace Hallagi.Api.Domain;

// ============ Finance: sales, expenses, drawers, shifts, movements, audit ============

public class Sale
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid BarberId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid CreatedBy { get; set; }
    public int ReceiptNo { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    /// <summary>Sum of line totals before any discount (as priced in the catalog).</summary>
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Taxable amount excluding VAT.</summary>
    public decimal SubtotalAmount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    /// <summary>Amount paid by the customer (VAT inclusive).</summary>
    public decimal TotalAmount { get; set; }
    /// <summary>Service revenue excluding VAT and products, after discount — the barber commission base.</summary>
    public decimal CommissionBase { get; set; }
    public Guid? CouponId { get; set; }
    public string? CouponCode { get; set; }
    public int PointsRedeemed { get; set; }
    public int PointsEarned { get; set; }
    public Guid? AppointmentId { get; set; }
    /// <summary>Seller identity snapshot for the tax invoice (null when VAT is disabled).</summary>
    public string? InvoiceSellerName { get; set; }
    public string? InvoiceVatNumber { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public Guid? CancelledBy { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public SaleItemType ItemType { get; set; } = SaleItemType.Service;
    public Guid? ServiceId { get; set; }
    public Guid? ProductId { get; set; }
    /// <summary>Display name snapshot (service or product).</summary>
    public string ServiceName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public Sale Sale { get; set; } = default!;
}

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

public class CashDrawer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Shift
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DrawerId { get; set; }
    public Guid OpenedBy { get; set; }
    public decimal OpeningCash { get; set; }
    public DateTime OpenedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Difference { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public string? Note { get; set; }
    public bool AutoOpened { get; set; }
    public Guid? DeviceId { get; set; }               // POS device used to open the shift (optional)

    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}

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

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
