namespace Hallagi.Application.Organizations;

public class BranchesService(IAppDbContext db, SubscriptionService subscriptions)
{
    public async Task<object> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        // Cashiers and barbers only see the branches they may access
        var query = db.Branches.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId);
        if (!Permissions.AccessesAllBranches(user))
            query = query.Where(b => user.BranchIds.Contains(b.Id));
        var branches = await query.OrderBy(b => b.Name).ToListAsync(ct);

        var items = new List<object>();
        foreach (var b in branches)
        {
            var barberCount = await db.BarberBranchAssignments.CountAsync(a => a.BranchId == b.Id, ct);
            var userCount = await db.MemberBranchAccess.CountAsync(a => a.BranchId == b.Id, ct);
            var openShift = await db.Shifts.AsNoTracking()
                .AnyAsync(s => s.BranchId == b.Id && s.Status == ShiftStatus.Open, ct);
            items.Add(new
            {
                b.Id,
                b.Name,
                b.City,
                b.Address,
                b.Phone,
                OpensAt = b.OpensAt.ToString("HH\\:mm"),
                ClosesAt = b.ClosesAt.ToString("HH\\:mm"),
                b.Status,
                StatusText = b.Status == EntityStatus.Active ? "نشط" : "غير نشط",
                barberCount,
                userCount,
                hasOpenShift = openShift
            });
        }
        return new { items };
    }

    public async Task<Guid> CreateAsync(CurrentUser user, BranchInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BranchManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الفروع");
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City))
            throw AppException.Bad("missing_fields", "اسم الفرع والمدينة مطلوبان");

        await subscriptions.EnsureBranchLimitAsync(user.OrganizationId, ct);

        var now = DateTime.UtcNow;
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            Name = input.Name.Trim(),
            City = input.City.Trim(),
            Address = input.Address?.Trim(),
            Phone = input.Phone?.Trim(),
            OpensAt = ParseHour(input.OpensAt, new TimeOnly(9, 0)),
            ClosesAt = ParseHour(input.ClosesAt, new TimeOnly(23, 0)),
            Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
            Drawer = new CashDrawer { OrganizationId = user.OrganizationId, CreatedAt = now }
        };
        db.Branches.Add(branch);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "branch.created",
            EntityType = nameof(Branch),
            EntityId = branch.Id.ToString(),
            Details = $"name={branch.Name}; city={branch.City}",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return branch.Id;
    }

    public async Task UpdateAsync(CurrentUser user, Guid id, BranchInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BranchManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الفروع");
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الفرع غير موجود");
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City))
            throw AppException.Bad("missing_fields", "اسم الفرع والمدينة مطلوبان");

        branch.Name = input.Name.Trim();
        branch.City = input.City.Trim();
        branch.Address = input.Address?.Trim();
        branch.Phone = input.Phone?.Trim();
        branch.OpensAt = ParseHour(input.OpensAt, branch.OpensAt);
        branch.ClosesAt = ParseHour(input.ClosesAt, branch.ClosesAt);
        branch.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static TimeOnly ParseHour(string? value, TimeOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return TimeOnly.TryParseExact(value.Trim(), "HH:mm", out var t)
            ? t
            : throw AppException.Bad("invalid_hours", "صيغة ساعات العمل غير صحيحة (HH:mm)");
    }
}
