using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public class BranchesService(AppDbContext db, SubscriptionService subscriptions)
{
    public async Task<object> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId)
            .ToListAsync(ct);

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
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة الفروع");
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City))
            throw ApiException.Bad("missing_fields", "اسم الفرع والمدينة مطلوبان");

        await subscriptions.EnsureBranchLimitAsync(user.OrganizationId, ct);

        var now = DateTime.UtcNow;
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            Name = input.Name.Trim(),
            City = input.City.Trim(),
            Address = input.Address?.Trim(),
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
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة الفروع");
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("الفرع غير موجود");
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City))
            throw ApiException.Bad("missing_fields", "اسم الفرع والمدينة مطلوبان");

        branch.Name = input.Name.Trim();
        branch.City = input.City.Trim();
        branch.Address = input.Address?.Trim();
        branch.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

public class SettingsService(AppDbContext db)
{
    public async Task<object> GetAsync(CurrentUser user, CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("المنشأة غير موجودة");
        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == org.Id, ct);
        return new
        {
            organization = new { org.Id, org.Name, org.Currency, org.TimeZone, org.LogoUrl },
            settings = settings is null
                ? new { EnableCash = true, EnableCard = true, ReceiptFooter = (string?)null, ReceiptPhone = (string?)null }
                : new { settings!.EnableCash, settings.EnableCard, settings.ReceiptFooter, settings.ReceiptPhone }
        };
    }

    public async Task UpdateAsync(CurrentUser user, UpdateSettingsRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettingsManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لتعديل الإعدادات");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("المنشأة غير موجودة");
        if (string.IsNullOrWhiteSpace(req.OrganizationName))
            throw ApiException.Bad("name_required", "اسم المنشأة مطلوب");

        org.Name = req.OrganizationName.Trim();
        org.Currency = string.IsNullOrWhiteSpace(req.Currency) ? org.Currency : req.Currency.Trim();
        org.TimeZone = string.IsNullOrWhiteSpace(req.TimeZone) ? org.TimeZone : req.TimeZone.Trim();
        org.LogoUrl = req.LogoUrl?.Trim();
        org.UpdatedAt = DateTime.UtcNow;

        var settings = await db.OrganizationSettings.FirstOrDefaultAsync(s => s.OrganizationId == org.Id, ct);
        if (settings is null)
        {
            settings = new OrganizationSettings { OrganizationId = org.Id };
            db.OrganizationSettings.Add(settings);
        }
        settings.EnableCash = req.EnableCash;
        settings.EnableCard = req.EnableCard;
        settings.ReceiptFooter = req.ReceiptFooter?.Trim();
        settings.ReceiptPhone = req.ReceiptPhone?.Trim();

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Onboarding step 1/2/3/4 support: bulk add services and barbers for a new organization.</summary>
    public async Task BulkSeedAsync(CurrentUser user, List<string> serviceNames, List<BarberInput> barbers, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ServiceManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإعداد الخدمات");

        var now = DateTime.UtcNow;
        foreach (var name in serviceNames.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (await db.Services.AnyAsync(s => s.OrganizationId == user.OrganizationId && s.Name == name.Trim(), ct)) continue;
            db.Services.Add(new Service
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                Name = name.Trim(),
                BasePrice = 25m,
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var b in barbers)
        {
            var barber = new Barber
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                FullName = b.FullName.Trim(),
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Barbers.Add(barber);
            foreach (var br in b.BranchIds.Distinct())
                db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barber.Id, BranchId = br });
        }

        await db.SaveChangesAsync(ct);
    }
}

public record BranchInput(string Name, string City, string? Address, bool Active);
public record UpdateSettingsRequest(
    string OrganizationName,
    string? Currency,
    string? TimeZone,
    string? LogoUrl,
    bool EnableCash,
    bool EnableCard,
    string? ReceiptFooter,
    string? ReceiptPhone);
