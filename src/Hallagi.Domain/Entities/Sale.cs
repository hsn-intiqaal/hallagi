using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

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
