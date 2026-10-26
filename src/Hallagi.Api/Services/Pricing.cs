namespace Hallagi.Api.Services;

/// <summary>Result of pricing a cart: every amount rounded to halalas (2 decimals).</summary>
public record PriceBreakdown(
    decimal Gross,
    decimal Discount,
    decimal Subtotal,
    decimal VatRate,
    decimal Vat,
    decimal Total);

/// <summary>
/// Pure pricing rules shared by the POS, the receipt and the tests.
/// Discount is applied to the gross before tax; VAT is then derived either
/// from a tax-inclusive price (price already contains VAT) or added on top.
/// </summary>
public static class Pricing
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static PriceBreakdown Calculate(decimal gross, decimal discount, bool vatEnabled, decimal vatRate, bool pricesIncludeVat)
    {
        if (gross < 0) throw new ArgumentOutOfRangeException(nameof(gross));
        gross = Round(gross);
        discount = Round(Math.Clamp(discount, 0m, gross));
        var net = gross - discount;

        if (!vatEnabled || vatRate <= 0)
            return new PriceBreakdown(gross, discount, net, 0m, 0m, net);

        if (pricesIncludeVat)
        {
            var subtotal = Round(net / (1 + vatRate));
            return new PriceBreakdown(gross, discount, subtotal, vatRate, net - subtotal, net);
        }

        var vat = Round(net * vatRate);
        return new PriceBreakdown(gross, discount, net, vatRate, vat, net + vat);
    }

    /// <summary>Removes VAT from a tax-inclusive amount (used for commission bases).</summary>
    public static decimal ExcludeVat(decimal amount, bool vatEnabled, decimal vatRate, bool pricesIncludeVat)
        => vatEnabled && vatRate > 0 && pricesIncludeVat ? Round(amount / (1 + vatRate)) : Round(amount);

    /// <summary>Valid Saudi VAT registration number: 15 digits, starting and ending with 3.</summary>
    public static bool IsValidVatNumber(string? vat)
        => vat is { Length: 15 } && vat.All(char.IsAsciiDigit) && vat[0] == '3' && vat[^1] == '3';
}
