namespace Hallagi.UnitTests;

// ==================== Sales: acceptance tests 1, 2, 8, 9, 10 ====================

public class SalesFinancialTests : IDisposable
{
    private readonly TestDb _db;
    public SalesFinancialTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    [Fact] // Test 1: cash sale increases drawer expected cash by the sale amount
    public async Task CashSale_IncreasesDrawerExpectedCash()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var shiftService = new ShiftService(_db.Db);
        var cashier = _db.AsCashier();

        await shiftService.OpenAsync(cashier, _db.BranchA, 200m);
        var state = await shiftService.GetStateAsync(cashier, _db.BranchA);
        Ref.Dec(Ref.Val(state, "current"), "expectedCash").ShouldBe(200m);

        var result = await sales.CreateAsync(cashier, new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "key-1"));

        var sale = Ref.Val(result, "sale")!;
        Ref.Dec(sale, "totalAmount").ShouldBe(25m);
        Ref.Int(sale, "receiptNo").ShouldBePositive();

        var after = await shiftService.GetStateAsync(cashier, _db.BranchA);
        Ref.Dec(Ref.Val(after, "current"), "expectedCash").ShouldBe(225m);
    }

    [Fact] // Test 2: card sale never touches drawer cash
    public async Task CardSale_DoesNotChangeDrawerCash()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var shiftService = new ShiftService(_db.Db);
        var cashier = _db.AsCashier();
        await shiftService.OpenAsync(cashier, _db.BranchA, 200m);

        await sales.CreateAsync(cashier, new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Card,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1), new(_db.OtherServiceId, 1) }, "key-card"));

        var state = await shiftService.GetStateAsync(cashier, _db.BranchA);
        var current = Ref.Val(state, "current")!;
        Ref.Dec(current, "expectedCash").ShouldBe(200m);
        Ref.Dec(current, "cardSales").ShouldBe(75m);
    }

    [Fact] // Test 10: same idempotency key replays without creating a duplicate sale
    public async Task Idempotency_PreventsDuplicateSales()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var cashier = _db.AsCashier();
        var req = new CreateSaleRequest(_db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "same-key");

        var first = await sales.CreateAsync(cashier, req);
        var second = await sales.CreateAsync(cashier, req);

        Ref.Id(Ref.Val(first, "sale")!, "Id").ShouldBe(Ref.Id(Ref.Val(second, "sale")!, "Id"));
        Ref.Bool(second, "replayed").ShouldBeTrue();
        _db.Db.Sales.Count().ShouldBe(1);
    }

    [Fact] // Test 9: sale stores the price at the time of sale
    public async Task Sale_SnapshotsUnitPrice()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var cashier = _db.AsCashier();
        var result = await sales.CreateAsync(cashier, new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 2) }, "snap"));

        var sale = Ref.Val(result, "sale")!;
        Ref.Dec(sale, "totalAmount").ShouldBe(50m);
        var item = ((System.Collections.IEnumerable)Ref.Val(sale, "items")!).Cast<object?>().First()!;
        Ref.Dec(item, "unitPrice").ShouldBe(25m);
    }

    [Fact] // server rejects a barber not assigned to the branch
    public async Task Sale_RejectsBarberFromOtherBranch()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var owner = _db.AsOwner();
        var otherBarberId = Guid.NewGuid();
        _db.Db.Barbers.Add(new Barber { Id = otherBarberId, OrganizationId = _db.OrgId, FullName = "حلاق فرع آخر", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = otherBarberId, BranchId = _db.BranchB });
        _db.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AppException>(() => sales.CreateAsync(owner, new CreateSaleRequest(
            _db.BranchA, otherBarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "bad-barber")));
        ex.Code.ShouldBe("barber_not_in_branch");
    }

    [Fact] // Test 8: cashier cannot sell in a branch he is not assigned to
    public async Task Cashier_CannotSellInOtherBranch()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var cashier = _db.AsCashier(); // only branch A
        var ex = await Assert.ThrowsAsync<AppException>(() => sales.CreateAsync(cashier, new CreateSaleRequest(
            _db.BranchB, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "cross-branch")));
        ex.Status.ShouldBe(403);
    }

    [Fact] // total is always computed by the server
    public async Task Total_ComputedServerSide()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var owner = _db.AsOwner();
        var result = await sales.CreateAsync(owner, new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1), new(_db.SaleServiceId, 3) }, "multi-qty"));

        Ref.Dec(Ref.Val(result, "sale")!, "totalAmount").ShouldBe(100m); // 25*1 + 25*3
    }

    [Fact] // barber-operated POS: a barber records sales for himself (never for others)
    public async Task Barber_Can_Create_Own_Sale()
    {
        var sales = new SalesService(_db.Db, _db.Clock);
        var result = await sales.CreateAsync(_db.AsBarber(), new CreateSaleRequest(
            _db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 1) }, "barber-own-sale"));
        Ref.Dec(Ref.Val(result, "sale")!, "totalAmount").ShouldBe(25m);
    }
}

// ==================== Shift / drawer math: tests 4, 5, 6 ====================

public class ShiftTests : IDisposable
{
    private readonly TestDb _db;
    public ShiftTests() => _db = new TestDb();
    public void Dispose() => _db.Dispose();

    [Fact] // Test 6: expected = opening + cash sales - cash expenses; difference math
    public async Task CloseShift_ComputesExpectedAndDifference()
    {
        var owner = _db.AsOwner();
        var shifts = new ShiftService(_db.Db);
        await shifts.OpenAsync(owner, _db.BranchA, 200m);

        var sales = new SalesService(_db.Db, _db.Clock);
        await sales.CreateAsync(owner, new CreateSaleRequest(_db.BranchA, _db.BarberId, null, PaymentMethod.Cash,
            new List<CreateSaleItem> { new(_db.SaleServiceId, 3) }, "cash-3"));                // +75
        await sales.CreateAsync(owner, new CreateSaleRequest(_db.BranchA, _db.BarberId, null, PaymentMethod.Card,
            new List<CreateSaleItem> { new(_db.OtherServiceId, 1) }, "card-1"));            // card, ignored by drawer

        var expenses = new ExpenseService(_db.Db, _db.Clock);
        await expenses.CreateAsync(owner, new CreateExpenseRequest(_db.BranchA, ExpenseCategory.Supplies, 50m,
            DateOnly.FromDateTime(DateTime.Today), "مستلزمات", ExpenseSource.CashDrawer));   // -50

        // Test 5: bank expense must not change drawer cash
        await expenses.CreateAsync(owner, new CreateExpenseRequest(_db.BranchA, ExpenseCategory.Maintenance, 50m,
            DateOnly.FromDateTime(DateTime.Today), "صيانة من البنك", ExpenseSource.Bank));

        var closed = await shifts.CloseAsync(owner, _db.BranchA, 215m, null);
        Ref.Dec(closed, "expectedCash").ShouldBe(225m);   // 200 + 75 - 50
        Ref.Dec(closed, "countedCash").ShouldBe(215m);
        Ref.Dec(closed, "difference").ShouldBe(-10m);
        Ref.Str(closed, "differenceText").ShouldBe("يوجد عجز نقدي");
    }

    [Fact] // zero difference must read as a match
    public async Task CloseShift_ExactMatch()
    {
        var owner = _db.AsOwner();
        var shifts = new ShiftService(_db.Db);
        await shifts.OpenAsync(owner, _db.BranchA, 100m);
        var closed = await shifts.CloseAsync(owner, _db.BranchA, 100m, null);
        Ref.Dec(closed, "difference").ShouldBe(0m);
        Ref.Str(closed, "differenceText").ShouldBe("النقدية مطابقة");
    }

    [Fact] // a second open shift on the same drawer is rejected
    public async Task CannotOpenTwoShifts_OnSameDrawer()
    {
        var owner = _db.AsOwner();
        var shifts = new ShiftService(_db.Db);
        await shifts.OpenAsync(owner, _db.BranchA, 100m);
        var ex = await Assert.ThrowsAsync<AppException>(() => shifts.OpenAsync(owner, _db.BranchA, 100m));
        ex.Code.ShouldBe("shift_open");
    }

    [Fact] // closed shifts are immutable: closing again is refused
    public async Task CannotCloseClosedShift()
    {
        var owner = _db.AsOwner();
        var shifts = new ShiftService(_db.Db);
        await shifts.OpenAsync(owner, _db.BranchA, 100m);
        await shifts.CloseAsync(owner, _db.BranchA, 100m, null);
        var ex = await Assert.ThrowsAsync<AppException>(() => shifts.CloseAsync(owner, _db.BranchA, 110m, null));
        ex.Code.ShouldBe("no_open_shift");
    }

    [Fact] // voiding a cash-drawer expense restores drawer cash via a documented correction
    public async Task VoidCashExpense_ReversesDrawerMovement()
    {
        var owner = _db.AsOwner();
        var shifts = new ShiftService(_db.Db);
        await shifts.OpenAsync(owner, _db.BranchA, 200m);

        var expenses = new ExpenseService(_db.Db, _db.Clock);
        var id = await expenses.CreateAsync(owner, new CreateExpenseRequest(_db.BranchA, ExpenseCategory.Supplies, 50m,
            DateOnly.FromDateTime(DateTime.Today), "اختبار", ExpenseSource.CashDrawer));

        var afterExpense = await shifts.GetStateAsync(owner, _db.BranchA);
        Ref.Dec(Ref.Val(afterExpense, "current"), "expectedCash").ShouldBe(150m);

        await expenses.VoidAsync(owner, id, "سُجل بالخطأ");

        var afterVoid = await shifts.GetStateAsync(owner, _db.BranchA);
        Ref.Dec(Ref.Val(afterVoid, "current"), "expectedCash").ShouldBe(200m);
    }

    [Fact] // void without a reason is refused (documented corrections only)
    public async Task VoidExpense_RequiresReason()
    {
        var owner = _db.AsOwner();
        var expenses = new ExpenseService(_db.Db, _db.Clock);
        var id = await expenses.CreateAsync(owner, new CreateExpenseRequest(_db.BranchA, ExpenseCategory.Supplies, 50m,
            DateOnly.FromDateTime(DateTime.Today), "اختبار", ExpenseSource.Bank));
        var ex = await Assert.ThrowsAsync<AppException>(() => expenses.VoidAsync(owner, id, ""));
        ex.Code.ShouldBe("reason_required");
    }
}

// ==================== Permissions: test 7 + matrix ====================

public class PermissionTests
{
    [Fact]
    public void Owner_HasEverything()
    {
        var user = new CurrentUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserRole.Owner, true, Guid.Empty.AsArray(), null);
        foreach (var a in Permissions.All) Permissions.Can(user, a).ShouldBeTrue();
        Permissions.AccessesAllBranches(user).ShouldBeTrue();
    }

    [Fact]
    public void Cashier_Defaults()
    {
        var user = new CurrentUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserRole.Cashier, true, Guid.Empty.AsArray(), null);
        Permissions.Can(user, Permissions.SaleCreate).ShouldBeTrue();
        Permissions.Can(user, Permissions.ShiftOpen).ShouldBeTrue();
        Permissions.Can(user, Permissions.ExpenseCreate).ShouldBeFalse();
        Permissions.Can(user, Permissions.ReportView).ShouldBeFalse();
        Permissions.Can(user, Permissions.UserManage).ShouldBeFalse();
        Permissions.Can(user, Permissions.BarberManage).ShouldBeFalse();
    }

    [Fact]
    public void Barber_Defaults_Minimal()
    {
        var user = new CurrentUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserRole.Barber, true, Guid.Empty.AsArray(), null);
        // Barber-operated POS is allowed for the barber himself only (enforced in SalesService)
        Permissions.Can(user, Permissions.SaleCreate).ShouldBeTrue();
        Permissions.Can(user, Permissions.ExpenseView).ShouldBeFalse();
        Permissions.Can(user, Permissions.ShiftView).ShouldBeFalse();
        Permissions.Can(user, Permissions.ReportView).ShouldBeFalse();
        Permissions.Can(user, Permissions.UserManage).ShouldBeFalse();
        Permissions.AccessesAllBranches(user).ShouldBeFalse();
    }

    [Fact]
    public void OwnerOverride_WinsOverRoleDefault()
    {
        var overrides = new Dictionary<string, bool> { ["expense.create"] = true };
        var user = new CurrentUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserRole.Cashier, true, Guid.Empty.AsArray(), overrides);
        Permissions.Can(user, Permissions.ExpenseCreate).ShouldBeTrue();
        Permissions.Can(user, Permissions.ReportView).ShouldBeFalse();
    }

    [Fact]
    public void BranchAccess_IsScoped()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var cashier = new CurrentUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserRole.Cashier, true, new[] { a }, null);
        Permissions.CanAccessBranch(cashier, a).ShouldBeTrue();
        Permissions.CanAccessBranch(cashier, b).ShouldBeFalse();
    }

    [Fact]
    public void EffectivePermissions_ExposesMatrix()
    {
        var user = new CurrentUser(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserRole.Owner, true, Guid.Empty.AsArray(), null);
        var matrix = Permissions.EffectivePermissions(user);
        matrix.Count.ShouldBe(Permissions.All.Length + 1);
        matrix[Permissions.SaleCreate].ShouldBeTrue();
    }
}
