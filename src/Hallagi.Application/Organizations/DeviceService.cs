namespace Hallagi.Application.Organizations;

public class DeviceService(IAppDbContext db)
{
    public async Task<object> ListAsync(CurrentUser user, Guid? branchId, CancellationToken ct = default)
    {
        var allowed = Permissions.AccessesAllBranches(user)
            ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
            : user.BranchIds.AsQueryable();

        IQueryable<Device> query = db.Devices.AsNoTracking()
            .Where(d => d.OrganizationId == user.OrganizationId && allowed.Contains(d.BranchId));
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(d => d.BranchId == branchId.Value);
        }

        var items = await query.OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.BranchId,
                BranchName = db.Branches.Where(b => b.Id == d.BranchId).Select(b => b.Name).First(),
                d.Name,
                d.DeviceCode,
                d.Status,
                StatusText = d.Status == "Active" ? "مفعّل" : "معطّل",
                openShifts = db.Shifts.Count(s => s.DeviceId == d.Id && s.Status == ShiftStatus.Open)
            }).ToListAsync(ct);
        return new { items };
    }

    public async Task<Guid> CreateAsync(CurrentUser user, CreateDeviceRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.DeviceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الأجهزة");
        if (string.IsNullOrWhiteSpace(req.Name)) throw AppException.Bad("name_required", "اسم الجهاز مطلوب");
        if (!Permissions.CanAccessBranch(user, req.BranchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");

        var device = new Device
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = req.BranchId,
            Name = req.Name.Trim(),
            DeviceCode = req.DeviceCode?.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync(ct);
        return device.Id;
    }

    public async Task SetStatusAsync(CurrentUser user, Guid id, bool active, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.DeviceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الأجهزة");

        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الجهاز غير موجود");

        // A device with an open shift must close its shift first
        if (!active && await db.Shifts.AnyAsync(s => s.DeviceId == id && s.Status == ShiftStatus.Open, ct))
            throw AppException.Conflict("shift_open", "على هذا الجهاز وردية مفتوحة. أغلق الوردية أولاً");

        device.Status = active ? "Active" : "Disabled";
        device.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
