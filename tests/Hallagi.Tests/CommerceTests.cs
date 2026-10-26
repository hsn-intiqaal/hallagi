using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Hallagi.Tests;

// ==================== Pricing & ZATCA ====================

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

// ==================== Sales: VAT, products, coupons, discounts, loyalty ====================

public class CommerceSalesTests : IDisposable
{
    private readonly TestDb _db;
    private readonly SalesService _sales;

    public CommerceSalesTests()
    {
        _db = new TestDb();
        _sales = new SalesService(_db.Db);
    }

    public void Dispose() => _db.Dispose();

    private OrganizationSettings Settings() => _db.Db.OrganizationSettings.First(s => s.OrganizationId == _db.OrgId);

    private Guid AddProduct(decimal price, int stockA, decimal cost = 10m)
    {
        var id = Guid.NewGuid();
        _db.Db.Products.Add(new Product { Id = id, OrganizationId = _db.OrgId, Name = "زيت لحية", Price = price, Cost = cost, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Db.ProductStocks.Add(new ProductStock { Id = Guid.NewGuid(), ProductId = id, BranchId = _db.BranchA, Quantity = stockA });
        _db.Db.SaveChanges();
        return id;
    }

    private Guid AddCustomer()
    {
        var id = Guid.NewGuid();
        _db.Db.Customers.Add(new Customer { Id = id, OrganizationId = _db.OrgId, FullName = "سعد", Phone = "0551112222", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Db.SaveChanges();
        return id;
    }

    private Task<object> Sell(CurrentUser user, List<CreateSaleItem> items, string key, Guid? customer = null,
        string? coupon = null, decimal? manual = null, int points = 0, PaymentMethod method = PaymentMethod.Cash)
        => _sales.CreateAsync(user, new CreateSaleRequest(_db.BranchA, _db.BarberId, customer, method, items, key, coupon, manual, points));

    [Fact]
    public async Task VatSale_IssuesSimplifiedTaxInvoice_WithZatcaQr()
    {
        var s = Settings();
        s.VatEnabled = true;
        s.VatNumber = "310123456700003";
        s.LegalName = "مؤسسة اختبار";
        _db.Db.SaveChanges();

        var result = await Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "vat-1");
        var sale = Ref.Val(result, "sale")!;

        Ref.Dec(sale, "totalAmount").ShouldBe(25m);          // prices include VAT
        Ref.Dec(sale, "vatAmount").ShouldBe(3.26m);
        Ref.Dec(sale, "subtotalAmount").ShouldBe(21.74m);
        Ref.Bool(sale, "isTaxInvoice").ShouldBeTrue();

        var qr = Ref.Str(Ref.Val(sale, "invoice"), "zatcaQr");
        var fields = ZatcaQr.Decode(qr);
        fields[1].ShouldBe("مؤسسة اختبار");
        fields[2].ShouldBe("310123456700003");
        fields[4].ShouldBe("25.00");
        fields[5].ShouldBe("3.26");

        // barber commission is calculated on revenue excluding VAT
        _db.Db.Sales.Single().CommissionBase.ShouldBe(21.74m);
    }

    [Fact]
    public async Task VatEnabled_WithoutVatNumber_RefusesToIssueInvoices()
    {
        Settings().VatEnabled = true;
        _db.Db.SaveChanges();
        var ex = await Should.ThrowAsync<ApiException>(() => Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "vat-x"));
        ex.Code.ShouldBe("vat_number_missing");
    }

    [Fact]
    public async Task ProductSale_DecrementsStock_AndCancelRestoresIt()
    {
        var product = AddProduct(price: 40m, stockA: 5);
        var result = await Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1), new(null, 2, product) }, "prod-1");
        var saleId = Ref.Id(Ref.Val(result, "sale"), "id");

        Ref.Dec(Ref.Val(result, "sale"), "totalAmount").ShouldBe(105m);
        _db.Db.ProductStocks.Single().Quantity.ShouldBe(3);
        _db.Db.StockMovements.Single(m => m.Type == StockMovementType.Sale).Quantity.ShouldBe(-2);

        // retail products never count toward the barber's service commission
        _db.Db.Sales.Single().CommissionBase.ShouldBe(25m);

        await _sales.CancelAsync(_db.AsOwner(), saleId, "خطأ إدخال");
        _db.Db.ChangeTracker.Clear();
        _db.Db.ProductStocks.Single().Quantity.ShouldBe(5);
        _db.Db.StockMovements.Count(m => m.Type == StockMovementType.SaleVoid).ShouldBe(1);
    }

    [Fact]
    public async Task ProductSale_BeyondStock_IsRejected_AndNothingIsSaved()
    {
        var product = AddProduct(price: 40m, stockA: 1);
        var ex = await Should.ThrowAsync<ApiException>(() => Sell(_db.AsCashier(), new() { new(null, 2, product) }, "prod-x"));
        ex.Code.ShouldBe("insufficient_stock");
        _db.Db.Sales.Count().ShouldBe(0);
        _db.Db.ProductStocks.Single().Quantity.ShouldBe(1);
    }

    [Fact]
    public async Task Item_MustBeEitherServiceOrProduct()
    {
        var ex = await Should.ThrowAsync<ApiException>(() => Sell(_db.AsCashier(), new() { new(null, 1) }, "bad-item"));
        ex.Code.ShouldBe("invalid_item");
    }

    [Fact]
    public async Task PercentCoupon_RespectsMaxDiscount_AndTracksUsage()
    {
        _db.Db.Coupons.Add(new Coupon
        {
            Id = Guid.NewGuid(), OrganizationId = _db.OrgId, Code = "SAVE50", Type = DiscountType.Percent, Value = 50m,
            MaxDiscount = 20m, MaxUses = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _db.Db.SaveChanges();

        var result = await Sell(_db.AsCashier(), new() { new(_db.OtherServiceId, 1) }, "cp-1", coupon: "save50");
        var sale = Ref.Val(result, "sale")!;
        Ref.Dec(sale, "discountAmount").ShouldBe(20m);
        Ref.Dec(sale, "totalAmount").ShouldBe(30m);
        _db.Db.Coupons.Single().UsedCount.ShouldBe(1);

        var ex = await Should.ThrowAsync<ApiException>(() => Sell(_db.AsCashier(), new() { new(_db.OtherServiceId, 1) }, "cp-2", coupon: "SAVE50"));
        ex.Code.ShouldBe("coupon_exhausted");

        await _sales.CancelAsync(_db.AsOwner(), Ref.Id(sale, "id"), "إرجاع");
        _db.Db.ChangeTracker.Clear();
        _db.Db.Coupons.Single().UsedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExpiredCoupon_IsRejected()
    {
        _db.Db.Coupons.Add(new Coupon
        {
            Id = Guid.NewGuid(), OrganizationId = _db.OrgId, Code = "OLD", Type = DiscountType.Fixed, Value = 5m,
            ValidTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _db.Db.SaveChanges();
        var ex = await Should.ThrowAsync<ApiException>(() => Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "cp-old", coupon: "OLD"));
        ex.Code.ShouldBe("coupon_expired");
    }

    [Fact]
    public async Task ManualDiscount_RequiresPermission_AndIsCapped()
    {
        var cashier = _db.AsCashier();
        var forbidden = await Should.ThrowAsync<ApiException>(() => Sell(cashier, new() { new(_db.SaleServiceId, 1) }, "md-1", manual: 5m));
        forbidden.Status.ShouldBe(403);

        var allowed = cashier with { PermissionOverrides = new Dictionary<string, bool> { [Permissions.SaleDiscount] = true } };
        var tooHigh = await Should.ThrowAsync<ApiException>(() => Sell(allowed, new() { new(_db.SaleServiceId, 1) }, "md-2", manual: 10m));
        tooHigh.Code.ShouldBe("discount_too_high");   // default cap 30% of 25 = 7.50

        var ok = await Sell(allowed, new() { new(_db.SaleServiceId, 1) }, "md-3", manual: 7.5m);
        Ref.Dec(Ref.Val(ok, "sale"), "totalAmount").ShouldBe(17.5m);
    }

    [Fact]
    public async Task Loyalty_EarnsOnPayment_RedeemsAsDiscount_AndReversesOnCancel()
    {
        var s = Settings();
        s.LoyaltyEnabled = true;
        s.LoyaltyPointsPerSar = 1m;
        s.LoyaltyPointValue = 0.1m;
        s.LoyaltyMinRedeem = 50;
        _db.Db.SaveChanges();
        var customer = AddCustomer();

        await Sell(_db.AsCashier(), new() { new(_db.OtherServiceId, 2) }, "lo-1", customer);   // pays 100 → 100 pts
        _db.Db.Customers.Single(c => c.Id == customer).LoyaltyPoints.ShouldBe(100);

        var redeem = await Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "lo-2", customer, points: 100); // 10 SAR off
        var sale = Ref.Val(redeem, "sale")!;
        Ref.Dec(sale, "totalAmount").ShouldBe(15m);
        Ref.Int(sale, "pointsEarned").ShouldBe(15);
        _db.Db.Customers.Single(c => c.Id == customer).LoyaltyPoints.ShouldBe(15);

        await _sales.CancelAsync(_db.AsOwner(), Ref.Id(sale, "id"), "إلغاء");
        _db.Db.ChangeTracker.Clear();
        _db.Db.Customers.Single(c => c.Id == customer).LoyaltyPoints.ShouldBe(100);
        _db.Db.LoyaltyTransactions.Count(t => t.CustomerId == customer).ShouldBe(4);
    }

    [Fact]
    public async Task Loyalty_CannotRedeemMoreThanBalance()
    {
        Settings().LoyaltyEnabled = true;
        _db.Db.SaveChanges();
        var customer = AddCustomer();
        var ex = await Should.ThrowAsync<ApiException>(() => Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "lo-x", customer, points: 500));
        ex.Code.ShouldBe("insufficient_points");
    }

    [Fact]
    public async Task StockAdjustment_CannotGoNegative_AndPurchaseUpdatesCost()
    {
        var product = AddProduct(price: 40m, stockA: 2, cost: 10m);
        var svc = new ProductService(_db.Db);
        var owner = _db.AsOwner();

        await svc.AdjustStockAsync(owner, product, new StockAdjustInput(_db.BranchA, StockMovementType.Purchase, 10, 12m, "فاتورة مورد", null));
        _db.Db.ProductStocks.Single().Quantity.ShouldBe(12);
        _db.Db.Products.Single().Cost.ShouldBe(12m);

        var ex = await Should.ThrowAsync<ApiException>(() =>
            svc.AdjustStockAsync(owner, product, new StockAdjustInput(_db.BranchA, StockMovementType.Adjustment, -20, null, "جرد", null)));
        ex.Code.ShouldBe("negative_stock");
    }

    [Fact]
    public async Task Cashier_CannotManageProducts()
    {
        var svc = new ProductService(_db.Db);
        var ex = await Should.ThrowAsync<ApiException>(() => svc.CreateAsync(_db.AsCashier(), new ProductInput("x", null, null, 10m, 5m, true)));
        ex.Status.ShouldBe(403);
    }
}

// ==================== Appointments & online booking ====================

public class AppointmentTests : IDisposable
{
    private readonly TestDb _db;
    private readonly AppointmentService _svc;
    private readonly DateOnly _tomorrow;

    public AppointmentTests()
    {
        _db = new TestDb();
        _svc = new AppointmentService(_db.Db);
        _tomorrow = OrgTime.Today(OrgTime.Resolve("Asia/Riyadh")).AddDays(1);
    }

    public void Dispose() => _db.Dispose();

    private AppointmentInput Booking(string time, Guid? barber = null, string? phone = "0551234567")
        => new(_db.BranchA, barber ?? _db.BarberId, null, "فهد", phone, _tomorrow, time, new List<Guid> { _db.SaleServiceId }, null);

    private void EnableOnlineBooking()
    {
        _db.Db.Organizations.Single().Slug = "test-salon";
        _db.Db.OrganizationSettings.Single().OnlineBookingEnabled = true;
        _db.Db.SaveChanges();
    }

    [Fact]
    public async Task SameBarber_SameTime_IsRejected()
    {
        await _svc.CreateAsync(_db.AsCashier(), Booking("18:00"));
        var ex = await Should.ThrowAsync<ApiException>(() => _svc.CreateAsync(_db.AsCashier(), Booking("18:15", phone: "0559999999")));
        ex.Code.ShouldBe("slot_taken");
    }

    [Fact]
    public async Task Booking_OutsideBranchHours_IsRejected()
    {
        var ex = await Should.ThrowAsync<ApiException>(() => _svc.CreateAsync(_db.AsCashier(), Booking("23:45")));
        ex.Code.ShouldBe("outside_hours");
    }

    [Fact]
    public async Task Booking_WithPhone_CreatesOrReusesCustomer()
    {
        await _svc.CreateAsync(_db.AsCashier(), Booking("10:00", phone: "+966 55 123 4567"));
        var appt = _db.Db.Appointments.Single();
        appt.CustomerPhone.ShouldBe("0551234567");
        appt.CustomerId.ShouldNotBeNull();
        appt.EndAt.ShouldBe(appt.StartAt.AddMinutes(30));
    }

    [Fact]
    public async Task Slots_ExcludeBookedTimes()
    {
        await _svc.CreateAsync(_db.AsCashier(), Booking("12:00"));
        var result = await _svc.StaffSlotsAsync(_db.AsCashier(), _db.BranchA, _db.BarberId, _tomorrow, new() { _db.SaleServiceId }, null);
        var times = ((System.Collections.IEnumerable)Ref.Val(result, "slots")!).Cast<object>().Select(s => Ref.Str(s, "time")).ToList();

        times.ShouldContain("11:30");
        times.ShouldNotContain("11:45");   // would overlap 12:00–12:30
        times.ShouldNotContain("12:00");
        times.ShouldContain("12:30");
        times.Last().ShouldBe("22:30");    // last 30-minute slot before 23:00 closing
    }

    [Fact]
    public async Task WalkIn_FlowsFromQueue_ToService_ToSale()
    {
        var created = await _svc.CreateAsync(_db.AsCashier(),
            new AppointmentInput(_db.BranchA, null, null, "زائر", null, null, null, new() { _db.SaleServiceId }, null, WalkIn: true));
        var id = Ref.Id(created, "id");
        _db.Db.Appointments.Single().Status.ShouldBe(AppointmentStatus.Waiting);

        await _svc.SetStatusAsync(_db.AsCashier(), id, new AppointmentStatusInput(AppointmentStatus.InService, null, _db.BarberId));

        var sales = new SalesService(_db.Db);
        await sales.CreateAsync(_db.AsCashier(), new CreateSaleRequest(_db.BranchA, _db.BarberId, null, PaymentMethod.Card,
            new() { new(_db.SaleServiceId, 1) }, "walk-1", AppointmentId: id));

        _db.Db.ChangeTracker.Clear();
        var appt = _db.Db.Appointments.Single();
        appt.Status.ShouldBe(AppointmentStatus.Completed);
        appt.SaleId.ShouldNotBeNull();

        var again = await Should.ThrowAsync<ApiException>(() => sales.CreateAsync(_db.AsCashier(), new CreateSaleRequest(_db.BranchA, _db.BarberId, null,
            PaymentMethod.Card, new() { new(_db.SaleServiceId, 1) }, "walk-2", AppointmentId: id)));
        again.Code.ShouldBe("appointment_closed");
    }

    [Fact]
    public async Task Completed_IsOnlyReachedThroughASale()
    {
        var created = await _svc.CreateAsync(_db.AsCashier(), Booking("15:00"));
        var ex = await Should.ThrowAsync<ApiException>(() =>
            _svc.SetStatusAsync(_db.AsCashier(), Ref.Id(created, "id"), new AppointmentStatusInput(AppointmentStatus.Completed, null, null)));
        ex.Code.ShouldBe("invalid_transition");
    }

    [Fact]
    public async Task Barber_CannotManageAppointments()
    {
        var ex = await Should.ThrowAsync<ApiException>(() => _svc.CreateAsync(_db.AsBarber(), Booking("16:00")));
        ex.Status.ShouldBe(403);
    }

    [Fact]
    public async Task PublicBooking_IsClosed_UntilTheOwnerEnablesIt()
    {
        _db.Db.Organizations.Single().Slug = "test-salon";
        _db.Db.SaveChanges();
        var ex = await Should.ThrowAsync<ApiException>(() => _svc.PublicInfoAsync("test-salon"));
        ex.Status.ShouldBe(404);
    }

    [Fact]
    public async Task PublicBooking_AssignsAFreeBarber_AndCanBeCancelledByTheCustomer()
    {
        EnableOnlineBooking();
        var result = await _svc.PublicBookAsync("test-salon", new PublicBookingInput(
            _db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, "13:00", "عبدالرحمن", "0501112233", null));

        var reference = Ref.Str(result, "reference");
        reference.Length.ShouldBe(6);
        var appt = _db.Db.Appointments.Single();
        appt.BarberId.ShouldBe(_db.BarberId);
        appt.Source.ShouldBe(AppointmentSource.Online);

        // the only barber is now busy at 13:00
        var taken = await Should.ThrowAsync<ApiException>(() => _svc.PublicBookAsync("test-salon", new PublicBookingInput(
            _db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, "13:00", "آخر", "0507778899", null)));
        taken.Code.ShouldBe("slot_taken");

        var wrongPhone = await Should.ThrowAsync<ApiException>(() => _svc.PublicCancelAsync("test-salon", reference, "0500000000"));
        wrongPhone.Status.ShouldBe(404);

        await _svc.PublicCancelAsync("test-salon", reference.ToLowerInvariant(), "966501112233");
        _db.Db.Appointments.Single().Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task PublicBooking_LimitsActiveBookingsPerPhone()
    {
        EnableOnlineBooking();
        foreach (var time in new[] { "10:00", "11:00", "12:00" })
            await _svc.PublicBookAsync("test-salon", new PublicBookingInput(_db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, time, "سامي", "0501112233", null));

        var ex = await Should.ThrowAsync<ApiException>(() => _svc.PublicBookAsync("test-salon",
            new PublicBookingInput(_db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, "14:00", "سامي", "0501112233", null)));
        ex.Code.ShouldBe("too_many_bookings");
    }

    [Theory]
    [InlineData("0551234567", "0551234567")]
    [InlineData("551234567", "0551234567")]
    [InlineData("+966551234567", "0551234567")]
    [InlineData("00966 55 123 4567", "0551234567")]
    [InlineData("0112345678", null)]
    [InlineData("abc", null)]
    public void SaudiPhone_Normalization(string input, string? expected) => AppointmentService.NormalizePhone(input).ShouldBe(expected);
}

// ==================== Sessions ====================

public class RefreshTokenTests : IDisposable
{
    private readonly TestDb _db;
    private readonly AuthService _auth;

    public RefreshTokenTests()
    {
        _db = new TestDb();
        var owner = _db.Db.Profiles.Single(p => p.Id == _db.OwnerProfile);
        owner.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Secret#123");
        _db.Db.SaveChanges();
        _auth = new AuthService(_db.Db, new JwtSettings { Key = new string('k', 48) });
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Refresh_Rotates_AndReuseRevokesTheWholeFamily()
    {
        var email = _db.Db.Profiles.Single(p => p.Id == _db.OwnerProfile).Email;
        var (ok, _, login) = await _auth.LoginAsync(email, "Secret#123", default);
        ok.ShouldBeTrue();
        var first = Ref.Str(login, "refreshToken");

        var (ok2, _, refreshed) = await _auth.RefreshAsync(first, default);
        ok2.ShouldBeTrue();
        var second = Ref.Str(refreshed, "refreshToken");
        second.ShouldNotBe(first);
        Ref.Str(refreshed, "token").ShouldNotBeNullOrEmpty();

        // replaying the rotated token = theft signal → every session of the profile is revoked
        var (replayOk, _, _) = await _auth.RefreshAsync(first, default);
        replayOk.ShouldBeFalse();
        var (secondOk, _, _) = await _auth.RefreshAsync(second, default);
        secondOk.ShouldBeFalse();
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var email = _db.Db.Profiles.Single(p => p.Id == _db.OwnerProfile).Email;
        var (_, _, login) = await _auth.LoginAsync(email, "Secret#123", default);
        var token = Ref.Str(login, "refreshToken");
        await _auth.LogoutAsync(token, default);
        (await _auth.RefreshAsync(token, default)).ok.ShouldBeFalse();
    }
}
