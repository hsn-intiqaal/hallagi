namespace Hallagi.Application.Tax;

/// <summary>Result of pricing a cart: every amount rounded to halalas (2 decimals).</summary>
public record PriceBreakdown(
    decimal Gross,
    decimal Discount,
    decimal Subtotal,
    decimal VatRate,
    decimal Vat,
    decimal Total);
