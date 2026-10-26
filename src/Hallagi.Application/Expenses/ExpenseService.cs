namespace Hallagi.Application.Expenses;

public class ExpenseService(IAppDbContext db)
{
    public async Task<Guid> CreateAsync(CurrentUser user, CreateExpenseRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ExpenseCreate))
            throw AppException.Forbidden("ليس لديك صلاحية لتسجيل المصروفات");
        if (!Permissions.CanAccessBranch(user, req.BranchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        if (req.Amount <= 0 || req.Amount > 1_000_000)
            throw AppException.Bad("invalid_amount", "المبلغ غير صحيح");

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == req.BranchId && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الفرع غير موجود");

        var now = DateTime.UtcNow;
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            Category = req.Category,
            Amount = req.Amount,
            ExpenseDate = req.ExpenseDate == default ? DateOnly.FromDateTime(DateTime.Today) : req.ExpenseDate,
            Description = req.Description?.Trim(),
            Source = req.Source,
            Status = ExpenseStatus.Active,
            CreatedBy = user.ProfileId,
            CreatedAt = now
        };

        // Only cash-drawer payments affect drawer cash; bank/other sources do not
        var openShift = req.Source == ExpenseSource.CashDrawer
            ? await db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branch.Id && s.Status == ShiftStatus.Open, ct)
            : null;

        db.Expenses.Add(expense);

        if (openShift is not null)
        {
            db.CashMovements.Add(new CashMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BranchId = branch.Id,
                ShiftId = openShift.Id,
                Type = CashMovementType.Expense,
                Amount = req.Amount,
                ExpenseId = expense.Id,
                Description = $"مصروف: {CategoryText(req.Category)}",
                CreatedBy = user.ProfileId,
                CreatedAt = now
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "expense.created",
            EntityType = nameof(Expense),
            EntityId = expense.Id.ToString(),
            Details = $"category={req.Category}; amount={req.Amount}; source={req.Source}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        return expense.Id;
    }

    public async Task VoidAsync(CurrentUser user, Guid expenseId, string reason, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ExpenseVoid))
            throw AppException.Forbidden("ليس لديك صلاحية لإلغاء المصروفات");
        if (string.IsNullOrWhiteSpace(reason))
            throw AppException.Bad("reason_required", "اكتب سبب الإلغاء");

        var expense = await db.Expenses.FirstOrDefaultAsync(x => x.Id == expenseId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("المصروف غير موجود");
        if (expense.Status == ExpenseStatus.Voided)
            throw AppException.Conflict("already_voided", "المصروف ملغى مسبقًا");
        if (!Permissions.CanAccessBranch(user, expense.BranchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");

        var now = DateTime.UtcNow;
        expense.Status = ExpenseStatus.Voided;
        expense.VoidedAt = now;
        expense.VoidReason = reason.Trim();
        expense.VoidedBy = user.ProfileId;

        // Documented correction: reverse drawer movement on a NEW record (never edit history)
        if (expense.Source == ExpenseSource.CashDrawer)
        {
            var openShift = await db.Shifts.FirstOrDefaultAsync(s => s.BranchId == expense.BranchId && s.Status == ShiftStatus.Open, ct);
            if (openShift is not null)
            {
                db.CashMovements.Add(new CashMovement
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = user.OrganizationId,
                    BranchId = expense.BranchId,
                    ShiftId = openShift.Id,
                    Type = CashMovementType.ManualIn,
                    Amount = expense.Amount,
                    Description = $"تصحيح: إلغاء مصروف {CategoryText(expense.Category)} ({reason.Trim()})",
                    CreatedBy = user.ProfileId,
                    CreatedAt = now
                });
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "expense.voided",
            EntityType = nameof(Expense),
            EntityId = expense.Id.ToString(),
            Details = reason.Trim(),
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<object> ListAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ExpenseView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض المصروفات");

        var allowed = Permissions.AccessesAllBranches(user)
            ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
            : user.BranchIds.AsQueryable();

        var query = db.Expenses.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId && allowed.Contains(x.BranchId));
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(x => x.BranchId == branchId.Value);
        }
        if (from.HasValue) query = query.Where(x => x.ExpenseDate >= DateOnly.FromDateTime(from.Value.Date));
        if (to.HasValue) query = query.Where(x => x.ExpenseDate <= DateOnly.FromDateTime(to.Value.Date));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ExpenseDate,
                x.Category,
                CategoryText = CategoryText(x.Category),
                x.Amount,
                x.Description,
                x.Source,
                SourceText = SourceText(x.Source),
                x.Status,
                StatusText = x.Status == ExpenseStatus.Active ? "معتمد" : "ملغى",
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                CreatedByName = db.Profiles.Where(p => p.Id == x.CreatedBy).Select(p => p.FullName).First()
            }).ToListAsync(ct);
        return new { totalCount = total, page, pageSize, items };
    }

    public async Task<decimal> TotalAsync(Guid orgId, Guid? branchId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var from = DateOnly.FromDateTime(fromUtc.Date);
        var to = DateOnly.FromDateTime(toUtc.Date);
        var query = db.Expenses.AsNoTracking()
            .Where(x => x.OrganizationId == orgId && x.Status == ExpenseStatus.Active
                        && x.ExpenseDate >= from && x.ExpenseDate <= to);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        return await query.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    public static string CategoryText(ExpenseCategory c) => c switch
    {
        ExpenseCategory.Rent => "إيجار",
        ExpenseCategory.ElectricityWater => "كهرباء ومياه",
        ExpenseCategory.Supplies => "مستلزمات",
        ExpenseCategory.Maintenance => "صيانة",
        ExpenseCategory.Salaries => "رواتب",
        ExpenseCategory.Commissions => "عمولات",
        _ => "أخرى"
    };

    public static string SourceText(ExpenseSource s) => s switch
    {
        ExpenseSource.CashDrawer => "درج النقدية",
        ExpenseSource.Bank => "حساب بنكي",
        _ => "مصدر آخر"
    };
}
