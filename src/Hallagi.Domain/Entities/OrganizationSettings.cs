namespace Hallagi.Domain.Entities;

public class OrganizationSettings
{
    public Guid OrganizationId { get; set; }
    public bool EnableCash { get; set; } = true;
    public bool EnableCard { get; set; } = true;
    public string? ReceiptFooter { get; set; }
    public string? ReceiptPhone { get; set; }

    // ===== VAT / ZATCA (e-invoicing phase 1: simplified tax invoice + QR) =====
    public bool VatEnabled { get; set; }
    public decimal VatRate { get; set; } = 0.15m;
    /// <summary>True = catalog prices already include VAT (common for barbershops).</summary>
    public bool PricesIncludeVat { get; set; } = true;
    public string? VatNumber { get; set; }                  // 15 digits, starts and ends with 3
    public string? CommercialRegistration { get; set; }
    public string? LegalName { get; set; }                  // seller name on the tax invoice
    public string? TaxAddress { get; set; }

    // ===== Discounts & loyalty =====
    /// <summary>Upper bound for manual discounts entered at the POS (percent of the subtotal).</summary>
    public decimal MaxManualDiscountPercent { get; set; } = 30m;
    public bool LoyaltyEnabled { get; set; }
    public decimal LoyaltyPointsPerSar { get; set; } = 1m;   // points earned per 1 SAR paid
    public decimal LoyaltyPointValue { get; set; } = 0.05m;  // SAR value of one point
    public int LoyaltyMinRedeem { get; set; } = 100;

    // ===== Online booking =====
    public bool OnlineBookingEnabled { get; set; }
    public int BookingSlotMinutes { get; set; } = 15;
    public int BookingMaxDaysAhead { get; set; } = 14;

    public Organization Organization { get; set; } = default!;
}
