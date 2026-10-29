namespace Hallagi.Application.Treasury;

public class ShiftService(IAppDbContext db)
{
    public async Task<object> GetStateAsync(CurrentUser user, Guid branchId, CancellationToken ct = default)
    {
        if (!Permissions.CanAccessBranch(user, branchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        if (!Permissions.Can(user, Permissions.ShiftView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض الخزينة");

        var branch = await db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الفرع غير موجود");

        var openShift = await db.Shifts.AsNoTracking()
            .Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Open)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(ct);

        // Drawer balance when no shift is open = last closed shift's counted cash
        var lastClosed = await db.Shifts.AsNoTracking()
            .Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefaultAsync(ct);

        object? current = null;
        if (openShift is not null)
        {
            var summary = await SummarizeAsync(openShift, ct);
            current = new
            {
                openShift.Id,
                openShift.OpeningCash,
                openShift.OpenedAt,
                openShift.AutoOpened,
                OpenedByName = await db.Profiles.Where(p => p.Id == openShift.OpenedBy).Select(p => p.FullName).FirstAsync(ct),
                summary.CashSales,
                summary.CardSales,
                summary.CashExpenses,
                summary.MovementsIn,
                summary.MovementsOut,
                summary.ExpectedCash
            };
        }

        var now = DateTime.UtcNow;
        var staleBefore = now.AddHours(-14);
        var openStale = await db.Shifts.AsNoTracking()
            .Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Open && s.OpenedAt < staleBefore)
            .AnyAsync(ct);

        return new
        {
            branch = new { branch.Id, branch.Name, branch.City },
            hasOpenShift = openShift is not null,
            current,
            drawerBalanceWhenClosed = lastClosed?.CountedCash ?? 0m,
            lastClosedAt = lastClosed?.ClosedAt,
            openStale
        };
    }

    public async Task<object> OpenAsync(CurrentUser user, Guid branchId, decimal openingCash, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ShiftOpen))
            throw AppException.Forbidden("ليس لديك صلاحية لفتح الوردية");
        if (!Permissions.CanAccessBranch(user, branchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        if (openingCash < 0 || openingCash > 1_000_000)
            throw AppException.Bad("invalid_opening", "الرصيد الافتتاحي غير صحيح");

        var conflict = await db.Shifts.AnyAsync(s => s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (conflict)
            throw AppException.Conflict("shift_open", "توجد وردية مفتوحة على هذا الفرع. أغلقها أولاً");

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الفرع غير موجود");

        var drawer = await db.CashDrawers.FirstOrDefaultAsync(d => d.BranchId == branch.Id, ct);
        if (drawer is null)
        {
            drawer = new CashDrawer { Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, BranchId = branch.Id, CreatedAt = DateTime.UtcNow };
            db.CashDrawers.Add(drawer);
            await db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            DrawerId = drawer.Id,
            OpenedBy = user.ProfileId,
            OpeningCash = openingCash,
            OpenedAt = now,
            Status = ShiftStatus.Open,
            AutoOpened = false
        };
        db.Shifts.Add(shift);
        db.CashMovements.Add(new CashMovement
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            ShiftId = shift.Id,
            Type = CashMovementType.Opening,
            Amount = openingCash,
            Description = "رصيد افتتاحي للوردية",
            CreatedBy = user.ProfileId,
            CreatedAt = now
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "shift.opened",
            EntityType = nameof(Shift),
            EntityId = shift.Id.ToString(),
            Details = $"branch={branch.Name}; opening={openingCash}",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return await GetOpenShiftStateAsync(shift.Id, ct);
    }

    public async Task<object> CloseAsync(CurrentUser user, Guid branchId, decimal countedCash, string? note, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ShiftClose))
            throw AppException.Forbidden("ليس لديك صلاحية لإقفال الوردية");
        if (!Permissions.CanAccessBranch(user, branchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        if (countedCash < 0 || countedCash > 1_000_000)
            throw AppException.Bad("invalid_counted", "المبلغ المعدود غير صحيح");

        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branchId && s.Status == ShiftStatus.Open, ct)
            ?? throw AppException.Conflict("no_open_shift", "لا توجد وردية مفتوحة على هذا الفرع");

        var summary = await SummarizeAsync(shift, ct);
        var expected = summary.ExpectedCash;

        var now = DateTime.UtcNow;
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAt = now;
        shift.ClosedBy = user.ProfileId;
        shift.CountedCash = countedCash;
        shift.ExpectedCash = expected;
        shift.Difference = countedCash - expected;
        shift.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "shift.closed",
            EntityType = nameof(Shift),
            EntityId = shift.Id.ToString(),
            Details = $"expected={expected}; counted={countedCash}; diff={shift.Difference}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        return await GetOpenShiftStateAsync(shift.Id, ct);
    }

    public async Task<object> GetOpenShiftStateAsync(Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().FirstAsync(s => s.Id == shiftId, ct);
        var branchName = await db.Branches.Where(b => b.Id == shift.BranchId).Select(b => b.Name).FirstAsync(ct);
        var summary = await SummarizeAsync(shift, ct);
        return new
        {
            shift.Id,
            shift.BranchId,
            BranchName = branchName,
            shift.OpeningCash,
            shift.OpenedAt,
            shift.ClosedAt,
    shift.Status,
    shift.CountedCash,
    ClosedExpectedCash = shift.ExpectedCash,
    shift.Difference,
            shift.Note,
            shift.AutoOpened,
            summary.CashSales,
            summary.CardSales,
            summary.CashExpenses,
            summary.MovementsIn,
            summary.MovementsOut,
            ExpectedCash = summary.ExpectedCash,
            DifferenceText = DifferenceText(shift.Status, shift.Difference)
        };
    }

    public async Task<object> ListAsync(CurrentUser user, Guid? branchId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ShiftView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض الورديات");

        var allowed = Permissions.AccessesAllBranches(user)
            ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
            : user.BranchIds.AsQueryable();

        var query = db.Shifts.AsNoTracking().Where(s => s.OrganizationId == user.OrganizationId && allowed.Contains(s.BranchId));
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(s => s.OpenedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.OpenedAt,
                s.ClosedAt,
                s.Status,
                StatusText = s.Status == ShiftStatus.Open ? "مفتوحة" : "مغلقة",
                s.OpeningCash,
                s.ExpectedCash,
                s.CountedCash,
                s.Difference,
                BranchName = db.Branches.Where(b => b.Id == s.BranchId).Select(b => b.Name).First(),
                OpenedByName = db.Profiles.Where(p => p.Id == s.OpenedBy).Select(p => p.FullName).First()
            }).ToListAsync(ct);
        return new { totalCount = total, page, pageSize, items };
    }

    /// <summary>
    /// Drawer math: expected = opening + cash sales + other cash in − cash expenses − cash out.
    /// Card sales never affect the drawer.
    /// </summary>
    public async Task<(decimal CashSales, decimal CardSales, decimal CashExpenses, decimal MovementsIn, decimal MovementsOut, decimal ExpectedCash)>
        SummarizeAsync(Shift shift, CancellationToken ct)
    {
        var movements = await db.CashMovements.AsNoTracking().Where(m => m.ShiftId == shift.Id).ToListAsync(ct);

        decimal cashSales = 0m, cardSales = 0m, cashExpenses = 0m, inAmount = 0m, outAmount = 0m;

        // Card sales are attributed to the shift's day (branch + time window) without touching the drawer
        var to = shift.ClosedAt ?? DateTime.UtcNow;
        cardSales = await db.Sales.AsNoTracking()
            .Where(s => s.BranchId == shift.BranchId
                        && s.PaymentMethod == PaymentMethod.Card
                        && s.Status == SaleStatus.Completed
                        && s.CreatedAt >= shift.OpenedAt && s.CreatedAt <= to)
            .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;

        foreach (var m in movements)
        {
            switch (m.Type)
            {
                // Opening cash is already represented by shift.OpeningCash; the movement row is the audit record
                case CashMovementType.Opening:
                    break;
                case CashMovementType.ManualIn:
                    inAmount += m.Amount; break;
                case CashMovementType.Sale:
                    cashSales += m.Amount; inAmount += m.Amount; break;
                case CashMovementType.Expense:
                case CashMovementType.SaleVoid:
                case CashMovementType.ManualOut:
                    outAmount += m.Amount; break;
            }
            if (m.Type == CashMovementType.Expense)
                cashExpenses += m.Amount;
        }

        var expected = shift.OpeningCash + inAmount - outAmount;
        return (cashSales, cardSales, cashExpenses, inAmount, outAmount, expected);
    }

    public static string DifferenceText(ShiftStatus status, decimal? difference)
    {
        if (status == ShiftStatus.Open || difference is null) return "الوردية مفتوحة";
        if (difference == 0m) return "النقدية مطابقة";
        return difference < 0 ? "يوجد عجز نقدي" : "يوجد فائض نقدي";
    }
}
