namespace Hallagi.Application.Organizations;

public record BranchInput(string Name, string City, string? Address, bool Active, string? Phone = null, string? OpensAt = null, string? ClosesAt = null);

public record UpdateSettingsRequest(
    string OrganizationName,
    string? Currency,
    string? TimeZone,
    string? LogoUrl,
    bool EnableCash,
    bool EnableCard,
    string? ReceiptFooter,
    string? ReceiptPhone,
    string? CoverImageUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    TaxSettingsInput? Tax = null,
    LoyaltySettingsInput? Loyalty = null,
    BookingSettingsInput? Booking = null,
    decimal? MaxManualDiscountPercent = null);

public record TaxSettingsInput(bool VatEnabled, decimal VatRate, bool PricesIncludeVat, string? VatNumber, string? CommercialRegistration, string? LegalName, string? TaxAddress);

public record LoyaltySettingsInput(bool Enabled, decimal PointsPerSar, decimal PointValue, int MinRedeem);

public record BookingSettingsInput(bool Enabled, string? Slug, int SlotMinutes, int MaxDaysAhead);
