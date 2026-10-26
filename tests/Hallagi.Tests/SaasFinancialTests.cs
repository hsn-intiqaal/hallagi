using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Hallagi.Tests;

/// <summary>
/// Multi-tenant isolation tests: two independent organizations must never see each other.
/// </summary>
public class TenantIsolationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _branchA = Guid.NewGuid();
    private readonly Guid _branchB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _memberA = Guid.NewGuid();
    private readonly Guid _serviceA = Guid.NewGuid();
    private readonly Guid _barberA = Guid.NewGuid();
    private readonly Guid _customerA = Guid.NewGuid();

    public TenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"DataSource=file:iso_{Guid.NewGuid():N}?mode=memory&cache=shared").Options;
        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var now = DateTime.UtcNow;

        void AddOrg(Guid orgId, string name, Guid branchId, Guid userId, Guid memberId, Guid serviceId, Guid barberId, Guid customerId)
        {
            _db.Organizations.Add(new Organization { Id = orgId, Name = name, CreatedAt = now, UpdatedAt = now });
            _db.OrganizationSettings.Add(new OrganizationSettings { OrganizationId = orgId });
            _db.Branches.Add(new Branch { Id = branchId, OrganizationId = orgId, Name = $"فرع {name}", City = "الرياض", CreatedAt = now, UpdatedAt = now });
            _db.Services.Add(new Service { Id = serviceId, OrganizationId = orgId, Name = "حلاقة شعر", BasePrice = 25m, CreatedAt = now, UpdatedAt = now });
            _db.Barbers.Add(new Barber { Id = barberId, OrganizationId = orgId, FullName = $"حلاق {name}", CreatedAt = now, UpdatedAt = now });
            _db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barberId, BranchId = branchId });
            _db.Customers.Add(new Customer { Id = customerId, OrganizationId = orgId, FullName = $"عميل {name}", CreatedAt = now, UpdatedAt = now });
            _db.Profiles.Add(new Profile { Id = userId, FullName = name, Email = $"{name}@test.sa", PasswordHash = "x", CreatedAt = now, UpdatedAt = now });
            _db.OrganizationMembers.Add(new OrganizationMember
            {
                Id = memberId,
                OrganizationId = orgId,
                ProfileId = userId,
                Role = UserRole.Owner,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        AddOrg(_orgA, "منشأة أ", _branchA, _userA, _memberA, _serviceA, _barberA, _customerA);
        AddOrg(_orgB, "منشأة ب", _branchB, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _db.SaveChanges();

        // a sale exists in tenant A
        _db.Sales.Add(new Sale
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgA,
            BranchId = _branchA,
            BarberId = _barberA,
            CustomerId = _customerA,
            CreatedBy = _userA,
            ReceiptNo = 1001,
            PaymentMethod = PaymentMethod.Cash,
            TotalAmount = 25m,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();
    }

    private CurrentUser OwnerOf(Guid org, Guid member, Guid user)
        => new(user, org, member, UserRole.Owner, true, new[] { org == _orgA ? _branchA : _branchB }, null);

    [Fact]
    public async Task Tenant_A_Cannot_Read_Tenant_B_Sales()
    {
        var sales = newSalesServiceIsolated();
        var aResult = await sales.ListAsync(OwnerOf(_orgA, _memberA, _userA), null, null, null, null, 1, 20);
        var aItems = ((System.Collections.IEnumerable)Ref.Val(aResult, "items")!).Cast<object?>().ToList();
        aItems.Count.ShouldBe(1); // tenant A sees only its own sale

        var bResult = await sales.ListAsync(OwnerOf(_orgB, Guid.Empty, Guid.Empty), null, null, null, null, 1, 20);
        var bItems = ((System.Collections.IEnumerable)Ref.Val(bResult, "items")!).Cast<object?>().ToList();
        bItems.Count.ShouldBe(0); // tenant B has no sales of its own
        Ref.Id(aItems[0]!, "Id").ShouldNotBe(default); // and tenant A's sale stays with tenant A
    }

    private SalesService newSalesServiceIsolated() => new(_db);

    [Fact]
    public async Task Tenant_A_Cannot_Sell_Using_Tenant_B_Service()
    {
        var sales = new SalesService(_db);
        var a = OwnerOf(_orgA, _memberA, _userA);
        var bService = await _db.Services.FirstAsync(s => s.OrganizationId == _orgB);

        var ex = await Assert.ThrowsAsync<ApiException>(() => sales.CreateAsync(a, new CreateSaleRequest(
            _branchA, _barberA, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(bService.Id, 1) }, "cross-tenant")));
        ex.Code.ShouldBe("invalid_service"); // prices never come from another tenant
    }

    [Fact]
    public async Task Tenant_A_Cannot_Read_Another_Tenant_Customer()
    {
        var catalog = new CatalogService(_db);
        var bCustomer = await _db.Customers.FirstAsync(c => c.OrganizationId == _orgB);
        var ex = await Assert.ThrowsAsync<ApiException>(() => catalog.GetCustomerAsync(OwnerOf(_orgA, _memberA, _userA), bCustomer.Id));
        ex.Status.ShouldBe(404);
    }

    public void Dispose() => _db.Dispose();
}

// ==================== Settlement calculations per agreement type ====================

public class AgreementSettlementSeed : IDisposable
{
    public AppDbContext Db { get; }
    public Guid OrgId { get; }
    public Guid BranchId { get; }
    public Guid OwnerMember { get; }
    public Guid OwnerProfile { get; }

    public AgreementSettlementSeed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"DataSource=file:agr_{Guid.NewGuid():N}?mode=memory&cache=shared").Options;
        Db = new AppDbContext(options);
        Db.Database.OpenConnection();
        Db.Database.EnsureCreated();

        var now = DateTime.UtcNow;
        OrgId = Guid.NewGuid();
        BranchId = Guid.NewGuid();
        OwnerProfile = Guid.NewGuid();
        OwnerMember = Guid.NewGuid();

        Db.Organizations.Add(new Organization { Id = OrgId, Name = "منشأة التعويضات", CreatedAt = now, UpdatedAt = now });
        Db.Branches.Add(new Branch { Id = BranchId, OrganizationId = OrgId, Name = "الفرع", City = "الرياض", CreatedAt = now, UpdatedAt = now });
        Db.Profiles.Add(new Profile { Id = OwnerProfile, FullName = "مالك", Email = "o@x.sa", PasswordHash = "x", CreatedAt = now, UpdatedAt = now });
        Db.OrganizationMembers.Add(new OrganizationMember { Id = OwnerMember, OrganizationId = OrgId, ProfileId = OwnerProfile, Role = UserRole.Owner, CreatedAt = now, UpdatedAt = now });
        Db.Services.Add(new Service { Id = Guid.NewGuid(), OrganizationId = OrgId, Name = "حلاقة شعر", BasePrice = 100m, CreatedAt = now, UpdatedAt = now });
        Db.SaveChanges();
    }

    public Guid AddBarberWithAgreement(string name, Action<BarberCompensation> setup, decimal salesTotal)
    {
        var now = DateTime.UtcNow;
        var barberId = Guid.NewGuid();
        Db.Barbers.Add(new Barber { Id = barberId, OrganizationId = OrgId, FullName = name, CreatedAt = now, UpdatedAt = now });
        Db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barberId, BranchId = BranchId });
        var comp = new BarberCompensation
        {
            Id = Guid.NewGuid(),
            BarberId = barberId,
            Type = CompensationType.Commission,
            CommissionRate = 0m,
            EffectiveFrom = new DateOnly(2024, 1, 1),
            Status = EntityStatus.Active,
            CreatedAt = now
        };
        setup(comp);
        Db.BarberCompensations.Add(comp);

        if (salesTotal > 0)
        {
            Db.Sales.Add(new Sale
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrgId,
                BranchId = BranchId,
                BarberId = barberId,
                CreatedBy = OwnerProfile,
                ReceiptNo = 1000 + Db.Sales.Count() + 1,
                PaymentMethod = PaymentMethod.Cash,
                TotalAmount = salesTotal, GrossAmount = salesTotal, SubtotalAmount = salesTotal, CommissionBase = salesTotal,
                CreatedAt = DateTime.Today.AddDays(-2),
                UpdatedAt = now
            });
        }
        Db.SaveChanges();
        return barberId;
    }

    public CurrentUser Owner() => new(OwnerProfile, OrgId, OwnerMember, UserRole.Owner, true, new[] { BranchId }, null);

    public async Task<Guid> PeriodAsync(SettlementService svc)
        => await svc.CreatePeriodAsync(Owner(), "فترة اختبار",
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today), default);

    public void Dispose() => Db.Dispose();
}

public class SettlementCalculationTests
{
    [Fact]
    public async Task FixedSalary_Ignores_Sales()
    {
        using var seed = new AgreementSettlementSeed();
        var barberId = seed.AddBarberWithAgreement("راتب", c =>
        {
            c.Type = CompensationType.FixedSalary;
            c.MonthlySalary = 4000m;
        }, salesTotal: 1000m);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;

        Ref.Dec(s, "salaryEarned").ShouldBe(4000m);
        Ref.Dec(s, "commissionEarned").ShouldBe(0m);
        Ref.Dec(s, "netPayable").ShouldBe(4000m);
    }

    [Fact]
    public async Task Commission_Uses_EligibleSales_AndRate()
    {
        using var seed = new AgreementSettlementSeed();
        seed.AddBarberWithAgreement("عمولة", c =>
        {
            c.Type = CompensationType.Commission;
            c.CommissionRate = 0.30m;
        }, salesTotal: 1000m);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;

        Ref.Dec(s, "commissionEarned").ShouldBe(300m);
        Ref.Dec(s, "netPayable").ShouldBe(300m);
    }

    [Fact]
    public async Task SalaryPlusCommission_Adds_Both()
    {
        using var seed = new AgreementSettlementSeed();
        seed.AddBarberWithAgreement("راتب وعمولة", c =>
        {
            c.Type = CompensationType.FixedPlusCommission;
            c.MonthlySalary = 2000m;
            c.CommissionRate = 0.15m;
        }, salesTotal: 1000m);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;

        Ref.Dec(s, "salaryEarned").ShouldBe(2000m);
        Ref.Dec(s, "commissionEarned").ShouldBe(150m);
        Ref.Dec(s, "netPayable").ShouldBe(2150m);
    }

    [Fact]
    public async Task ChairRental_Is_Debt_Not_Salary()
    {
        using var seed = new AgreementSettlementSeed();
        seed.AddBarberWithAgreement("كرسي", c =>
        {
            c.Type = CompensationType.ChairRental;
            c.ChairRentAmount = 1200m;
        }, salesTotal: 1000m);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;

        Ref.Dec(s, "chairRent").ShouldBe(1200m);
        Ref.Dec(s, "netPayable").ShouldBe(-1200m); // barber owes the shop
        Ref.Dec(s, "salaryEarned").ShouldBe(0m);
    }

    [Fact]
    public async Task Advance_Reduces_Net_And_Is_Tracked()
    {
        using var seed = new AgreementSettlementSeed();
        var barberId = seed.AddBarberWithAgreement("راتب", c =>
        {
            c.Type = CompensationType.FixedSalary;
            c.MonthlySalary = 3000m;
        }, 0m);

        var ledger = new LedgerService(seed.Db);
        await ledger.CreateAdvanceAsync(seed.Owner(), new CreateAdvanceRequest(barberId, seed.BranchId, 800m,
            DateOnly.FromDateTime(DateTime.Today), "سلفة"), default);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;

        Ref.Dec(s, "advancesRepaid").ShouldBe(800m);
        Ref.Dec(s, "netPayable").ShouldBe(2200m); // 3000 - 800
    }

    [Fact]
    public async Task Approved_Settlement_Is_Locked_And_Not_Recalculated()
    {
        using var seed = new AgreementSettlementSeed();
        seed.AddBarberWithAgreement("عمولة", c =>
        {
            c.Type = CompensationType.Commission;
            c.CommissionRate = 0.30m;
        }, salesTotal: 1000m);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;
        var settlementId = Ref.Id(s, "Id");

        await svc.ApproveAsync(seed.Owner(), settlementId, default);

        // Change the agreement AFTER approval
        var comp = await seed.Db.BarberCompensations.FirstAsync();
        comp.CommissionRate = 0.90m;
        await seed.Db.SaveChangesAsync();

        var preview2 = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s2 = ((System.Collections.IEnumerable)Ref.Val(preview2, "settlements")!).Cast<object?>().First()!;

        Ref.Dec(s2, "netPayable").ShouldBe(300m);      // historical result preserved
        Ref.Str(s2, "statusText").ShouldBe("معتمدة");
    }

    [Fact]
    public async Task Payment_Reduces_Remaining_And_Refuses_Overpayment()
    {
        using var seed = new AgreementSettlementSeed();
        seed.AddBarberWithAgreement("راتب", c =>
        {
            c.Type = CompensationType.FixedSalary;
            c.MonthlySalary = 1000m;
        }, 0m);

        var svc = new SettlementService(seed.Db);
        var periodId = await seed.PeriodAsync(svc);
        var preview = await svc.PreviewAsync(seed.Owner(), periodId, null, default);
        var s = ((System.Collections.IEnumerable)Ref.Val(preview, "settlements")!).Cast<object?>().First()!;
        var settlementId = Ref.Id(s, "Id");

        await svc.ApproveAsync(seed.Owner(), settlementId, default);
        await svc.PayAsync(seed.Owner(), settlementId, 400m, "Cash", DateOnly.FromDateTime(DateTime.Today), null, default);
        await svc.PayAsync(seed.Owner(), settlementId, 600m, "Cash", DateOnly.FromDateTime(DateTime.Today), null, default);

        var detail = await svc.GetAsync(seed.Owner(), settlementId, default);
        Ref.Dec(detail, "alreadyPaid").ShouldBe(1000m);
        Ref.Dec(detail, "remaining").ShouldBe(0m);
        Ref.Str(detail, "statusText").ShouldBe("مدفوعة");

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            svc.PayAsync(seed.Owner(), settlementId, 10m, null, null, null, default));
        ex.Code.ShouldBe("too_much"); // fully paid settlements refuse further payments
    }
}

// ==================== Ledger: advances, handovers, reversals ====================

public class LedgerTests : IDisposable
{
    private readonly AgreementSettlementSeed _seed;
    public LedgerTests() => _seed = new AgreementSettlementSeed();
    public void Dispose() => _seed.Dispose();

    [Fact]
    public async Task Advance_Repayment_Partial_Balance()
    {
        var barberId = _seed.AddBarberWithAgreement("حلاق", c => c.Type = CompensationType.Commission, 0m);
        var ledger = new LedgerService(_seed.Db);
        var advanceId = await ledger.CreateAdvanceAsync(_seed.Owner(), new CreateAdvanceRequest(barberId, _seed.BranchId, 500m,
            DateOnly.FromDateTime(DateTime.Today), "سلفة"), default);

        await ledger.RepayAdvanceAsync(_seed.Owner(), advanceId, 200m, default);

        var list = await ledger.ListAdvancesAsync(_seed.Owner(), barberId, default);
        var item = ((System.Collections.IEnumerable)Ref.Val(list, "items")!).Cast<object?>().First()!;
        Ref.Dec(item, "OriginalAmount").ShouldBe(500m);
        Ref.Dec(item, "Outstanding").ShouldBe(300m);
        Ref.Str(item, "StatusText").ShouldBe("معتمدة");

        var tooMuch = await Assert.ThrowsAsync<ApiException>(() => ledger.RepayAdvanceAsync(_seed.Owner(), advanceId, 400m, default));
        tooMuch.Code.ShouldBe("too_much");
    }

    [Fact]
    public async Task Duplicate_Ledger_Entry_Is_Rejected()
    {
        var barberId = _seed.AddBarberWithAgreement("حلاق", c => c.Type = CompensationType.Commission, 0m);
        var ledger = new LedgerService(_seed.Db);
        await ledger.AddEntryAsync(_seed.Owner(), new LedgerEntryRequest(barberId, _seed.BranchId,
            BarberTxType.ReimbursementDue, BarberTxDirection.Credit, 100m,
            DateOnly.FromDateTime(DateTime.Today), "مسترد", "ref-1", null), default);

        var ex = await Assert.ThrowsAsync<ApiException>(() => ledger.AddEntryAsync(_seed.Owner(),
            new LedgerEntryRequest(barberId, _seed.BranchId,
                BarberTxType.ReimbursementDue, BarberTxDirection.Credit, 100m,
                DateOnly.FromDateTime(DateTime.Today), "مسترد", "ref-1", null), default));
        ex.Code.ShouldBe("duplicate_entry");
    }

    [Fact]
    public async Task Reversal_Creates_Offsetting_Entry()
    {
        var barberId = _seed.AddBarberWithAgreement("حلاق", c => c.Type = CompensationType.Commission, 0m);
        var ledger = new LedgerService(_seed.Db);
        var entryId = await ledger.AddEntryAsync(_seed.Owner(), new LedgerEntryRequest(barberId, _seed.BranchId,
            BarberTxType.Bonus, BarberTxDirection.Credit, 200m,
            DateOnly.FromDateTime(DateTime.Today), "مكافأة", "bonus-1", null), default);

        await ledger.ReverseAsync(_seed.Owner(), entryId, "قيد بالخطأ", default);

        var list = await ledger.ListLedgerAsync(_seed.Owner(), barberId, DateTime.Today.AddDays(-1), DateTime.Today, default);
        var entries = ((System.Collections.IEnumerable)Ref.Val(list, "entries")!).Cast<object?>().ToList();
        entries.Count.ShouldBe(2); // original (reversed, shown for audit) + correction entry
        Ref.Dec(list, "balance").ShouldBe(0m); // the reversal fully offsets the original
    }

    [Fact]
    public async Task Handover_Rejects_Same_User()
    {
        var ledger = new LedgerService(_seed.Db);
        var ex = await Assert.ThrowsAsync<ApiException>(() => ledger.CreateHandoverAsync(_seed.Owner(),
            new CreateHandoverRequest(_seed.BranchId, _seed.OwnerProfile, _seed.OwnerProfile, null, null, 100m,
                DateOnly.FromDateTime(DateTime.Today), null), default));
        ex.Code.ShouldBe("same_user");
    }
}

// ==================== Barber-operated POS ====================

public class BarberPosTests : IDisposable
{
    private readonly TestDb _db;
    public BarberPosTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Barber_Can_Sell_For_Himself()
    {
        var sales = new SalesService(_db.Db);
        var result = await sales.CreateAsync(_db.AsBarber(), new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "barber-self-sale"));

        Ref.Dec(Ref.Val(result, "sale")!, "totalAmount").ShouldBe(25m);
    }

    [Fact]
    public async Task Barber_Cannot_Sell_For_Another_Barber()
    {
        var sales = new SalesService(_db.Db);
        var otherBarberId = Guid.NewGuid();
        _db.Db.Barbers.Add(new Barber { Id = otherBarberId, OrganizationId = _db.OrgId, FullName = "حلاق آخر", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = otherBarberId, BranchId = _db.BranchA });
        _db.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ApiException>(() => sales.CreateAsync(_db.AsBarber(), new CreateSaleRequest(
            _db.BranchA, otherBarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "barber-other")));
        ex.Status.ShouldBe(403);
    }

    [Fact]
    public async Task Cashier_Sale_Keeps_Cashier_And_Barber_Distinct()
    {
        var sales = new SalesService(_db.Db);
        var result = await sales.CreateAsync(_db.AsCashier(), new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Card,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 2) }, "cashier-sale"));

        var sale = Ref.Val(result, "sale")!;
        Ref.Id(sale, "BarberId").ShouldBe(_db.BarberId);
        Ref.Dec(sale, "totalAmount").ShouldBe(50m);
    }

    [Fact]
    public async Task Barber_Cannot_Open_Another_Barber_Ledger()
    {
        var ledger = new LedgerService(_db.Db);
        var otherBarberId = Guid.NewGuid();
        _db.Db.Barbers.Add(new Barber { Id = otherBarberId, OrganizationId = _db.OrgId, FullName = "حلاق آخر", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ApiException>(() => ledger.ListLedgerAsync(_db.AsBarber(), otherBarberId, null, null, default));
        ex.Status.ShouldBe(403);
    }
}

// ==================== Plan limits ====================

public class PlanLimitTests : IDisposable
{
    private readonly AgreementSettlementSeed _seed;
    public PlanLimitTests() => _seed = new AgreementSettlementSeed();
    public void Dispose() => _seed.Dispose();

    [Fact]
    public async Task Branch_Limit_Is_Enforced()
    {
        var svc = new SubscriptionService(_seed.Db);
        await svc.EnsureSeededAsync(default);
        var basic = await _seed.Db.SubscriptionPlans.FirstAsync(p => p.Name == "أساسي");
        await svc.ChangePlanAsync(_seed.OrgId, basic.Id, _seed.Owner(), default);

        // the seed has 1 branch; the basic plan allows 3 -> no error yet
        await svc.EnsureBranchLimitAsync(_seed.OrgId, default);

        // add branches until the limit is exceeded
        for (int i = 0; i < 2; i++)
        {
            _seed.Db.Branches.Add(new Branch
            {
                Id = Guid.NewGuid(),
                OrganizationId = _seed.OrgId,
                Name = $"فرع {i}",
                City = "الرياض",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _seed.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.EnsureBranchLimitAsync(_seed.OrgId, default));
        ex.Status.ShouldBe(403);
    }
}
