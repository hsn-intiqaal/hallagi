namespace Hallagi.Application.Commerce;

public record CouponInput(
    string Code,
    string? Description,
    DiscountType Type,
    decimal Value,
    decimal? MinSubtotal,
    decimal? MaxDiscount,
    int? MaxUses,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool Active);
