namespace Hallagi.UnitTests;

/// <summary>
/// Report figures are checked against hand-computed values. The clock is frozen at
/// 2026-10-27 12:00 Riyadh (09:00 UTC); Riyadh is UTC+3 with no daylight saving.
/// </summary>
public class ReportCorrectnessTests : IDisposable
{
    private readonly TestDb _db;
    private readonly SalesService _sales;
    private readonly ReportsService _reports;

    private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateOnly Yesterday = new(2026, 10, 26);

    public ReportCorrectnessTests()
    {
        _db = new TestDb();
        _db.Time.Set(Now);
        _sales = new SalesService(_db.Db, _db.Clock);
        _reports = new ReportsService(_db.Db, _db.Clock);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Records a sale through the real engine, then pins its timestamp.</summary>
    private async Task<Guid> SellAt(DateTime utc, Guid serviceId, int qty = 1, PaymentMethod method = PaymentMethod.Card,
        Guid? customerId = null, Guid? productId = null)
    {
        var items = new List<CreateSaleItem> { productId is null ? new(serviceId, qty) : new(null, qty, productId) };
        var result = await _sales.CreateAsync(_db.AsOwner(), new CreateSaleRequest(_db.BranchA, _db.BarberId, customerId, method, items, Guid.NewGuid().ToString()));
        var id = Ref.Id(Ref.Val(result, "sale"), "id");
        var sale = await _db.Db.Sales.FirstAsync(s => s.Id == id);
        sale.CreatedAt = utc;
        await _db.Db.SaveChangesAsync();
        _db.Db.ChangeTracker.Clear();
        return id;
    }

    /// <summary>A Riyadh wall-clock time in October 2026, as the UTC instant stored in the database.</summary>
    private static DateTime Riyadh(int day, int hour, int minute = 0)
        => DateTime.SpecifyKind(new DateTime(2026, 10, day, hour, minute, 0).AddHours(-3), DateTimeKind.Utc);

    private static object? Day(object report, DateOnly day)
        => ((System.Collections.IEnumerable)Ref.Val(report, "daily")!).Cast<object>().Single(d => (DateOnly)Ref.Val(d, "day")! == day);

    [Fact]
    public async Task SaleAfterLocalMidnight_BelongsToTheNewLocalDay()
    {
        // 00:30 Riyadh on the 27th is still the 26th in UTC (21:30Z)
        await SellAt(Riyadh(27, 0, 30), _db.SaleServiceId);
        // 23:50 Riyadh on the 26th
        await SellAt(Riyadh(26, 23, 50), _db.OtherServiceId);

        var report = await _reports.SalesReportAsync(_db.AsOwner(), null, Yesterday.ToDateTime(TimeOnly.MinValue), Today.ToDateTime(TimeOnly.MinValue));

        Ref.Dec(Day(report, Today), "collected").ShouldBe(25m);
        Ref.Dec(Day(report, Yesterday), "collected").ShouldBe(50m);
    }

    [Fact]
    public async Task RangeEnd_IsInclusive_ForReportsAndSalesList()
    {
        await SellAt(Riyadh(26, 23, 50), _db.SaleServiceId);

        var report = await _reports.SalesReportAsync(_db.AsOwner(), null, Yesterday.ToDateTime(TimeOnly.MinValue), Yesterday.ToDateTime(TimeOnly.MinValue));
        Ref.Int(Ref.Val(report, "summary"), "salesCount").ShouldBe(1);

        // the screen sends plain dates: from=to=2026-10-26 must include the whole day
        var list = await _sales.ListAsync(_db.AsOwner(), null, Yesterday.ToDateTime(TimeOnly.MinValue), Yesterday.ToDateTime(TimeOnly.MinValue), null, 1, 20);
        Ref.Int(list, "totalCount").ShouldBe(1);
    }

    [Fact]
    public async Task DailySeries_IsZeroFilled()
    {
        await SellAt(Riyadh(27, 10), _db.SaleServiceId);
        var report = await _reports.SalesReportAsync(_db.AsOwner(), null, null, null);   // default: last 7 days

        var days = ((System.Collections.IEnumerable)Ref.Val(report, "daily")!).Cast<object>().ToList();
        days.Count.ShouldBe(7);
        days.Sum(d => Ref.Dec(d, "collected")).ShouldBe(25m);
        days.Count(d => Ref.Dec(d, "collected") == 0).ShouldBe(6);
    }

    [Fact]
    public async Task OperatingResult_ExcludesVat_ProductCost_AndExpenses()
    {
        var settings = _db.Db.OrganizationSettings.Single();
        settings.VatEnabled = true;
        settings.VatNumber = "310123456700003";
        var productId = Guid.NewGuid();
        _db.Db.Products.Add(new Product { Id = productId, OrganizationId = _db.OrgId, Name = "زيت", Price = 46m, Cost = 20m, CreatedAt = Now, UpdatedAt = Now });
        _db.Db.ProductStocks.Add(new ProductStock { Id = Guid.NewGuid(), ProductId = productId, BranchId = _db.BranchA, Quantity = 10 });
        _db.Db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(), OrganizationId = _db.OrgId, BranchId = _db.BranchA, Category = ExpenseCategory.Supplies,
            Amount = 10m, ExpenseDate = Today, Source = ExpenseSource.Bank, CreatedBy = _db.OwnerProfile, CreatedAt = Now
        });
        _db.Db.SaveChanges();

        await SellAt(Riyadh(27, 10), _db.OtherServiceId);           // 50 incl. VAT → net 43.48, VAT 6.52
        await SellAt(Riyadh(27, 11), _db.SaleServiceId, productId: productId); // 46 incl. VAT → net 40.00, VAT 6.00, cost 20

        // a later cost change must not rewrite history
        _db.Db.Products.Single().Cost = 35m;
        _db.Db.SaveChanges();

        var summary = Ref.Val(await _reports.SalesReportAsync(_db.AsOwner(), null, Today.ToDateTime(TimeOnly.MinValue), Today.ToDateTime(TimeOnly.MinValue)), "summary")!;
        Ref.Dec(summary, "collected").ShouldBe(96m);
        Ref.Dec(summary, "vat").ShouldBe(12.52m);
        Ref.Dec(summary, "netSales").ShouldBe(83.48m);
        Ref.Dec(summary, "productCost").ShouldBe(20m);
        Ref.Dec(summary, "expenses").ShouldBe(10m);
        Ref.Dec(summary, "operatingResult").ShouldBe(53.48m);
        Ref.Dec(summary, "averageTicket").ShouldBe(48m);
    }

    [Fact]
    public async Task CancelledSales_AreExcludedEverywhere()
    {
        var id = await SellAt(Riyadh(27, 10), _db.SaleServiceId);
        await SellAt(Riyadh(27, 11), _db.OtherServiceId);
        await _sales.CancelAsync(_db.AsOwner(), id, "خطأ");

        var summary = Ref.Val(await _reports.SalesReportAsync(_db.AsOwner(), null, null, null), "summary")!;
        Ref.Int(summary, "salesCount").ShouldBe(1);
        Ref.Dec(summary, "collected").ShouldBe(50m);
    }

    [Fact]
    public async Task PaymentSplit_AddsUpToCollected()
    {
        await SellAt(Riyadh(27, 9), _db.SaleServiceId, method: PaymentMethod.Cash);
        await SellAt(Riyadh(27, 10), _db.OtherServiceId, method: PaymentMethod.Card);
        var s = Ref.Val(await _reports.SalesReportAsync(_db.AsOwner(), null, null, null), "summary")!;
        (Ref.Dec(s, "cashCollected") + Ref.Dec(s, "cardCollected")).ShouldBe(Ref.Dec(s, "collected"));
        Ref.Dec(s, "cashCollected").ShouldBe(25m);
    }

    [Fact]
    public async Task BarberDues_ShowBranches_AndCommissionOnServiceNetOnly()
    {
        _db.Db.BarberCompensations.Add(new BarberCompensation
        {
            Id = Guid.NewGuid(), BarberId = _db.BarberId, Type = CompensationType.Commission, CommissionRate = 0.4m,
            EffectiveFrom = new DateOnly(2026, 1, 1), CreatedAt = Now
        });
        _db.Db.SaveChanges();
        await SellAt(Riyadh(27, 10), _db.OtherServiceId);   // 50 → commission 20

        var dues = await _reports.BarberDuesAsync(_db.AsOwner(), _db.BranchA, null, null);
        var row = ((System.Collections.IEnumerable)Ref.Val(dues, "items")!).Cast<object>().Single();
        ((IEnumerable<string>)Ref.Val(row, "branchNames")!).ShouldContain("فرع النرجس");
        Ref.Dec(row, "eligibleSales").ShouldBe(50m);
        Ref.Dec(row, "estimatedCommission").ShouldBe(20m);

        // the other branch has no barbers assigned and no sales
        var other = await _reports.BarberDuesAsync(_db.AsOwner(), _db.BranchB, null, null);
        ((System.Collections.IEnumerable)Ref.Val(other, "items")!).Cast<object>().ShouldBeEmpty();
    }

    [Fact]
    public async Task CustomersReport_IncludesLastDay_AndSplitsNewFromReturning()
    {
        var regular = _db.Db.Customers.Single().Id;           // seeded customer
        await SellAt(new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), _db.SaleServiceId, customerId: regular); // before range
        await SellAt(Riyadh(27, 10), _db.SaleServiceId, customerId: regular);

        var newcomer = Guid.NewGuid();
        _db.Db.Customers.Add(new Customer { Id = newcomer, OrganizationId = _db.OrgId, FullName = "جديد", CreatedAt = Now, UpdatedAt = Now });
        _db.Db.SaveChanges();
        await SellAt(Riyadh(27, 11), _db.SaleServiceId, customerId: newcomer);
        await SellAt(Riyadh(27, 11, 30), _db.SaleServiceId, customerId: newcomer);
        await SellAt(Riyadh(27, 12), _db.SaleServiceId); // walk-in

        var report = await _reports.CustomersAsync(_db.AsOwner(), null, Today.ToDateTime(TimeOnly.MinValue), Today.ToDateTime(TimeOnly.MinValue));
        var s = Ref.Val(report, "summary")!;
        Ref.Int(s, "uniqueCustomers").ShouldBe(2);
        Ref.Int(s, "newCustomers").ShouldBe(1);
        Ref.Int(s, "returningCustomers").ShouldBe(1);
        Ref.Int(s, "repeatInPeriod").ShouldBe(1);
        Ref.Int(s, "walkInVisits").ShouldBe(1);
    }

    [Fact]
    public async Task Cashier_CannotReadManagementReports()
    {
        var ex = await Should.ThrowAsync<AppException>(() => _reports.SalesReportAsync(_db.AsCashier(), null, null, null));
        ex.Status.ShouldBe(403);
    }

    [Fact]
    public async Task Csv_UsesLocalTime_AndNeutralizesFormulas()
    {
        await SellAt(Riyadh(27, 0, 30), _db.SaleServiceId);
        _db.Db.Barbers.Single().FullName = "=HYPERLINK(\"x\")";
        _db.Db.SaveChanges();

        var csv = await _reports.SalesCsvAsync(_db.AsOwner(), null, Today.ToDateTime(TimeOnly.MinValue), Today.ToDateTime(TimeOnly.MinValue), default);
        csv.ShouldContain("2026-10-27 00:30");
        csv.ShouldContain("\"'=HYPERLINK");
    }

    [Fact]
    public async Task Dashboard_Today_ComparesWithYesterdayAtTheSameTime()
    {
        var dashboard = new DashboardService(_db.Db, new ShiftService(_db.Db), _reports, _db.Clock);
        await SellAt(Riyadh(27, 10), _db.OtherServiceId);     // today before noon: 50
        await SellAt(Riyadh(26, 10), _db.SaleServiceId);      // yesterday before noon: 25
        await SellAt(Riyadh(26, 20), _db.OtherServiceId);     // yesterday evening: not comparable yet

        var d = await dashboard.GetAsync(_db.AsOwner(), _db.BranchA, "today");
        Ref.Dec(Ref.Val(d, "metrics"), "totalSales").ShouldBe(50m);
        Ref.Dec(Ref.Val(d, "comparison"), "previousNetSales").ShouldBe(25m);
        Ref.Dec(Ref.Val(d, "comparison"), "changePercent").ShouldBe(100m);

        var chart = ((System.Collections.IEnumerable)Ref.Val(Ref.Val(d, "chart"), "items")!).Cast<object>().ToList();
        chart.Count.ShouldBe(7);
        Ref.Bool(chart.Last(), "isToday").ShouldBeTrue();
        Ref.Dec(chart[^2], "total").ShouldBe(75m);
    }
}
