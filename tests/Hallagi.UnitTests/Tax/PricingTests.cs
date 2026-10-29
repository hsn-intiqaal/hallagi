namespace Hallagi.UnitTests;

public class PricingTests
{
    [Fact]
    public void InclusiveVat_IsExtractedFromThePrice()
    {
        var p = Pricing.Calculate(115m, 0m, vatEnabled: true, vatRate: 0.15m, pricesIncludeVat: true);
        p.Total.ShouldBe(115m);
        p.Subtotal.ShouldBe(100m);
        p.Vat.ShouldBe(15m);
    }

    [Fact]
    public void ExclusiveVat_IsAddedOnTop()
    {
        var p = Pricing.Calculate(100m, 0m, true, 0.15m, pricesIncludeVat: false);
        p.Subtotal.ShouldBe(100m);
        p.Vat.ShouldBe(15m);
        p.Total.ShouldBe(115m);
    }

    [Fact]
    public void Discount_IsAppliedBeforeVat_AndNeverExceedsGross()
    {
        var p = Pricing.Calculate(100m, 20m, true, 0.15m, pricesIncludeVat: false);
        p.Subtotal.ShouldBe(80m);
        p.Vat.ShouldBe(12m);
        p.Total.ShouldBe(92m);

        Pricing.Calculate(50m, 80m, false, 0m, true).Total.ShouldBe(0m);
    }

    [Fact]
    public void VatDisabled_LeavesTotalsUntouched()
    {
        var p = Pricing.Calculate(25m, 0m, false, 0.15m, true);
        p.Vat.ShouldBe(0m);
        p.Total.ShouldBe(25m);
        p.Subtotal.ShouldBe(25m);
    }

    [Theory]
    [InlineData("310123456700003", true)]
    [InlineData("300000000000003", true)]
    [InlineData("210123456700003", false)]
    [InlineData("31012345670000", false)]
    [InlineData("31012345670000X", false)]
    [InlineData(null, false)]
    public void VatNumber_Validation(string? vat, bool valid) => Pricing.IsValidVatNumber(vat).ShouldBe(valid);

    [Fact]
    public void ZatcaQr_MatchesTheOfficialTlvExample()
    {
        // Reference vector from the ZATCA e-invoicing implementation guidance
        var qr = ZatcaQr.Encode("Bobs Records", "310122393500003", new DateTime(2022, 4, 25, 15, 30, 0, DateTimeKind.Utc), 1000m, 150m);
        qr.ShouldBe("AQxCb2JzIFJlY29yZHMCDzMxMDEyMjM5MzUwMDAwMwMUMjAyMi0wNC0yNVQxNTozMDowMFoEBzEwMDAuMDAFBjE1MC4wMA==");
    }

    [Fact]
    public void ZatcaQr_RoundTripsArabicSellerNames()
    {
        var qr = ZatcaQr.Encode("مؤسسة صالون النخبة", "310123456700003", DateTime.UtcNow, 28.75m, 3.75m);
        var fields = ZatcaQr.Decode(qr);
        fields[1].ShouldBe("مؤسسة صالون النخبة");
        fields[2].ShouldBe("310123456700003");
        fields[4].ShouldBe("28.75");
        fields[5].ShouldBe("3.75");
    }
}
