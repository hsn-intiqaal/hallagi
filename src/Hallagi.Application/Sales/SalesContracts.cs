namespace Hallagi.Application.Sales;

public record CreateSaleItem(Guid? ServiceId, int Quantity, Guid? ProductId = null);

public record CreateSaleRequest(
    Guid BranchId,
    Guid BarberId,
    Guid? CustomerId,
    PaymentMethod PaymentMethod,
    List<CreateSaleItem> Items,
    string? IdempotencyKey,
    string? CouponCode = null,
    decimal? ManualDiscount = null,
    int RedeemPoints = 0,
    Guid? AppointmentId = null);
