namespace Hallagi.UnitTests;

public class CommerceSalesTests : IDisposable
{
    private readonly TestDb _db;
    private readonly SalesService _sales;

    public CommerceSalesTests()
    {
        _db = new TestDb();
        _sales = new SalesService(_db.Db, _db.Clock);
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
        var ex = await Should.ThrowAsync<AppException>(() => Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "vat-x"));
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
        var ex = await Should.ThrowAsync<AppException>(() => Sell(_db.AsCashier(), new() { new(null, 2, product) }, "prod-x"));
        ex.Code.ShouldBe("insufficient_stock");
        _db.Db.Sales.Count().ShouldBe(0);
        _db.Db.ProductStocks.Single().Quantity.ShouldBe(1);
    }

    [Fact]
    public async Task Item_MustBeEitherServiceOrProduct()
    {
        var ex = await Should.ThrowAsync<AppException>(() => Sell(_db.AsCashier(), new() { new(null, 1) }, "bad-item"));
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

        var ex = await Should.ThrowAsync<AppException>(() => Sell(_db.AsCashier(), new() { new(_db.OtherServiceId, 1) }, "cp-2", coupon: "SAVE50"));
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
        var ex = await Should.ThrowAsync<AppException>(() => Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "cp-old", coupon: "OLD"));
        ex.Code.ShouldBe("coupon_expired");
    }

    [Fact]
    public async Task ManualDiscount_RequiresPermission_AndIsCapped()
    {
        var cashier = _db.AsCashier();
        var forbidden = await Should.ThrowAsync<AppException>(() => Sell(cashier, new() { new(_db.SaleServiceId, 1) }, "md-1", manual: 5m));
        forbidden.Status.ShouldBe(403);

        var allowed = cashier with { PermissionOverrides = new Dictionary<string, bool> { [Permissions.SaleDiscount] = true } };
        var tooHigh = await Should.ThrowAsync<AppException>(() => Sell(allowed, new() { new(_db.SaleServiceId, 1) }, "md-2", manual: 10m));
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
        var ex = await Should.ThrowAsync<AppException>(() => Sell(_db.AsCashier(), new() { new(_db.SaleServiceId, 1) }, "lo-x", customer, points: 500));
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

        var ex = await Should.ThrowAsync<AppException>(() =>
            svc.AdjustStockAsync(owner, product, new StockAdjustInput(_db.BranchA, StockMovementType.Adjustment, -20, null, "جرد", null)));
        ex.Code.ShouldBe("negative_stock");
    }

    [Fact]
    public async Task Cashier_CannotManageProducts()
    {
        var svc = new ProductService(_db.Db);
        var ex = await Should.ThrowAsync<AppException>(() => svc.CreateAsync(_db.AsCashier(), new ProductInput("x", null, null, 10m, 5m, true)));
        ex.Status.ShouldBe(403);
    }
}
